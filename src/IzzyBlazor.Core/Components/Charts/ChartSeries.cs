using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// What every series of a <see cref="Chart{TItem}"/> has: a name, a color, labels and a trend. Use
/// <see cref="ChartBarSeries{TItem}"/>, <see cref="ChartLineSeries{TItem}"/>,
/// <see cref="ChartAreaSeries{TItem}"/>, <see cref="ChartScatterSeries{TItem}"/>,
/// <see cref="ChartBubbleSeries{TItem}"/>, <see cref="ChartWaterfallSeries{TItem}"/>,
/// <see cref="ChartRangeBarSeries{TItem}"/> or <see cref="ChartRangeAreaSeries{TItem}"/>. A series
/// renders nothing itself; the chart draws it.
/// </summary>
/// <typeparam name="TItem">The chart's item type.</typeparam>
public abstract class ChartSeriesBase<TItem> : ComponentBase, IDisposable
{
    private Expression<Func<TItem, object?>>? _lastValue;
    private Func<TItem, object?>? _compiled;
    private Expression<Func<TItem, object?>>? _lastLow;
    private Func<TItem, object?>? _low;
    private Expression<Func<TItem, object?>>? _lastTarget;
    private Func<TItem, object?>? _target;
    private Expression<Func<TItem, object?>>? _lastOpen;
    private Func<TItem, object?>? _open;
    private Expression<Func<TItem, object?>>? _lastClose;
    private Func<TItem, object?>? _close;
    private Expression<Func<TItem, object?>>? _lastSize;
    private Func<TItem, object?>? _size;
    private string? _sizeName;
    private string? _memberName;
    private Expression<Func<TItem, object?>>? _lastSeriesBy;
    private Func<TItem, object?>? _seriesBy;
    private Expression<Func<TItem, object?>>? _lastLabelText;
    private Func<TItem, object?>? _labelText;
    private string?[]? _labelTexts;

    private protected ChartSeriesBase()
    {
    }

    /// <summary>
    /// With <see cref="ChartDrillLevel{TItem}"/>s, how each level adds up its items' values:
    /// <see cref="DataAggregate.Sum"/> (the default), <see cref="DataAggregate.Avg"/>,
    /// <see cref="DataAggregate.Min"/>, <see cref="DataAggregate.Max"/> or
    /// <see cref="DataAggregate.Count"/> (the items with a value).
    /// </summary>
    [Parameter] public DataAggregate Aggregate { get; set; }

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
    /// What each label says in place of its value, such as <c>r =&gt; r.Region</c> to name each
    /// bubble or point. Setting it turns the labels on (<see cref="ChartLabels.Auto"/>, unless
    /// <see cref="Labels"/> says otherwise), and the tooltip stays on for the values.
    /// </summary>
    [Parameter] public Expression<Func<TItem, object?>>? LabelText { get; set; }

    /// <summary>
    /// What labels do when they'd overlap others: left out at those widths (<see cref="ChartLabelOverlap.Hide"/>,
    /// the default), or shown anyway (<see cref="ChartLabelOverlap.Show"/>).
    /// </summary>
    [Parameter] public ChartLabelOverlap LabelOverlap { get; set; }

    /// <summary>
    /// Draws a trend through the values: a dashed line in the series' color, named "Revenue trend"
    /// (a moving average "Revenue, 7-day average") in the legend, tooltips and data table. <see cref="ChartTrend.Linear"/>,
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

    /// <summary>The value's own name, such as "Delivery days" for <c>o =&gt; o.DeliveryDays</c>.</summary>
    internal string? ValueName => _memberName;

    internal bool Named => Name is not null;

    internal bool Split => SeriesBy is not null;

    internal bool IsHistogram => BinsOf is not null;

    internal ChartBins? HistogramBins => BinsOf;

    private protected abstract ChartType Type { get; }

    // The value each item is drawn at (a range's high end), and a range's low end.
    private protected abstract Expression<Func<TItem, object?>>? ValueOf { get; }

    private protected virtual Expression<Func<TItem, object?>>? LowOf => null;

    private protected virtual bool IsRange => false;

    // A bar series' target for each item.
    private protected virtual Expression<Func<TItem, object?>>? TargetOf => null;

