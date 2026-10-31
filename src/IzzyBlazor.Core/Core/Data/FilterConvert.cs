using System.Globalization;
using System.Text.Json;

namespace IzzyBlazor;

/// <summary>What kind of value a field holds, which decides the operators it takes.</summary>
internal enum FieldKind
{
    /// <summary>Nothing a filter can compare.</summary>
    None,

    /// <summary>Strings.</summary>
    Text,

    /// <summary>Numbers.</summary>
    Number,

    /// <summary>DateOnly, DateTime and DateTimeOffset.</summary>
    Date,

    /// <summary>TimeOnly and TimeSpan.</summary>
    Time,

    /// <summary>Enums and booleans.</summary>
    Choice,

    /// <summary>Guids.</summary>
    Guid,
}

/// <summary>
/// Turns filter values into the field's type: text as the person writes it (the current culture,
/// then the invariant form), numbers, dates and the values JSON gives back.
/// </summary>
internal static class FilterConvert
{
    public static FieldKind KindOf(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        if (t == typeof(string))
        {
            return FieldKind.Text;
        }

        if (t.IsEnum || t == typeof(bool))
        {
            return FieldKind.Choice;
        }

        if (t == typeof(DateOnly) || t == typeof(DateTime) || t == typeof(DateTimeOffset))
        {
            return FieldKind.Date;
        }

        if (t == typeof(TimeOnly) || t == typeof(TimeSpan))
        {
            return FieldKind.Time;
        }

        if (t == typeof(System.Guid))
        {
            return FieldKind.Guid;
        }

        return IsNumeric(t) ? FieldKind.Number : FieldKind.None;
    }

