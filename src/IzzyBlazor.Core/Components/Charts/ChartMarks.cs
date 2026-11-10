using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>The axis a mark is laid out against, with the second value axis apart.</summary>
internal enum MarkAxis
{
    Value,
    Argument,
    Secondary,
}

/// <summary>A value worked out from a series, for a <see cref="ChartReferenceLine"/>.</summary>
public enum ChartStatistic
{
    /// <summary>The mean of the series' values.</summary>
    Average,

    /// <summary>The middle value of the series' values.</summary>
    Median,
}

/// <summary>
/// The kinds of marks a chart draws with its series: a line, a band, or a note. The chart lists
/// each one in its description for screen readers, with its value.
/// </summary>
public abstract class ChartMark : ComponentBase, IDisposable
{
    [CascadingParameter] private ChartContextBase? Context { get; set; }

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
    private protected abstract void Check();

    internal abstract ChartMarkInput Read();

    /// <inheritdoc />
    protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
    {
    }

    /// <inheritdoc />
    public void Dispose() => Context?.Unregister(this);

}

/// <summary>
/// A line across a chart at a value, such as a target, or at an argument, such as a launch date,
/// labeled in text beside it:
/// <c>&lt;ChartReferenceLine Value="150_000" Label="Target" /&gt;</c>. With
/// <see cref="Statistic"/> and <see cref="Series"/>, the value is the series' average or median.
/// The line is dashed, so it reads apart from the series.
/// </summary>
public sealed class ChartReferenceLine : ChartMark
{
    /// <summary>The value the line is drawn at, on the value axis.</summary>
    [Parameter] public double? Value { get; set; }

    /// <summary>The argument the line is drawn at, across the argument axis: a name, number or date, such as a launch day.</summary>
    [Parameter] public object? Argument { get; set; }

    /// <summary>
    /// The <see cref="ChartValueAxis.Name"/> of the value axis <see cref="Value"/> or
    /// <see cref="Statistic"/> is read on. Defaults to the first.
    /// </summary>
    [Parameter] public string? ValueAxis { get; set; }

    /// <summary>Works the value out from <see cref="Series"/> in place of <see cref="Value"/>.</summary>
    [Parameter] public ChartStatistic? Statistic { get; set; }

    /// <summary>The name of the series <see cref="Statistic"/> is worked out from.</summary>
    [Parameter] public string? Series { get; set; }

    /// <summary>The line's label, such as "Target". Defaults to its value (or the statistic's name).</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>
    /// Where the label sits along the line. On a line across it sits just above the line, at the
    /// left by default; on an upright line it sits on the line, at the top by default.
    /// <see cref="ChartAlign.Start"/> is the left or the top, <see cref="ChartAlign.Center"/> the
    /// middle, <see cref="ChartAlign.End"/> the right or the bottom.
    /// </summary>
    [Parameter] public ChartAlign? LabelAlign { get; set; }

    private protected override void Check()
    {
        var count = (Value is not null ? 1 : 0) + (Argument is not null ? 1 : 0) + (Statistic is not null ? 1 : 0);
        if (count != 1)
        {
            throw new InvalidOperationException("ChartReferenceLine takes one of Value, Argument or Statistic (with Series).");
        }

        if (Argument is not null && ValueAxis is not null)
        {
            throw new InvalidOperationException("ChartReferenceLine's ValueAxis is for a Value or Statistic: a line at an Argument crosses every value axis.");
        }

        if (Statistic is not null && string.IsNullOrEmpty(Series))
        {
            throw new InvalidOperationException("ChartReferenceLine's Statistic needs Series, the name of the series it's worked out from.");
        }
    }

    internal override ChartMarkInput Read() => new(ChartMarkKind.Line, Argument is not null ? MarkAxis.Argument : MarkAxis.Value, Label)
    {
        Value = Value,
        At = Argument,
        Statistic = Statistic,
        Series = Series,
        ValueAxis = ValueAxis,
        Align = LabelAlign,
    };
}

/// <summary>
/// A shaded range across a chart, labeled in text inside it: between two values with
/// <see cref="From"/> and <see cref="To"/>, such as
/// <c>&lt;ChartReferenceBand From="120_000" To="150_000" Label="Target range" /&gt;</c>, or between
/// two arguments with <see cref="ArgumentFrom"/> and <see cref="ArgumentTo"/>, such as
/// <c>&lt;ChartReferenceBand ArgumentFrom="@("Nov")" ArgumentTo="@("Dec")" Label="Holiday season" /&gt;</c>.
/// Its edges are drawn, so it reads apart from the grid.
/// </summary>
public sealed class ChartReferenceBand : ChartMark
{
    /// <summary>Where a band of values starts, on the value axis.</summary>
    [Parameter] public double? From { get; set; }

    /// <summary>Where a band of values ends.</summary>
    [Parameter] public double? To { get; set; }

    /// <summary>Where a band of arguments starts: a name, number or date.</summary>
    [Parameter] public object? ArgumentFrom { get; set; }

    /// <summary>Where a band of arguments ends. On names, the band covers both ends' bands.</summary>
    [Parameter] public object? ArgumentTo { get; set; }

    /// <summary>
    /// The <see cref="ChartValueAxis.Name"/> of the value axis <see cref="From"/> and
    /// <see cref="To"/> are read on. Defaults to the first.
    /// </summary>
    [Parameter] public string? ValueAxis { get; set; }

