using System.Globalization;
using System.Text;

namespace IzzyBlazor;

/// <summary>A pie or donut slice: where it starts and ends as shares of the turn, and its path.</summary>
internal sealed record PieSlice(int Index, double Start, double End, string Path)
{
    /// <summary>The slice's middle, as a share of the turn.</summary>
    public double Middle => (Start + End) / 2;
}

/// <summary>
/// A slice's label beside a pie: on the end side (the right, for slices whose middle is on the
/// right half) or the start side; where it starts (X) and its middle (Y), in shares of the circle's
/// box; and its leader line, from the slice's edge out to the label, in the pie's viewBox.
/// </summary>
internal sealed record PieLabel(int Index, bool End, double X, double Y, string Leader);

/// <summary>
/// A FunnelChart stage in a 100 by 100 box: its outline, the height of its middle (Y, top down),
/// how far right its edge reaches there (X), where its label's line starts, and how tall it is.
/// Its width at the middle is <c>(X - 50) * 2</c>. None for a stage with no value.
/// </summary>
internal sealed record FunnelStage(int Index, string Points, double X, double Y, double Height)
{
    public double MiddleWidth => (X - 50) * 2;
}

/// <summary>The geometry of the round charts and sparklines, as SVG paths. Pure, so tested alone.</summary>
internal static class ChartShapes
{
    /// <summary>A donut's hole, as a share of its radius.</summary>
    public const double DonutHole = 0.62;

    // + 0.0 turns -0 into 0.
    private static string N(double value) => (Math.Round(value, 4) + 0.0).ToString("0.####", CultureInfo.InvariantCulture);

    // A point on a circle of radius r centered on 0,0 at a share of the turn, clockwise from the top.
    private static (double X, double Y) At(double turn, double r) =>
        (r * Math.Sin(turn * 2 * Math.PI), -r * Math.Cos(turn * 2 * Math.PI));