    // A price series' open and close for each item, and its look.
    private protected virtual Expression<Func<TItem, object?>>? OpenOf => null;

    private protected virtual Expression<Func<TItem, object?>>? CloseOf => null;

    private protected virtual ChartOhlcLook OhlcLookOf => ChartOhlcLook.Candlestick;

    // What to say when the value is missing.
    private protected virtual string ValueNeeded => $"{Kind} needs a Value, such as m => m.Revenue.";

    private protected virtual ChartCurve CurveOf => ChartCurve.Straight;

    private protected virtual ChartLineStyle? LineStyleOf => null;

    private protected virtual bool? MarkersOf => null;

    private protected virtual bool ConnectGapsOf => false;

    private protected virtual bool EndLabelOf => false;

    private protected virtual double? OpacityOf => null;

    private protected virtual Expression<Func<TItem, object?>>? SizeOf => null;

    private protected virtual string? SizeFormatOf => null;

    private protected virtual bool ShowSizeKeyOf => false;

    // A waterfall's: which items are totals, and the colors of rises and falls.
    private protected virtual Func<TItem, bool>? TotalOf => null;

    private protected virtual bool IsWaterfall => false;

    private protected virtual string? IncreaseColorOf => null;

    private protected virtual string? DecreaseColorOf => null;

    // A histogram's: its bins, and one of its dates (null for numbers).
    private protected virtual ChartBins? BinsOf => null;

    private protected virtual object? DateOf => null;

    // Each value as a number; a histogram reads dates too.
    private protected virtual double? Number(object? value) => ChartMembers.ToNumber(value);

    private protected virtual void BeginRead()
    {
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Context is null)
        {
            throw new InvalidOperationException($"{Kind} must be inside a Chart.");
        }

        Context.Register(this);
    }

    private protected string Kind => GetType().Name.Split('`')[0];

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        var value = ValueOf;
        var low = LowOf;
        if (value is null || (low is null && IsRange))
        {
            throw new InvalidOperationException(ValueNeeded);
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

        if (!ReferenceEquals(LabelText, _lastLabelText))
        {
            _lastLabelText = LabelText;
            _labelText = LabelText?.Compile();
        }

        var size = SizeOf;
        if (!ReferenceEquals(size, _lastSize))
        {
            _lastSize = size;
            _size = size?.Compile();
            _sizeName = size is null ? null : ChartMembers.Name(size);
        }

        if (!ReferenceEquals(value, _lastValue) || !ReferenceEquals(low, _lastLow))
        {
            (_lastValue, _lastLow) = (value, low);
            _compiled = value.Compile();
            _low = low?.Compile();
            // A range is named by both ends, such as "Low – High", until it's given a Name; prices
            // by none (their tooltip names each one).
            _memberName = CloseOf is not null ? null : low is null ? ChartMembers.Name(value)
                : ChartMembers.Name(low) is { } l && ChartMembers.Name(value) is { } h ? $"{l} – {h}" : null;
        }

        if (!ReferenceEquals(OpenOf, _lastOpen) || !ReferenceEquals(CloseOf, _lastClose))
        {
            (_lastOpen, _lastClose) = (OpenOf, CloseOf);
            (_open, _close) = (_lastOpen?.Compile(), _lastClose?.Compile());
        }

        if (!ReferenceEquals(TargetOf, _lastTarget))
        {
            _lastTarget = TargetOf;
            _target = _lastTarget?.Compile();
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

        BeginRead();
        var values = items.Select(item => Number(_compiled!(item))).ToArray();
        var lows = _low is null ? null : items.Select(item => ChartMembers.ToNumber(_low(item))).ToArray();
        var targets = _target is null ? null : items.Select(item => ChartMembers.ToNumber(_target(item))).ToArray();
        _labelTexts = _labelText is null ? null : items.Select(item => _labelText(item)?.ToString()).ToArray();
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

        if (_close is not null)
        {
            yield return Data(DisplayName, values, Color) with
            {
                Lows = lows,
                Opens = _open is null ? null : items.Select(item => ChartMembers.ToNumber(_open(item))).ToArray(),
                Closes = items.Select(item => ChartMembers.ToNumber(_close(item))).ToArray(),
                OhlcBars = OhlcLookOf == ChartOhlcLook.Bars,
                IncreaseColor = IncreaseColorOf,
                DecreaseColor = DecreaseColorOf,
            };
            yield break;
        }

        if (_seriesBy is null)
        {
            yield return Data(DisplayName, values, Color) with { Sizes = sizes, SizeName = _sizeName, Lows = lows, Targets = targets };
            yield break;
        }

        // One series per key, in the order the keys first appear; each keeps only its own items.
        var keys = items.Select(_seriesBy).ToArray();
        foreach (var key in keys.Distinct())
        {
            var own = values.Select((v, i) => Equals(keys[i], key) ? v : null).ToArray();
            var ownSizes = sizes?.Select((v, i) => Equals(keys[i], key) ? v : null).ToArray();
            var ownLows = lows?.Select((v, i) => Equals(keys[i], key) ? v : null).ToArray();
            var ownTargets = targets?.Select((v, i) => Equals(keys[i], key) ? v : null).ToArray();
            yield return Data(ChartLayout.ArgumentText(key, null, CultureInfo.CurrentCulture), own, null) with { Sizes = ownSizes, SizeName = _sizeName, Lows = ownLows, Targets = ownTargets };
        }
    }

    private ChartSeriesData Data(string name, double?[] values, string? color) =>
        new(name, Type, values)
        {
            Aggregate = Aggregate,
            Color = color,
            Curve = CurveOf,
            LineStyle = LineStyleOf,
            Markers = MarkersOf,
            ConnectGaps = ConnectGapsOf,
            EndLabel = EndLabelOf,
            SizeFormat = SizeFormatOf,
            ShowSizeKey = ShowSizeKeyOf,
            Opacity = OpacityOf,
            Hidden = Hidden,
            Labels = Labels == ChartLabels.None && _labelText is not null ? ChartLabels.Auto : Labels,
            LabelFormat = LabelFormat,
            LabelTexts = _labelTexts,
            LabelOverlap = LabelOverlap,
            Trend = Trend,
            TrendOrder = TrendOrder,
            TrendPeriod = TrendPeriod,
            TrendColor = TrendColor,
            ValueAxis = ValueAxis,
            Bins = BinsOf,
            Date = DateOf,
        };

    /// <inheritdoc />
    public void Dispose() => Context?.Unregister(this);
}

