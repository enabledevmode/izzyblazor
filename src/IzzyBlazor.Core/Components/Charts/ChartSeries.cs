using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// What every series of a <see cref="Chart{TItem}"/> has: a value from each item, a name, a color,
/// labels and a trend. Use <see cref="ChartBarSeries{TItem}"/>, <see cref="ChartLineSeries{TItem}"/>,
/// <see cref="ChartAreaSeries{TItem}"/>, <see cref="ChartScatterSeries{TItem}"/>,
/// <see cref="ChartBubbleSeries{TItem}"/> or <see cref="ChartWaterfallSeries{TItem}"/>. A series renders nothing itself; the chart draws it.
/// </summary>
/// <typeparam name="TItem">The chart's item type.</typeparam>
public abstract class ChartSeriesBase<TItem> : ComponentBase, IDisposable
{
    private Expression<Func<TItem, object?>>? _lastValue;
    private Func<TItem, object?>? _compiled;
    private Expression<Func<TItem, object?>>? _lastSize;
    private Func<TItem, object?>? _size;
    private string? _sizeName;
    private string? _memberName;
    private Expression<Func<TItem, object?>>? _lastSeriesBy;
    private Func<TItem, object?>? _seriesBy;

    private protected ChartSeriesBase()
    {
    }

    /// <summary>The value to draw, such as <c>m =&gt; m.Revenue</c>: any number, or null for none.</summary>
    [Parameter, EditorRequired] public Expression<Func<TItem, object?>> Value { get; set; } = default!;

    /// <summary>
    /// The series' name, in the legend, the data table and each value's description. Defaults to
    /// the property's <c>[Display]</c> name, or its name in words.
    /// </summary>
    [Parameter] public string? Name { get; set; }

    /// <summary>
    /// Splits the series into one per value of this, such as <c>p =&gt; p.Device</c>: each draws
    /// only its own items' values, is named by its value, and takes the next color and marker, in
    /// the order the values first appear. <see cref="Color"/> doesn't apply to a split series.
    /// </summary>
    [Parameter] public Expression<Func<TItem, object?>>? SeriesBy { get; set; }

    /// <summary>
    /// A CSS color, such as <c>var(--ib-color-danger)</c> or <c>#0f766e</c>. Defaults to the next
    /// of the chart palette's eight colors (<c>--ib-chart-1</c> to <c>--ib-chart-8</c>).
    /// </summary>
    [Parameter] public string? Color { get; set; }

    /// <summary>
    /// Writes each value beside its bar or point. Labels that would overlap others are left out at
    /// the widths where they would, and screen readers skip them: the data table has every value.
    /// </summary>
    [Parameter] public ChartLabels Labels { get; set; }

    /// <summary>A .NET format string for this series' labels, such as <c>C0</c>. Defaults to the values' own text.</summary>
    [Parameter] public string? LabelFormat { get; set; }

    /// <summary>
    /// What labels do when they'd overlap others: left out at those widths (<see cref="ChartLabelOverlap.Hide"/>,
    /// the default), or shown anyway (<see cref="ChartLabelOverlap.Show"/>).
    /// </summary>
    [Parameter] public ChartLabelOverlap LabelOverlap { get; set; }

    /// <summary>
    /// Draws a trend through the values: a dashed line in the series' color, named "Revenue trend"
    /// in the legend, tooltips and data table. <see cref="ChartTrend.Linear"/>,
    /// <see cref="ChartTrend.Polynomial"/> (of <see cref="TrendOrder"/>) or
    /// <see cref="ChartTrend.MovingAverage"/> (over <see cref="TrendPeriod"/>).
    /// </summary>
    [Parameter] public ChartTrend Trend { get; set; }

    /// <summary>A polynomial trend's order, 2 (a parabola) to 6. Defaults to 2.</summary>
    [Parameter] public int TrendOrder { get; set; } = 2;

    /// <summary>How many values a moving average takes in, 2 or more. Defaults to 3.</summary>
    [Parameter] public int TrendPeriod { get; set; } = 3;

