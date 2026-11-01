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

    /// <summary>A list of checkboxes (enums and booleans); matches any value ticked, and all ticked is no filter.</summary>
    Choice,

    /// <summary>A date input (DateOnly, DateTime, DateTimeOffset); matches that day.</summary>
    Date,
}

/// <summary>
/// One column's filter, as its panel and the address hold it: the operator, the values as they were
/// typed (several for a list, so a comma in one is just a comma, and null is its blank row), a range's
/// upper end, and whether a list has nothing ticked, which keeps no rows.
/// </summary>
internal sealed record ColumnFilterValue(FilterOperator Op, IReadOnlyList<string?> Values, string? To = null, bool None = false)
{
    /// <summary>The first value, or "" for none.</summary>
    public string Value => Values.Count > 0 ? Values[0] ?? "" : "";

    /// <summary>Whether it's a condition at all: an empty field isn't, nor an operator with no value, which only draws the panel that way.</summary>
    public bool IsFilter => None || ColumnFilter.TakesNoValue(Op) || Values.Count > 0 || (Op == FilterOperator.Between && !string.IsNullOrWhiteSpace(To));

    /// <summary>What it is, for telling whether it changed.</summary>
    public string Signature => $"{Op}\u001f{string.Join('\u001f', Values.Select(v => v ?? "\u0000"))}\u001f{To}\u001f{None}";
}

