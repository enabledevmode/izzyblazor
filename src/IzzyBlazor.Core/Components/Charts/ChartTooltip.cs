using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// A chart's tooltip content, in place of the item's argument and each series' value:
/// <c>&lt;ChartTooltip Context="t"&gt;@t.Argument: @t.Points[0].Text&lt;/ChartTooltip&gt;</c>.
/// It's drawn on the server for each item, so keep it to text and inline content (no buttons or
/// links). The chart's <c>Tooltip</c> turns tooltips on or off.
/// </summary>
/// <typeparam name="TItem">The chart's item type.</typeparam>
public sealed class ChartTooltip<TItem> : ComponentBase, IDisposable
{
    [CascadingParameter] private ChartContext<TItem>? Context { get; set; }

    /// <summary>The tooltip's content for one item.</summary>
    [Parameter, EditorRequired] public RenderFragment<ChartTooltipContext<TItem>>? ChildContent { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Context is null)
        {
            throw new InvalidOperationException("ChartTooltip must be inside a Chart.");
        }

        if (!ReferenceEquals(Context.TooltipTemplate, ChildContent))
        {
            Context.TooltipTemplate = ChildContent;
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
        // Taken out of the chart: its tooltips go back to the default rows.
        if (Context is not null && ReferenceEquals(Context.TooltipTemplate, ChildContent))
        {
            Context.TooltipTemplate = null;
            Context.Plot?.Refresh();
        }
    }
}

/// <summary>
/// What a <see cref="ChartTooltip{TItem}"/> draws from: the item under the pointer (or keyboard
/// focus) and each series' value for it.
/// </summary>
/// <typeparam name="TItem">The chart's item type.</typeparam>
public sealed class ChartTooltipContext<TItem>
{
    internal ChartTooltipContext(TItem item, int index, string argument, IReadOnlyList<ChartTooltipPoint> points)
    {
        Item = item;
        Index = index;
        Argument = argument;
        Points = points;
    }

    /// <summary>The item.</summary>
    public TItem Item { get; }

    /// <summary>The item's place in the chart's <c>Items</c>, from 0.</summary>
    public int Index { get; }

    /// <summary>The item's argument as the chart shows it, formatted as on the argument axis.</summary>
    public string Argument { get; }

    /// <summary>Each series' value for the item, in the chart's order.</summary>
    public IReadOnlyList<ChartTooltipPoint> Points { get; }
}

/// <summary>One series' value in a <see cref="ChartTooltipContext{TItem}"/>.</summary>
/// <param name="Series">The series' name.</param>
/// <param name="Value">The value, or null where the item has none.</param>
/// <param name="Text">The value formatted as on its value axis, or null.</param>
/// <param name="Color">The series' color, as a CSS color, such as <c>var(--ib-chart-1)</c>.</param>
public sealed record ChartTooltipPoint(string Series, double? Value, string? Text, string Color)
{
    /// <summary>A bubble's size (null for other series).</summary>
    public double? Size { get; init; }

    /// <summary>What a bubble's size measures, such as "Customers": its Size property's name.</summary>
    public string? SizeName { get; init; }

    /// <summary>A bubble's size as text.</summary>
    public string? SizeText { get; init; }
}
