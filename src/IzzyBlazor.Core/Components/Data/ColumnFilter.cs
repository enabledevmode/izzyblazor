using Microsoft.Extensions.Primitives;

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
/// One column's filter, as its panel and the address hold it: the operator, the values as they were
/// typed (several for a list, so a comma in one is just a comma), and a range's upper end.
/// </summary>
internal sealed record ColumnFilterValue(FilterOperator Op, IReadOnlyList<string?> Values, string? To = null)
{
    /// <summary>The first value, or "" for none.</summary>
    public string Value => Values.Count > 0 ? Values[0] ?? "" : "";

    /// <summary>Whether it's a condition at all: an empty field isn't.</summary>
    public bool IsFilter => Values.Count > 0 || !string.IsNullOrWhiteSpace(To);

    /// <summary>What it is, for telling whether it changed.</summary>
    public string Signature => $"{Op}\u001f{string.Join('\u001f', Values)}\u001f{To}";
}

/// <summary>
/// A Table column's filter as people type it (<see cref="ColumnFilterValue"/>, in its panel and in
/// the address) and as the condition it stands for: contains for text, between for numbers, equals
/// for dates, equals or in for choices. The conditions themselves are built by
/// <see cref="FilterExpression"/>.
/// </summary>
internal static class ColumnFilter
{
    public static ColumnFilterKind KindOf(Type type) => FilterConvert.KindOf(type) switch
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

    /// <summary>The operator a column's filter uses when the address names none.</summary>
    public static FilterOperator DefaultOperator<TItem>(ColumnBase<TItem> column) => column.FilterKind switch
    {
        ColumnFilterKind.Text => FilterOperator.Contains,
        ColumnFilterKind.Number => FilterOperator.Between,
        ColumnFilterKind.Choice when column.FilterMultiple => FilterOperator.In,
        _ => FilterOperator.Equals,
    };

    /// <summary>A column's filter with nothing in it.</summary>
    public static ColumnFilterValue Empty<TItem>(ColumnBase<TItem> column) => new(DefaultOperator(column), []);

    /// <summary>
    /// Every key in the address a column's filter uses, given its first (<c>f-total</c>), to clear
    /// them and to carry the rest of the address past them.
    /// </summary>
    public static IEnumerable<string> KeysOf<TItem>(ColumnBase<TItem> column, string key) => column.FilterKind == ColumnFilterKind.Number
        ? [key, $"{key}-to"]
        : [key];

    /// <summary>
    /// A column's filter from the address, or null when it has none. A repeated key is a list of
    /// values to text, the first one to anything that takes one value.
    /// </summary>
    public static ColumnFilterValue? Read<TItem>(ColumnBase<TItem> column, string key, IReadOnlyDictionary<string, StringValues> query)
    {
        var op = DefaultOperator(column);
        var values = query.GetValueOrDefault(key).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        if (values.Count > 1 && column.FilterKind == ColumnFilterKind.Text)
        {
            op = FilterOperator.In;
        }
        else if (values.Count > 1 && op != FilterOperator.In)
        {
            values.RemoveRange(1, values.Count - 1);
        }

        var value = new ColumnFilterValue(op, values, column.FilterKind == ColumnFilterKind.Number ? query.GetValueOrDefault($"{key}-to").FirstOrDefault() : null);
        return value.IsFilter ? value : null;
    }

    /// <summary>
    /// What a column's filter makes of the address: its value for each of the column's keys, null
    /// for one it doesn't use. Null clears them all. <see cref="Read"/> reads it back.
    /// </summary>
    public static Dictionary<string, object?> Write<TItem>(ColumnBase<TItem> column, string key, ColumnFilterValue? value)
    {
        var keys = KeysOf(column, key).ToDictionary(k => k, _ => (object?)null);
        if (value is { IsFilter: true })
        {
            if (value.Values.Count > 0)
            {
                keys[key] = value.Values.OfType<string>().ToArray();
            }

            if (!string.IsNullOrWhiteSpace(value.To))
            {
                keys[$"{key}-to"] = value.To;
            }
        }

        return keys;
    }

    /// <summary>
    /// The condition a column's filter stands for, or null for no filter. Values are parsed for the
    /// column's type (<see cref="FilterConvert.Read"/>), and a date stays a day; one that doesn't
    /// parse stays text and matches nothing.
    /// </summary>
    public static FieldFilter? Condition<TItem>(ColumnBase<TItem> column, ColumnFilterValue value)
    {
        if (!value.IsFilter || column.Key is not { } key || column.ValueType is not { } type)
        {
            return null;
        }

        object? Typed(string? text) => string.IsNullOrWhiteSpace(text) ? null
            : column.FilterKind == ColumnFilterKind.Date && FilterConvert.TryParseDay(text, out var day) ? day
            : FilterConvert.Read(type, text);

        return value.Op switch
        {
            FilterOperator.Between => new FieldFilter(key, FilterOperator.Between, Typed(value.Value), Typed(value.To)),
            FilterOperator.In => new FieldFilter(key, FilterOperator.In, value.Values.Select(Typed).ToList()),
            _ => new FieldFilter(key, value.Op, Typed(value.Value)),
        };
    }

    /// <summary>
    /// What a column's panel shows for a condition on it, or null when the panel can't show it
    /// (another operator, matching case, several values on a single choice): a bound filter's
    /// other conditions still apply, and show as tags of their own.
    /// </summary>
    public static ColumnFilterValue? StateOf<TItem>(ColumnBase<TItem> column, FieldFilter filter)
    {
        if (filter.MatchCase && column.FilterKind == ColumnFilterKind.Text)
        {
            return null;
        }

        var value = FilterConvert.Unwrap(filter.Value);
        var op = DefaultOperator(column);
        var text = FilterConvert.Format(value);
        return (column.FilterKind, filter.Operator) switch
        {
            (ColumnFilterKind.Text, FilterOperator.Contains) when value is not null => new(op, [text]),
            (ColumnFilterKind.Date, FilterOperator.OnDay) when FilterConvert.TryDay(value, out var day) => new(op, [FilterConvert.Format(day)]),
            (ColumnFilterKind.Date, FilterOperator.Equals) when FilterConvert.IsWholeDay(value, out var day) => new(op, [FilterConvert.Format(day)]),
            (ColumnFilterKind.Number, FilterOperator.Between) => Range(op, text, FilterConvert.Format(filter.Value2)),
            (ColumnFilterKind.Number, FilterOperator.GreaterThanOrEqual) when value is not null => Range(op, text, ""),
            (ColumnFilterKind.Number, FilterOperator.LessThanOrEqual) when value is not null => Range(op, "", text),
            (ColumnFilterKind.Number, FilterOperator.Equals) when value is not null => Range(op, text, text),
            (ColumnFilterKind.Choice, FilterOperator.Equals) when value is not null => new(op, [text]),
            (ColumnFilterKind.Choice, FilterOperator.In) when filter.Values.Count > 0 && (column.FilterMultiple || filter.Values.Count == 1) && filter.Values.All(v => FilterConvert.Unwrap(v) is not null)
                => new(op, filter.Values.Select(FilterConvert.Format).ToList()),
            _ => null,
        };

        static ColumnFilterValue? Range(FilterOperator op, string from, string to) =>
            from.Length == 0 && to.Length == 0 ? null : new(op, from.Length == 0 ? [] : [from], to.Length == 0 ? null : to);
    }

    internal static bool TryParseDay(string text, out DateOnly day) => FilterConvert.TryParseDay(text, out day);
}