    public static bool IsNumeric(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type == typeof(int) || type == typeof(long) || type == typeof(decimal) || type == typeof(double)
            || type == typeof(float) || type == typeof(short) || type == typeof(byte) || type == typeof(uint)
            || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte);
    }

    /// <summary>Whether a field of this type can hold null: reference types and <c>Nullable&lt;T&gt;</c>.</summary>
    public static bool CanBeNull(Type type) => !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;

    /// <summary>The operators a field of this type takes.</summary>
    public static IReadOnlyList<FilterOperator> OperatorsFor(Type type)
    {
        var ops = KindOf(type) switch
        {
            FieldKind.Text => new List<FilterOperator>
            {
                FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.Contains, FilterOperator.DoesNotContain,
                FilterOperator.StartsWith, FilterOperator.EndsWith, FilterOperator.In, FilterOperator.NotIn,
                FilterOperator.IsEmpty, FilterOperator.IsNotEmpty,
            },
            FieldKind.Number or FieldKind.Time => Comparisons(),
            FieldKind.Date => [.. Comparisons(), FilterOperator.OnDay],
            FieldKind.Choice or FieldKind.Guid => [FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.In, FilterOperator.NotIn],
            _ => [],
        };

        if (ops.Count > 0 && CanBeNull(type))
        {
            ops.AddRange([FilterOperator.IsNull, FilterOperator.IsNotNull]);
        }

        return ops;

        static List<FilterOperator> Comparisons() =>
        [
            FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.LessThan, FilterOperator.LessThanOrEqual,
            FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual, FilterOperator.Between, FilterOperator.In, FilterOperator.NotIn,
        ];
    }

    /// <summary>A value JSON gave back (a <see cref="JsonElement"/>) as a plain one: text, a number, true or false, a list, or null.</summary>
    public static object? Unwrap(object? value) => value is JsonElement element ? FromJson(element) : value;

    private static object? FromJson(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.TryGetDecimal(out var d) ? d : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Array => element.EnumerateArray().Select(FromJson).ToList(),
        _ => null,
    };

    /// <summary>
    /// A value as the field's type: text parsed (<see cref="TryParse"/>), a number converted when it
    /// fits exactly, a date as another kind of date. Null converts only for a type that can be null.
    /// </summary>
    public static bool TryConvert(Type type, object? value, out object? result)
    {
        result = null;
        value = Unwrap(value);
        var t = Nullable.GetUnderlyingType(type) ?? type;
        switch (value)
        {
            case null:
                return CanBeNull(type);
            case string s:
                return TryParse(type, s, out result);
            case var _ when t.IsInstanceOfType(value):
                result = value;
                return true;
        }

        if (t.IsEnum)
        {
            if (IsNumeric(value.GetType()))
            {
                try
                {
                    var e = Enum.ToObject(t, Convert.ToInt64(value, CultureInfo.InvariantCulture));
                    if (Enum.IsDefined(t, e))
                    {
                        result = e;
                        return true;
                    }
                }
                catch (Exception ex) when (ex is OverflowException or InvalidCastException)
                {
                }
            }

            return TryParse(type, value.ToString() ?? "", out result);
        }

        if (IsNumeric(t) && IsNumeric(value.GetType()))
        {
            try
            {
                var number = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                if (t != typeof(double) && t != typeof(float) && t != typeof(decimal) && number != decimal.Truncate(number))
                {
                    return false;
                }

                result = Convert.ChangeType(value, t, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception ex) when (ex is OverflowException or InvalidCastException or FormatException)
            {
                // double too large for decimal, or for the type.
                if (t == typeof(double) || t == typeof(float))
                {
                    result = Convert.ChangeType(value, t, CultureInfo.InvariantCulture);
                    return true;
                }

                return false;
            }
        }

        if (KindOf(t) == FieldKind.Date && TryDay(value, out var day))
        {
            // A day (or a date and time) as another kind of date: its date and time, as on the clock.
            DateTime clock = value switch
            {
                DateTime dt => dt,
                DateTimeOffset dto => dto.DateTime,
                _ => day.ToDateTime(TimeOnly.MinValue),
            };
            // Boxed one by one: DateTime converts to DateTimeOffset, which a ?: would choose for both.
            result = t == typeof(DateOnly) ? (object)DateOnly.FromDateTime(clock)
                : t == typeof(DateTime) ? (object)clock
                : value is DateTime { Kind: not DateTimeKind.Unspecified } known ? (object)new DateTimeOffset(known) // UTC, or the local zone's offset
                : new DateTimeOffset(DateTime.SpecifyKind(clock, DateTimeKind.Unspecified), TimeSpan.Zero);
            return true;
        }

        return false;
    }

    /// <summary>A value that names a whole day, not an instant: a date, or text with no time in it.</summary>
    public static bool IsWholeDay(object? value, out DateOnly day)
    {
        switch (Unwrap(value))
        {
            case DateOnly d:
                day = d;
                return true;
            case string s when !s.Contains(':', StringComparison.Ordinal):
                return TryParseDay(s, out day);
            default:
                day = default;
                return false;
        }
    }

    /// <summary>
    /// What a condition means when it compares a field with a time to a whole day, which is all of that
    /// day, not its first instant: "on or before 3 Jan" keeps 3 Jan at noon. Null when nothing changes.
    /// An offset's "on or after" and "before" come back as they are, as they read the day on its clock.
    /// </summary>
    public static Filter? DayCondition(FieldFilter filter, Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        if (t != typeof(DateTime) && t != typeof(DateTimeOffset))
        {
            return null;
        }

        var (field, offset) = (filter.Field, t == typeof(DateTimeOffset));
        Filter Through(DateOnly day) => day == DateOnly.MaxValue
            ? Filter.Where(field, FilterOperator.LessThanOrEqual, DateTime.MaxValue)
            : Filter.Where(field, FilterOperator.LessThan, day.AddDays(1));

        switch (filter.Operator)
        {
            case FilterOperator.Equals when IsWholeDay(filter.Value, out var day):
                return Filter.Where(field, FilterOperator.OnDay, day);
            case FilterOperator.NotEquals when IsWholeDay(filter.Value, out var day):
                return new NotFilter(Filter.Where(field, FilterOperator.OnDay, day));
            case FilterOperator.LessThanOrEqual when IsWholeDay(filter.Value, out var day):
                return Through(day);
            case FilterOperator.GreaterThan when IsWholeDay(filter.Value, out var day):
                // Nothing comes after the last day.
                return day == DateOnly.MaxValue ? Filter.Any() : Filter.Where(field, FilterOperator.GreaterThanOrEqual, day.AddDays(1));
            case FilterOperator.GreaterThanOrEqual or FilterOperator.LessThan when offset && IsWholeDay(filter.Value, out _):
                return filter;
            case FilterOperator.Between when IsWholeDay(filter.Value2, out _) || (offset && IsWholeDay(filter.Value, out _)):
            {
                var parts = new List<Filter>();
                if (Unwrap(filter.Value) is not null)
                {
                    parts.Add(Filter.Where(field, FilterOperator.GreaterThanOrEqual, filter.Value));
                }

                if (Unwrap(filter.Value2) is not null)
                {
                    parts.Add(IsWholeDay(filter.Value2, out var to) ? Through(to) : Filter.Where(field, FilterOperator.LessThanOrEqual, filter.Value2));
                }

                return Filter.All([.. parts]);
            }

            default:
                return null;
        }
    }

    /// <summary>A value as a day: a date, a date and time (its date on the clock), or text (<see cref="TryParseDay"/>).</summary>
    public static bool TryDay(object? value, out DateOnly day)
    {
        switch (Unwrap(value))
        {
            case DateOnly d:
                day = d;
                return true;
            case DateTime dt:
                day = DateOnly.FromDateTime(dt);
                return true;
            case DateTimeOffset dto:
                day = DateOnly.FromDateTime(dto.DateTime);
                return true;
            case string s:
                return TryParseDay(s, out day);
            default:
                day = default;
                return false;
        }
    }

    /// <summary>A number as a double, for a bound beyond what the field's type holds: text in the current culture or the invariant form, or any number.</summary>
    public static bool TryNumber(object? value, out double number)
    {
        number = 0;
        value = Unwrap(value);
        if (value is string s)
        {
            return TryParse(typeof(double), s, out var parsed) && parsed is double d && !double.IsNaN(number = d);
        }

        if (value is not null && IsNumeric(value.GetType()))
        {
            number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return !double.IsNaN(number);
        }

        return false;
    }

    // A date input sends yyyy-MM-dd; someone typing into the URL may write their own form.
    public static bool TryParseDay(string text, out DateOnly day) =>
        DateOnly.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day)
        || DateOnly.TryParse(text.Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out day);

    /// <summary>
    /// Text as a value of the type: enums by name (ignoring case) or number, true or false, numbers
    /// as the person writes them (the current culture, "1.234,5" in German, with its groups whole) or
    /// the invariant form ("1234.5"), dates and times in the invariant (ISO) form or the current culture's. A fraction
    /// doesn't parse for a whole-number type.
    /// </summary>
    public static bool TryParse(Type type, string text, out object? value)
    {
        value = null;
        var t = Nullable.GetUnderlyingType(type) ?? type;
        text = text.Trim();
        if (t == typeof(string))
        {
            value = text;
            return true;
        }

        if (t.IsEnum)
        {
            // One name or number: "Paid,Failed" would parse as the two combined, as flags.
            if (!text.Contains(',', StringComparison.Ordinal) && Enum.TryParse(t, text, ignoreCase: true, out var e) && Enum.IsDefined(t, e!))
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

        if (t == typeof(System.Guid))
        {
            if (System.Guid.TryParse(text, out var g))
            {
                value = g;
                return true;
            }

            return false;
        }

        if (t == typeof(DateOnly))
        {
            // A day, or a date and time (as JSON writes one), whose date it is.
            if (TryParseDay(text, out var day))
            {
                value = day;
                return true;
            }

            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var withTime))
            {
                value = DateOnly.FromDateTime(withTime);
                return true;
            }

            return false;
        }

        if (t == typeof(DateTime))
        {
            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt)
                || DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out dt))
            {
                value = dt;
                return true;
            }

            return false;
        }

        if (t == typeof(DateTimeOffset))
        {
            // No offset written: UTC.
            if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto)
                || DateTimeOffset.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.AssumeUniversal, out dto))
            {
                value = dto;
                return true;
            }

            return false;
        }

        if (t == typeof(TimeOnly))
        {
            if (TimeOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
                || TimeOnly.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out time))
            {
                value = time;
                return true;
            }

            return false;
        }

        if (t == typeof(TimeSpan))
        {
            if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var span) || TimeSpan.TryParse(text, CultureInfo.CurrentCulture, out span))
            {
                value = span;
                return true;
            }

            return false;
        }

        if (!IsNumeric(t))
        {
            return false;
        }

        // Numbers: as the person writes them, else the invariant form; then converted to the
        // field's type (a fraction doesn't match a whole-number field). Whole numbers and decimals
        // go through decimal, which holds every long exactly; a double only past its range.
        foreach (var (culture, styles) in new[]
        {
            (CultureInfo.CurrentCulture, NumberStyles.Float | NumberStyles.AllowThousands),
            (CultureInfo.InvariantCulture, NumberStyles.Float),
        })
        {
            // Not as a culture's thousands unless its groups are whole: "1.5" in German isn't 15. Nor
            // Infinity or NaN, which no filter's text can write back.
            if (!GroupsAreWhole(text, culture) || !double.TryParse(text, styles, culture, out var number) || !double.IsFinite(number))
            {
                continue;
            }

            try
            {
                var exact = decimal.TryParse(text, styles, culture, out var m) ? m : (decimal?)null;
                value = t == typeof(double) || t == typeof(float)
                    ? Convert.ChangeType(number, t, CultureInfo.InvariantCulture)
                    : exact is not { } d ? null
                    : t == typeof(decimal) ? d
                    : d == decimal.Truncate(d) ? Convert.ChangeType(d, t, CultureInfo.InvariantCulture) : null;
                return value is not null;
            }
            catch (Exception ex) when (ex is OverflowException or FormatException or InvalidCastException)
            {
                return false;
            }
        }

        return false;
    }

    private static bool GroupsAreWhole(string text, CultureInfo culture)
    {
        var format = culture.NumberFormat;
        if (format.NumberGroupSeparator.Length == 0 || !text.Contains(format.NumberGroupSeparator, StringComparison.Ordinal))
        {
            return true;
        }

        var groups = text.Split(format.NumberDecimalSeparator)[0].TrimStart('-', '+').Split(format.NumberGroupSeparator);
        var sizes = format.NumberGroupSizes;
        return groups[0].Length >= 1 && groups[0].Length <= sizes.Max()
            && groups[^1].Length == sizes[0]
            && groups.Skip(1).All(g => sizes.Contains(g.Length));
    }

    /// <summary>
    /// Text typed into a filter, as the field's type when it parses; for a number that the type
    /// can't hold (a fraction for whole numbers, or too large), the number, which a comparison
    /// rounds or limits; otherwise the text, which matches nothing.
    /// </summary>
    public static object Read(Type type, string text)
    {
        if (TryParse(type, text, out var value) && value is not null)
        {
            return value;
        }

        if (IsNumeric(type) && TryParse(typeof(decimal), text, out var number) && number is not null)
        {
            return number;
        }

        return IsNumeric(type) && TryParse(typeof(double), text, out var large) && large is not null ? large : text.Trim();
    }

    /// <summary>A value as text a filter field shows: numbers in the current culture, days as yyyy-MM-dd, enums by name.</summary>
    public static string Format(object? value) => Unwrap(value) switch
    {
        null => "",
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime dt when dt.TimeOfDay == TimeSpan.Zero => dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, CultureInfo.CurrentCulture),
        var v => v.ToString() ?? "",
    };
}
