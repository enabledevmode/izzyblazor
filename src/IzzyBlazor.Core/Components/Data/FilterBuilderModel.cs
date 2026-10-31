using System.Text;
using System.Text.Json;

namespace IzzyBlazor;

/// <summary>
/// What FilterBuilder needs from a filter and its fields: which operators a field offers, and a filter
/// as the JSON its script draws from. What it can't draw as a rule, such as a not around a group, stays
/// as its text, so nothing in a filter is lost by opening it in the builder.
/// </summary>
internal static class FilterBuilderModel
{
    /// <summary>A field's kind, as the script knows it; null for a type the builder doesn't offer.</summary>
    public static string? KindWord(Type type) => ColumnFilter.KindOf(type) switch
    {
        ColumnFilterKind.Text => "text",
        ColumnFilterKind.Number => "number",
        ColumnFilterKind.Date => "date",
        ColumnFilterKind.Choice => "choice",
        _ => null,
    };

    /// <summary>The operators a field offers, with the words the script writes them by, its default first.</summary>
    public static IEnumerable<(string Word, FilterOperator Op)> OperatorsOf<TItem>(DataField<TItem> field)
    {
        var allowed = field.EffectiveOperators;
        var kind = KindWord(field.Type);
        (string, FilterOperator)[] all = kind switch
        {
            "text" =>
            [
                ("contains", FilterOperator.Contains), ("notcontains", FilterOperator.DoesNotContain), ("eq", FilterOperator.Equals), ("ne", FilterOperator.NotEquals),
                ("startswith", FilterOperator.StartsWith), ("endswith", FilterOperator.EndsWith), ("isempty", FilterOperator.IsEmpty), ("isnotempty", FilterOperator.IsNotEmpty),
            ],
            "choice" => [("in", FilterOperator.In), ("notin", FilterOperator.NotIn), ("isempty", FilterOperator.IsNull), ("isnotempty", FilterOperator.IsNotNull)],
            _ =>
            [
                ("eq", FilterOperator.Equals), ("ne", FilterOperator.NotEquals), ("gt", FilterOperator.GreaterThan), ("ge", FilterOperator.GreaterThanOrEqual),
                ("lt", FilterOperator.LessThan), ("le", FilterOperator.LessThanOrEqual), ("between", FilterOperator.Between), ("isempty", FilterOperator.IsNull), ("isnotempty", FilterOperator.IsNotNull),
            ],
        };
        return all.Where(o => allowed.Contains(o.Item2));
    }

    /// <summary>An operator's words in the builder.</summary>
    public static string TextOf(Type type, FilterOperator op, IzzyText text) => op switch
    {
        FilterOperator.In => text.FilterIsAnyOf,
        FilterOperator.NotIn => text.FilterIsNoneOf,
        FilterOperator.IsNull => text.FilterIsEmpty,
        FilterOperator.IsNotNull => text.FilterIsNotEmpty,
        _ => ColumnFilter.TextOf(ColumnFilter.KindOf(type), op, text),
    };

