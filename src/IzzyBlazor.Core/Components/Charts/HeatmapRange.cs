using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// A class of values on a <see cref="HeatmapChart{TItem}"/>'s or <see cref="CalendarHeatmap{TItem}"/>'s scale, with its own color and name,
/// in place of the chart's even steps:
/// <c>&lt;HeatmapRange From="0" To="10" Color="Color.Of(&quot;#dbeafe&quot;)" Label="Quiet" /&gt;</c>.
/// A value is in the first range whose <see cref="From"/> it reaches and whose <see cref="To"/>
/// it's under (the last range keeps its To). A value in no range is drawn as no data, with its
/// value still in the cell, the tooltip and the table. Up to nine.
/// </summary>
public sealed class HeatmapRange : ComponentBase, IDisposable
{
    [CascadingParameter] private HeatmapContext? Context { get; set; }

    /// <summary>Where the range starts, on the chart's values.</summary>
    [Parameter, EditorRequired] public double From { get; set; }

    /// <summary>Where it ends: values under it are in the range.</summary>
    [Parameter, EditorRequired] public double To { get; set; }

    /// <summary>
    /// The cells' color, such as <c>Color.Of("#dbeafe")</c>. Order the ranges' colors light to
    /// dark (or dark to light in dark mode, with <c>light-dark()</c>). Values in cells take black
    /// or white by <c>contrast-color()</c>; in a browser without it, the stronger half of the
    /// ranges take white, so keep the light colors first.
    /// </summary>
    [Parameter, EditorRequired] public Color? Color { get; set; }

    /// <summary>What a value in the range means, such as "Quiet": the legend shows it with the range's bounds.</summary>
    [Parameter] public string? Label { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Context is null)
        {
            throw new InvalidOperationException("HeatmapRange must be inside a HeatmapChart or a CalendarHeatmap.");
        }

        Context.Ranges.Add(this);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Color is null)
        {
            throw new InvalidOperationException("HeatmapRange needs a Color, such as Color.Of(\"#dbeafe\").");
        }

        if (!double.IsFinite(From) || !double.IsFinite(To) || To <= From)
        {
            throw new ArgumentOutOfRangeException(nameof(To), To, "A HeatmapRange's To must be more than its From.");
        }

        Context!.Ranges.Seen(this);
        Context.Plot?.Refresh();
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Context?.Ranges.Remove(this) == true)
        {
            Context.Plot?.Refresh();
        }
    }
}

/// <summary>
/// A <see cref="HeatmapChart{TItem}"/>'s or <see cref="CalendarHeatmap{TItem}"/>'s tooltip content, in place of the cell's row, column and
/// value: <c>&lt;HeatmapTooltip Context="c"&gt;@c.Row, @c.Column: @c.Text orders&lt;/HeatmapTooltip&gt;</c>.
/// It's drawn on the server for each cell that has an item, so keep it to text and inline
/// content (no buttons or links), and on a large grid (thousands of cells) leave the default,
/// which the script writes and costs nothing on the page.
/// </summary>
/// <typeparam name="TItem">The chart's item type.</typeparam>
public sealed class HeatmapTooltip<TItem> : ComponentBase, IDisposable
{
    [CascadingParameter] private HeatmapContext? Context { get; set; }

    /// <summary>The tooltip's content for one cell.</summary>
    [Parameter, EditorRequired] public RenderFragment<HeatmapCellContext<TItem>>? ChildContent { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Context is null)
        {
            throw new InvalidOperationException("HeatmapTooltip must be inside a HeatmapChart or a CalendarHeatmap.");
        }

        if (!ReferenceEquals(Context.Tooltip, ChildContent))
        {
            Context.Tooltip = ChildContent;
            Context.Plot?.Refresh();
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Context is not null && ReferenceEquals(Context.Tooltip, ChildContent))
        {
            Context.Tooltip = null;
            Context.Plot?.Refresh();
        }
    }
}

/// <summary>What a <see cref="HeatmapTooltip{TItem}"/> draws from: one cell and its item.</summary>
/// <typeparam name="TItem">The chart's item type.</typeparam>
public sealed class HeatmapCellContext<TItem>
{
    internal HeatmapCellContext(TItem item, int index, string column, string row, double? value, string? text, DateOnly? date = null)
    {
        Date = date;
        Item = item;
        Index = index;
        Column = column;
        Row = row;
        Value = value;
        Text = text;
    }

    /// <summary>The item drawn in the cell.</summary>
    public TItem Item { get; }

    /// <summary>The item's place in the chart's <c>Items</c>, from 0.</summary>
    public int Index { get; }

    /// <summary>The cell's column name, as the chart shows it.</summary>
    public string Column { get; }

    /// <summary>The cell's row name.</summary>
    public string Row { get; }

    /// <summary>The value, or null for none.</summary>
    public double? Value { get; }

    /// <summary>On a <see cref="CalendarHeatmap{TItem}"/>, the cell's day; null on a HeatmapChart.</summary>
    public DateOnly? Date { get; }

    /// <summary>The value as text, formatted by the chart's <c>ValueFormat</c>, or null.</summary>
    public string? Text { get; }
}

/// <summary>What a heatmap shares with its ranges and tooltip.</summary>
internal sealed class HeatmapContext
{
    public ChildOrder<HeatmapRange> Ranges { get; } = new();

    /// <summary>A <see cref="HeatmapTooltip{TItem}"/>'s content, typed by the chart.</summary>
    public object? Tooltip { get; set; }

    public ChartPlot? Plot { get; set; }
}