    /// <summary>
    /// The trend's CSS color, such as <c>var(--ib-color-danger)</c>, used as given. Defaults to the
    /// series' color mixed toward the text color (darker in light mode, lighter in dark), so it
    /// reads over the series' own bars.
    /// </summary>
    [Parameter] public string? TrendColor { get; set; }

    /// <summary>
    /// The <see cref="ChartValueAxis.Name"/> of the value axis the series is read off, for a
    /// measure in other units on a second axis at the end side, such as <c>"margin"</c>. Defaults
    /// to the first value axis. Two charts side by side usually read better than one with two axes.
    /// </summary>
    [Parameter] public string? ValueAxis { get; set; }

    /// <summary>
    /// Starts with the series hidden: the chart is drawn as if it weren't there, and it keeps its
    /// place in the legend, to show it again, and in the data table.
    /// </summary>
    [Parameter] public bool Hidden { get; set; }

    [CascadingParameter] private ChartContext<TItem>? Context { get; set; }

    internal string DisplayName => Name ?? _memberName ?? "";

    private protected abstract ChartType Type { get; }

    private protected virtual ChartCurve CurveOf => ChartCurve.Straight;

    private protected virtual ChartLineStyle? LineStyleOf => null;

    private protected virtual bool? MarkersOf => null;

    private protected virtual bool ConnectGapsOf => false;

    private protected virtual double? OpacityOf => null;

    private protected virtual Expression<Func<TItem, object?>>? SizeOf => null;

    // A waterfall's: which items are totals, and the colors of rises and falls.
    private protected virtual Func<TItem, bool>? TotalOf => null;

    private protected virtual bool IsWaterfall => false;

    private protected virtual string? IncreaseColorOf => null;

    private protected virtual string? DecreaseColorOf => null;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Context is null)
        {
            throw new InvalidOperationException($"{Kind} must be inside a Chart.");
        }

        Context.Register(this);
    }

    private string Kind => GetType().Name.Split('`')[0];

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Value is null)
        {
            throw new InvalidOperationException($"{Kind} needs a Value, such as m => m.Revenue.");
        }

        if (TrendOrder is < 2 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(TrendOrder), TrendOrder, $"A {Kind}'s TrendOrder goes from 2 to 6.");
        }

        if (TrendPeriod < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(TrendPeriod), TrendPeriod, $"A {Kind}'s TrendPeriod is 2 or more.");
        }

        if (OpacityOf is { } opacity && (opacity is < 0 or > 1 || double.IsNaN(opacity)))
        {
            throw new ArgumentOutOfRangeException("Opacity", opacity, $"A {Kind}'s Opacity goes from 0 to 1.");
        }

        var size = SizeOf;
        if (!ReferenceEquals(size, _lastSize))
        {
            _lastSize = size;
            _size = size?.Compile();
            _sizeName = size is null ? null : ChartMembers.Name(size);
        }

        if (!ReferenceEquals(Value, _lastValue))
        {
            _lastValue = Value;
            _compiled = Value.Compile();
            _memberName = ChartMembers.Name(Value);
        }

        if (!ReferenceEquals(SeriesBy, _lastSeriesBy))
        {
            _lastSeriesBy = SeriesBy;
            _seriesBy = SeriesBy?.Compile();
        }

        Context!.Seen(this);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
    }

    internal IEnumerable<ChartSeriesData> Read(IReadOnlyList<TItem> items)
    {
        // Checked as the chart draws: an exception from a series' own parameters doesn't stop the plot drawing first.
        if (Type == ChartType.Bubble && _size is null)
        {
            throw new InvalidOperationException($"{Kind} needs a Size, such as d => d.Customers.");
        }

        var values = items.Select(item => ChartMembers.ToNumber(_compiled!(item))).ToArray();
        var sizes = _size is null ? null : items.Select(item => ChartMembers.ToNumber(_size(item))).ToArray();
        if (IsWaterfall)
        {
            // A total's value is everything so far, whatever its own Value says.
            var totals = items.Select(item => TotalOf?.Invoke(item) ?? false).ToArray();
            var sum = 0.0;
            for (var i = 0; i < values.Length; i++)
            {
                if (totals[i]) values[i] = sum;
                else if (values[i] is { } v) sum += v;
            }

            yield return Data(DisplayName, values, Color) with
            {
                Totals = totals,
                IncreaseColor = IncreaseColorOf,
                DecreaseColor = DecreaseColorOf,
            };
            yield break;
        }

        if (_seriesBy is null)
        {
            yield return Data(DisplayName, values, Color) with { Sizes = sizes, SizeName = _sizeName };
            yield break;
        }

        // One series per key, in the order the keys first appear; each keeps only its own items.
        var keys = items.Select(_seriesBy).ToArray();
        foreach (var key in keys.Distinct())
        {
            var own = values.Select((v, i) => Equals(keys[i], key) ? v : null).ToArray();
            var ownSizes = sizes?.Select((v, i) => Equals(keys[i], key) ? v : null).ToArray();
            yield return Data(ChartLayout.ArgumentText(key, null, CultureInfo.CurrentCulture), own, null) with { Sizes = ownSizes, SizeName = _sizeName };
        }
    }

    private ChartSeriesData Data(string name, double?[] values, string? color) =>
        new(name, Type, values)
        {
            Color = color,
            Curve = CurveOf,
            LineStyle = LineStyleOf,
            Markers = MarkersOf,
            ConnectGaps = ConnectGapsOf,
            Opacity = OpacityOf,
            Hidden = Hidden,
            Labels = Labels,
            LabelFormat = LabelFormat,
            LabelOverlap = LabelOverlap,
            Trend = Trend,
            TrendOrder = TrendOrder,
            TrendPeriod = TrendPeriod,
            TrendColor = TrendColor,
            ValueAxis = ValueAxis,
        };

    /// <inheritdoc />
    public void Dispose() => Context?.Unregister(this);
}

