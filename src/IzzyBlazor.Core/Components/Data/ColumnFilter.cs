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

    /// <summary>Whether it's a condition at all: an empty field isn't, nor an operator with no value, which only draws the panel that way.</summary>
    public bool IsFilter => ColumnFilter.TakesNoValue(Op) || Values.Count > 0 || (Op == FilterOperator.Between && !string.IsNullOrWhiteSpace(To));

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
    /// <summary>An operator's word for people, as a date reads it.</summary>
    public static string TextOf(ColumnFilterKind kind, FilterOperator op, IzzyText text)
    {
        var date = kind == ColumnFilterKind.Date;
        return op switch
        {
            FilterOperator.Contains => text.FilterContains,
            FilterOperator.DoesNotContain => text.FilterDoesNotContain,
            FilterOperator.Equals => date ? text.FilterOn : text.FilterEquals,
            FilterOperator.NotEquals => date ? text.FilterNotOn : text.FilterDoesNotEqual,
            FilterOperator.StartsWith => text.FilterStartsWith,
            FilterOperator.EndsWith => text.FilterEndsWith,
            FilterOperator.GreaterThan => date ? text.FilterAfter : text.FilterGreaterThan,
            FilterOperator.GreaterThanOrEqual => date ? text.FilterOnOrAfter : text.FilterGreaterOrEqual,
            FilterOperator.LessThan => date ? text.FilterBefore : text.FilterLessThan,
            FilterOperator.LessThanOrEqual => date ? text.FilterOnOrBefore : text.FilterLessOrEqual,
            FilterOperator.Between => text.FilterIsBetween,
            FilterOperator.IsEmpty => text.FilterIsEmpty,
            FilterOperator.IsNotEmpty => text.FilterIsNotEmpty,
            _ => op.ToString(),
        };
    }

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

    /// <summary>The operators with a word in the address, the same as in the filter's text form.</summary>
    private static readonly (FilterOperator Op, string Word)[] Words =
    [
        (FilterOperator.Equals, "eq"), (FilterOperator.NotEquals, "ne"), (FilterOperator.GreaterThan, "gt"), (FilterOperator.GreaterThanOrEqual, "ge"),
        (FilterOperator.LessThan, "lt"), (FilterOperator.LessThanOrEqual, "le"), (FilterOperator.Contains, "contains"), (FilterOperator.DoesNotContain, "notcontains"),
        (FilterOperator.StartsWith, "startswith"), (FilterOperator.EndsWith, "endswith"), (FilterOperator.Between, "between"), (FilterOperator.In, "in"),
        (FilterOperator.NotIn, "notin"), (FilterOperator.IsEmpty, "isempty"), (FilterOperator.IsNotEmpty, "isnotempty"),
    ];

    /// <summary>An operator's word in the address.</summary>
    public static string WordOf(FilterOperator op) => Words.First(w => w.Op == op).Word;

    /// <summary>The operator a word in the address names, or null for an unknown word.</summary>
    public static FilterOperator? OperatorOf(string? word)
    {
        foreach (var (op, known) in Words)
        {
            if (string.Equals(known, word, StringComparison.OrdinalIgnoreCase))
            {
                return op;
            }
        }

        return null;
    }

    /// <summary>Whether a condition with this operator stands on its own.</summary>
    public static bool TakesNoValue(FilterOperator op) => op is FilterOperator.IsEmpty or FilterOperator.IsNotEmpty;

    // Is empty is one choice for people; on a number or date, null is the only empty there is.
    private static List<FilterOperator> PanelOperators(ColumnFilterKind kind, Type? type)
    {
        List<FilterOperator> ops = kind switch
        {
            ColumnFilterKind.Text =>
            [
                FilterOperator.Contains, FilterOperator.DoesNotContain, FilterOperator.Equals, FilterOperator.NotEquals,
                FilterOperator.StartsWith, FilterOperator.EndsWith,
            ],
            ColumnFilterKind.Number or ColumnFilterKind.Date =>
            [
                FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual,
                FilterOperator.LessThan, FilterOperator.LessThanOrEqual, FilterOperator.Between,
            ],
            _ => [],
        };
        if (ops.Count > 0 && type is not null && FilterConvert.CanBeNull(type))
        {
            ops.AddRange([FilterOperator.IsEmpty, FilterOperator.IsNotEmpty]);
        }

        return ops;
    }

    /// <summary>
    /// The operators a column's filter offers, its default first: <c>FilterOperators</c> where it
    /// has them and they can be shown, all of them in a DataGrid, and in a Table only today's simple
    /// field's. More than one draws a Select.
    /// </summary>
    public static IReadOnlyList<FilterOperator> OperatorsOf<TItem>(ColumnBase<TItem> column)
    {
        if (column.FilterKind == ColumnFilterKind.Choice)
        {
            return [column.FilterMultiple ? FilterOperator.In : FilterOperator.Equals];
        }

        var panel = PanelOperators(column.FilterKind, column.ValueType);
        if (column.FilterOperators is { } chosen)
        {
            return chosen.Select(Same).Where(panel.Contains).Distinct().ToList();
        }

        if (column.Table is { GridColumns: true })
        {
            return panel;
        }

        return column.FilterKind switch
        {
            ColumnFilterKind.Text => [FilterOperator.Contains],
            ColumnFilterKind.Number => [FilterOperator.Between],
            ColumnFilterKind.Date => [FilterOperator.Equals],
            _ => [],
        };

        // What a panel writes for what's the same choice to people.
        FilterOperator Same(FilterOperator op) => op switch
        {
            FilterOperator.OnDay when column.FilterKind == ColumnFilterKind.Date => FilterOperator.Equals,
            FilterOperator.IsNull => FilterOperator.IsEmpty,
            FilterOperator.IsNotNull => FilterOperator.IsNotEmpty,
            _ => op,
        };
    }

    /// <summary>
    /// What a <c>FilterOperators</c> list allows on the column's field, so an address written by
    /// hand can't get round it; null without one.
    /// </summary>
    public static IReadOnlyList<FilterOperator>? FieldOperators<TItem>(ColumnBase<TItem> column) =>
        column.FilterOperators is null || column.FilterKind == ColumnFilterKind.Choice
            ? null
            : OperatorsOf(column).SelectMany<FilterOperator, FilterOperator>(op => op switch
            {
                FilterOperator.Equals when column.FilterKind == ColumnFilterKind.Date => [FilterOperator.Equals, FilterOperator.OnDay],
                FilterOperator.IsEmpty when column.FilterKind != ColumnFilterKind.Text => [FilterOperator.IsNull],
                FilterOperator.IsNotEmpty when column.FilterKind != ColumnFilterKind.Text => [FilterOperator.IsNotNull],
                _ => [op],
            }).ToList();

    /// <summary>Why a column's <c>FilterOperators</c> can't work, or null when they can.</summary>
    public static string? OperatorsProblem<TItem>(ColumnBase<TItem> column)
    {
        if (column.FilterOperators is null || column.FilterKind == ColumnFilterKind.Choice)
        {
            return null;
        }

        var ops = OperatorsOf(column);
        return ops.Count == 0 ? $"The \"{column.HeaderText}\" column's {nameof(column.FilterOperators)} has none its filter can offer."
            : TakesNoValue(ops[0]) ? $"The \"{column.HeaderText}\" column's first of {nameof(column.FilterOperators)} is {ops[0]}, but the first is the default, which needs a value, or the column would always be filtered."
            : null;
    }

    /// <summary>The operator a column's filter uses when the address names none.</summary>
    public static FilterOperator DefaultOperator<TItem>(ColumnBase<TItem> column) => OperatorsOf(column).FirstOrDefault();

    /// <summary>A column's filter with nothing in it.</summary>
    public static ColumnFilterValue Empty<TItem>(ColumnBase<TItem> column) => new(DefaultOperator(column), []);

    /// <summary>
    /// Every key in the address a column's filter uses, given its first (<c>f-total</c>), to clear
    /// them and to carry the rest of the address past them.
    /// </summary>
    public static IEnumerable<string> KeysOf<TItem>(ColumnBase<TItem> column, string key) => column.FilterKind == ColumnFilterKind.Choice
        ? [key]
        : [key, $"{key}-op", $"{key}-to"];

    /// <summary>
    /// A column's filter from the address, or null when it has none. A word the column doesn't offer
    /// reads as its default. A repeated key is a list of values to text, the first one to anything
    /// that takes one value. Each field posts whether it's used or not, so a value the operator
    /// doesn't take is dropped, and an operator with no value is only what the panel draws.
    /// </summary>
    public static ColumnFilterValue? Read<TItem>(ColumnBase<TItem> column, string key, IReadOnlyDictionary<string, StringValues> query)
    {
        var ops = OperatorsOf(column);
        var fallback = ops.FirstOrDefault();
        var op = OperatorOf(query.GetValueOrDefault($"{key}-op").FirstOrDefault()) is { } named && ops.Contains(named) ? named : fallback;
        var values = query.GetValueOrDefault(key).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        if (values.Count > 1 && column.FilterKind == ColumnFilterKind.Text && op == fallback && column.FilterOperators is null)
        {
            op = FilterOperator.In;
        }
        else if (values.Count > 1 && op != FilterOperator.In)
        {
            values.RemoveRange(1, values.Count - 1);
        }

        var value = TakesNoValue(op)
            ? new ColumnFilterValue(op, [])
            : new ColumnFilterValue(op, values, op == FilterOperator.Between ? query.GetValueOrDefault($"{key}-to").FirstOrDefault() : null);
        return value.IsFilter || op != fallback ? value : null;
    }

    /// <summary>
    /// What a column's filter makes of the address: its value for each of the column's keys, null
    /// for one it doesn't use. Null clears them all. <see cref="Read"/> reads it back. The default
    /// operator isn't written, nor one with no value to go with it.
    /// </summary>
    public static Dictionary<string, object?> Write<TItem>(ColumnBase<TItem> column, string key, ColumnFilterValue? value)
    {
        var keys = KeysOf(column, key).ToDictionary(k => k, _ => (object?)null);
        if (value is { IsFilter: true })
        {
            if (keys.ContainsKey($"{key}-op") && value.Op != DefaultOperator(column) && value.Op != FilterOperator.In)
            {
                keys[$"{key}-op"] = WordOf(value.Op);
            }

            if (value.Values.Count > 0 && !TakesNoValue(value.Op))
            {
                keys[key] = value.Values.OfType<string>().ToArray();
            }

            if (value.Op == FilterOperator.Between && !string.IsNullOrWhiteSpace(value.To))
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

        var text = column.FilterKind == ColumnFilterKind.Text;
        return value.Op switch
        {
            FilterOperator.IsEmpty => new FieldFilter(key, text ? FilterOperator.IsEmpty : FilterOperator.IsNull),
            FilterOperator.IsNotEmpty => new FieldFilter(key, text ? FilterOperator.IsNotEmpty : FilterOperator.IsNotNull),
            FilterOperator.Between => new FieldFilter(key, FilterOperator.Between, Typed(value.Value), Typed(value.To)),
            FilterOperator.In => new FieldFilter(key, FilterOperator.In, value.Values.Select(Typed).ToList()),
            _ => new FieldFilter(key, value.Op, Typed(value.Value)),
        };
    }

    /// <summary>
    /// What a column's panel shows for a condition on it, or null when the panel can't show it
    /// (an operator it doesn't offer, matching case, several values on a single choice, a date and
    /// time where it takes days): a bound filter's other conditions still apply, and show as tags of
    /// their own.
    /// </summary>
    public static ColumnFilterValue? StateOf<TItem>(ColumnBase<TItem> column, FieldFilter filter)
    {
        if (filter.MatchCase && column.FilterKind == ColumnFilterKind.Text)
        {
            return null;
        }

        var ops = OperatorsOf(column);
        var value = FilterConvert.Unwrap(filter.Value);
        if (column.FilterKind == ColumnFilterKind.Choice)
        {
            return filter.Operator switch
            {
                FilterOperator.Equals when value is not null => new(ops[0], [FilterConvert.Format(value)]),
                FilterOperator.In when filter.Values.Count > 0 && (column.FilterMultiple || filter.Values.Count == 1) && filter.Values.All(v => FilterConvert.Unwrap(v) is not null)
                    => new(ops[0], filter.Values.Select(FilterConvert.Format).ToList()),
                _ => null,
            };
        }

        var date = column.FilterKind == ColumnFilterKind.Date;
        var op = filter.Operator switch
        {
            FilterOperator.OnDay when date => FilterOperator.Equals,
            FilterOperator.IsNull when column.FilterKind != ColumnFilterKind.Text => FilterOperator.IsEmpty,
            FilterOperator.IsNotNull when column.FilterKind != ColumnFilterKind.Text => FilterOperator.IsNotEmpty,
            var same => same,
        };

        if (TakesNoValue(op))
        {
            return ops.Contains(op) ? new(op, []) : null;
        }

        // A day is what a date panel holds; a date and time isn't one, and stays a condition of its own.
        // Nor is text a box would read back as something else: empty, or with spaces at an end.
        string? Shown(object? end, bool optional) => FilterConvert.Unwrap(end) is null ? optional ? "" : null
            : column.FilterKind == ColumnFilterKind.Text ? FilterConvert.Format(end) is { Length: > 0 } text && text.Trim() == text ? text : null
            : !date ? FilterConvert.Format(end)
            : filter.Operator == FilterOperator.OnDay && FilterConvert.TryDay(end, out var on) ? FilterConvert.Format(on)
            : FilterConvert.IsWholeDay(end, out var day) ? FilterConvert.Format(day)
            : null;

        // A panel with Between and not this comparison shows it as a range.
        if (!ops.Contains(op) && ops.Contains(FilterOperator.Between) && column.FilterKind == ColumnFilterKind.Number)
        {
            filter = op switch
            {
                FilterOperator.GreaterThanOrEqual => filter with { Operator = FilterOperator.Between, Value2 = null },
                FilterOperator.LessThanOrEqual => filter with { Operator = FilterOperator.Between, Value = null, Value2 = filter.Value },
                FilterOperator.Equals => filter with { Operator = FilterOperator.Between, Value2 = filter.Value },
                _ => filter,
            };
            op = filter.Operator;
        }

        if (!ops.Contains(op))
        {
            return null;
        }

        if (op == FilterOperator.Between)
        {
            if (Shown(filter.Value, optional: true) is not { } low || Shown(filter.Value2, optional: true) is not { } high || (low.Length == 0 && high.Length == 0))
            {
                return null;
            }

            return new(op, low.Length == 0 ? [] : [low], high.Length == 0 ? null : high);
        }

        return Shown(filter.Value, optional: false) is { } shown ? new(op, [shown]) : null;
    }

    internal static bool TryParseDay(string text, out DateOnly day) => FilterConvert.TryParseDay(text, out day);
}
