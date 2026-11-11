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
/// One box of a box plot, worked out from its values: the whiskers' ends (<c>Low</c>, <c>High</c>),
/// the quartiles and median, the mean, how many values, the outliers past the whiskers, and the
/// smallest and largest values of all.
/// </summary>
internal sealed record ChartBox(double Low, double Q1, double Median, double Q3, double High, double Mean, int Count, double[] Outliers)
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
/// Draws a box for each <c>Argument</c> from the <see cref="Value"/>s of the items that share it,
/// to compare how values spread across groups, such as delivery times by carrier. Each box shows
/// the quartiles and median, with whiskers and outliers by <see cref="Whiskers"/>. <c>SeriesBy</c>
/// puts a box per group side by side; the chart's <c>Orientation="Orientation.Horizontal"</c> lays
/// them across. A box plot's chart holds only box plot series.
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
public sealed class ChartBoxPlotSeries<TItem> : ChartSeriesBase<TItem>
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

    private protected override ChartType Type => ChartType.Bar;

    private protected override Expression<Func<TItem, object?>>? ValueOf => Value;

    private protected override ChartWhiskers? WhiskersOf => Whiskers;

    private protected override bool ShowMeanOf => ShowMean;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Trend != ChartTrend.None)
        {
            throw new InvalidOperationException("A ChartBoxPlotSeries takes no Trend: its boxes are groups of values, not a run of them.");
        }

        if (Labels != ChartLabels.None || LabelText is not null)
        {
            throw new InvalidOperationException("A ChartBoxPlotSeries takes no Labels: each box's median, quartiles and whiskers are in its tooltip.");
        }

        base.OnParametersSet();
    }
}