/// <summary>
/// A series with one value from each item: its <see cref="Value"/>. Use
/// <see cref="ChartBarSeries{TItem}"/>, <see cref="ChartLineSeries{TItem}"/>,
/// <see cref="ChartAreaSeries{TItem}"/>, <see cref="ChartScatterSeries{TItem}"/>,
/// <see cref="ChartBubbleSeries{TItem}"/> or <see cref="ChartWaterfallSeries{TItem}"/>.
/// </summary>
/// <typeparam name="TItem">The chart's item type.</typeparam>
public abstract class ChartValueSeriesBase<TItem> : ChartSeriesBase<TItem>
{
    private protected ChartValueSeriesBase()
    {
    }

    /// <summary>The value to draw, such as <c>m =&gt; m.Revenue</c>: any number, or null for none.</summary>
    [Parameter, EditorRequired] public Expression<Func<TItem, object?>> Value { get; set; } = default!;

    private protected override Expression<Func<TItem, object?>>? ValueOf => Value;
}

/// <summary>
/// A bar chart's bars, one per item, to compare values across names such as products or
/// regions. Bars stand upright, or lie across with the chart's
/// <c>Orientation="Orientation.Horizontal"</c>, which suits long names and rankings. Several bar
/// series sit side by side, or stack with the chart's <c>Stacking</c>.
/// </summary>
/// <typeparam name="TItem">The chart's item type.</typeparam>
/// <example>
/// <code>
/// &lt;Chart Items="months" Argument="m =&gt; m.Month" Title="Revenue by month, 2026"&gt;
///     &lt;ChartValueAxis Format="C0" /&gt;
///     &lt;ChartBarSeries Value="m =&gt; m.Revenue" Name="Revenue" /&gt;
/// &lt;/Chart&gt;
/// </code>
/// </example>
[ModeSupport]
public sealed class ChartBarSeries<TItem> : ChartValueSeriesBase<TItem>
{
    /// <summary>
    /// Each item's target, such as <c>r =&gt; r.Quota</c>: a tick across its bar in the text
    /// color, named in the tooltip and data table ("$1.2M, target $1.5M"). Null for none. The
    /// value axis takes in every target. For bars side by side, not stacked. For one measure
    /// against its target and qualitative ranges, use a <see cref="BulletChart{TItem}"/>.
    /// </summary>
    [Parameter] public Expression<Func<TItem, object?>>? Target { get; set; }

