using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>Which axis a chart mark is drawn against.</summary>
public enum ChartAxis
{
    /// <summary>The value axis: a mark at a value, across the plot.</summary>
    Value,

    /// <summary>The argument axis: a mark at an argument (a name, number or date).</summary>
    Argument,

    /// <summary>
    /// The second value axis, at the end side: a series on it (<c>ChartSeries Axis</c>) has its own
    /// scale, and a reference line or band on it is drawn by that scale.
    /// </summary>
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

    private protected static double? ToNumber(object? value) => value switch
    {
        null => null,
        IConvertible c when value is not string and not DateTime => Convert.ToDouble(c, CultureInfo.InvariantCulture),
        _ => throw new ArgumentException($"A value-axis mark takes numbers; got {value.GetType().Name}."),
    };
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

    /// <summary>
    /// The axis: <see cref="ChartAxis.Value"/>, the default; <see cref="ChartAxis.Secondary"/>, a
    /// value on the second axis; or <see cref="ChartAxis.Argument"/> with <see cref="At"/>.
    /// </summary>
    [Parameter] public ChartAxis Axis { get; set; }

    /// <summary>The argument the line is drawn at, with <c>Axis="ChartAxis.Argument"</c>: a name, number or date.</summary>
    [Parameter] public object? At { get; set; }

    /// <summary>Works the value out from <see cref="Series"/> in place of <see cref="Value"/>.</summary>
    [Parameter] public ChartStatistic? Statistic { get; set; }

    /// <summary>The name of the series <see cref="Statistic"/> is worked out from.</summary>
    [Parameter] public string? Series { get; set; }

    /// <summary>The line's label, such as "Target". Defaults to its value (or the statistic's name).</summary>
    [Parameter] public string? Label { get; set; }

    private protected override void Check()
    {
        var count = (Value is not null ? 1 : 0) + (At is not null ? 1 : 0) + (Statistic is not null ? 1 : 0);
        if (count != 1)
        {
            throw new InvalidOperationException("ChartReferenceLine takes one of Value, At (with Axis=\"ChartAxis.Argument\") or Statistic (with Series).");
        }

        if (At is not null && Axis != ChartAxis.Argument)
        {
            throw new InvalidOperationException("ChartReferenceLine's At is an argument: set Axis=\"ChartAxis.Argument\" with it.");
        }

        if (Statistic is not null && string.IsNullOrEmpty(Series))
        {
            throw new InvalidOperationException("ChartReferenceLine's Statistic needs Series, the name of the series it's worked out from.");
        }
    }

    internal override ChartMarkInput Read() => new(ChartMarkKind.Line, At is not null ? ChartAxis.Argument : Axis == ChartAxis.Secondary ? ChartAxis.Secondary : ChartAxis.Value, Label)
    {
        Value = Value,
        At = At,
        Statistic = Statistic,
        Series = Series,
    };
}

/// <summary>
/// A shaded range across a chart, on either axis, labeled in text inside it:
/// <c>&lt;ChartReferenceBand Axis="ChartAxis.Argument" From="@("Nov")" To="@("Dec")" Label="Holiday season" /&gt;</c>.
/// Its edges are drawn, so it reads apart from the grid.
/// </summary>
public sealed class ChartReferenceBand : ChartMark
{
    /// <summary>The axis: <see cref="ChartAxis.Value"/>, the default, <see cref="ChartAxis.Secondary"/>, or <see cref="ChartAxis.Argument"/>.</summary>
    [Parameter] public ChartAxis Axis { get; set; }

    /// <summary>Where the band starts: a number on the value axis, or an argument (a name, number or date).</summary>
    [Parameter, EditorRequired] public object? From { get; set; }

    /// <summary>Where it ends. On names, the band covers both ends' bands.</summary>
    [Parameter, EditorRequired] public object? To { get; set; }

    /// <summary>The band's label, such as "Holiday season".</summary>
    [Parameter] public string? Label { get; set; }

    private protected override void Check()
    {
        if (From is null || To is null)
        {
            throw new InvalidOperationException("ChartReferenceBand needs From and To.");
        }

        if (Axis != ChartAxis.Argument)
        {
            ToNumber(From);
            ToNumber(To);
        }
    }

    internal override ChartMarkInput Read() => Axis != ChartAxis.Argument
        ? new(ChartMarkKind.Band, Axis, Label) { Value = ToNumber(From), To = ToNumber(To) }
        : new(ChartMarkKind.Band, ChartAxis.Argument, Label) { At = From, AtTo = To };
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

    /// <summary>The note, a few words. Required.</summary>
    [Parameter, EditorRequired] public string Text { get; set; } = "";

    private protected override void Check()
    {
        if (Argument is null || string.IsNullOrWhiteSpace(Text))
        {
            throw new InvalidOperationException("ChartAnnotation needs an Argument and Text.");
        }
    }

    internal override ChartMarkInput Read() => new(ChartMarkKind.Note, ChartAxis.Value, Text) { Value = Value, At = Argument };
}

internal enum ChartMarkKind { Line, Band, Note }

/// <summary>A mark as the layout sees it.</summary>
internal sealed record ChartMarkInput(ChartMarkKind Kind, ChartAxis Axis, string? Label)
{
    public double? Value { get; init; }
    public double? To { get; init; }
    public object? At { get; init; }
    public object? AtTo { get; init; }
    public ChartStatistic? Statistic { get; init; }
    public string? Series { get; init; }
}

/// <summary>
/// A mark placed: <c>P</c> (and <c>Q</c>, a band's other end) run 0 to 1 along its axis from the
/// start; a note's <c>A</c> is its argument's place. <c>V</c> and <c>VTo</c> are the values behind
/// value-axis positions, for the script to place again when it rescales. <c>Series</c> is the
/// series a statistic hides with.
/// </summary>
internal sealed record MarkShape(ChartMarkKind Kind, ChartAxis Axis, double P, double Q, double A, string Text)
{
    public double? V { get; init; }
    public double? VTo { get; init; }
    public int Series { get; init; } = -1;
}

/// <summary>What a chart shares with its series and marks.</summary>
internal abstract class ChartContextBase
{
    private readonly ChildOrder<ChartMark> _marks = new();

    public IReadOnlyList<ChartMark> Marks => _marks.Items;

    public ChartPlot? Plot { get; set; }

    public virtual void BeginPass() => _marks.BeginPass();

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