    /// <summary>
    /// The slices for the values, clockwise from <paramref name="from"/> (a share of the turn, 0 at
    /// the top), in a viewBox of -1 -1 2 2. A slice's Start and End can pass 1 when it crosses the
    /// top. Missing, zero and negative values get no slice; a single slice is a whole circle (or ring).
    /// <paramref name="outer"/> is the slices' outer radius (under 1 for a nested donut's inner rings).
    /// </summary>
    public static List<PieSlice> Pie(IReadOnlyList<double?> values, double hole, double from = 0, double outer = 1)
    {
        var total = values.Sum(v => v is > 0 ? v.Value : 0);
        var slices = new List<PieSlice>();
        if (total <= 0) return slices;

        var start = from;
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] is not > 0) continue;
            var end = Math.Min(from + 1, start + values[i]!.Value / total);
            slices.Add(new PieSlice(i, start, end, Slice(start, end, hole, outer)));
            start = end;
        }

        return slices;
    }

    private static string Slice(double start, double end, double hole, double outer)
    {
        var sb = new StringBuilder();
        var o = N(outer);
        if (end - start >= 0.99999)
        {
            // A whole circle: two half arcs, and the hole the other way round so it stays empty.
            sb.Append($"M0,-{o}A{o},{o} 0 1,1 0,{o}A{o},{o} 0 1,1 0,-{o}Z");
            if (hole > 0)
            {
                sb.Append($"M0,{N(-hole)}A{N(hole)},{N(hole)} 0 1,0 0,{N(hole)}A{N(hole)},{N(hole)} 0 1,0 0,{N(-hole)}Z");
            }

            return sb.ToString();
        }

        var large = end - start > 0.5 ? 1 : 0;
        var (x0, y0) = At(start, outer);
        var (x1, y1) = At(end, outer);
        sb.Append($"M{N(x0)},{N(y0)}A{o},{o} 0 {large},1 {N(x1)},{N(y1)}");
        if (hole > 0)
        {
            var (ix1, iy1) = At(end, hole);
            var (ix0, iy0) = At(start, hole);
            sb.Append($"L{N(ix1)},{N(iy1)}A{N(hole)},{N(hole)} 0 {large},0 {N(ix0)},{N(iy0)}Z");
        }
        else
        {
            sb.Append("L0,0Z");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Labels for the slices just outside the circle, each by its slice, as DevExtreme's and
    /// Highcharts' outside labels: level with a point a little out from the slice's middle, nudged
    /// apart a line each on its side (<see cref="ChartLayout.Spread"/>) over a circle
    /// <paramref name="sizePx"/> across, at the distance from the middle a circle 1.15 times the
    /// pie's has at that height (and no nearer than the line's bend). The leader runs from the
    /// slice's edge out to the bend, then to the label. X and Y are where the label starts, in
    /// shares of the circle's box (past 0 and 1 outside it). In item order.
    /// </summary>
    public static List<PieLabel> PieLabels(IReadOnlyList<PieSlice> slices, double sizePx, double linePx)
    {
        // The labels spread over the circle's height and a little above and below it.
        const double reach = 1.3;
        var span = sizePx * reach;
        var labels = new List<PieLabel>();
        foreach (var end in new[] { true, false })
        {
            var side = slices.Where(s => s.Middle % 1 < 0.5 == end).ToList();
            var centers = ChartLayout.Spread(side.Select(s => (At(s.Middle, 1.12).Y + reach) / (2 * reach) * span).ToList(), span, linePx);
            for (var k = 0; k < side.Count; k++)
            {
                var (ex, ey) = At(side[k].Middle, 1);
                var (bx, by) = At(side[k].Middle, 1.08);
                var y = centers[k] / span * 2 * reach - reach;
                var x = Math.Max(Math.Sqrt(Math.Max(0, 1.15 * 1.15 - y * y)), Math.Abs(bx)) * (end ? 1 : -1);
                var tip = x + (end ? 0.06 : -0.06);
                labels.Add(new PieLabel(side[k].Index, end, (tip + 1) / 2, (y + 1) / 2,
                    $"M{N(ex)},{N(ey)}L{N(bx)},{N(by)}L{N(x)},{N(y)}H{N(tip)}"));
            }
        }

        return [.. labels.OrderBy(l => l.Index)];
    }

    /// <summary>Where a share goes inside its slice: its middle, halfway across the ring (or 62% out on a pie), 0 to 1 of the box.</summary>
    public static (double X, double Y) PieInside(PieSlice slice, double hole)
    {
        var (x, y) = At(slice.Middle, hole > 0 ? (1 + hole) / 2 : 0.62);
        return ((x + 1) / 2, (y + 1) / 2);
    }

    /// <summary>A gauge's sweep, in turns: 240°, with the gap at the bottom.</summary>
    public const double GaugeSweep = 2.0 / 3;

    /// <summary>
    /// An arc along a gauge's track from <paramref name="from"/> to <paramref name="to"/> (shares of
    /// the track, 0 to 1), centered on 0,0 with radius <paramref name="r"/>, as a stroke's path.
    /// </summary>
    public static string GaugeArc(double from, double to, double r)
    {
        from = Math.Clamp(from, 0, 1);
        to = Math.Clamp(to, 0, 1);
        var a0 = -GaugeSweep / 2 + from * GaugeSweep;
        var a1 = -GaugeSweep / 2 + to * GaugeSweep;
        var (x0, y0) = At(a0, r);
        var (x1, y1) = At(a1, r);
        var large = a1 - a0 > 0.5 ? 1 : 0;
        return $"M{N(x0)},{N(y0)}A{N(r)},{N(r)} 0 {large},1 {N(x1)},{N(y1)}";
    }

    /// <summary>A tick across a gauge's track at <paramref name="at"/> (a share of the track), from radius <paramref name="r0"/> out to <paramref name="r1"/>.</summary>
    public static string GaugeTick(double at, double r0, double r1)
    {
        var a = -GaugeSweep / 2 + Math.Clamp(at, 0, 1) * GaugeSweep;
        var (x0, y0) = At(a, r0);
        var (x1, y1) = At(a, r1);
        return $"M{N(x0)},{N(y0)}L{N(x1)},{N(y1)}";
    }

    /// <summary>
    /// A sparkline's value range: its values' (with zero, for bars), or <paramref name="min"/> and
    /// <paramref name="max"/> where given, so sparklines in a column can share one.
    /// </summary>
    public static (double Min, double Max) SparkRange(IReadOnlyList<double?> values, bool includeZero, double? min, double? max)
    {
        var (lo, hi) = Range(values, includeZero);
        (lo, hi) = (min ?? lo, max ?? hi);
        return lo < hi ? (lo, hi) : Range(values, includeZero);
    }

    // A value's height in the box, 0 at the top and 1000 at the bottom, held inside it.
    private static double SparkY(double v, double min, double max) => Math.Clamp(1000 - (v - min) / (max - min) * 1000, 0, 1000);

    /// <summary>
    /// A sparkline's line (and, for an area, its fill down to the bottom), in a 1000 by 1000 box
    /// that stretches, and each value's spot in percent of the box, for dots. Missing values break
    /// the line.
    /// </summary>
    public static (string Line, string? Area, Dictionary<int, (double X, double Y)> Spots) Spark(
        IReadOnlyList<double?> values, bool area, (double Min, double Max) range, ChartCurve curve)
    {
        var n = values.Count;
        var line = new StringBuilder();
        var fill = new StringBuilder();
        var spots = new Dictionary<int, (double X, double Y)>();
        var run = new List<(double A, double V)>();

        void Flush()
        {
            if (run.Count == 0) return;
            var (first, segments) = ChartLayout.Curve(run, curve);
            var path = new StringBuilder($"M{N(first.A)},{N(first.V)}");
            foreach (var g in segments)
            {
                path.Append(g is { C1: { } c1, C2: { } c2 }
                    ? $"C{N(c1.A)},{N(c1.V)} {N(c2.A)},{N(c2.V)} {N(g.To.A)},{N(g.To.V)}"
                    : $"L{N(g.To.A)},{N(g.To.V)}");
            }

            if (run.Count == 1) path.Append("h0");
            line.Append(path);
            if (area)
            {
                fill.Append(path).Append($"L{N(run[^1].A)},1000L{N(run[0].A)},1000Z");
            }

            run.Clear();
        }

        for (var i = 0; i < n; i++)
        {
            if (values[i] is not { } v)
            {
                Flush();
                continue;
            }

            var x = n == 1 ? 500 : i * 1000.0 / (n - 1);
            var y = SparkY(v, range.Min, range.Max);
            run.Add((x, y));
            spots[i] = (x / 10, y / 10);
        }

        Flush();
        return (line.ToString(), area ? fill.ToString() : null, spots);
    }

    /// <summary>A sparkline's bars, in percent of its box: x, y, width and height, with 0 as the base (or the range's nearer end).</summary>
    public static List<(int Index, double X, double Y, double Width, double Height)> SparkBars(IReadOnlyList<double?> values, (double Min, double Max) range)
    {
        var bars = new List<(int, double, double, double, double)>();
        var n = values.Count;
        var band = 100.0 / n;
        var zero = SparkY(0, range.Min, range.Max) / 10;
        for (var i = 0; i < n; i++)
        {
            if (values[i] is not { } v) continue;
            var y = SparkY(v, range.Min, range.Max) / 10;
            bars.Add((i, i * band + band * 0.15, Math.Min(y, zero), band * 0.7, Math.Abs(zero - y)));
        }

        return bars;
    }

    /// <summary>
    /// A win-loss sparkline's bars, in percent of its box: the top half for a value over the
    /// threshold (1), the bottom half under it (-1), a tick across the middle at it (0).
    /// </summary>
    public static List<(int Index, int Sign, double X, double Y, double Width, double Height)> SparkWinLoss(IReadOnlyList<double?> values, double threshold)
    {
        var bars = new List<(int, int, double, double, double, double)>();
        var n = values.Count;
        var band = 100.0 / n;
        for (var i = 0; i < n; i++)
        {
            if (values[i] is not { } v) continue;
            var sign = Math.Sign(v - threshold);
            var (y, h) = sign switch { > 0 => (0.0, 46.0), < 0 => (54.0, 46.0), _ => (47.0, 6.0) };
            bars.Add((i, sign, i * band + band * 0.15, y, band * 0.7, h));
        }

        return bars;
    }

    /// <summary>The indexes a sparkline marks: its first and last present values, and its lowest and highest (the first of a tie).</summary>
    public static HashSet<int> SparkMarked(IReadOnlyList<double?> values, SparklinePoints points)
    {
        var marked = new HashSet<int>();
        int? first = null, last = null, low = null, high = null;
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] is not { } v) continue;
            first ??= i;
            last = i;
            if (low is null || v < values[low.Value]) low = i;
            if (high is null || v > values[high.Value]) high = i;
        }

        if (points.HasFlag(SparklinePoints.First) && first is { } f) marked.Add(f);
        if (points.HasFlag(SparklinePoints.Last) && last is { } l) marked.Add(l);
        if (points.HasFlag(SparklinePoints.Low) && low is { } lo) marked.Add(lo);
        if (points.HasFlag(SparklinePoints.High) && high is { } hi) marked.Add(hi);
        return marked;
    }

    private static (double Min, double Max) Range(IReadOnlyList<double?> values, bool includeZero)
    {
        var present = values.Where(v => v is not null).Select(v => v!.Value).ToList();
        if (includeZero) present.Add(0);
        if (present.Count == 0) return (0, 1);
        var (min, max) = (present.Min(), present.Max());
        // All equal: a flat line through the middle.
        return min == max ? (min - 1, max + 1) : (min, max);
    }

    /// <summary>
    /// A funnel's stages, as DevExtreme's (its "dynamicSlope"): each as tall as the others, its top
    /// as wide as its value (the largest's the full width), sloping to the next stage's width; the
    /// last, and a stage before one with no value, is as wide at the bottom as at the top. A pyramid's: a triangle on its base, the first
    /// item at the bottom, each level as tall as its share of the total.
    /// </summary>
    public static List<FunnelStage> Funnel(IReadOnlyList<double?> values, bool pyramid)
    {
        var stages = new List<FunnelStage>();
        var n = values.Count;
        string Points(double top, double wTop, double bottom, double wBottom) =>
            $"{N(50 - wTop / 2)},{N(top)} {N(50 + wTop / 2)},{N(top)} {N(50 + wBottom / 2)},{N(bottom)} {N(50 - wBottom / 2)},{N(bottom)}";

        if (pyramid)
        {
            var total = values.Sum(v => v is > 0 ? v.Value : 0);
            var from = 0.0;
            for (var i = 0; i < n; i++)
            {
                if (values[i] is not > 0 || total <= 0) continue;
                var to = from + values[i]!.Value / total * 100;
                var mid = (from + to) / 2;
                stages.Add(new FunnelStage(i, Points(100 - to, 100 - to, 100 - from, 100 - from), 50 + (100 - mid) / 2, 100 - mid, to - from));
                from = to;
            }

            return stages;
        }

        var max = values.Select(v => v is > 0 ? v.Value : 0).DefaultIfEmpty(0).Max();
        double Width(int i) => i < n && values[i] is > 0 && max > 0 ? values[i]!.Value / max * 100 : 0;
        for (var i = 0; i < n; i++)
        {
            if (values[i] is not > 0) continue;
            var (top, bottom) = (i * 100.0 / n, (i + 1) * 100.0 / n);
            // The last stage, and one before a stage with no value, keep their width to the bottom:
            // narrowing to nothing would show a drop the data doesn't have.
            var below = i == n - 1 || Width(i + 1) == 0 ? Width(i) : Width(i + 1);
            stages.Add(new FunnelStage(i, Points(top, Width(i), bottom, below), 50 + (Width(i) + below) / 4, (top + bottom) / 2, bottom - top));
        }

        return stages;
    }
}
