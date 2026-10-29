using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace IzzyBlazor;

/// <summary>
/// A filter on data: a condition on one field (<see cref="FieldFilter"/>), conditions joined by
/// and or or (<see cref="FilterGroup"/>), or the opposite of one (<see cref="NotFilter"/>). Fields
/// are named by their key (a column's <c>SortKey</c>, or a <see cref="DataField{TItem}"/>'s
/// <c>Key</c>). It becomes a LINQ expression on the fields' own properties, so an EF Core query
/// filters in the database; a value never becomes code.
/// </summary>
/// <example><code>Filter.All(Filter.Where("status", FilterOperator.Equals, "Paid"), Filter.Between("total", 100, 500))</code></example>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(FieldFilter), "field")]
[JsonDerivedType(typeof(FilterGroup), "group")]
[JsonDerivedType(typeof(NotFilter), "not")]
public abstract record Filter
{
    private protected Filter()
    {
    }

    /// <summary>A condition on one field, such as <c>Where("total", FilterOperator.GreaterThan, 100)</c>.</summary>
    public static FieldFilter Where(string field, FilterOperator op, object? value = null) => new(field, op, value);

    /// <summary>The values from <paramref name="min"/> to <paramref name="max"/>, both included; either may be null for no limit.</summary>
    public static FieldFilter Between(string field, object? min, object? max) => new(field, FilterOperator.Between, min, max);

    /// <summary>Any of these values.</summary>
    public static FieldFilter In(string field, IEnumerable<object?> values) => new(field, FilterOperator.In, values.ToList());

    /// <summary>Every one of these conditions (and). With none, everything matches.</summary>
    public static FilterGroup All(params Filter[] filters) => new(FilterLogic.And, filters);

    /// <summary>Any one of these conditions (or). With none, nothing matches.</summary>
    public static FilterGroup Any(params Filter[] filters) => new(FilterLogic.Or, filters);

    /// <summary>The items this filter doesn't keep.</summary>
    public static NotFilter Not(Filter filter) => new(filter);

    /// <summary>
    /// The condition on <paramref name="field"/> that every match must meet: this filter itself, or
    /// one inside its and groups (not inside an or group or a not). Null when there's none. For a
    /// provider that passes conditions on to an API: <c>request.Filter?.Find("who")?.Value</c>.
    /// </summary>
    public FieldFilter? Find(string field) => this switch
    {
        FieldFilter f when string.Equals(f.Field, field, StringComparison.OrdinalIgnoreCase) => f,
        FilterGroup { Logic: FilterLogic.And } g => g.Filters.Select(c => c.Find(field)).FirstOrDefault(c => c is not null),
        _ => null,
    };

    /// <summary>
    /// Reads a filter written in the subset of OData's <c>$filter</c> that <c>?filter=</c> takes:
    /// <c>total ge 100 and (status in ('Paid','Pending') or customer contains 'ada')</c>. Comparisons
    /// (<c>eq ne gt ge lt le</c>), <c>in</c>, <c>and or not</c>, parentheses, <c>contains</c>,
    /// <c>startswith</c> and <c>endswith</c> (as functions or between field and value),
    /// <c>date(field) eq 2026-01-31</c> for a day, and <c>null</c>. Text matches ignoring case, as a
    /// Table's filters do (OData's own <c>contains</c> matches case).
    /// </summary>
    /// <exception cref="FormatException">The text isn't in that form.</exception>
    public static Filter Parse(string text) => ODataFilter.Parse(text);

