using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// What a chart's axes share: each one renders nothing itself and tells the chart how to draw
/// that axis. Leave an axis out and it takes its defaults.
/// </summary>
public abstract class ChartAxisBase : ComponentBase, IDisposable
{
    private protected ChartAxisBase()
    {
    }

    [CascadingParameter] private ChartContextBase? Context { get; set; }

    /// <summary>A title beside the axis, such as "Revenue (USD)" or "Month".</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>Leaves out the axis's labels, for a small chart or one whose values are labeled on the marks. The scale stays. Not on a second value axis.</summary>
    [Parameter] public bool HideLabels { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Context is null)
        {
            throw new InvalidOperationException($"{GetType().Name} must be inside a Chart.");
        }

        Context.Register(this);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        Check();
        Context!.Seen(this);
    }

    /// <summary>Throws for parameters that don't go together.</summary>
    private protected virtual void Check()
    {
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
    }

    /// <inheritdoc />
    public void Dispose() => Context?.Unregister(this);
}

/// <summary>
/// A chart's value axis: its range, ticks, format, title and side. A second
/// <c>ChartValueAxis</c> with a <see cref="Name"/> is drawn at the other side, for series in other
/// units that name it as their <c>ValueAxis</c>:
/// <c>&lt;ChartValueAxis Name="margin" Format="P0" Title="Margin" /&gt;</c>.
/// </summary>
public sealed class ChartValueAxis : ChartAxisBase
{
    /// <summary>
    /// The axis's name, which makes it the second value axis, at the side across from the first: series and
    /// reference marks with this as their <c>ValueAxis</c> are read off it. The first value axis
    /// has none. A second axis has a <see cref="Format"/>, <see cref="Min"/>, <see cref="Max"/> and
    /// <see cref="ChartAxisBase.Title"/>; its ticks fall on the first axis's grid lines.
    /// </summary>
    [Parameter] public string? Name { get; set; }

    /// <summary>
    /// A .NET format string for the values, such as <c>C0</c>, <c>N1</c> or <c>P0</c>: on the
    /// axis, in tooltips and in the data table.
    /// </summary>
    [Parameter] public string? Format { get; set; }

    /// <summary>
    /// Writes each value's text yourself, such as <c>v =&gt; $"${v / 1e6:0.#}M"</c>, in place of
    /// <see cref="Format"/>: on the axis, in tooltips and in the data table. (A percent stack's
    /// axis still reads in percent.) Hiding a series from the legend then keeps the axis as it
    /// is rather than rescaling it, since its text comes from the server. First axis only.
    /// </summary>
    [Parameter] public Func<double, string>? Formatter { get; set; }

    /// <summary>
    /// Where the axis starts. Defaults to a round number at or below the lowest value (0 with bars
    /// and areas); on a second axis, one its ticks line up with the first axis's grid at.
    /// </summary>
    [Parameter] public double? Min { get; set; }

    /// <summary>Where the axis ends. Defaults to a round number at or above the highest value.</summary>
    [Parameter] public double? Max { get; set; }

    /// <summary>
    /// The gap between ticks, such as <c>25_000</c>. The axis's ends round to it unless
    /// <see cref="Min"/> and <see cref="Max"/> are set. Defaults to a round step for the chart's
    /// size; ticks that would crowd still thin out. A step that would make more than 500 ticks is
    /// ignored. First axis only: a second axis's ticks fall on the first's grid lines.
    /// </summary>
    [Parameter] public double? Step { get; set; }

    /// <summary>
    /// Draws the axis on a log scale (base 10), for values that span several powers of ten, such
    /// as company sizes. Values at or under zero have no place on it and aren't drawn; bars and
    /// areas start at the axis's bottom. First axis only.
    /// </summary>
    [Parameter] public bool Logarithmic { get; set; }

    /// <summary>
    /// Draws a grid line across the plot at each tick. On by default for the first value axis;
    /// a second axis's ticks fall on the first's lines, so it has none of its own.
    /// </summary>
    [Parameter] public bool GridLines { get; set; } = true;

    /// <summary>
    /// The side the axis is drawn at: <see cref="ChartAxisPosition.Left"/> (the default) or
    /// <see cref="ChartAxisPosition.Right"/> on an upright chart, <see cref="ChartAxisPosition.Bottom"/>
    /// (the default) or <see cref="ChartAxisPosition.Top"/> on a horizontal one. Sides are physical:
    /// a right-to-left page doesn't move the axis. A second axis takes the other side. First axis only.
    /// </summary>
    [Parameter] public ChartAxisPosition Position { get; set; }

