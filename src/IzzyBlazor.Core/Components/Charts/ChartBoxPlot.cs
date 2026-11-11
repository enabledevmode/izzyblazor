using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>How far a box plot's whiskers reach from its box.</summary>
public enum ChartWhiskers
{
    /// <summary>
    /// Tukey's rule, the default: to the furthest values within 1.5 times the box's height (the
    /// interquartile range) of its ends. Values past them are outliers, drawn as hollow circles.
    /// </summary>
    Tukey,

    /// <summary>To the smallest and largest values, with no outliers.</summary>
    MinMax,
}

/// <summary>
/// One box of a box plot, worked out from its values or given: the whiskers' ends (<c>Low</c>,
/// <c>High</c>), the quartiles and median, the mean (NaN with none), how many values (null when
/// given, not counted), the outliers past the whiskers, and the smallest and largest values of all.
/// </summary>
internal sealed record ChartBox(double Low, double Q1, double Median, double Q3, double High, double Mean, int? Count, double[] Outliers)
{
    public double Min => Outliers.Length > 0 ? Math.Min(Low, Outliers.Min()) : Low;

    public double Max => Outliers.Length > 0 ? Math.Max(High, Outliers.Max()) : High;

    /// <summary>
    /// The box for some values, or null with none. Quartiles by linear interpolation between the
    /// sorted values (R-7, Excel's QUARTILE.INC, Plotly's and Chart.js's default).
    /// </summary>
    public static ChartBox? Of(IEnumerable<double> values, ChartWhiskers whiskers)
    {
        var v = values.Where(double.IsFinite).Order().ToArray();
        if (v.Length == 0) return null;
        double Quantile(double p)
        {
            var h = (v.Length - 1) * p;
            var k = (int)Math.Floor(h);
            return k + 1 < v.Length ? v[k] + (h - k) * (v[k + 1] - v[k]) : v[k];
        }

        var (q1, median, q3) = (Quantile(0.25), Quantile(0.5), Quantile(0.75));
        var mean = v.Average();
        if (whiskers == ChartWhiskers.MinMax) return new(v[0], q1, median, q3, v[^1], mean, v.Length, []);

        // The fences: 1.5 IQR past the box. The whiskers end at the last values inside them (never
        // inside the box: the value at or under Q1 is always within its fence).
        var fence = 1.5 * (q3 - q1);
        var (lowFence, highFence) = (q1 - fence, q3 + fence);
        var low = v.First(x => x >= lowFence);
        var high = v.Last(x => x <= highFence);
        return new(low, q1, median, q3, high, mean, v.Length, v.Where(x => x < lowFence || x > highFence).ToArray());
    }
}

/// <summary>
/// What <see cref="ChartBoxPlotSeries{TItem}"/> and <see cref="ChartBoxSummarySeries{TItem}"/>
/// share: a box per argument, side by side under <c>SeriesBy</c>, upright or across, with no
/// Trend or Labels.
/// </summary>
/// <typeparam name="TItem">The chart's item type.</typeparam>
public abstract class ChartBoxSeriesBase<TItem> : ChartSeriesBase<TItem>
{
    private protected ChartBoxSeriesBase()
    {
    }

    private protected override ChartType Type => ChartType.Bar;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Trend != ChartTrend.None)
        {
            throw new InvalidOperationException($"A {Kind} takes no Trend: its boxes are groups of values, not a run of them.");
        }

        if (Labels != ChartLabels.None || LabelText is not null)
        {
            throw new InvalidOperationException($"A {Kind} takes no Labels: each box's median, quartiles and whiskers are in its tooltip.");
        }

        base.OnParametersSet();
    }
}

/// <summary>
/// Draws a box for each <c>Argument</c> from the <see cref="Value"/>s of the items that share it,
/// to compare how values spread across groups, such as delivery times by carrier. Each box shows
/// the quartiles and median, with whiskers and outliers by <see cref="Whiskers"/>. <c>SeriesBy</c>
/// puts a box per group side by side; the chart's <c>Orientation="Orientation.Horizontal"</c> lays
/// them across. A box plot's chart holds only box plot series. For numbers already worked out,
/// such as by a database, use <see cref="ChartBoxSummarySeries{TItem}"/>.
/// </summary>
/// <typeparam name="TItem">The chart's item type.</typeparam>
/// <example>
/// <code>
/// &lt;Chart Items="deliveries" Argument="d =&gt; d.Carrier" Title="Delivery time by carrier"&gt;
///     &lt;ChartValueAxis Title="Days" /&gt;
///     &lt;ChartBoxPlotSeries Value="d =&gt; d.Days" Name="Delivery time" /&gt;
/// &lt;/Chart&gt;
/// </code>
/// </example>
[ModeSupport]
public sealed class ChartBoxPlotSeries<TItem> : ChartBoxSeriesBase<TItem>
{
    /// <summary>Each item's value, such as <c>d =&gt; d.Days</c>: a number, or null to leave the item out.</summary>
    [Parameter, EditorRequired] public Expression<Func<TItem, object?>> Value { get; set; } = default!;

    /// <summary>
    /// How far the whiskers reach: <see cref="ChartWhiskers.Tukey"/> (the default), to the furthest
    /// values within 1.5 times the box's height, with outliers past them; or
    /// <see cref="ChartWhiskers.MinMax"/>, to the smallest and largest values.
    /// </summary>
    [Parameter] public ChartWhiskers Whiskers { get; set; }

    /// <summary>Marks each box's mean with a diamond, and says it in the tooltip and data table.</summary>
    [Parameter] public bool ShowMean { get; set; }