    private protected override Expression<Func<TItem, object?>>? TargetOf => Target;

    /// <summary>
    /// How solid the bars are, from 0 (the surface's color) to 1 (the series color). Defaults to
    /// 0.85 in light mode and 0.9 in dark, or the theme's <c>--ib-chart-fill-opacity</c>.
    /// </summary>
    [Parameter] public double? Opacity { get; set; }

    private protected override ChartType Type => ChartType.Bar;

    private protected override double? OpacityOf => Opacity;
}

/// <summary>
/// A line chart's line through each item's value, to show how a measure changes over time. Each
/// line series is one line, and several lines differ by dash as well as by color.
/// </summary>
/// <typeparam name="TItem">The chart's item type.</typeparam>
/// <example>
/// <code>
/// &lt;Chart Items="months" Argument="m =&gt; m.Month" Title="Weekly active users by app, 2026"&gt;
///     &lt;ChartLineSeries Value="m =&gt; m.Web" Name="Web" EndLabel /&gt;
///     &lt;ChartLineSeries Value="m =&gt; m.Ios" Name="iOS" EndLabel /&gt;
/// &lt;/Chart&gt;
/// </code>
/// </example>
[ModeSupport]
public sealed class ChartLineSeries<TItem> : ChartValueSeriesBase<TItem>
{
    /// <summary>
    /// How the line is drawn. Defaults to the next of solid, dashed, dotted and dash-dot, so lines
    /// differ by more than color; set it to mark a line apart, such as a plan as
    /// <see cref="ChartLineStyle.Dashed"/>.
    /// </summary>
    [Parameter] public ChartLineStyle? LineStyle { get; set; }

    /// <summary>The line's shape between values. Defaults to straight.</summary>
    [Parameter] public ChartCurve Curve { get; set; }

    /// <summary>Whether each value is marked. Defaults to marking them when there are 12 or fewer: more read as a line, and the markers crowd it.</summary>
    [Parameter] public bool? Markers { get; set; }

    /// <summary>Draws the line straight across a missing value, instead of leaving a gap.</summary>
    [Parameter] public bool ConnectGaps { get; set; }

    /// <summary>
    /// Writes the series' name just past the plot's end, level with its last value, in its color:
    /// the reader names the line where it ends, without looking across to a legend. Names that
    /// would overlap are nudged apart. When every series has one, the chart leaves its legend out
    /// by default. Upright charts only.
    /// </summary>
    [Parameter] public bool EndLabel { get; set; }

    private protected override ChartType Type => ChartType.Line;

    private protected override ChartCurve CurveOf => Curve;

    private protected override ChartLineStyle? LineStyleOf => LineStyle;

    private protected override bool? MarkersOf => Markers;

    private protected override bool ConnectGapsOf => ConnectGaps;

    private protected override bool EndLabelOf => EndLabel;
}

/// <summary>
/// An area chart's filled area under a line through each item's value, to show a quantity over
/// time. The chart's <c>Stacking</c> stacks area series, so the parts add up to the whole, such as
/// visits by channel.
/// </summary>
/// <typeparam name="TItem">The chart's item type.</typeparam>
/// <example>
/// <code>
/// &lt;Chart Items="months"
///        Argument="m =&gt; m.Month"
///        Title="Visits by channel, 2026"
///        Stacking="ChartStacking.Normal"&gt;
///     &lt;ChartAreaSeries Value="m =&gt; m.Search" Name="Search" /&gt;
///     &lt;ChartAreaSeries Value="m =&gt; m.Direct" Name="Direct" /&gt;
/// &lt;/Chart&gt;
/// </code>
/// </example>
[ModeSupport]
public sealed class ChartAreaSeries<TItem> : ChartValueSeriesBase<TItem>
{
    /// <summary>How the area's line is drawn. Defaults to the next of solid, dashed, dotted and dash-dot.</summary>
    [Parameter] public ChartLineStyle? LineStyle { get; set; }