/// <summary>
/// A series of bars, one per item: upright, or across with the chart's
/// <c>Orientation="Orientation.Horizontal"</c>. The chart's <c>Stacking</c> stacks bar series.
/// </summary>
/// <typeparam name="TItem">The chart's item type.</typeparam>
public sealed class ChartBarSeries<TItem> : ChartSeriesBase<TItem>
{
    /// <summary>
    /// How solid the bars are, from 0 (the surface's color) to 1 (the series color). Defaults to
    /// 0.85 in light mode and 0.9 in dark, or the theme's <c>--ib-chart-fill-opacity</c>.
    /// </summary>
    [Parameter] public double? Opacity { get; set; }

    private protected override ChartType Type => ChartType.Bar;

    private protected override double? OpacityOf => Opacity;
}

/// <summary>A series drawn as a line through each item's value.</summary>
/// <typeparam name="TItem">The chart's item type.</typeparam>
public sealed class ChartLineSeries<TItem> : ChartSeriesBase<TItem>
{
    /// <summary>
    /// How the line is drawn. Defaults to the next of solid, dashed, dotted and dash-dot, so lines
    /// differ by more than color; set it to mark a line apart, such as a plan as
    /// <see cref="ChartLineStyle.Dashed"/>.
    /// </summary>
    [Parameter] public ChartLineStyle? LineStyle { get; set; }

    /// <summary>The line's shape between values. Defaults to straight.</summary>
    [Parameter] public ChartCurve Curve { get; set; }

    /// <summary>Whether each value is marked. Defaults to marking them when there are 40 or fewer.</summary>
    [Parameter] public bool? Markers { get; set; }

    /// <summary>Draws the line straight across a missing value, instead of leaving a gap.</summary>
    [Parameter] public bool ConnectGaps { get; set; }

    private protected override ChartType Type => ChartType.Line;

    private protected override ChartCurve CurveOf => Curve;

