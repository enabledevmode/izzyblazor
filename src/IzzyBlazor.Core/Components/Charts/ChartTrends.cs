namespace IzzyBlazor;

/// <summary>A trend drawn through a <see cref="ChartSeries{TItem}"/>'s values.</summary>
public enum ChartTrend
{
    /// <summary>No trend.</summary>
    None,

    /// <summary>The straight line that fits the values best (least squares).</summary>
    Linear,

    /// <summary>A curve that fits the values best, of <c>TrendOrder</c> (2, a parabola, by default).</summary>
    Polynomial,

    /// <summary>Each value the average of the last <c>TrendPeriod</c> values (3 by default), which smooths out the ups and downs.</summary>
    MovingAverage,
}

/// <summary>
/// Trend series, worked out on the server: each series with a <see cref="ChartTrend"/> gets a
/// dashed line after it in its own color, named "Revenue trend", in the legend, tooltips and data
/// table like any series.
/// </summary>
internal static class ChartTrends
{
    public static List<ChartSeriesData> Add(List<ChartSeriesData> series, IReadOnlyList<object?> arguments, IzzyText text)
    {
        if (series.All(s => s.Trend == ChartTrend.None)) return series;

        // Where each item is along the axis: its index in bands, its number or date on a scale.
        var kind = series.Any(s => s.Type == ChartType.Bar) ? ArgumentKind.Category : ChartLayout.KindOf(arguments);
        var x = arguments.Select((a, i) => kind == ArgumentKind.Category ? i : ChartLayout.ToDouble(a, kind)).ToArray();

        var result = new List<ChartSeriesData>();
        for (var s = 0; s < series.Count; s++)
        {
            var data = series[s];
            result.Add(data);
            if (data.Trend == ChartTrend.None) continue;
            var source = result.Count - 1;
            var values = data.Trend == ChartTrend.MovingAverage
                ? MovingAverage(data.Values, data.TrendPeriod)
                : Fit(x, data.Values, data.Trend == ChartTrend.Linear ? 1 : data.TrendOrder);
            result.Add(new ChartSeriesData(string.Format(System.Globalization.CultureInfo.CurrentCulture, text.ChartTrendName, data.Name), ChartType.Line, values)
            {
                Color = data.Color,
                ColorFrom = source,
                Curve = data.Trend == ChartTrend.Linear ? ChartCurve.Straight : ChartCurve.Smooth,
                LineStyle = ChartLineStyle.Dashed,
                Markers = false,
                ConnectNulls = true,
                Hidden = data.Hidden,
            });
        }

        return result;
    }

    /// <summary>The average of each item's window of <paramref name="period"/> items, from when the window is full.</summary>
    internal static double?[] MovingAverage(double?[] values, int period)
    {
        var result = new double?[values.Length];
        for (var i = period - 1; i < values.Length; i++)
        {
            var window = values[(i - period + 1)..(i + 1)].Where(v => v is { } x && double.IsFinite(x)).Select(v => v!.Value).ToArray();
            result[i] = window.Length > 0 ? window.Average() : null;
        }

        return result;
    }

    /// <summary>
    /// The least-squares polynomial of <paramref name="order"/> through the values, at every item.
    /// x is scaled to 0 to 1 first, so higher orders stay well conditioned.
    /// </summary>
    internal static double?[] Fit(double?[] x, double?[] values, int order)
    {
        var points = x.Zip(values).Where(p => p.First is { } a && double.IsFinite(a) && p.Second is { } b && double.IsFinite(b))
            .Select(p => (X: p.First!.Value, Y: p.Second!.Value)).ToArray();
        var result = new double?[x.Length];
        if (points.Length < 2) return result;
        order = Math.Min(order, points.Length - 1);
        var (lo, hi) = (points.Min(p => p.X), points.Max(p => p.X));
        var span = hi > lo ? hi - lo : 1;

        // Normal equations: (AᵀA) c = Aᵀy, with A's columns 1, x, x², ….
        var m = order + 1;
        var a = new double[m, m + 1];
        foreach (var (px, py) in points)
        {
            var t = (px - lo) / span;
            var powers = new double[2 * m];
            powers[0] = 1;
            for (var k = 1; k < powers.Length; k++) powers[k] = powers[k - 1] * t;
            for (var r = 0; r < m; r++)
            {
                for (var c = 0; c < m; c++) a[r, c] += powers[r + c];
                a[r, m] += powers[r] * py;
            }
        }

        var coefficients = Solve(a, m);
        if (coefficients is null) return result;
        for (var i = 0; i < x.Length; i++)
        {
            if (x[i] is not { } xi || !double.IsFinite(xi)) continue;
            var t = (xi - lo) / span;
            double y = 0, power = 1;
            foreach (var c in coefficients)
            {
                y += c * power;
                power *= t;
            }

            result[i] = y;
        }

        return result;
    }

    // Gaussian elimination with partial pivoting; null when the system has no single answer.
    private static double[]? Solve(double[,] a, int m)
    {
        for (var col = 0; col < m; col++)
        {
            var pivot = col;
            for (var r = col + 1; r < m; r++)
            {
                if (Math.Abs(a[r, col]) > Math.Abs(a[pivot, col])) pivot = r;
            }

            if (Math.Abs(a[pivot, col]) < 1e-12) return null;
            for (var c = 0; c <= m; c++) (a[col, c], a[pivot, c]) = (a[pivot, c], a[col, c]);
            for (var r = 0; r < m; r++)
            {
                if (r == col) continue;
                var f = a[r, col] / a[col, col];
                for (var c = col; c <= m; c++) a[r, c] -= f * a[col, c];
            }
        }

        var result = new double[m];
        for (var i = 0; i < m; i++) result[i] = a[i, m] / a[i, i];
        return result;
    }
}