    /// <summary>A filter as the script's tree: <c>{"logic":"all","items":[...]}</c>, a rule being <c>{"field","op","values","to"}</c> and what can't be one <c>{"raw"}</c>.</summary>
    public static string Write<TItem>(Filter? filter, IEnumerable<DataField<TItem>> fields)
    {
        var offered = fields.ToList();
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream))
        {
            // The top is always a group, so it has a logic to choose.
            switch (filter)
            {
                case null:
                    WriteGroup(json, "all", [], offered);
                    break;
                case FieldFilter or NotFilter when RuleOf(filter, offered) is not null:
                    WriteGroup(json, "all", [filter], offered);
                    break;
                case FilterGroup { Logic: var logic } g:
                    WriteGroup(json, logic == FilterLogic.And ? "all" : "any", g.Filters, offered);
                    break;
                default:
                    WriteGroup(json, "all", [filter], offered);
                    break;
            }
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteGroup<TItem>(Utf8JsonWriter json, string logic, IReadOnlyList<Filter> items, List<DataField<TItem>> fields)
    {
        json.WriteStartObject();
        json.WriteString("logic", logic);
        json.WriteStartArray("items");
        foreach (var item in items)
        {
            WriteItem(json, item, fields);
        }

        json.WriteEndArray();
        json.WriteEndObject();
    }

    private static void WriteItem<TItem>(Utf8JsonWriter json, Filter item, List<DataField<TItem>> fields)
    {
        if (RuleOf(item, fields) is { } rule)
        {
            json.WriteStartObject();
            json.WriteString("field", rule.Field);
            json.WriteString("op", rule.Op);
            json.WriteStartArray("values");
            foreach (var value in rule.Values)
            {
                json.WriteStringValue(value);
            }

            json.WriteEndArray();
            json.WriteString("to", rule.To);
            json.WriteEndObject();
            return;
        }

        switch (item)
        {
            case FilterGroup { Logic: var logic } g:
                WriteGroup(json, logic == FilterLogic.And ? "all" : "any", g.Filters, fields);
                break;
            default:
                json.WriteStartObject();
                json.WriteString("raw", item.ToString());
                json.WriteEndObject();
                break;
        }
    }

    private sealed record Rule(string Field, string Op, IReadOnlyList<string> Values, string? To);

    // A condition as a rule the field's controls can show: an operator it offers, and values that fit.
    private static Rule? RuleOf<TItem>(Filter item, List<DataField<TItem>> fields)
    {
        // How the parser reads "not (x in (...))".
        if (item is NotFilter { Filter: FieldFilter { Operator: FilterOperator.In } list })
        {
            item = list with { Operator = FilterOperator.NotIn };
        }

        if (item is not FieldFilter { MatchCase: false } f || fields.FirstOrDefault(d => string.Equals(d.Key, f.Field, StringComparison.OrdinalIgnoreCase)) is not { } field)
        {
            return null;
        }

        var op = f.Operator switch
        {
            FilterOperator.OnDay => FilterOperator.Equals,
            FilterOperator.IsEmpty when KindWord(field.Type) != "text" => FilterOperator.IsNull,
            FilterOperator.IsNotEmpty when KindWord(field.Type) != "text" => FilterOperator.IsNotNull,
            FilterOperator.IsNull when KindWord(field.Type) == "text" => FilterOperator.IsEmpty,
            FilterOperator.IsNotNull when KindWord(field.Type) == "text" => FilterOperator.IsNotEmpty,
            FilterOperator.Equals when KindWord(field.Type) == "choice" => FilterOperator.In,
            FilterOperator.NotEquals when KindWord(field.Type) == "choice" => FilterOperator.NotIn,
            var same => same,
        };
        var word = OperatorsOf(field).Where(o => o.Op == op).Select(o => o.Word).FirstOrDefault();
        if (word is null)
        {
            return null;
        }

        var date = KindWord(field.Type) == "date";
        var kind = KindWord(field.Type);
        var choices = kind == "choice" ? (field.Choices is { Count: > 0 } listed ? listed : ColumnFilter.Choices(field.Type, new IzzyText())) : [];

        // A value as its control holds it, or null for one it can't, which then stays as text and isn't
        // quietly dropped: a box would show nothing, and Apply would leave the rule out.
        string? Text(object? value)
        {
            if (FilterConvert.Unwrap(value) is null)
            {
                return null;
            }

            if (date)
            {
                return FilterConvert.IsWholeDay(value, out var day) || FilterConvert.TryParseDay(value!.ToString() ?? "", out day) ? FilterConvert.Format(day) : null;
            }

            var text = FilterConvert.Invariantly(() => FilterConvert.Format(value));
            return kind switch
            {
                "choice" => choices.Where(c => string.Equals(c.Value, text, StringComparison.OrdinalIgnoreCase)).Select(c => c.Value).FirstOrDefault(),
                "number" => double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _) ? text : null,
                _ => text.Length > 0 ? text : null,
            };
        }

        switch (op)
        {
            case FilterOperator.IsEmpty or FilterOperator.IsNotEmpty or FilterOperator.IsNull or FilterOperator.IsNotNull:
                return new(field.Key, word, [], null);
            case FilterOperator.Between:
            {
                var (low, high) = (Text(f.Value), Text(f.Value2));
                return (FilterConvert.Unwrap(f.Value) is not null && low is null) || (FilterConvert.Unwrap(f.Value2) is not null && high is null)
                    ? null
                    : new(field.Key, word, low is null ? [] : [low], high);
            }

            case FilterOperator.In or FilterOperator.NotIn:
            {
                var values = f.Values.Select(Text).ToList();
                return values.Contains(null) ? null : new(field.Key, word, values!, null);
            }

            default:
                return Text(f.Value) is { } text ? new(field.Key, word, [text], null) : null;
        }
    }
}