    private protected override Expression<Func<TItem, object?>>? ValueOf => Value;

    private protected override ChartWhiskers? WhiskersOf => Whiskers;

    private protected override bool ShowMeanOf => ShowMean;
}

/// <summary>
/// Draws a box plot from numbers already worked out, one item per box: its <see cref="Low"/>,
/// <see cref="Q1"/>, <see cref="Median"/>, <see cref="Q3"/> and <see cref="High"/>, such as a
/// database's percentiles over millions of rows. The whiskers end at <see cref="Low"/> and
/// <see cref="High"/>, whatever they stand for (the minimum and maximum, the 5th and 95th
/// percentiles, Tukey's fences): say which in the chart's title. <see cref="Mean"/> adds a diamond
/// and <see cref="Outliers"/> circles past the whiskers. <c>SeriesBy</c> puts the boxes of items
/// that share an <c>Argument</c> side by side. To work the numbers out from raw values, use
/// <see cref="ChartBoxPlotSeries{TItem}"/>.
/// </summary>
/// <typeparam name="TItem">The chart's item type.</typeparam>
/// <example>
/// <code>
/// &lt;Chart Items="regions" Argument="r =&gt; r.Region" Title="API response time (whiskers: 5th to 95th percentile)"&gt;
///     &lt;ChartValueAxis Format="0 ms" /&gt;
///     &lt;ChartBoxSummarySeries Low="r =&gt; r.P5" Q1="r =&gt; r.P25" Median="r =&gt; r.P50"
///                            Q3="r =&gt; r.P75" High="r =&gt; r.P95" Name="Response time" /&gt;
/// &lt;/Chart&gt;
/// </code>
/// </example>
[ModeSupport]
public sealed class ChartBoxSummarySeries<TItem> : ChartBoxSeriesBase<TItem>
{
    private object?[] _last = [];
    private Func<TItem, object?>[] _numbers = [];
    private Func<TItem, object?>? _mean;
    private Func<TItem, IEnumerable<double>?>? _outliers;

    /// <summary>Where each box's lower whisker ends, such as <c>r =&gt; r.P5</c>.</summary>
    [Parameter, EditorRequired] public Expression<Func<TItem, object?>> Low { get; set; } = default!;

    /// <summary>Each box's first quartile, its lower edge, such as <c>r =&gt; r.P25</c>.</summary>
    [Parameter, EditorRequired] public Expression<Func<TItem, object?>> Q1 { get; set; } = default!;

    /// <summary>Each box's median, the line across it, such as <c>r =&gt; r.P50</c>.</summary>
    [Parameter, EditorRequired] public Expression<Func<TItem, object?>> Median { get; set; } = default!;

    /// <summary>Each box's third quartile, its upper edge, such as <c>r =&gt; r.P75</c>.</summary>
    [Parameter, EditorRequired] public Expression<Func<TItem, object?>> Q3 { get; set; } = default!;

    /// <summary>Where each box's upper whisker ends, such as <c>r =&gt; r.P95</c>.</summary>
    [Parameter, EditorRequired] public Expression<Func<TItem, object?>> High { get; set; } = default!;

    /// <summary>Each box's mean, marked with a diamond and said in the tooltip and data table.</summary>
    [Parameter] public Expression<Func<TItem, object?>>? Mean { get; set; }

    /// <summary>Each box's values past its whiskers, drawn as circles, such as <c>r =&gt; r.Outliers</c>.</summary>
    [Parameter] public Expression<Func<TItem, IEnumerable<double>?>>? Outliers { get; set; }

    private protected override Expression<Func<TItem, object?>>? ValueOf => Median;

    private protected override bool ShowMeanOf => Mean is not null;

    private protected override Func<TItem, ChartBox?>? SummaryOf => Summary;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Low is null || Q1 is null || Median is null || Q3 is null || High is null)
        {
            throw new InvalidOperationException("A ChartBoxSummarySeries needs a Low, Q1, Median, Q3 and High, such as r => r.P25 for Q1.");
        }

        object?[] given = [Low, Q1, Median, Q3, High, Mean, Outliers];
        if (!given.SequenceEqual(_last, ReferenceEqualityComparer.Instance))
        {
            _last = given;
            _numbers = [Low.Compile(), Q1.Compile(), Median.Compile(), Q3.Compile(), High.Compile()];
            _mean = Mean?.Compile();
            _outliers = Outliers?.Compile();
        }

        base.OnParametersSet();
    }

    // An item's box: left out when a number it needs is missing.
    private ChartBox? Summary(TItem item)
    {
        var n = new double[5];
        for (var k = 0; k < 5; k++)
        {
            if (ChartMembers.ToNumber(_numbers[k](item)) is not { } x || !double.IsFinite(x)) return null;
            n[k] = x;
        }

        if (!(n[0] <= n[1] && n[1] <= n[2] && n[2] <= n[3] && n[3] <= n[4]))
        {
            throw new InvalidOperationException(FormattableString.Invariant(
                $"A ChartBoxSummarySeries' numbers run Low ≤ Q1 ≤ Median ≤ Q3 ≤ High; an item has {n[0]}, {n[1]}, {n[2]}, {n[3]}, {n[4]}."));
        }

        var mean = _mean is not null && ChartMembers.ToNumber(_mean(item)) is { } m && double.IsFinite(m) ? m : double.NaN;
        return new(n[0], n[1], n[2], n[3], n[4], mean, null, _outliers?.Invoke(item)?.Where(double.IsFinite).ToArray() ?? []);
    }
}