    private protected override ChartLineStyle? LineStyleOf => LineStyle;

    private protected override bool? MarkersOf => Markers;

    private protected override bool ConnectGapsOf => ConnectGaps;
}

/// <summary>
/// A series drawn as a line with the area under it filled. The chart's <c>Stacking</c> stacks
/// area series.
/// </summary>
/// <typeparam name="TItem">The chart's item type.</typeparam>
public sealed class ChartAreaSeries<TItem> : ChartSeriesBase<TItem>
{
    /// <summary>How the area's line is drawn. Defaults to the next of solid, dashed, dotted and dash-dot.</summary>
    [Parameter] public ChartLineStyle? LineStyle { get; set; }

    /// <summary>The area's shape between values. Defaults to straight.</summary>
    [Parameter] public ChartCurve Curve { get; set; }

    /// <summary>Whether each value is marked. Defaults to not.</summary>
    [Parameter] public bool? Markers { get; set; }

    /// <summary>Draws the area straight across a missing value, instead of leaving a gap.</summary>
    [Parameter] public bool ConnectGaps { get; set; }

    private protected override ChartType Type => ChartType.Area;

    private protected override ChartCurve CurveOf => Curve;

    private protected override ChartLineStyle? LineStyleOf => LineStyle;

    private protected override bool? MarkersOf => Markers;

    private protected override bool ConnectGapsOf => ConnectGaps;
}

/// <summary>
/// A marker for each item at two measures, the chart's <c>Argument</c> and this <c>Value</c>,
/// such as a page's load time and bounce rate, to show how they relate.
/// </summary>
/// <typeparam name="TItem">The chart's item type.</typeparam>
public sealed class ChartScatterSeries<TItem> : ChartSeriesBase<TItem>
{
    private protected override ChartType Type => ChartType.Scatter;
}

/// <summary>
/// A circle for each item at two measures, sized by a third: each circle's area is in proportion
/// to its <see cref="Size"/>.
/// </summary>
/// <typeparam name="TItem">The chart's item type.</typeparam>
public sealed class ChartBubbleSeries<TItem> : ChartSeriesBase<TItem>
{
    /// <summary>
    /// Each bubble's size, such as <c>d =&gt; d.Customers</c>: its area is in proportion to it,
    /// on one scale across the chart's bubble series, the largest capped to the plot. The tooltip
    /// and the data table give it, named after its property.
    /// </summary>
    [Parameter, EditorRequired] public Expression<Func<TItem, object?>> Size { get; set; } = default!;

    private protected override ChartType Type => ChartType.Bubble;

    private protected override Expression<Func<TItem, object?>>? SizeOf => Size;
}

/// <summary>
/// A waterfall: each item's <c>Value</c> is a change, drawn as a bar from where the last one
/// ended, so the bars walk from a starting amount to a result, such as revenue less costs to
/// profit. An item that's a <see cref="Total"/> is a bar from zero to everything so far. Rises,
/// falls and totals each take a color, and the values read signed ("+12,000", "−4,500").
/// A waterfall is its chart's only series, and the chart's legend keys its three colors.
/// </summary>
/// <typeparam name="TItem">The chart's item type.</typeparam>
public sealed class ChartWaterfallSeries<TItem> : ChartSeriesBase<TItem>
{
    private Expression<Func<TItem, bool>>? _lastTotal;
    private Func<TItem, bool>? _total;

    /// <summary>
    /// Which items are totals, such as <c>r =&gt; r.IsTotal</c>: each is a bar from zero showing
    /// everything so far (gross profit, net profit), whatever its own Value.
    /// </summary>
    [Parameter] public Expression<Func<TItem, bool>>? Total { get; set; }

    /// <summary>
    /// How solid the bars are, from 0 (the surface's color) to 1 (the bar's color). Defaults to
    /// 0.85 in light mode and 0.9 in dark, or the theme's <c>--ib-chart-fill-opacity</c>.
    /// </summary>
    [Parameter] public double? Opacity { get; set; }