    /// <summary>
    /// The least room for the axis's labels beside the plot, such as <c>LabelWidth="Length.Rem(4)"</c>,
    /// on an upright chart. Give charts stacked over each other the same, such as synced ones
    /// (<c>Sync</c>), so their plots line up whatever their values' widths. Wider labels still fit.
    /// </summary>
    [Parameter] public Length? LabelWidth { get; set; }

    private protected override void Check()
    {
        if (Name is not null && Position != ChartAxisPosition.Auto)
        {
            throw new InvalidOperationException("A second ChartValueAxis takes the side across from the first: set Position on the first axis.");
        }

        if (Min is { } lo && Max is { } hi && !(hi > lo))
        {
            throw new ArgumentOutOfRangeException(nameof(Max), hi, "A ChartValueAxis's Max must be more than its Min.");
        }

        if (Step is { } step && !(step > 0 && double.IsFinite(step)))
        {
            throw new ArgumentOutOfRangeException(nameof(Step), step, "A ChartValueAxis's Step must be more than 0.");
        }

        if (Logarithmic)
        {
            if (Min <= 0 || Max <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(Min), "A logarithmic ChartValueAxis needs Min and Max over 0: a log scale has no zero.");
            }

            if (Step is not null)
            {
                throw new InvalidOperationException("A logarithmic ChartValueAxis takes no Step: a log scale's ticks are powers of ten.");
            }
        }
    }
}

/// <summary>
/// A chart's argument axis, where each item is plotted: its format, title, grid lines and how
/// crowded names fit: <c>&lt;ChartArgumentAxis Title="Month" LabelFit="ChartLabelFit.Rotate" /&gt;</c>.
/// </summary>
public sealed class ChartArgumentAxis : ChartAxisBase
{
    /// <summary>A .NET format string for the arguments, such as <c>MMM</c> or <c>N0</c>.</summary>
    [Parameter] public string? Format { get; set; }

    /// <summary>
    /// Writes each argument's text yourself, in place of <see cref="Format"/>. It gets the item's
    /// argument, or a tick on a number or date scale: a double, or a date of the argument's own
    /// type. Dates then have no second line for the month or year.
    /// </summary>
    [Parameter] public Func<object?, string>? Formatter { get; set; }

    /// <summary>
    /// How names along the bottom fit when they crowd: skipped (the default), turned 45° first, or
    /// all shown, wrapped. Applies to names in bands on an upright chart; ticks on a number or date
    /// scale are always chosen to fit.
    /// </summary>
    [Parameter] public ChartLabelFit LabelFit { get; set; }

    /// <summary>Draws grid lines between the arguments (at each tick on a number or date scale). Off by default.</summary>
    [Parameter] public bool GridLines { get; set; }

    /// <summary>
    /// Draws the arguments on a log scale (base 10), for numbers that span several powers of ten,
    /// such as income per person, where each step of ten counts alike. Arguments at or under zero
    /// have no place on it and aren't drawn. Numbers only: a chart of names or dates throws.
    /// </summary>
    [Parameter] public bool Logarithmic { get; set; }

    /// <summary>
    /// Runs the arguments backward: the first at the right, or at the bottom on a horizontal chart.
    /// For an app whose readers take time right to left. A right-to-left page alone doesn't
    /// reverse a chart.
    /// </summary>
    [Parameter] public bool Reverse { get; set; }

    /// <summary>
    /// Spaces dates by item, not by time: each item takes the same room and a date with no item
    /// (a weekend, a holiday, a closed night) takes none, as on a stock chart. The labels still mark
    /// where a day, month or year begins. The items need to be in date order. A chart with bars or
    /// candles already spaces its dates by item.
    /// </summary>
    [Parameter] public bool SkipGaps { get; set; }

    /// <summary>
    /// Lets the reader zoom into part of the arguments and pan along them: a drag across the plot,
    /// Ctrl (⌘ on a Mac) and the wheel, a pinch, + and − on the focused chart, or the Zoom in and
    /// Zoom out buttons by the title. The page still
    /// scrolls under the plain wheel and one finger. A <see cref="ChartRangeSelector"/> in the chart
    /// turns it on by itself. The chart's <c>View</c> and <c>ViewChanged</c> say what's in view.
    /// </summary>
    [Parameter] public bool Zoom { get; set; }

    /// <summary>The fewest items a zoom can come down to. 2 by default.</summary>
    [Parameter] public int MinZoomSpan { get; set; } = 2;
}
