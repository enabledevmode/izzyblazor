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
/// A Table column's filter as people type it (the text in its panel, as in the address) and as
/// the condition it stands for: contains for text, between for numbers, on day for dates, equals
/// or in for choices. The conditions themselves are built by <see cref="FilterExpression"/>.
/// </summary>
internal static class ColumnFilter
{
    public static ColumnFilterKind KindOf(Type type) => FilterValues.KindOf(type) switch
    {
        FieldKind.Text => ColumnFilterKind.Text,
        FieldKind.Choice => ColumnFilterKind.Choice,
        FieldKind.Date => ColumnFilterKind.Date,
        FieldKind.Number => ColumnFilterKind.Number,
        _ => ColumnFilterKind.None,
    };

    /// <summary>Enum names (with [Display(Name)] text) or true/false.</summary>
    public static IReadOnlyList<(string Value, string Text)> Choices(Type type, IzzyText text) =>
        (Nullable.GetUnderlyingType(type) ?? type) == typeof(bool)
            ? [("true", text.Yes), ("false", text.No)]
            : DataFields.EnumChoices(type) ?? [];

    /// <summary>
    /// The condition a column's filter text stands for, or null for empty text. Values are parsed
    /// for the column's type (<see cref="FilterValues.Read"/>); one that doesn't parse stays text and
    /// matches nothing.
    /// </summary>
    public static FieldFilter? Condition<TItem>(ColumnBase<TItem> column, string text)
    {
        text = text.Trim();
        if (text.Length == 0 || column.Key is not { } key || column.ValueType is not { } type)
        {
            return null;
        }

        switch (column.FilterKind)
        {
            case ColumnFilterKind.Text:
                return new FieldFilter(key, FilterOperator.Contains, text);
            case ColumnFilterKind.Date:
                return new FieldFilter(key, FilterOperator.OnDay, FilterValues.TryParseDay(text, out var day) ? day : text);
            case ColumnFilterKind.Number:
            {
                var (min, max) = SplitRange(text);
                return new FieldFilter(key, FilterOperator.Between,
                    min.Length == 0 ? null : FilterValues.Read(type, min),
                    max.Length == 0 ? null : FilterValues.Read(type, max));
            }

            case ColumnFilterKind.Choice when column.FilterMultiple:
                return new FieldFilter(key, FilterOperator.In, SplitValues(text).Select(v => (object?)FilterValues.Read(type, v)).ToList());
            case ColumnFilterKind.Choice:
                return new FieldFilter(key, FilterOperator.Equals, FilterValues.Read(type, text));
            default:
                return null;
        }
    }

    /// <summary>
    /// The text a column's panel shows for a condition on it, or null when the panel can't show it
    /// (another operator, matching case, several values on a single choice): a bound filter's
    /// other conditions still apply, and show as tags of their own.
    /// </summary>
    public static string? TextOf<TItem>(ColumnBase<TItem> column, FieldFilter filter)
    {
        if (filter.MatchCase && column.FilterKind == ColumnFilterKind.Text)
        {
            return null;
        }

        var value = FilterValues.Unwrap(filter.Value);
        return (column.FilterKind, filter.Operator) switch
        {
            (ColumnFilterKind.Text, FilterOperator.Contains) when value is not null => FilterValues.Format(value),
            (ColumnFilterKind.Date, FilterOperator.OnDay) when FilterValues.TryDay(value, out var day) => FilterValues.Format(day),
            (ColumnFilterKind.Number, FilterOperator.Between) => NonEmpty(JoinRange(FilterValues.Format(value), FilterValues.Format(filter.Value2))),
            (ColumnFilterKind.Number, FilterOperator.GreaterThanOrEqual) when value is not null => JoinRange(FilterValues.Format(value), ""),
            (ColumnFilterKind.Number, FilterOperator.LessThanOrEqual) when value is not null => JoinRange("", FilterValues.Format(value)),
            (ColumnFilterKind.Number, FilterOperator.Equals) when value is not null => JoinRange(FilterValues.Format(value), FilterValues.Format(value)),
            (ColumnFilterKind.Choice, FilterOperator.Equals) when value is not null => FilterValues.Format(value),
            (ColumnFilterKind.Choice, FilterOperator.In) when filter.Values.Count > 0 && (column.FilterMultiple || filter.Values.Count == 1) && filter.Values.All(v => FilterValues.Unwrap(v) is not null)
                => string.Join(',', filter.Values.Select(FilterValues.Format)),
            _ => null,
        };

        static string? NonEmpty(string text) => text.Length > 0 ? text : null;
    }

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

    /// <summary>Several values in one filter value: "Paid,Pending" (repeated URL keys arrive joined).</summary>
    internal static IReadOnlyList<string> SplitValues(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    internal static bool TryParseDay(string text, out DateOnly day) => FilterValues.TryParseDay(text, out day);
}