    /// <summary>The area's shape between values. Defaults to straight.</summary>
    [Parameter] public ChartCurve Curve { get; set; }

    /// <summary>Whether each value is marked. Defaults to not.</summary>
    [Parameter] public bool? Markers { get; set; }

    /// <summary>Draws the area straight across a missing value, instead of leaving a gap.</summary>
    [Parameter] public bool ConnectGaps { get; set; }

    /// <summary>
    /// Writes the series' name just past the plot's end, level with its last value (the top of its stack, when stacked), in its color:
    /// the reader names the line where it ends, without looking across to a legend. Names that
    /// would overlap are nudged apart. When every series has one, the chart leaves its legend out
    /// by default. Upright charts only.
    /// </summary>
    [Parameter] public bool EndLabel { get; set; }

    private protected override ChartType Type => ChartType.Area;

    private protected override ChartCurve CurveOf => Curve;

    private protected override ChartLineStyle? LineStyleOf => LineStyle;

    private protected override bool? MarkersOf => Markers;

    private protected override bool ConnectGapsOf => ConnectGaps;

    private protected override bool EndLabelOf => EndLabel;
}

/// <summary>
/// A scatter chart's markers, one per item at two measures, the chart's <c>Argument</c> and this
/// <c>Value</c>, to show how they relate, such as a page's load time and bounce rate.
/// </summary>
/// <typeparam name="TItem">The chart's item type.</typeparam>
/// <example>
/// <code>
/// &lt;Chart Items="pages" Argument="p =&gt; p.LoadSeconds" Title="Load time against bounce rate"&gt;
///     &lt;ChartValueAxis Format="P0" Title="Bounce rate" /&gt;
///     &lt;ChartArgumentAxis Title="Load time (s)" /&gt;
///     &lt;ChartScatterSeries Value="p =&gt; p.BounceRate" Name="Pages" /&gt;
/// &lt;/Chart&gt;
/// </code>
/// </example>
[ModeSupport]
public sealed class ChartScatterSeries<TItem> : ChartValueSeriesBase<TItem>
{
    private protected override ChartType Type => ChartType.Scatter;
}

/// <summary>
/// A bubble chart's circles, one per item at two measures and sized by a third, such as products
/// by growth and margin, sized by revenue. Each circle's area is in proportion to its
/// <see cref="Size"/>.
/// </summary>
/// <typeparam name="TItem">The chart's item type.</typeparam>
/// <example>
/// <code>
/// &lt;Chart Items="products" Argument="p =&gt; p.Growth" Title="Growth, margin and revenue, 2026"&gt;
///     &lt;ChartValueAxis Format="P0" Title="Gross margin" /&gt;
///     &lt;ChartArgumentAxis Format="P0" Title="Revenue growth" /&gt;
///     &lt;ChartBubbleSeries Value="p =&gt; p.Margin" Size="p =&gt; p.Revenue" Name="Products" /&gt;
/// &lt;/Chart&gt;
/// </code>
/// </example>
[ModeSupport]
public sealed class ChartBubbleSeries<TItem> : ChartValueSeriesBase<TItem>
{
    /// <summary>
    /// Each bubble's size, such as <c>d =&gt; d.Customers</c>: its area is in proportion to it,
    /// on one scale across the chart's bubble series, the largest capped to the plot. The tooltip
    /// and the data table give it, named after its property.
    /// </summary>
    [Parameter, EditorRequired] public Expression<Func<TItem, object?>> Size { get; set; } = default!;

    private protected override ChartType Type => ChartType.Bubble;

    /// <summary>
    /// A .NET format string for the sizes, such as <c>C0</c> or <c>$#,##0,,.#M</c>, in the
    /// tooltip, the data table and the size key. Defaults to the sizes' own text.
    /// </summary>
    [Parameter] public string? SizeFormat { get; set; }

    private protected override Expression<Func<TItem, object?>>? SizeOf => Size;

    /// <summary>
    /// Shows a size key: reference circles at the far end of the legend row, for a round size
    /// near the largest and a quarter of it, so a bubble's size can be read, not only compared.
    /// </summary>
    [Parameter] public bool ShowSizeKey { get; set; }

    private protected override string? SizeFormatOf => SizeFormat;

    private protected override bool ShowSizeKeyOf => ShowSizeKey;
}

