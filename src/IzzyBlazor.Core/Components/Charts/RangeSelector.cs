using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// A strip under a chart's plot for choosing the part of its arguments in view: the whole range
/// drawn small, with a window over the part shown. Drag the window to pan, its two handles to
/// zoom, or click the strip to move there; each handle is a slider for the keyboard. Inside a
/// <see cref="Chart{TItem}"/> it draws the chart's series and zooms it, turning on
/// <see cref="ChartArgumentAxis.Zoom"/>: <c>&lt;RangeSelector /&gt;</c>. The chart's <c>View</c>
/// and <c>ViewChanged</c> say what's in view.
/// </summary>
public sealed class RangeSelector : ComponentBase, IDisposable
{
    [CascadingParameter] private ChartContextBase? Context { get; set; }

    /// <summary>
    /// Buttons for common spans back from the last date: 1 month, 3 and 6 months, the year to
    /// date, a year, and all. For a chart of dates.
    /// </summary>
    [Parameter] public bool Ranges { get; set; }

    /// <summary>Spans of your own to choose in a click, by their first and last days, beside or in place of <see cref="Ranges"/>.</summary>
    [Parameter] public IEnumerable<DateRangePreset>? Presets { get; set; }

    /// <summary>The strip's height. 4rem by default.</summary>
    [Parameter] public Length? Height { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Context is null)
        {
            throw new InvalidOperationException("RangeSelector goes inside a Chart, under its plot: <Chart ...><RangeSelector /></Chart>.");
        }

        Context.Selector = this;
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => Context?.Plot?.Refresh();

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Context?.Selector == this)
        {
            Context.Selector = null;
            Context.Plot?.Refresh();
        }
    }
}