    /// <summary>Reads a filter as <see cref="Parse"/> does, or returns false when the text isn't in that form.</summary>
    public static bool TryParse(string? text, [NotNullWhen(true)] out Filter? filter)
    {
        filter = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            filter = ODataFilter.Parse(text);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>The filter in the form <see cref="Parse"/> reads.</summary>
    public sealed override string ToString() => ODataFilter.Write(this, forOData: false);
}

/// <summary>
/// A condition on one field: <c>total ge 100</c>. The values are parsed for the field's type when
/// the filter is applied (text as the person writes it in the current culture, or the invariant
/// form); a value that doesn't parse matches nothing.
/// </summary>
/// <param name="Field">The field's key.</param>
/// <param name="Operator">How the field's value compares.</param>
/// <param name="Value">The value to compare with. For <see cref="FilterOperator.In"/> and <see cref="FilterOperator.NotIn"/>, a list of values; for <see cref="FilterOperator.Between"/>, the lower end (null for none).</param>
/// <param name="Value2">The upper end for <see cref="FilterOperator.Between"/> (null for none).</param>
/// <param name="MatchCase">For text: match upper and lower case exactly. Text ignores case by default.</param>
public sealed record FieldFilter(
    string Field,
    FilterOperator Operator,
    [property: JsonConverter(typeof(FilterValueConverter))] object? Value = null,
    [property: JsonConverter(typeof(FilterValueConverter))] object? Value2 = null,
    bool MatchCase = false) : Filter
{
    /// <summary>The values of an <see cref="FilterOperator.In"/> or <see cref="FilterOperator.NotIn"/> condition (a single value is a list of one).</summary>
    [JsonIgnore]
    public IReadOnlyList<object?> Values => Value switch
    {
        null => [],
        string s => [s],
        System.Collections.IEnumerable list => list.Cast<object?>().ToList(),
        _ => [Value],
    };
}

/// <summary>Conditions joined by and (every one) or or (any one).</summary>
/// <param name="Logic">And or Or.</param>
/// <param name="Filters">The conditions. An empty and group keeps everything; an empty or group keeps nothing.</param>
public sealed record FilterGroup(FilterLogic Logic, IReadOnlyList<Filter> Filters) : Filter;

/// <summary>Keeps the items its <paramref name="Filter"/> doesn't.</summary>
/// <param name="Filter">The filter to turn around.</param>
public sealed record NotFilter(Filter Filter) : Filter;

/// <summary>How a <see cref="FilterGroup"/> joins its conditions.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FilterLogic>))]
public enum FilterLogic
{
    /// <summary>Every condition.</summary>
    And,

    /// <summary>Any condition.</summary>
    Or,
}

/// <summary>
/// How a <see cref="FieldFilter"/> compares. Which ones a field takes depends on its type: text
/// takes the text operators, equality, the lists and the empty checks; numbers and dates the
/// comparisons, <see cref="Between"/> and the lists, and dates <see cref="OnDay"/>; enums,
/// booleans and Guids equality and the lists. Types that can be null add the null checks. An
/// operator a field doesn't take matches nothing.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<FilterOperator>))]
public enum FilterOperator
{
    /// <summary>Equal to the value (text ignores case unless <see cref="FieldFilter.MatchCase"/>). With a null value, the same as <see cref="IsNull"/>.</summary>
    Equals,

    /// <summary>Not equal to the value.</summary>
    NotEquals,

    /// <summary>Less than the value.</summary>
    LessThan,

    /// <summary>Less than or equal to the value.</summary>
    LessThanOrEqual,

    /// <summary>Greater than the value.</summary>
    GreaterThan,

    /// <summary>Greater than or equal to the value.</summary>
    GreaterThanOrEqual,

    /// <summary>From <see cref="FieldFilter.Value"/> to <see cref="FieldFilter.Value2"/>, both included; a null end has no limit.</summary>
    Between,

    /// <summary>Text that contains the value.</summary>
    Contains,

    /// <summary>Text that doesn't contain the value (or is null).</summary>
    DoesNotContain,

    /// <summary>Text that starts with the value.</summary>
    StartsWith,

    /// <summary>Text that ends with the value.</summary>
    EndsWith,

    /// <summary>Any of the values in <see cref="FieldFilter.Value"/>, a list.</summary>
    In,

    /// <summary>None of the values in <see cref="FieldFilter.Value"/>, a list.</summary>
    NotIn,

    /// <summary>No value.</summary>
    IsNull,

    /// <summary>Any value.</summary>
    IsNotNull,

    /// <summary>Text that is empty or null.</summary>
    IsEmpty,

    /// <summary>Text that has at least one character.</summary>
    IsNotEmpty,

    /// <summary>
    /// A date on the day in the value. A <c>DateTime</c> keeps the day's range (an index can serve
    /// it); a <c>DateTimeOffset</c> compares the day on the clock where it was recorded.
    /// </summary>
    OnDay,
}
