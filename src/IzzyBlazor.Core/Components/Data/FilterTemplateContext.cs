using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// What a column's <see cref="ColumnBase{TItem}.FilterTemplate"/> gets: the filter as it stands, and
/// what its controls need to change it. Spread <see cref="Attributes"/> on a control and it works on
/// every kind of page; its value still needs setting.
/// </summary>
public sealed class FilterTemplateContext
{
    private readonly Func<IReadOnlyList<string?>, Task> _set;
    private readonly IReadOnlyDictionary<string, object> _shared;
    private readonly Func<string, EventCallback<ChangeEventArgs>>? _tick;

    internal FilterTemplateContext(
        ColumnFilterValue current,
        string name,
        IReadOnlyDictionary<string, object> attributes,
        IReadOnlyDictionary<string, object> toAttributes,
        IReadOnlyDictionary<string, object> shared,
        Func<IReadOnlyList<string?>, Task> set,
        Func<string, EventCallback<ChangeEventArgs>>? tick)
    {
        Name = name;
        Operator = current.Op;
        Values = current.Values;
        To = current.To ?? "";
        IsFiltered = current.IsFilter;
        Attributes = attributes;
        ToAttributes = toAttributes;
        _shared = shared;
        _set = set;
        _tick = tick;
    }

    /// <summary>The column's key in the address, such as <c>f-total</c>, which its value posts under.</summary>
    public string Name { get; }

    /// <summary>The operator its value applies with: the column's first in <see cref="ColumnBase{TItem}.FilterOperators"/>, or its usual one.</summary>
    public FilterOperator Operator { get; }

    /// <summary>The values it's set to, none when it isn't. More than one makes an <see cref="FilterOperator.In"/>.</summary>
    public IReadOnlyList<string?> Values { get; }

    /// <summary>The first of <see cref="Values"/>, or "".</summary>
    public string Value => Values.Count > 0 ? Values[0] ?? "" : "";

    /// <summary>The upper end, for a column whose operator is <see cref="FilterOperator.Between"/>, or "".</summary>
    public string To { get; }

    /// <summary>Whether the column is filtered.</summary>
    public bool IsFiltered { get; }

    /// <summary>
    /// For a control that holds the one value, such as an <c>input</c>, a <c>select</c> or a library
    /// field: its name and form on a page that keeps filters in the address, its change event on a
    /// page that binds them. A library field also needs <see cref="SetAsync"/> as its <c>ValueChanged</c>.
    /// </summary>
    public IReadOnlyDictionary<string, object> Attributes { get; }

    /// <summary>As <see cref="Attributes"/>, for a control that holds the upper end of a <see cref="FilterOperator.Between"/>.</summary>
    public IReadOnlyDictionary<string, object> ToAttributes { get; }

    /// <summary>Whether this value is among <see cref="Values"/>.</summary>
    public bool Has(string value) => Values.Contains(value, StringComparer.OrdinalIgnoreCase);

    /// <summary>For a native checkbox that adds this value: as <see cref="Attributes"/>, with its type, value and whether it's ticked.</summary>
    public IReadOnlyDictionary<string, object> CheckboxAttributes(string value)
    {
        Dictionary<string, object> attributes = new(_shared)
        {
            ["type"] = "checkbox",
            ["value"] = value,
            ["checked"] = Has(value),
        };
        if (_tick is not null)
        {
            attributes["onchange"] = _tick(value);
        }

        return attributes;
    }

    /// <summary>Filters by this value on an interactive page, or removes the filter with null or "".</summary>
    public Task SetAsync(string? value) => _set(string.IsNullOrEmpty(value) ? [] : [value]);

    /// <summary>Filters by these values on an interactive page, or removes the filter with none.</summary>
    public Task SetValuesAsync(IEnumerable<string?> values) => _set(values.ToList());
}