/// <summary>
/// A waterfall chart: each item's <c>Value</c> is a change, drawn as a bar from where the last one
/// ended, so the bars walk from a starting amount to a result, such as revenue less costs to
/// profit. The first bar, the starting amount, and each item that's a <see cref="Total"/> stand
/// on the baseline, a total showing everything so far. Rises, falls and totals each take a color,
/// and the changes read signed ("+12,000", "−4,500"). Line items such as a profit and loss read
/// best as bars across (the chart's <c>Orientation.Horizontal</c>); changes over time, as columns.
/// A waterfall is its chart's only series, and the chart's legend keys its three colors.
/// </summary>
/// <typeparam name="TItem">The chart's item type.</typeparam>
/// <example>
/// <code>
/// &lt;Chart Items="lines" Argument="l =&gt; l.Name" Title="Profit and loss, Q3 2026"&gt;
///     &lt;ChartValueAxis Format="C0" /&gt;
///     &lt;ChartWaterfallSeries Value="l =&gt; l.Amount" Total="l =&gt; l.IsTotal" /&gt;
/// &lt;/Chart&gt;
/// </code>
/// </example>
[ModeSupport]
public sealed class ChartWaterfallSeries<TItem> : ChartValueSeriesBase<TItem>
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
    /// palette's blue (<c>--ib-chart-increase</c>, or <c>--ib-chart-1</c>), which stays apart from
    /// the red of falls for red-green color blindness. <c>Color</c> is the totals' (gray by default).
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

/// <summary>
/// A series with a low and a high value from each item, drawn from one to the other:
/// <see cref="ChartRangeBarSeries{TItem}"/> or <see cref="ChartRangeAreaSeries{TItem}"/>.
/// </summary>
/// <typeparam name="TItem">The chart's item type.</typeparam>
public abstract class ChartRangeSeriesBase<TItem> : ChartSeriesBase<TItem>
{
    private protected ChartRangeSeriesBase()
    {
    }

    /// <summary>
    /// Each item's low end, such as <c>d =&gt; d.Min</c>: any number, or null for none (no range
    /// at that item). With <see cref="ChartDrillLevel{TItem}"/>s, a level takes its items' lowest.
    /// </summary>
    [Parameter, EditorRequired] public Expression<Func<TItem, object?>> Low { get; set; } = default!;

    /// <summary>Each item's high end, such as <c>d =&gt; d.Max</c>. With drill-down levels, a level takes its items' highest.</summary>
    [Parameter, EditorRequired] public Expression<Func<TItem, object?>> High { get; set; } = default!;

    private protected override Expression<Func<TItem, object?>>? ValueOf => High;

    private protected override Expression<Func<TItem, object?>>? LowOf => Low;

    private protected override bool IsRange => true;

    private protected override string ValueNeeded => $"{Kind} needs a Low and a High, such as d => d.Min and d => d.Max.";

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Trend != ChartTrend.None)
        {
            throw new InvalidOperationException($"A {Kind} takes no Trend: draw the measure inside the range, such as its middle, as a line series with a Trend.");
        }

        base.OnParametersSet();
    }
}

/// <summary>
/// A bar from each item's low value to its high one, such as a day's lowest and highest
/// temperature. Bars stand upright, or lie across with the chart's
/// <c>Orientation="Orientation.Horizontal"</c>. The value axis needn't start at zero: a range
/// bar's length is the spread, not the amount.
/// </summary>
/// <typeparam name="TItem">The chart's item type.</typeparam>
/// <example>
/// <code>
/// &lt;Chart Items="days" Argument="d =&gt; d.Day" Title="Temperature, March 2026"&gt;
///     &lt;ChartValueAxis Format="0°C" /&gt;
///     &lt;ChartRangeBarSeries Low="d =&gt; d.Low" High="d =&gt; d.High" Name="Daily range" /&gt;
/// &lt;/Chart&gt;
/// </code>
/// </example>
[ModeSupport]
public sealed class ChartRangeBarSeries<TItem> : ChartRangeSeriesBase<TItem>
{
    /// <summary>
    /// How solid the bars are, from 0 (the surface's color) to 1 (the series color). Defaults to
    /// 0.85 in light mode and 0.9 in dark, or the theme's <c>--ib-chart-fill-opacity</c>.
    /// </summary>
    [Parameter] public double? Opacity { get; set; }

