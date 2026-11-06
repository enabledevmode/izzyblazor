using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// A header over the columns inside it, in a <see cref="Table{TItem}"/> or <c>DataGrid</c>: quarters
/// over their revenue and margin, a week over its days. Bands can hold bands. A column outside any
/// band has an empty cell above its header. Sorting, filtering and widths stay on the columns; in a
/// DataGrid a column moves only among its band's columns, and a band moves as a whole.
/// </summary>
/// <example><code>
/// &lt;ColumnBand Title="Q1"&gt;
///     &lt;PropertyColumn Property="r =&gt; r.Q1Revenue" Title="Revenue" /&gt;
///     &lt;PropertyColumn Property="r =&gt; r.Q1Margin" Title="Margin" /&gt;
/// &lt;/ColumnBand&gt;
/// </code></example>
public sealed class ColumnBand : ComponentBase, IDisposable
{
    /// <summary>The band's header text, which screen readers also say before each of its columns' names.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>The band's header content, in place of <see cref="Title"/>.</summary>
    [Parameter] public RenderFragment? HeaderTemplate { get; set; }

    /// <summary>Where the header sits across the band. Centered by default.</summary>
    [Parameter] public CellAlign Align { get; set; } = CellAlign.Center;

    /// <summary>A class for the band's header cell.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>The columns, and bands, in the band.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    [CascadingParameter] internal IColumnBandHost? Host { get; set; }

    [CascadingParameter] internal ColumnBand? Parent { get; set; }

    /// <summary>Its columns and bands, in page order.</summary>
    internal List<object> Nodes { get; } = [];

    /// <summary>Its bands from the outermost down to itself.</summary>
    internal IReadOnlyList<ColumnBand> Chain => Parent is null ? [this] : [.. Parent.Chain, this];

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Host is null)
        {
            throw new InvalidOperationException("A ColumnBand must be inside a Table or a DataGrid.");
        }

        Host.AddBand(this);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Title is null && HeaderTemplate is null)
        {
            throw new InvalidOperationException("A ColumnBand needs a Title or a HeaderTemplate.");
        }

        // Only what the header shows: a template is a new one on each render.
        var shown = (Title, HeaderTemplate is not null, Align, Class);
        if (_shown is { } before && !before.Equals(shown))
        {
            Host!.BandChanged();
        }

        _shown = shown;
    }

    private (string?, bool, CellAlign, string?)? _shown;

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<ColumnBand>>(0);
        builder.AddComponentParameter(1, "Value", this);
        builder.AddComponentParameter(2, "IsFixed", true);
        builder.AddComponentParameter(3, "ChildContent", ChildContent);
        builder.CloseComponent();
        // After its columns: they've joined the table by the time this is made.
        builder.OpenComponent<ColumnBandReady>(4);
        builder.AddComponentParameter(5, nameof(ColumnBandReady.Band), this);
        builder.CloseComponent();
    }

    /// <inheritdoc />
    public void Dispose() => Host?.RemoveBand(this);
}

/// <summary>A table that takes bands: they join it, and it waits for their columns before it loads.</summary>
internal interface IColumnBandHost
{
    void AddBand(ColumnBand band);

    void RemoveBand(ColumnBand band);

    void BandChanged();

    /// <summary>A band's columns have joined the table.</summary>
    void BandReady(ColumnBand band);
}

/// <summary>Made after a band's columns, so it knows they've joined the table.</summary>
internal sealed class ColumnBandReady : ComponentBase
{
    [Parameter] public ColumnBand? Band { get; set; }

    protected override void OnInitialized() => Band?.Host?.BandReady(Band);

    protected override bool ShouldRender() => false;
}
