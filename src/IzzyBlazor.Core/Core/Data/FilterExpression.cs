using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace IzzyBlazor;

/// <summary>
/// Builds a filter as a LINQ expression on the fields' own expressions, so an EF Core query filters
/// in the database. Each value is parsed for its field's type and captured in a closure, which EF
/// Core turns into a query parameter; nothing in a value becomes code. A condition that can't hold
/// (an unknown field, an operator the field doesn't take, a value that doesn't parse) is
/// <c>false</c>; one that always holds is <c>true</c>, and both fold away where they can.
/// </summary>
internal static class FilterExpression
{
    private static readonly MethodInfo ToLower = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
    private static readonly MethodInfo StringContains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
    private static readonly MethodInfo StringStartsWith = typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string)])!;
    private static readonly MethodInfo StringEndsWith = typeof(string).GetMethod(nameof(string.EndsWith), [typeof(string)])!;

    private static readonly Expression True = Expression.Constant(true);
    private static readonly Expression False = Expression.Constant(false);

    private sealed class Captured<T>(T value)
    {
        public T Value { get; } = value;
    }

    private static Expression Capture<T>(T value) =>
        Expression.Property(Expression.Constant(new Captured<T>(value)), typeof(Captured<T>).GetProperty(nameof(Captured<T>.Value))!);

    // A value of the field's type, as a query parameter.
    private static Expression Parameter(object? value, Type type) => Expression.Convert(Capture(value), type);

    /// <summary>The most conditions, groups and nots a filter may have; a larger one matches nothing.</summary>
    public const int MaxNodes = 1000;

    /// <summary>The deepest a filter may nest; a deeper one matches nothing.</summary>
    public const int MaxDepth = 64;

    /// <summary>
    /// The filter as <c>x =&gt; ...</c>, with fields found by key (ignoring case) among the filterable
    /// ones. A filter larger than <see cref="MaxNodes"/> or deeper than <see cref="MaxDepth"/> (from
    /// a request that came over the wire, say) matches nothing, rather than building an expression
    /// so deep that compiling or translating it overflows the stack.
    /// </summary>
    public static Expression<Func<TItem, bool>> Build<TItem>(Filter filter, IReadOnlyList<DataField<TItem>> fields)
    {
        var x = Expression.Parameter(typeof(TItem), "x");
        return Expression.Lambda<Func<TItem, bool>>(WithinLimits(filter) ? Body(filter, fields, x) : False, x);
    }

    // Counted without recursion, so a filter of any shape can be checked.
    private static bool WithinLimits(Filter filter)
    {
        var nodes = 0;
        var pending = new Stack<(Filter Node, int Depth)>();
        pending.Push((filter, 1));
        while (pending.Count > 0)
        {
            var (node, depth) = pending.Pop();
            if (++nodes > MaxNodes || depth > MaxDepth)
            {
                return false;
            }

            switch (node)
            {
                case FilterGroup g:
                    foreach (var child in g.Filters)
                    {
                        pending.Push((child, depth + 1));
                    }

                    break;
                case NotFilter n:
                    pending.Push((n.Filter, depth + 1));
                    break;
            }
        }

        return true;
    }

    /// <summary>Keeps the items that match, or every item for a filter that always holds.</summary>
    public static IQueryable<TItem> Where<TItem>(IQueryable<TItem> source, Filter filter, IReadOnlyList<DataField<TItem>> fields)
    {
        var lambda = Build(filter, fields);
        return lambda.Body == True ? source : source.Where(lambda);
    }

    /// <summary>Any searchable text field contains the text, ignoring case. With no searchable field, nothing matches.</summary>
    public static Expression<Func<TItem, bool>> Search<TItem>(string search, IReadOnlyList<DataField<TItem>> fields)
    {
        var x = Expression.Parameter(typeof(TItem), "x");
        var contains = new FieldFilter("", FilterOperator.Contains, search);
        var parts = fields
            .Where(f => f.Searchable && FilterValues.KindOf(f.Type) == FieldKind.Text)
            .Select(f => Default(contains, Rebind(f.Value, x), f.Type))
            .ToList();
        return Expression.Lambda<Func<TItem, bool>>(Join(parts, and: false), x);
    }

    private static Expression Body<TItem>(Filter filter, IReadOnlyList<DataField<TItem>> fields, ParameterExpression x) => filter switch
    {
        FieldFilter f => Condition(f, fields, x),
        FilterGroup g => Join(g.Filters.Select(c => Body(c, fields, x)).ToList(), g.Logic == FilterLogic.And),
        NotFilter n => Not(Body(n.Filter, fields, x)),
        _ => False,
    };

    // Conditions joined as a balanced tree, so many of them don't make one deep chain.
    private static Expression Join(IReadOnlyList<Expression> parts, bool and)
    {
        if (parts.Count == 0)
        {
            return and ? True : False;
        }

        if (parts.Count == 1)
        {
            return parts[0];
        }

        var half = parts.Count / 2;
        var (left, right) = (Join(parts.Take(half).ToList(), and), Join(parts.Skip(half).ToList(), and));
        return and ? And(left, right) : Or(left, right);
    }

    private static Expression And(Expression a, Expression b) =>
        a == False || b == False ? False : a == True ? b : b == True ? a : Expression.AndAlso(a, b);

    private static Expression Or(Expression a, Expression b) =>
        a == True || b == True ? True : a == False ? b : b == False ? a : Expression.OrElse(a, b);

    private static Expression Not(Expression a) => a == True ? False : a == False ? True : Expression.Not(a);

    private static Expression Condition<TItem>(FieldFilter filter, IReadOnlyList<DataField<TItem>> fields, ParameterExpression x)
    {
        var field = fields.FirstOrDefault(f => f.Filterable && string.Equals(f.Key, filter.Field, StringComparison.OrdinalIgnoreCase));
        if (field is null)
        {
            return False;
        }

        if (field.Filter is { } custom)
        {
            // The field's own condition, given the values as its type (as far as they convert).
            var typed = Typed(filter, field.Type);
            if (custom(typed) is { } own)
            {
                return Rebind(own, x);
            }
        }

        if (!field.EffectiveOperators.Contains(filter.Operator))
        {
            return False;
        }

        var body = Rebind(field.Value, x);
        return FilterValues.DayCondition(filter, field.Type) is { } days ? Days(days, body, field.Type) : Default(filter, body, field.Type);
    }

    // What a comparison with a whole day came to (FilterValues.DayCondition). On an offset, the day is
    // the one on its clock, as OnDay reads it.
    private static Expression Days(Filter filter, Expression body, Type type)
    {
        switch (filter)
        {
            case NotFilter not:
                return Not(Days(not.Filter, body, type));
            case FilterGroup group:
                return Join(group.Filters.Select(f => Days(f, body, type)).ToList(), group.Logic == FilterLogic.And);
            case FieldFilter { Operator: FilterOperator.GreaterThanOrEqual or FilterOperator.LessThan } f
                when (Nullable.GetUnderlyingType(type) ?? type) == typeof(DateTimeOffset) && FilterValues.IsWholeDay(f.Value, out var day):
            {
                var (date, hasValue) = ClockDate(body, type);
                var start = Capture(day.ToDateTime(TimeOnly.MinValue));
                Expression compare = f.Operator == FilterOperator.LessThan ? Expression.LessThan(date, start) : Expression.GreaterThanOrEqual(date, start);
                return hasValue is null ? compare : Expression.AndAlso(hasValue, compare);
            }

            case FieldFilter f:
                return Default(f, body, type);
            default:
                return False;
        }
    }

    // An offset's date on its own clock: x.Date, through .Value when nullable, with what guards that.
    private static (Expression Date, Expression? HasValue) ClockDate(Expression body, Type type)
    {
        var date = typeof(DateTimeOffset).GetProperty(nameof(DateTimeOffset.Date))!;
        if (type == typeof(DateTimeOffset))
        {
            return (Expression.Property(body, date), null);
        }

        var value = Expression.Property(body, typeof(DateTimeOffset?).GetProperty(nameof(Nullable<DateTimeOffset>.Value))!);
        return (Expression.Property(value, date), Expression.Property(body, typeof(DateTimeOffset?).GetProperty(nameof(Nullable<DateTimeOffset>.HasValue))!));
    }

    // A condition on the value expression `body`, of `type`.
    private static Expression Default(FieldFilter filter, Expression body, Type type)
    {
        var kind = FilterValues.KindOf(type);
        var text = kind == FieldKind.Text;
        var ignoreCase = text && !filter.MatchCase;
        switch (filter.Operator)
        {
            case FilterOperator.IsNull:
                return FilterValues.CanBeNull(type) ? Expression.Equal(body, Expression.Constant(null, type)) : False;
            case FilterOperator.IsNotNull:
                return FilterValues.CanBeNull(type) ? Expression.NotEqual(body, Expression.Constant(null, type)) : True;
            case FilterOperator.IsEmpty:
                return Expression.OrElse(Expression.Equal(body, Expression.Constant(null, type)), Expression.Equal(body, Expression.Constant("")));
            case FilterOperator.IsNotEmpty:
                return Expression.AndAlso(Expression.NotEqual(body, Expression.Constant(null, type)), Expression.NotEqual(body, Expression.Constant("")));
            case FilterOperator.Equals or FilterOperator.NotEquals:
            {
                var equal = Equal(body, type, filter.Value, ignoreCase);
                return filter.Operator == FilterOperator.Equals ? equal : Not(equal);
            }

            case FilterOperator.Contains or FilterOperator.DoesNotContain or FilterOperator.StartsWith or FilterOperator.EndsWith:
            {
                if (FilterValues.Unwrap(filter.Value) is not { } raw || raw.ToString() is not { } value)
                {
                    return filter.Operator == FilterOperator.DoesNotContain ? False : True;
                }

                var method = filter.Operator switch
                {
                    FilterOperator.StartsWith => StringStartsWith,
                    FilterOperator.EndsWith => StringEndsWith,
                    _ => StringContains,
                };
                var match = Expression.AndAlso(
                    Expression.NotEqual(body, Expression.Constant(null, typeof(string))),
                    Expression.Call(ignoreCase ? Expression.Call(body, ToLower) : body, method,
                        Capture(ignoreCase ? value.ToLower(CultureInfo.CurrentCulture) : value)));
                return filter.Operator == FilterOperator.DoesNotContain ? Not(match) : match;
            }

            case FilterOperator.In or FilterOperator.NotIn:
            {
                var any = AnyOf(body, type, filter.Values, ignoreCase);
                return filter.Operator == FilterOperator.In ? any : Not(any);
            }

            case FilterOperator.Between:
                return And(Compare(body, type, filter.Value, ExpressionType.GreaterThanOrEqual, open: true),
                           Compare(body, type, filter.Value2, ExpressionType.LessThanOrEqual, open: true));
            case FilterOperator.GreaterThan:
                return Compare(body, type, filter.Value, ExpressionType.GreaterThan, open: false);
            case FilterOperator.GreaterThanOrEqual:
                return Compare(body, type, filter.Value, ExpressionType.GreaterThanOrEqual, open: false);
            case FilterOperator.LessThan:
                return Compare(body, type, filter.Value, ExpressionType.LessThan, open: false);
            case FilterOperator.LessThanOrEqual:
                return Compare(body, type, filter.Value, ExpressionType.LessThanOrEqual, open: false);
            case FilterOperator.OnDay:
                return OnDay(body, type, filter.Value);
            default:
                return False;
        }
    }

    // x == value: text ignoring case (x != null && x.ToLower() == value), enums as their
    // underlying number, as the C# compiler writes it. A null value is x == null.
    private static Expression Equal(Expression body, Type type, object? value, bool ignoreCase)
    {
        if (FilterValues.Unwrap(value) is null)
        {
            return FilterValues.CanBeNull(type) ? Expression.Equal(body, Expression.Constant(null, type)) : False;
        }

        if (!FilterValues.TryConvert(type, value, out var parsed))
        {
            return False;
        }

        if (ignoreCase)
        {
            return Expression.AndAlso(
                Expression.NotEqual(body, Expression.Constant(null, typeof(string))),
                Expression.Equal(Expression.Call(body, ToLower), Capture(((string)parsed!).ToLower(CultureInfo.CurrentCulture))));
        }

        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        Expression left = body;
        Expression right = Parameter(parsed, type);
        if (underlying.IsEnum)
        {
            var compareAs = type == underlying ? Enum.GetUnderlyingType(underlying) : typeof(Nullable<>).MakeGenericType(Enum.GetUnderlyingType(underlying));
            left = Expression.Convert(left, compareAs);
            right = Expression.Convert(right, compareAs);
        }

        return Expression.Equal(left, right);
    }

    /// <summary>
    /// <c>x op value</c>, with the value parsed for the field's type. For a whole-number field, a
    /// fraction rounds the way the comparison means ("at least 2.5" is 3 or more, "at most 2.5" 2
    /// or less), and a number beyond what the type holds keeps everything or nothing, as the
    /// comparison would, rather than failing. A null value (an open end of a range) holds always.
    /// </summary>
    private static Expression Compare(Expression body, Type type, object? value, ExpressionType op, bool open)
    {
        if (FilterValues.Unwrap(value) is null || FilterValues.Unwrap(value) is string { Length: 0 })
        {
            return open ? True : False;
        }

        if (!FilterValues.TryConvert(type, value, out var parsed) || parsed is null)
        {
            var (outcome, bound) = NumberBound(type, value, op);
            if (outcome != Bound.Value)
            {
                return outcome == Bound.All ? True : False;
            }

            parsed = bound;
        }

        var right = Parameter(parsed, type);
        return op switch
        {
            ExpressionType.GreaterThan => Expression.GreaterThan(body, right),
            ExpressionType.GreaterThanOrEqual => Expression.GreaterThanOrEqual(body, right),
            ExpressionType.LessThan => Expression.LessThan(body, right),
            _ => Expression.LessThanOrEqual(body, right),
        };
    }

    private enum Bound
    {
        /// <summary>A value to compare with.</summary>
        Value,

        /// <summary>Every value passes ("at most 1e12" on an int field).</summary>
        All,

        /// <summary>No value passes, or it isn't a number.</summary>
        None,
    }

    // A number the field's type can't hold exactly, as a bound for the comparison.
    private static (Bound Outcome, object? Value) NumberBound(Type type, object? value, ExpressionType op)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        if (!FilterValues.IsNumeric(t) || !FilterValues.TryNumber(value, out var number))
        {
            return (Bound.None, null);
        }

        // x >= 2.5 is x >= 3 and x > 2.5 is x > 2; x <= 2.5 is x <= 2 and x < 2.5 is x < 3.
        var above = op is ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual;
        var fractional = t == typeof(decimal) || t == typeof(double) || t == typeof(float);
        var whole = fractional ? number
            : op is ExpressionType.GreaterThanOrEqual or ExpressionType.LessThan ? Math.Ceiling(number) : Math.Floor(number);
        var (min, max) = Limits(t);
        if (whole > max)
        {
            return (above ? Bound.None : Bound.All, null);
        }

        if (whole < min)
        {
            return (above ? Bound.All : Bound.None, null);
        }

        return FilterValues.TryParse(type, whole.ToString("R", CultureInfo.InvariantCulture), out var parsed) ? (Bound.Value, parsed) : (Bound.None, null);
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

    /// <summary>
    /// The day in the value. DateOnly compares directly; DateTime keeps the day's range
    /// (<c>x &gt;= day &amp;&amp; x &lt; day + 1</c>, which uses an index); DateTimeOffset compares its
    /// <c>Date</c>, the day on the clock where it was recorded. A value that isn't a date matches
    /// nothing.
    /// </summary>
    private static Expression OnDay(Expression body, Type type, object? value)
    {
        if (!FilterValues.TryDay(value, out var day))
        {
            return False;
        }

        var t = Nullable.GetUnderlyingType(type) ?? type;
        if (t == typeof(DateOnly))
        {
            return Expression.Equal(body, Parameter(day, type));
        }

        if (t == typeof(DateTime))
        {
            // [day, day + 1): on the last representable day there is no next day, so it's open-ended.
            var start = day.ToDateTime(TimeOnly.MinValue);
            var onOrAfter = Expression.GreaterThanOrEqual(body, Parameter(start, type));
            return day == DateOnly.MaxValue ? onOrAfter : Expression.AndAlso(onOrAfter, Expression.LessThan(body, Parameter(start.AddDays(1), type)));
        }

        // DateTimeOffset(?): x.Date == day.
        var (date, hasValue) = ClockDate(body, type);
        var equal = Expression.Equal(date, Capture(day.ToDateTime(TimeOnly.MinValue)));
        return hasValue is null ? equal : Expression.AndAlso(hasValue, equal);
    }

    /// <summary>
    /// <c>values.Contains(x)</c> for the values that parse (EF Core turns it into <c>IN (...)</c>),
    /// text lower-cased when ignoring case; a null among them adds <c>x == null</c>. No valid value
    /// matches nothing.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "Enumerable.Contains<T> places no requirements on T, so any field type works.")]
    private static Expression AnyOf(Expression body, Type type, IReadOnlyList<object?> values, bool ignoreCase)
    {
        var parsed = new List<object>();
        var withNull = false;
        foreach (var value in values)
        {
            if (FilterValues.Unwrap(value) is null)
            {
                withNull |= FilterValues.CanBeNull(type);
            }
            else if (FilterValues.TryConvert(type, value, out var typed) && typed is not null)
            {
                parsed.Add(ignoreCase ? ((string)typed).ToLower(CultureInfo.CurrentCulture) : typed);
            }
        }

        parsed = parsed.Distinct().ToList();
        var isNull = withNull ? Expression.Equal(body, Expression.Constant(null, type)) : False;
        if (parsed.Count == 0)
        {
            return isNull;
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
        Expression test = ignoreCase
            ? Expression.AndAlso(Expression.NotEqual(body, Expression.Constant(null, typeof(string))), Expression.Call(contains, list, Expression.Call(body, ToLower)))
            : Expression.Call(contains, list, body);
        return Or(test, isNull);
    }

    /// <summary>
    /// The filter with each condition's values as its field's type where they convert: values that
    /// came back from JSON as text are Guids, dates and numbers again, for writing OData.
    /// </summary>
    public static Filter TypedFor<TItem>(Filter filter, IReadOnlyList<DataField<TItem>> fields) => !WithinLimits(filter) ? filter : filter switch
    {
        FieldFilter f => fields.FirstOrDefault(d => string.Equals(d.Key, f.Field, StringComparison.OrdinalIgnoreCase)) is { } field ? TypedDays(FilterValues.DayCondition(f, field.Type) ?? f, field.Type) : f,
        FilterGroup g => g with { Filters = g.Filters.Select(c => TypedFor(c, fields)).ToList() },
        NotFilter n => new NotFilter(TypedFor(n.Filter, fields)),
        _ => filter,
    };

    // What a comparison with a whole day came to, so OData is sent the same whole day.
    private static Filter TypedDays(Filter filter, Type type) => filter switch
    {
        FieldFilter f => Typed(f, type),
        FilterGroup g => g with { Filters = g.Filters.Select(c => TypedDays(c, type)).ToList() },
        NotFilter n => new NotFilter(TypedDays(n.Filter, type)),
        _ => filter,
    };

    // The condition's values as the field's type where they convert (a list's each), for a
    // field's own condition.
    private static FieldFilter Typed(FieldFilter filter, Type type)
    {
        object? Convert(object? value) => FilterValues.TryConvert(type, value, out var typed) ? typed : FilterValues.Unwrap(value);

        if (filter.Operator == FilterOperator.OnDay)
        {
            return FilterValues.TryDay(filter.Value, out var day) ? filter with { Value = day } : filter;
        }

        return filter.Operator is FilterOperator.In or FilterOperator.NotIn
            ? filter with { Value = filter.Values.Select(Convert).ToList() }
            : filter with { Value = Convert(filter.Value), Value2 = Convert(filter.Value2) };
    }

    // A lambda's body on another parameter, so conditions on several fields share one.
    private static Expression Rebind(LambdaExpression lambda, ParameterExpression x) =>
        new Rebinder(lambda.Parameters[0], x).Visit(lambda.Body);

    private sealed class Rebinder(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }
}