    private protected override ChartType Type => ChartType.Bar;

    private protected override double? OpacityOf => Opacity;
}

/// <summary>
/// A band filled from each item's low value to its high one, such as a forecast's interval or a
/// city's daily spread of temperatures over a year. Draw the measure inside it (the forecast, the
/// average) as a <see cref="ChartLineSeries{TItem}"/> on top, and say in the band's <c>Name</c>
/// what it spans, such as "80% interval".
/// </summary>
/// <typeparam name="TItem">The chart's item type.</typeparam>
/// <example>
/// <code>
/// &lt;Chart Items="forecast" Argument="f =&gt; f.Month" Title="Revenue forecast, 2027"&gt;
///     &lt;ChartValueAxis Format="C0" /&gt;
///     &lt;ChartRangeAreaSeries Low="f =&gt; f.Low" High="f =&gt; f.High" Name="80% interval" /&gt;
///     &lt;ChartLineSeries Value="f =&gt; f.Expected" Name="Expected" /&gt;
/// &lt;/Chart&gt;
/// </code>
/// </example>
[ModeSupport]
public sealed class ChartRangeAreaSeries<TItem> : ChartRangeSeriesBase<TItem>
{
    /// <summary>How the band's edges are drawn. Defaults to the next of solid, dashed, dotted and dash-dot.</summary>
    [Parameter] public ChartLineStyle? LineStyle { get; set; }

    /// <summary>The band's shape between values, at both edges. Defaults to straight.</summary>
    [Parameter] public ChartCurve Curve { get; set; }

    /// <summary>Draws the band straight across a missing value, instead of leaving a gap.</summary>
    [Parameter] public bool ConnectGaps { get; set; }

    private protected override ChartType Type => ChartType.Area;

    private protected override ChartCurve CurveOf => Curve;

    private protected override ChartLineStyle? LineStyleOf => LineStyle;

    private protected override bool ConnectGapsOf => ConnectGaps;
}

/// <summary>
/// Prices over time: each item's open, high, low and close, as a candlestick or an OHLC bar. A
/// candle is hollow when the price closed above its open and filled when it closed below, in the
/// chart's increase and decrease colors (blue and red by default, which stay apart for red-green
/// color blindness). Pair it with the argument axis's <c>SkipGaps</c> on a chart without bars, and
/// with a <see cref="ChartLineSeries{TItem}"/> for a moving average. Volume reads best as bars in a
/// chart of its own under this one, joined with <c>Sync</c>. Stands upright only.
/// </summary>
/// <typeparam name="TItem">The chart's item type.</typeparam>
/// <example>
/// <code>
/// &lt;Chart Items="days" Argument="d =&gt; d.Date" Title="ACME, March 2026"&gt;
///     &lt;ChartValueAxis Format="C0" /&gt;
///     &lt;ChartOhlcSeries Open="d =&gt; d.Open"
///                      High="d =&gt; d.High"
///                      Low="d =&gt; d.Low"
///                      Close="d =&gt; d.Close"
///                      Name="ACME" /&gt;
/// &lt;/Chart&gt;
/// </code>
/// </example>
[ModeSupport]
public sealed class ChartOhlcSeries<TItem> : ChartSeriesBase<TItem>
{
    /// <summary>Each item's opening price, such as <c>d =&gt; d.Open</c>. Without it, only <c>Look="ChartOhlcLook.Bars"</c> applies: high, low and the close's tick.</summary>
    [Parameter] public Expression<Func<TItem, object?>>? Open { get; set; }

    /// <summary>Each item's highest price, such as <c>d =&gt; d.High</c>.</summary>
    [Parameter, EditorRequired] public Expression<Func<TItem, object?>> High { get; set; } = default!;

    /// <summary>Each item's lowest price, such as <c>d =&gt; d.Low</c>.</summary>
    [Parameter, EditorRequired] public Expression<Func<TItem, object?>> Low { get; set; } = default!;

    /// <summary>Each item's closing price, such as <c>d =&gt; d.Close</c>. An item missing any price is left out.</summary>
    [Parameter, EditorRequired] public Expression<Func<TItem, object?>> Close { get; set; } = default!;

