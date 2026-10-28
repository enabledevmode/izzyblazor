using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace IzzyBlazor;

/// <summary>The filter a Table column offers.</summary>
internal enum ColumnFilterKind
{
    /// <summary>Not filterable.</summary>
    None,

    /// <summary>A text box; matches anywhere, ignoring case.</summary>
    Text,

    /// <summary>Two number boxes, at least and at most; keeps the values between them (either may be empty).</summary>
    Number,

    /// <summary>A menu (enums and booleans); matches the chosen value, or any of them with FilterMultiple.</summary>
    Choice,

    /// <summary>A date input (DateOnly, DateTime, DateTimeOffset); matches that day.</summary>
    Date,
}

/// <summary>
/// Builds a Table column's filter as an expression on the column's own property, so an EF Core
/// query filters in the database. The typed value is captured in a closure, which EF Core turns
/// into a query parameter.
/// </summary>
internal static class ColumnFilter
{
    private static readonly MethodInfo ToLower = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
    private static readonly MethodInfo StringContains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;

    private sealed class Captured<T>(T value)
    {
        public T Value { get; } = value;
    }

    private static Expression Capture<T>(T value) =>
        Expression.Property(Expression.Constant(new Captured<T>(value)), typeof(Captured<T>).GetProperty(nameof(Captured<T>.Value))!);

    public static ColumnFilterKind KindOf(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        if (t == typeof(string))
        {
            return ColumnFilterKind.Text;
        }

        if (t.IsEnum || t == typeof(bool))
        {
            return ColumnFilterKind.Choice;
        }

        if (t == typeof(DateOnly) || t == typeof(DateTime) || t == typeof(DateTimeOffset))
        {
            return ColumnFilterKind.Date;
        }

        return ColumnBase<object>.IsNumeric(t) ? ColumnFilterKind.Number : ColumnFilterKind.None;
    }