/// <summary>
/// A Table column's filter as people type it (<see cref="ColumnFilterValue"/>, in its panel and in
/// the address) and as the condition it stands for: contains for text, between for numbers, equals
/// for dates, in for choices. The conditions themselves are built by
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

    /// <summary>An operator as a filter row shows it, on its button and in its menu, the same in any language.</summary>
    public static IconRole IconOf(FilterOperator op) => op switch
    {
        FilterOperator.Contains => IconRole.FilterContains,
        FilterOperator.DoesNotContain => IconRole.FilterDoesNotContain,
        FilterOperator.NotEquals => IconRole.FilterNotEquals,
        FilterOperator.StartsWith => IconRole.FilterStartsWith,
        FilterOperator.EndsWith => IconRole.FilterEndsWith,
        FilterOperator.GreaterThan => IconRole.FilterGreaterThan,
        FilterOperator.GreaterThanOrEqual => IconRole.FilterGreaterOrEqual,
        FilterOperator.LessThan => IconRole.FilterLessThan,
        FilterOperator.LessThanOrEqual => IconRole.FilterLessOrEqual,
        FilterOperator.Between => IconRole.FilterBetween,
        FilterOperator.IsEmpty => IconRole.FilterIsEmpty,
        FilterOperator.IsNotEmpty => IconRole.FilterIsNotEmpty,
        _ => IconRole.FilterEquals,
    };

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

    /// <summary>A list filter's rows: its choices, then a blank row where the column can be null.</summary>
    public static IReadOnlyList<(string? Value, string Text)> ListOf<TItem>(ColumnBase<TItem> column, IzzyText text)
    {
        var rows = column.FilterChoices(text).Select(c => ((string?)c.Value, c.Text)).ToList();
        if (column.ValueType is { } type && FilterConvert.CanBeNull(type))
        {
            rows.Add((null, text.FilterBlank));
        }

        return rows;
    }

    /// <summary>Whether a list filter's row is ticked: all of them are, with no filter.</summary>
    public static bool IsTicked(ColumnFilterValue current, string? value) =>
        !current.IsFilter || current.Values.Contains(value, StringComparer.OrdinalIgnoreCase);

    /// <summary>A list filter with one row ticked or unticked: all ticked is no filter, none ticked keeps no rows.</summary>
    public static ColumnFilterValue Tick<TItem>(ColumnBase<TItem> column, ColumnFilterValue current, string? value, bool on)
    {
        var rows = ListOf(column, Words).Select(r => r.Value).ToList();
        var ticked = rows.Where(r => string.Equals(r, value, StringComparison.OrdinalIgnoreCase) ? on : IsTicked(current, r)).ToList();
        return ticked.Count == rows.Count ? Empty(column)
            : ticked.Count == 0 ? new(FilterOperator.In, [], None: true)
            : new(FilterOperator.In, ticked);
    }

    // For a list's values alone, where the words don't matter.
    private static readonly IzzyText Words = new();

    /// <summary>How many values a list of looked-up values can hold ticked or unticked, as an address has a limit.</summary>
    public const int MaxListValues = 100;

    /// <summary>Whether a filter is one a list of looked-up values can show.</summary>
    public static bool IsListState(ColumnFilterValue value) => value.None || value.Op is FilterOperator.In or FilterOperator.NotIn;

    /// <summary>Whether a row of a list of looked-up values is ticked: all are, unless the filter says otherwise.</summary>
    public static bool IsValueTicked(ColumnFilterValue current, string? value, StringComparer same) => current switch
    {
        { None: true } => false,
        { Op: FilterOperator.In, IsFilter: true } => current.Values.Contains(value, same),
        { Op: FilterOperator.NotIn } => !current.Values.Contains(value, same),
        _ => true,
    };

    /// <summary>
    /// A list of looked-up values with one row ticked or unticked. Its whole length may not be known,
    /// so it's written as what's ticked once Select all has been cleared, and as what's unticked
    /// starting from all ticked. Past <see cref="MaxListValues"/>, nothing is added.
    /// </summary>
    public static ColumnFilterValue TickValue<TItem>(ColumnBase<TItem> column, ColumnFilterValue current, string? value, bool on, StringComparer same)
    {
        if (IsValueTicked(current, value, same) == on)
        {
            return current;
        }

        var inList = current is { None: false, Op: FilterOperator.In, IsFilter: true };
        var names = inList || current is { None: false, Op: FilterOperator.NotIn } ? current.Values : [];
        var op = inList || current.None ? FilterOperator.In : FilterOperator.NotIn;

        // In the list means ticked, or not in it does.
        var adding = on == (op == FilterOperator.In);
        if (adding && names.Count >= MaxListValues)
        {
            return current;
        }

        IReadOnlyList<string?> next = adding ? [.. names, value] : names.Where(v => !same.Equals(v, value)).ToList();
        return next.Count > 0 ? new(op, next)
            : op == FilterOperator.In ? new(FilterOperator.In, [], None: true)
            : Empty(column);
    }

    /// <summary>Why a column can't list values this way, or null when it can.</summary>
    public static string? ValueListProblem<TItem>(ColumnBase<TItem> column)
    {
        if (!column.HasValueList)
        {
            return null;
        }

        var name = column.FilterValues is not null ? nameof(column.FilterValues) : nameof(column.FilterList);
        return column.FilterKind == ColumnFilterKind.Choice ? $"The \"{column.HeaderText}\" column has {name}, but its type has a list of its own."
            : column.FilterOperators is not null ? $"The \"{column.HeaderText}\" column has both {name} and {nameof(column.FilterOperators)}: a list has no operator to choose."
            : column.FilterValues is null && column.FilterKind != ColumnFilterKind.Text ? $"The \"{column.HeaderText}\" column has {nameof(column.FilterList)}, which reads text. Use {nameof(column.FilterValues)} to list other values."
            : column.FilterValues is null && column.Table is { ItemsProvider: not null } ? $"The \"{column.HeaderText}\" column has {nameof(column.FilterList)}, but with an ItemsProvider there are no Items to read. Use {nameof(column.FilterValues)}."
            : null;
    }

    /// <summary>The operators with a word in the address, the same as in the filter's text form.</summary>
    private static readonly (FilterOperator Op, string Word)[] OperatorWords =
    [
        (FilterOperator.Equals, "eq"), (FilterOperator.NotEquals, "ne"), (FilterOperator.GreaterThan, "gt"), (FilterOperator.GreaterThanOrEqual, "ge"),
        (FilterOperator.LessThan, "lt"), (FilterOperator.LessThanOrEqual, "le"), (FilterOperator.Contains, "contains"), (FilterOperator.DoesNotContain, "notcontains"),
        (FilterOperator.StartsWith, "startswith"), (FilterOperator.EndsWith, "endswith"), (FilterOperator.Between, "between"), (FilterOperator.In, "in"),
        (FilterOperator.NotIn, "notin"), (FilterOperator.IsEmpty, "isempty"), (FilterOperator.IsNotEmpty, "isnotempty"),
    ];

    /// <summary>An operator's word in the address.</summary>
    public static string WordOf(FilterOperator op) => OperatorWords.First(w => w.Op == op).Word;

    /// <summary>The operator a word in the address names, or null for an unknown word.</summary>
    public static FilterOperator? OperatorOf(string? word)
    {
        foreach (var (op, known) in OperatorWords)
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
            return [FilterOperator.In];
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
    public static IEnumerable<string> KeysOf<TItem>(ColumnBase<TItem> column, string key) =>
        column.FilterKind == ColumnFilterKind.Choice ? [key, $"{key}-blank", $"{key}-none"]
        : column.HasValueList ? [key, $"{key}-op", $"{key}-to", $"{key}-blank", $"{key}-none"]
        : [key, $"{key}-op", $"{key}-to"];

    /// <summary>
    /// A column's filter from the address, or null when it has none. A word the column doesn't offer
    /// reads as its default. A repeated key is a list of values to text, the first one to anything
    /// that takes one value. Each field posts whether it's used or not, so a value the operator
    /// doesn't take is dropped, and an operator with no value is only what the panel draws.
    /// </summary>
    public static ColumnFilterValue? Read<TItem>(ColumnBase<TItem> column, string key, IReadOnlyDictionary<string, StringValues> query)
    {
        if (column.FilterKind == ColumnFilterKind.Choice)
        {
            return ReadList(column, key, query);
        }

        if (column.HasValueList && ReadValueList(key, query) is { } list)
        {
            return list.IsFilter ? list : null;
        }

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

    // A list's ticks, each as its row: a value with no row ticks nothing, so the boxes always say what
    // the rows are. Every row ticked is no filter, which is what a form with no script posts when it's
    // left alone. A tick outweighs the none marker, as a form posting both has been ticked since.
    private static ColumnFilterValue? ReadList<TItem>(ColumnBase<TItem> column, string key, IReadOnlyDictionary<string, StringValues> query)
    {
        static bool On(StringValues value) => value.Any(v => !string.IsNullOrWhiteSpace(v));

        var rows = ListOf(column, Words).Select(r => r.Value).ToList();
        var written = query.GetValueOrDefault(key).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        var values = rows.Where(r => r is not null && written.Any(v => Names(column, v!, r))).ToList();
        if (rows.Contains(null) && On(query.GetValueOrDefault($"{key}-blank")))
        {
            values.Add(null);
        }

        if (values.Count == 0)
        {
            return written.Count > 0 || On(query.GetValueOrDefault($"{key}-none")) ? new(FilterOperator.In, [], None: true) : null;
        }

        return values.Count == rows.Count ? null : new(FilterOperator.In, values);
    }

    // A list of looked-up values, or null when the address holds what the column's usual filter wrote.
    // It always names its operator, as f-x=ada on its own is that filter's, and means the same with or
    // without the list. The list's rows aren't known here, so its values are taken as they come.
    private static ColumnFilterValue? ReadValueList(string key, IReadOnlyDictionary<string, StringValues> query)
    {
        var op = OperatorOf(query.GetValueOrDefault($"{key}-op").FirstOrDefault());
        var none = query.GetValueOrDefault($"{key}-none").Any(v => !string.IsNullOrWhiteSpace(v));
        if (op is not (FilterOperator.In or FilterOperator.NotIn) && !none)
        {
            return null;
        }

        // As they came, as a value can have spaces at its ends, or be nothing but.
        List<string?> values = [.. query.GetValueOrDefault(key).Where(v => !string.IsNullOrEmpty(v))];
        if (query.GetValueOrDefault($"{key}-blank").Any(v => !string.IsNullOrWhiteSpace(v)))
        {
            values.Add(null);
        }

        // As a tick outweighs the none marker in a list of choices.
        return values.Count == 0 && op != FilterOperator.NotIn && none ? new(FilterOperator.In, [], None: true)
            : new(op == FilterOperator.NotIn ? FilterOperator.NotIn : FilterOperator.In, values);
    }

    // Whether text in the address means this row: its name in any case, or anything the column's type reads as it.
    private static bool Names<TItem>(ColumnBase<TItem> column, string text, string? row)
    {
        text = text.Trim();
        return string.Equals(text, row, StringComparison.OrdinalIgnoreCase)
            || (column.ValueType is { } type && FilterConvert.Unwrap(FilterConvert.Read(type, text)) is { } typed && typed is not string && string.Equals(FilterConvert.Format(typed), row, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// What a column's filter makes of the address: its value for each of the column's keys, null
    /// for one it doesn't use. Null clears them all. <see cref="Read"/> reads it back. The default
    /// operator isn't written, nor one with no value to go with it.
    /// </summary>
    public static Dictionary<string, object?> Write<TItem>(ColumnBase<TItem> column, string key, ColumnFilterValue? value)
    {
        var keys = KeysOf(column, key).ToDictionary(k => k, _ => (object?)null);
        if (value is { IsFilter: true } && column.FilterKind == ColumnFilterKind.Choice)
        {
            keys[key] = value.Values.OfType<string>().ToArray();
            keys[$"{key}-blank"] = value.Values.Contains(null) ? "1" : null;
            keys[$"{key}-none"] = value.None ? "1" : null;
        }
        else if (value is { IsFilter: true } && column.HasValueList && IsListState(value))
        {
            keys[key] = value.Values.OfType<string>().ToArray();
            keys[$"{key}-blank"] = value.Values.Contains(null) ? "1" : null;
            keys[$"{key}-none"] = value.None ? "1" : null;
            keys[$"{key}-op"] = value.None ? null : WordOf(value.Op);
        }
        else if (value is { IsFilter: true })
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
            // A looked-up value is exactly what was listed: text as it is, and others in the invariant form.
            FilterOperator.In or FilterOperator.NotIn when column.HasValueList => new FieldFilter(key, value.Op, value.Values.Select(v => text ? v : FilterConvert.Invariantly(() => Typed(v))).ToList()),
            FilterOperator.In or FilterOperator.NotIn => new FieldFilter(key, value.Op, value.Values.Select(Typed).ToList()),
            _ => new FieldFilter(key, value.Op, Typed(value.Value)),
        };
    }

    /// <summary>
    /// What a column's panel shows for a condition on it, or null when the panel can't show it
    /// (an operator it doesn't offer, matching case, a date and time where it takes days): a bound
    /// filter's other conditions still apply, and show as tags of their own.
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
                FilterOperator.Equals when value is not null => ListState(column, [FilterConvert.Format(value)]),
                FilterOperator.IsNull => ListState(column, [null]),
                FilterOperator.In => ListState(column, filter.Values.Select(v => FilterConvert.Unwrap(v) is null ? null : FilterConvert.Format(v)).ToList()),
                _ => null,
            };
        }

        if (column.HasValueList && filter.Operator is FilterOperator.In or FilterOperator.NotIn)
        {
            List<string?> listed = [.. filter.Values.Select(v => FilterConvert.Unwrap(v) is null ? null : FilterConvert.Invariantly(() => FilterConvert.Format(v)))];
            return listed.Count == 0 && filter.Operator == FilterOperator.In ? new(FilterOperator.In, [], None: true) : new(filter.Operator, listed);
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

    // A list ticked this way, by the rules a person ticking it would get: all of them is no filter, and
    // a blank on a column that can't be null ticks nothing.
    private static ColumnFilterValue ListState<TItem>(ColumnBase<TItem> column, IReadOnlyList<string?> values)
    {
        var canBeNull = column.ValueType is { } type && FilterConvert.CanBeNull(type);
        var ticked = values.Where(v => v is not null || canBeNull).ToList();
        return ticked.Count == 0 ? new(FilterOperator.In, [], None: true)
            : ListOf(column, Words).All(r => ticked.Contains(r.Value, StringComparer.OrdinalIgnoreCase)) ? Empty(column)
            : new(FilterOperator.In, ticked);
    }

    internal static bool TryParseDay(string text, out DateOnly day) => FilterConvert.TryParseDay(text, out day);
}