    /// <summary>Candlesticks (the default) or OHLC bars, which suit many items.</summary>
    [Parameter] public ChartOhlcLook Look { get; set; }

    /// <summary>
    /// The CSS color of items that closed up, such as <c>var(--ib-color-success)</c>. Defaults to
    /// the palette's blue (<c>--ib-chart-increase</c>, or <c>--ib-chart-1</c>), as a waterfall's rises.
    /// </summary>
    [Parameter] public string? IncreaseColor { get; set; }

    /// <summary>The CSS color of items that closed down. Defaults to the palette's red (<c>--ib-chart-decrease</c>, or <c>--ib-chart-8</c>).</summary>
    [Parameter] public string? DecreaseColor { get; set; }

    private protected override ChartType Type => ChartType.Bar;

    private protected override Expression<Func<TItem, object?>>? ValueOf => High;

    private protected override Expression<Func<TItem, object?>>? LowOf => Low;

    private protected override bool IsRange => true;

    private protected override Expression<Func<TItem, object?>>? OpenOf => Open;

    private protected override Expression<Func<TItem, object?>>? CloseOf => Close;

    private protected override ChartOhlcLook OhlcLookOf => Look;

    private protected override string? IncreaseColorOf => IncreaseColor;

    private protected override string? DecreaseColorOf => DecreaseColor;

    private protected override string ValueNeeded => "A ChartOhlcSeries needs a High, a Low and a Close, such as d => d.High.";

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Close is null)
        {
            throw new InvalidOperationException(ValueNeeded);
        }

        if (Open is null && Look == ChartOhlcLook.Candlestick)
        {
            throw new InvalidOperationException("A candlestick needs an Open, such as d => d.Open: without one, use Look=\"ChartOhlcLook.Bars\" for high, low and close.");
        }

        if (Trend != ChartTrend.None)
        {
            throw new InvalidOperationException("A ChartOhlcSeries takes no Trend: draw a moving average of the close as a ChartLineSeries with Trend=\"ChartTrend.MovingAverage\".");
        }

        if (SeriesBy is not null)
        {
            throw new InvalidOperationException("A ChartOhlcSeries can't be split with SeriesBy: give each instrument a series, or a chart, of its own.");
        }

        if (Labels != ChartLabels.None || LabelText is not null)
        {
            throw new InvalidOperationException("A ChartOhlcSeries takes no Labels: its four prices are in each item's tooltip.");
        }

        base.OnParametersSet();
    }
}

/// <summary>What a <see cref="Chart{TItem}"/> shares with its series: the series, in order.</summary>
internal sealed class ChartContext<TItem> : ChartContextBase
{
    private readonly ChildOrder<ChartSeriesBase<TItem>> _series = new();
    private readonly ChildOrder<ChartDrillLevel<TItem>> _levels = new();

    public IReadOnlyList<ChartSeriesBase<TItem>> Series => _series.Items;

    /// <summary>The chart's drill-down levels, from the top.</summary>
    public IReadOnlyList<ChartDrillLevel<TItem>> Levels => _levels.Items;

    /// <summary>The <see cref="ChartTooltip{TItem}"/>'s content, if the chart has one.</summary>
    public RenderFragment<ChartTooltipContext<TItem>>? TooltipTemplate { get; set; }

    public override void BeginPass()
    {
        base.BeginPass();
        _series.BeginPass();
        _levels.BeginPass();
    }

    public void Register(ChartDrillLevel<TItem> level) => _levels.Add(level);

    public void Seen(ChartDrillLevel<TItem> level)
    {
        _levels.Seen(level);
        Plot?.Refresh();
    }

    public void Unregister(ChartDrillLevel<TItem> level)
    {
        if (_levels.Remove(level))
        {
            Plot?.Refresh();
        }
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

    /// <summary>
    /// A share as a percent: whole, with a tenth under 9.5%, and "&lt;0.1%" for less, so a small
    /// share doesn't read as 0%.
    /// </summary>
    public static string Share(double share) => share switch
    {
        > 0 and < 0.0005 => "<" + 0.001.ToString("P1", CultureInfo.CurrentCulture),
        > 0 and < 0.095 => share.ToString("P1", CultureInfo.CurrentCulture),
        _ => share.ToString("P0", CultureInfo.CurrentCulture),
    };
}