    /// <summary>The band's label, such as "Holiday season".</summary>
    [Parameter] public string? Label { get; set; }

    private bool OnArguments => ArgumentFrom is not null || ArgumentTo is not null;

    private protected override void Check()
    {
        var values = From is not null || To is not null;
        if (values == OnArguments)
        {
            throw new InvalidOperationException("ChartReferenceBand takes From and To (values) or ArgumentFrom and ArgumentTo (arguments), not both.");
        }

        if (values ? From is null || To is null : ArgumentFrom is null || ArgumentTo is null)
        {
            throw new InvalidOperationException(values
                ? "ChartReferenceBand needs both From and To."
                : "ChartReferenceBand needs both ArgumentFrom and ArgumentTo.");
        }

        if (OnArguments && ValueAxis is not null)
        {
            throw new InvalidOperationException("ChartReferenceBand's ValueAxis is for a band of values: a band of arguments crosses every value axis.");
        }
    }

    internal override ChartMarkInput Read() => OnArguments
        ? new(ChartMarkKind.Band, MarkAxis.Argument, Label) { At = ArgumentFrom, AtTo = ArgumentTo }
        : new(ChartMarkKind.Band, MarkAxis.Value, Label) { Value = From, To = To, ValueAxis = ValueAxis };
}

/// <summary>
/// A short note at a point on a chart, with a line down to it:
/// <c>&lt;ChartAnnotation Argument="@("Jun")" Value="98_000" Text="Price change" /&gt;</c>.
/// </summary>
public sealed class ChartAnnotation : ChartMark
{
    /// <summary>The argument the note is at: a name, number or date.</summary>
    [Parameter, EditorRequired] public object? Argument { get; set; }

    /// <summary>The value the note points at.</summary>
    [Parameter, EditorRequired] public double Value { get; set; }

    /// <summary>The <see cref="ChartValueAxis.Name"/> of the value axis <see cref="Value"/> is read on. Defaults to the first.</summary>
    [Parameter] public string? ValueAxis { get; set; }

    /// <summary>The note, a few words. Required.</summary>
    [Parameter, EditorRequired] public string Text { get; set; } = "";

    private protected override void Check()
    {
        if (Argument is null || string.IsNullOrWhiteSpace(Text))
        {
            throw new InvalidOperationException("ChartAnnotation needs an Argument and Text.");
        }
    }

    internal override ChartMarkInput Read() => new(ChartMarkKind.Note, MarkAxis.Value, Text) { Value = Value, At = Argument, ValueAxis = ValueAxis };
}

internal enum ChartMarkKind { Line, Band, Note }

/// <summary>A mark as the layout sees it.</summary>
internal sealed record ChartMarkInput(ChartMarkKind Kind, MarkAxis Axis, string? Label)
{
    public double? Value { get; init; }
    public double? To { get; init; }
    public object? At { get; init; }
    public object? AtTo { get; init; }
    public ChartStatistic? Statistic { get; init; }
    public string? Series { get; init; }
    public string? ValueAxis { get; init; }
    public ChartAlign? Align { get; init; }
}

/// <summary>
/// A mark placed: <c>P</c> (and <c>Q</c>, a band's other end) run 0 to 1 along its axis from the
/// start; a note's <c>A</c> is its argument's place. <c>V</c> and <c>VTo</c> are the values behind
/// value-axis positions, for the script to place again when it rescales. <c>Series</c> is the
/// series a statistic hides with.
/// </summary>
internal sealed record MarkShape(ChartMarkKind Kind, MarkAxis Axis, double P, double Q, double A, string Text)
{
    public double? V { get; init; }
    public double? VTo { get; init; }
    public int Series { get; init; } = -1;
    public ChartAlign? Align { get; init; }

    /// <summary>
    /// With zoom on, an argument mark's place (a band's two ends; a note's argument) as shares of
    /// the whole axis, before any reversal, for the script to place it in a view.
    /// </summary>
    public (double P, double Q)? Zoom { get; init; }

    /// <summary>With zoom on, wholly out of view: drawn hidden, for the script to bring into another view.</summary>
    public bool Out { get; init; }
}

/// <summary>What a chart shares with its series and marks.</summary>
internal abstract class ChartContextBase
{
    private readonly ChildOrder<ChartMark> _marks = new();
    private readonly ChildOrder<ChartAxisBase> _axes = new();

    public IReadOnlyList<ChartMark> Marks => _marks.Items;

    public IReadOnlyList<ChartAxisBase> Axes => _axes.Items;

    public ChartPlot? Plot { get; set; }

    /// <summary>The chart's ChartRangeSelector, if it has one.</summary>
    public IRangeSelector? Selector { get; set; }

    public virtual void BeginPass()
    {
        _marks.BeginPass();
        _axes.BeginPass();
    }

    public void Register(ChartAxisBase axis) => _axes.Add(axis);

    public void Seen(ChartAxisBase axis)
    {
        _axes.Seen(axis);
        Plot?.Refresh();
    }

    public void Unregister(ChartAxisBase axis)
    {
        if (_axes.Remove(axis))
        {
            Plot?.Refresh();
        }
    }

    public void Register(ChartMark mark) => _marks.Add(mark);

    public void Seen(ChartMark mark)
    {
        _marks.Seen(mark);
        Plot?.Refresh();
    }

    public void Unregister(ChartMark mark)
    {
        if (_marks.Remove(mark))
        {
            Plot?.Refresh();
        }
    }
}