    /// <summary>Enum names (with [Display(Name)] text) or true/false.</summary>
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "The trimmer keeps every field of an enum it keeps, and their [Display] attributes when DisplayAttribute is kept, which this method's use of it ensures.")]
    public static IReadOnlyList<(string Value, string Text)> Choices(Type type, IzzyText text)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        if (t == typeof(bool))
        {
            return [("true", text.Yes), ("false", text.No)];
        }

        if (!t.IsEnum)
        {
            return [];
        }

        return t.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (f.Name, f.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? f.Name))
            .ToList();
    }

    /// <summary><c>x =&gt; text(x) != null &amp;&amp; text(x).ToLower().Contains(value.ToLower())</c>.</summary>
    public static IQueryable<TItem> Contains<TItem>(IQueryable<TItem> source, LambdaExpression text, string value)
    {
        var body = text.Body;
        var test = Expression.AndAlso(
            Expression.NotEqual(body, Expression.Constant(null, typeof(string))),
            Expression.Call(Expression.Call(body, ToLower), StringContains, Capture(value.ToLower(CultureInfo.CurrentCulture))));
        return source.Where(Expression.Lambda<Func<TItem, bool>>(test, text.Parameters));
    }

    /// <summary>
    /// <c>x =&gt; property(x) == value</c>, with the value parsed for the property's type. A value
    /// that doesn't parse ("abc" for a number) matches nothing.
    /// </summary>
    public static IQueryable<TItem> EqualTo<TItem>(IQueryable<TItem> source, LambdaExpression property, string value)
    {
        var type = property.Body.Type;
        if (!TryParse(type, value, out var parsed))
        {
            return source.Where(_ => false);
        }

        // Enums compare as their underlying number, as the C# compiler writes it.
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        var compareAs = underlying.IsEnum
            ? (type == underlying ? Enum.GetUnderlyingType(underlying) : typeof(Nullable<>).MakeGenericType(Enum.GetUnderlyingType(underlying)))
            : type;

        Expression left = property.Body;
        Expression right = Expression.Convert(Capture(parsed), type);
        if (compareAs != type)
        {
            left = Expression.Convert(left, compareAs);
            right = Expression.Convert(right, compareAs);
        }

        return source.Where(Expression.Lambda<Func<TItem, bool>>(Expression.Equal(left, right), property.Parameters));
    }

    /// <summary>
    /// <c>x =&gt; x &gt;= min &amp;&amp; x &lt;= max</c> for a range value ("100..500", "100..", "..500";
    /// a lone number is that number exactly), each end parsed for the property's type. An end
    /// that isn't a number matches nothing.
    /// </summary>
    public static IQueryable<TItem> Between<TItem>(IQueryable<TItem> source, LambdaExpression property, string value)
    {
        var (min, max) = SplitRange(value);
        var type = property.Body.Type;
        Expression? test = null;
        foreach (var (end, atLeast) in new[] { (min, true), (max, false) })
        {
            if (end.Length == 0)
            {
                continue;
            }

            var (outcome, parsed) = ParseBound(type, end, atLeast);
            if (outcome == Bound.None)
            {
                return source.Where(_ => false);
            }

            if (outcome == Bound.All)
            {
                continue;
            }

            var bound = Expression.Convert(Capture(parsed), type);
            Expression compare = atLeast ? Expression.GreaterThanOrEqual(property.Body, bound) : Expression.LessThanOrEqual(property.Body, bound);
            test = test is null ? compare : Expression.AndAlso(test, compare);
        }

        return test is null ? source : source.Where(Expression.Lambda<Func<TItem, bool>>(test, property.Parameters));
    }

    private enum Bound
    {
        /// <summary>A value to compare with.</summary>
        Value,

        /// <summary>Every value passes this end ("at most 1e12" on an int column).</summary>
        All,

        /// <summary>No value can pass this end, or it isn't a number.</summary>
        None,
    }

    // A range end for the property's type. A fraction on a whole-number column rounds inward
    // ("at least 2.5" is 3 or more, "at most 2.5" is 2 or less), and a number beyond what the
    // type holds either keeps everything or nothing, as the comparison would, rather than failing.
    private static (Bound Outcome, object? Value) ParseBound(Type type, string text, bool atLeast)
    {
        if (TryParse(type, text, out var value))
        {
            return (Bound.Value, value);
        }

        if (!TryParse(typeof(double), text, out var parsed) || parsed is not double number || double.IsNaN(number))
        {
            return (Bound.None, null);
        }

        var t = Nullable.GetUnderlyingType(type) ?? type;
        var whole = t == typeof(decimal) || t == typeof(double) || t == typeof(float)
            ? number
            : atLeast ? Math.Ceiling(number) : Math.Floor(number);
        var (min, max) = Limits(t);
        if (whole > max)
        {
            return (atLeast ? Bound.None : Bound.All, null);
        }

        if (whole < min)
        {
            return (atLeast ? Bound.All : Bound.None, null);
        }

        return TryParse(type, whole.ToString("R", CultureInfo.InvariantCulture), out value) ? (Bound.Value, value) : (Bound.None, null);
    }

    // What a numeric type holds, as doubles.
    private static (double Min, double Max) Limits(Type t) => Type.GetTypeCode(t) switch
    {
        TypeCode.Byte => (byte.MinValue, byte.MaxValue),
        TypeCode.SByte => (sbyte.MinValue, sbyte.MaxValue),
        TypeCode.Int16 => (short.MinValue, short.MaxValue),
        TypeCode.UInt16 => (ushort.MinValue, ushort.MaxValue),
        TypeCode.Int32 => (int.MinValue, int.MaxValue),
        TypeCode.UInt32 => (uint.MinValue, uint.MaxValue),
        TypeCode.Int64 => (long.MinValue, long.MaxValue),
        TypeCode.UInt64 => (ulong.MinValue, ulong.MaxValue),
        TypeCode.Decimal => ((double)decimal.MinValue, (double)decimal.MaxValue),
        TypeCode.Single => (float.MinValue, float.MaxValue),
        _ => (double.MinValue, double.MaxValue),
    };

    /// <summary>The two ends of a range value: "100..500" → ("100", "500"); a lone value is both ends.</summary>
    internal static (string Min, string Max) SplitRange(string value)
    {
        var at = value.IndexOf("..", StringComparison.Ordinal);
        return at < 0 ? (value.Trim(), value.Trim()) : (value[..at].Trim(), value[(at + 2)..].Trim());
    }

    /// <summary>
    /// A range value from its ends, or "" when both are empty. Each end is tidied so the two dots
    /// between them stay unambiguous while someone types: "1." is "1", ".5" is "0.5".
    /// </summary>
    internal static string JoinRange(string min, string max)
    {
        var (low, high) = (TidyEnd(min), TidyEnd(max));
        return low.Length == 0 && high.Length == 0 ? "" : $"{low}..{high}";
    }

    private static string TidyEnd(string end)
    {
        end = end.Trim().TrimEnd('.');
        return end.StartsWith('.') ? "0" + end : end.StartsWith("-.", StringComparison.Ordinal) ? "-0" + end[1..] : end;
    }

    /// <summary>
    /// Keeps the items on one day. DateOnly compares directly; DateTime keeps the day's range
    /// (<c>x &gt;= day &amp;&amp; x &lt; day + 1</c>, which uses an index); DateTimeOffset compares its
    /// <c>Date</c>, the day on the clock where it was recorded. A value that isn't a date matches
    /// nothing.
    /// </summary>
    public static IQueryable<TItem> OnDay<TItem>(IQueryable<TItem> source, LambdaExpression property, string value)
    {
        if (!TryParseDay(value, out var day))
        {
            return source.Where(_ => false);
        }

        var body = property.Body;
        var type = body.Type;
        var t = Nullable.GetUnderlyingType(type) ?? type;
        Expression test;
        if (t == typeof(DateOnly))
        {
            test = Expression.Equal(body, Expression.Convert(Capture(day), type));
        }
        else if (t == typeof(DateTime))
        {
            // [day, day + 1): on the last representable day there is no next day, so it's open-ended.
            var start = day.ToDateTime(TimeOnly.MinValue);
            var onOrAfter = Expression.GreaterThanOrEqual(body, Expression.Convert(Capture(start), type));
            test = day == DateOnly.MaxValue
                ? onOrAfter
                : Expression.AndAlso(onOrAfter, Expression.LessThan(body, Expression.Convert(Capture(start.AddDays(1)), type)));
        }
        else
        {
            // DateTimeOffset(?): x.Date == day, through .Value when nullable.
            var date = typeof(DateTimeOffset).GetProperty(nameof(DateTimeOffset.Date))!;
            Expression instance = type == t ? body : Expression.Property(body, typeof(DateTimeOffset?).GetProperty(nameof(Nullable<DateTimeOffset>.Value))!);
            var equal = Expression.Equal(Expression.Property(instance, date), Capture(day.ToDateTime(TimeOnly.MinValue)));
            test = type == t ? equal : Expression.AndAlso(Expression.Property(body, typeof(DateTimeOffset?).GetProperty(nameof(Nullable<DateTimeOffset>.HasValue))!), equal);
        }

        return source.Where(Expression.Lambda<Func<TItem, bool>>(test, property.Parameters));
    }

    /// <summary>
    /// <c>x =&gt; values.Contains(property(x))</c> for the values that parse (EF Core turns it into
    /// <c>IN (...)</c>). No valid value matches nothing.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "Enumerable.Contains<T> places no requirements on T, so any column type works.")]
    public static IQueryable<TItem> AnyOf<TItem>(IQueryable<TItem> source, LambdaExpression property, IReadOnlyList<string> values)
    {
        var type = property.Body.Type;
        var parsed = values.Select(v => TryParse(type, v, out var value) ? value : null).Where(v => v is not null).Distinct().ToList();
        if (parsed.Count == 0)
        {
            return source.Where(_ => false);
        }

        var array = Array.CreateInstance(type, parsed.Count);
        for (var i = 0; i < parsed.Count; i++)
        {
            array.SetValue(parsed[i], i);
        }

        var contains = typeof(Enumerable).GetMethods()
            .First(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
            .MakeGenericMethod(type);
        var list = Expression.Convert(Capture<object>(array), array.GetType());
        var test = Expression.Call(contains, list, property.Body);
        return source.Where(Expression.Lambda<Func<TItem, bool>>(test, property.Parameters));
    }

    /// <summary>Several values in one filter value: "Paid,Pending" (repeated URL keys arrive joined).</summary>
    internal static IReadOnlyList<string> SplitValues(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // A date input sends yyyy-MM-dd; someone typing into the URL may write their own form.
    internal static bool TryParseDay(string text, out DateOnly day) =>
        DateOnly.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day)
        || DateOnly.TryParse(text.Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out day);

    internal static bool TryParse(Type type, string text, out object? value)
    {
        value = null;
        var t = Nullable.GetUnderlyingType(type) ?? type;
        text = text.Trim();
        if (t.IsEnum)
        {
            if (Enum.TryParse(t, text, ignoreCase: true, out var e) && Enum.IsDefined(t, e!))
            {
                value = e;
                return true;
            }

            return false;
        }

        if (t == typeof(bool))
        {
            if (bool.TryParse(text, out var b))
            {
                value = b;
                return true;
            }

            return false;
        }

        // Numbers: as the person writes them (the current culture, "1.234,5" in German), else the
        // invariant form ("1234.5"); then converted to the column's type (a fraction doesn't
        // match a whole-number column).
        foreach (var (culture, styles) in new[]
        {
            (CultureInfo.CurrentCulture, NumberStyles.Float | NumberStyles.AllowThousands),
            (CultureInfo.InvariantCulture, NumberStyles.Float),
        })
        {
            if (!double.TryParse(text, styles, culture, out var number))
            {
                continue;
            }

            try
            {
                value = t == typeof(decimal)
                    ? decimal.Parse(text, styles, culture)
                    : t == typeof(double) || t == typeof(float)
                        ? Convert.ChangeType(number, t, CultureInfo.InvariantCulture)
                        : number == Math.Truncate(number) ? Convert.ChangeType(number, t, CultureInfo.InvariantCulture) : null;
                return value is not null;
            }
            catch (Exception ex) when (ex is OverflowException or FormatException or InvalidCastException)
            {
                return false;
            }
        }

        return false;
    }
}