    /// <summary>
    /// The CSS color of bars that add, such as <c>var(--ib-color-success)</c>. Defaults to the
    /// palette's green (<c>--ib-chart-increase</c>, or <c>--ib-chart-2</c>). <c>Color</c> is the totals'.
    /// </summary>
    [Parameter] public string? IncreaseColor { get; set; }

    /// <summary>
    /// The CSS color of bars that take away. Defaults to the palette's red
    /// (<c>--ib-chart-decrease</c>, or <c>--ib-chart-8</c>).
    /// </summary>
    [Parameter] public string? DecreaseColor { get; set; }

    private protected override ChartType Type => ChartType.Bar;

    private protected override double? OpacityOf => Opacity;

    private protected override bool IsWaterfall => true;

    private protected override Func<TItem, bool>? TotalOf => _total;

    private protected override string? IncreaseColorOf => IncreaseColor;

    private protected override string? DecreaseColorOf => DecreaseColor;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Trend != ChartTrend.None)
        {
            throw new InvalidOperationException("A ChartWaterfallSeries takes no Trend: its bars are changes, not a run of values.");
        }

        if (SeriesBy is not null)
        {
            throw new InvalidOperationException("A ChartWaterfallSeries can't be split with SeriesBy: a waterfall is one run of changes.");
        }

        if (!ReferenceEquals(Total, _lastTotal))
        {
            _lastTotal = Total;
            _total = Total?.Compile();
        }

        base.OnParametersSet();
    }
}


/// <summary>What a <see cref="Chart{TItem}"/> shares with its series: the series, in order.</summary>
internal sealed class ChartContext<TItem> : ChartContextBase
{
    private readonly ChildOrder<ChartSeriesBase<TItem>> _series = new();

    public IReadOnlyList<ChartSeriesBase<TItem>> Series => _series.Items;

    /// <summary>The <see cref="ChartTooltip{TItem}"/>'s content, if the chart has one.</summary>
    public RenderFragment<ChartTooltipContext<TItem>>? TooltipTemplate { get; set; }

    public override void BeginPass()
    {
        base.BeginPass();
        _series.BeginPass();
    }

    public void Seen(ChartSeriesBase<TItem> series)
    {
        _series.Seen(series);

        // A series changed on its own (an interactive page): draw again.
        Plot?.Refresh();
    }

    public void Register(ChartSeriesBase<TItem> series) => _series.Add(series);

    public void Unregister(ChartSeriesBase<TItem> series)
    {
        if (_series.Remove(series))
        {
            Plot?.Refresh();
        }
    }
}

/// <summary>
/// Where a chart draws, rendered after its series so it knows them all. The chart passes the
/// drawing as a fragment that reads the series when it runs.
/// </summary>
internal sealed class ChartPlot : ComponentBase
{
    [Parameter] public RenderFragment? Content { get; set; }

    [Parameter] public Action<ChartPlot>? Attach { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized() => Attach?.Invoke(this);

    internal void Refresh() => StateHasChanged();

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, Content);
}

internal static class ChartMembers
{
    /// <summary>The member a lambda reads, such as Revenue in <c>m =&gt; m.Revenue</c>, as its display name.</summary>
    public static string? Name(LambdaExpression expression)
    {
        var body = expression.Body;
        while (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } convert)
        {
            body = convert.Operand;
        }

        return body is MemberExpression { Member: var member }
            ? member.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? DataFields.Words(member.Name)
            : null;
    }

    public static double? ToNumber(object? value) => value switch
    {
        null => null,
        double d => double.IsFinite(d) ? d : null,
        float f => float.IsFinite(f) ? f : null,
        decimal m => (double)m,
        IConvertible c when value is byte or sbyte or short or ushort or int or uint or long or ulong => c.ToDouble(CultureInfo.InvariantCulture),
        _ => throw new InvalidOperationException($"A chart value must be a number; this one is a {value.GetType().Name}."),
    };
}
