using System.Globalization;
using System.Text;

namespace IzzyBlazor;

/// <summary>What the argument axis holds: names in bands, numbers, or dates and times.</summary>
internal enum ArgumentKind { Category, Number, Time }

/// <summary>One series as the layout sees it: its values in item order, null where there's none.</summary>
internal sealed record ChartSeriesData(string Name, ChartType Type, double?[] Values)
{
    public string? Color { get; init; }
    public ChartCurve Curve { get; init; }
    public bool? Markers { get; init; }
    public bool ConnectNulls { get; init; }
    public bool Hidden { get; init; }
}

/// <summary>Everything a chart's layout depends on. No widths: the chart fills its container.</summary>
internal sealed record ChartInput(IReadOnlyList<object?> Arguments, IReadOnlyList<ChartSeriesData> Series)
{
    public ChartStack Stacked { get; init; }
    public bool Horizontal { get; init; }
    public double HeightPx { get; init; } = 288;
    public string? ValueFormat { get; init; }
    public string? ArgumentFormat { get; init; }
    public double? ValueMin { get; init; }
    public double? ValueMax { get; init; }
    public CultureInfo Culture { get; init; } = CultureInfo.CurrentCulture;
}

/// <summary>
/// A label on an axis. <c>Pos</c> runs 0 to 1 from the axis's start (left, or top for the
/// arguments of a horizontal chart; bottom for upright values). <c>From</c> is the narrowest
/// container width, as an index into <see cref="ChartLayout.Widths"/>, at which it shows.
/// </summary>
internal sealed record AxisLabel(double Pos, string Text, string? Context = null, int From = 0, string? Title = null);

/// <summary>A grid line across the plot at a value, shown from a container width as its label is.</summary>
internal sealed record GridLine(double Pos, int From, bool Zero);

/// <summary>A bar, in percent of the plot.</summary>
/// <summary>
/// A bar, in percent of the plot. <c>Base</c> is set on a bar whose value end is rounded: the
/// square half at the baseline, drawn over the rounded rectangle's baseline corners.
/// </summary>
internal sealed record BarRect(double X, double Y, double Width, double Height, string Title, BarHalf? Base = null);

/// <summary>A bar's baseline half, in percent of the plot.</summary>
internal sealed record BarHalf(double X, double Y, double Width, double Height);

/// <summary>A marker at a point, in percent of the plot.</summary>
internal sealed record PointMark(double X, double Y, string Title);

/// <summary>A series drawn: bars, or a line and its area in a 1000 by 1000 box that stretches, with markers.</summary>
internal sealed class SeriesShape
{
    public required ChartSeriesData Data { get; init; }
    public required int Slot { get; init; }
    public required int Index { get; init; }
    public int Style { get; init; }
    public List<BarRect> Bars { get; } = [];


    public string? Line { get; set; }
    public string? Area { get; set; }
    public List<PointMark> Marks { get; } = [];
}

internal sealed class ChartModel
{
    public required bool Horizontal { get; init; }
    public required ArgumentKind Kind { get; init; }
    public List<AxisLabel> ArgumentLabels { get; } = [];
    public List<AxisLabel> ValueLabels { get; } = [];
    public List<GridLine> Grid { get; } = [];
    public List<SeriesShape> Series { get; } = [];

    /// <summary>For bands: each width's room for a label, in percent of the plot, by <see cref="ChartLayout.Widths"/>.</summary>
    public double[]? LabelRoom { get; set; }

    /// <summary>Room the last label along the bottom of a scale needs past the plot's end, in pixels.</summary>
    public double EndRoomPx { get; set; }

    /// <summary>Whether the argument labels have a second line (a month under days, a year under months).</summary>
    public bool TwoLine { get; set; }

    /// <summary>Each item's place along the argument axis, 0 to 1 (NaN where it has none).</summary>
    public double[] ArgumentPositions { get; set; } = [];

    public required string[] ArgumentTexts { get; init; }
    public required string?[][] ValueTexts { get; init; }
}

/// <summary>
/// Lays a chart out with no width known. The plot stretches to its container, and the labels are
/// HTML placed by percent, so they keep their size. Labels that would crowd at a narrow width are
/// marked to hide below it (container queries in chart.css): each gets the narrowest of
/// <see cref="Widths"/> at which it fits, estimated from Inter's character widths. Coarser labels
/// show at narrow widths and are always among the finer ones, so the axis thins out evenly.
/// </summary>
internal static class ChartLayout
{
    /// <summary>The container widths the labels are fitted at, in pixels. chart.css has a container query for each but the first.</summary>
    public static readonly int[] Widths = [0, 320, 400, 480, 560, 640, 800, 960];

    private const double FontPx = 12, LineHeightPx = 16, LabelGapPx = 8, GapPx = 8;

    // The width each breakpoint is fitted at: its own, or 280 for anything under 320.
    private static double FitWidth(int k) => k == 0 ? 280 : Widths[k];

    public static ChartModel Build(ChartInput input)
    {
        var n = input.Arguments.Count;
        var hasBars = input.Series.Any(s => s.Type == ChartType.Bar);
        var kind = hasBars ? ArgumentKind.Category : KindOf(input.Arguments);
        var culture = input.Culture;
        var full = input.Stacked == ChartStack.Full;
        var stacked = input.Stacked != ChartStack.None;

        var model = new ChartModel
        {
            Horizontal = input.Horizontal,
            Kind = kind,
            ArgumentTexts = input.Arguments.Select(a => ArgumentText(a, input.ArgumentFormat, culture)).ToArray(),
            ValueTexts = input.Series.Select(s => s.Values.Select(v => v is { } x ? x.ToString(input.ValueFormat ?? "#,##0.##", culture) : null).ToArray()).ToArray(),
        };

        // Stacks: bars with bars, areas with areas. Each point's base and top, before scaling.
        var lows = new double?[input.Series.Count][];
        var highs = new double?[input.Series.Count][];
        StackValues(input, stacked, full, lows, highs);

        // The value domain.
        var min = double.PositiveInfinity;
        var max = double.NegativeInfinity;
        for (var s = 0; s < input.Series.Count; s++)
        {
            for (var i = 0; i < n; i++)
            {
                if (highs[s][i] is { } h) { min = Math.Min(min, h); max = Math.Max(max, h); }
                if (lows[s][i] is { } l) { min = Math.Min(min, l); max = Math.Max(max, l); }
            }
        }

        if (input.Series.Any(s => s.Type != ChartType.Line))
        {
            min = Math.Min(min, 0);
            max = Math.Max(max, 0);
        }

        if (!double.IsFinite(min)) { (min, max) = (0, 1); }
        if (input.ValueMin is { } vmin) min = vmin;
        if (input.ValueMax is { } vmax) max = vmax;

        // Upright values get a tick per 56px of height; values across are fitted by width.
        var plotHeight = Math.Max(80, input.HeightPx);
        var argumentRoomPx = EstimateValueColumn(input, model, kind);
        double dmin, dmax;
        if (!input.Horizontal)
        {
            var count = Math.Max(2, (int)Math.Round(plotHeight / 56));
            (dmin, dmax) = input.ValueMin is not null && input.ValueMax is not null ? (min, max) : Nice(min, max, count, input);
            var spec = ChartScale.TickSpec(dmin, dmax, count);
            var format = full ? "P0" : input.ValueFormat ?? "N" + ChartScale.Decimals(spec.Step);
            for (var i = spec.I1; i <= spec.I2; i++)
            {
                var v = spec.Value(i);
                var pos = (v - dmin) / (dmax - dmin);
                model.ValueLabels.Add(new AxisLabel(pos, v.ToString(format, culture)));
                model.Grid.Add(new GridLine(pos, 0, v == 0));
            }
        }
        else
        {
            var count = Math.Max(2, PlotWidth(Widths.Length - 1, argumentRoomPx) / 80);
            (dmin, dmax) = input.ValueMin is not null && input.ValueMax is not null ? (min, max) : Nice(min, max, (int)count, input);
            var format = full ? "P0" : input.ValueFormat;
            NumberLabels(model.ValueLabels, dmin, dmax, count, format, culture, argumentRoomPx, grid: model.Grid);
        }

        // The argument axis.
        double[] positions;
        if (kind == ArgumentKind.Category)
        {
            positions = Enumerable.Range(0, n).Select(i => (i + 0.5) / Math.Max(1, n)).ToArray();
            if (input.Horizontal)
            {
                // Down the side: one line each, thinned by the plot's height only.
                var stride = 1;
                while (n / stride > 1 && plotHeight / n * stride < LineHeightPx + 2) stride *= 2;
                for (var i = 0; i < n; i += stride)
                {
                    model.ArgumentLabels.Add(new AxisLabel(positions[i], model.ArgumentTexts[i]));
                }
            }
            else
            {
                CategoryLabels(model, positions, argumentRoomPx);
            }
        }
        else
        {
            var values = input.Arguments.Select(a => ToDouble(a, kind)).ToArray();
            var finite = values.Where(v => v is not null).Select(v => v!.Value).ToArray();
            var amin = finite.Length > 0 ? finite.Min() : 0;
            var amax = finite.Length > 0 ? finite.Max() : 1;
            if (amin == amax) { amin -= kind == ArgumentKind.Time ? TimeSpan.TicksPerDay : 1; amax += kind == ArgumentKind.Time ? TimeSpan.TicksPerDay : 1; }
            positions = values.Select(v => v is { } x ? (x - amin) / (amax - amin) : double.NaN).ToArray();
            var count = Math.Max(2, PlotWidth(Widths.Length - 1, argumentRoomPx) / 90);
            if (kind == ArgumentKind.Number)
            {
                NumberLabels(model.ArgumentLabels, amin, amax, count, input.ArgumentFormat, culture, argumentRoomPx, grid: null);
            }
            else
            {
                TimeLabels(model, new DateTime((long)amin), new DateTime((long)amax), count, input.ArgumentFormat, culture, argumentRoomPx);
            }
        }

        model.ArgumentPositions = positions;

        // A scale's last label is centered on the plot's end: leave room for its far half.
        var bottom = input.Horizontal ? model.ValueLabels : kind == ArgumentKind.Category ? null : model.ArgumentLabels;
        if (bottom is { Count: > 0 } && bottom[^1] is { Pos: > 0.9 } last)
        {
            var half = Math.Max(Width(last.Text), last.Context is { } c ? Width(c) : 0) / 2;
            model.EndRoomPx = Math.Ceiling(Math.Max(0, half - (1 - last.Pos) * 200));
        }

        // The series.
        double Scale(double v) => Math.Clamp((v - dmin) / (dmax - dmin), 0, 1);
        var zero = Scale(0);
        var bars = input.Series.Where(s => s.Type == ChartType.Bar).ToList();
        var band = 1.0 / Math.Max(1, n);
        var lineStyle = 0;
        for (var s = 0; s < input.Series.Count; s++)
        {
            var data = input.Series[s];
            var shape = new SeriesShape
            {
                Data = data,
                Slot = s % 8 + 1,
                Index = s,
                Style = data.Type == ChartType.Bar ? 0 : lineStyle++ + s / 8,
            };
            model.Series.Add(shape);

            if (data.Type == ChartType.Bar)
            {
                // Chart.js's proportions: the group takes 80% of its band, each bar 90% of its share.
                var j = bars.IndexOf(data);
                var slot = stacked ? band * 0.8 : band * 0.8 / bars.Count;
                var width = slot * 0.9;
                for (var i = 0; i < n; i++)
                {
                    if (highs[s][i] is not { } top) continue;
                    var a0 = i * band + band * 0.1 + (stacked ? 0 : j * slot) + slot * 0.05;
                    var v0 = Scale(lows[s][i] ?? 0);
                    var v1 = Scale(top);
                    var title = $"{model.ArgumentTexts[i]}, {data.Name}: {model.ValueTexts[s][i]}";
                    var (lo, hi) = (Math.Min(v0, v1), Math.Max(v0, v1));
                    var rect = input.Horizontal
                        ? new BarRect(lo * 100, a0 * 100, (hi - lo) * 100, width * 100, title)
                        : new BarRect(a0 * 100, (1 - hi) * 100, width * 100, (hi - lo) * 100, title);

                    // The value end is rounded on the outermost bar: every bar side by side, the
                    // last segment of a stack each way from zero.
                    var below = v1 < v0;
                    var outermost = !stacked || !bars.Skip(j + 1).Any(other =>
                        other.Values[i] is { } x && double.IsFinite(x) && x != 0 && (x < 0) == ((highs[s][i] ?? 0) < (lows[s][i] ?? 0)));
                    if (outermost && hi > lo)
                    {
                        rect = rect with
                        {
                            Base = (input.Horizontal, below) switch
                            {
                                (false, false) => new BarHalf(rect.X, rect.Y + rect.Height / 2, rect.Width, rect.Height / 2),
                                (false, true) => new BarHalf(rect.X, rect.Y, rect.Width, rect.Height / 2),
                                (true, false) => new BarHalf(rect.X, rect.Y, rect.Width / 2, rect.Height),
                                (true, true) => new BarHalf(rect.X + rect.Width / 2, rect.Y, rect.Width / 2, rect.Height),
                            },
                        };
                    }

                    shape.Bars.Add(rect);
                }

                continue;
            }

            // Lines and areas: runs of points, split at missing values unless they're joined.
            var points = new List<(double A, double V, int I)?>();
            for (var i = 0; i < n; i++)
            {
                if (highs[s][i] is { } v && !double.IsNaN(positions[i]))
                {
                    points.Add((positions[i], Scale(v), i));
                }
                else if (!data.ConnectNulls)
                {
                    points.Add(null);
                }
            }

            var runs = Runs(points);
            var line = new StringBuilder();
            var area = new StringBuilder();
            foreach (var run in runs)
            {
                var upper = Curve(run.Select(p => (p.A, p.V)).ToList(), data.Curve);
                AppendPath(line, upper, input.Horizontal, move: true);
                if (data.Type == ChartType.Area)
                {
                    var lower = run.Select(p => (p.A, lows[s][p.I] is { } l ? Scale(l) : zero)).ToList();
                    AppendPath(area, upper, input.Horizontal, move: true);
                    AppendPath(area, Reverse(Curve(lower, stacked ? data.Curve : ChartCurve.Straight)), input.Horizontal, move: false);
                    area.Append('Z');
                }
            }

            shape.Line = line.Length > 0 ? line.ToString() : null;
            shape.Area = area.Length > 0 ? area.ToString() : null;

            var count = points.Count(p => p is not null);
            if (data.Markers ?? (data.Type == ChartType.Line && count <= 40))
            {
                foreach (var p in points)
                {
                    if (p is not { } point) continue;
                    var (x, y) = Map(point.A, point.V, input.Horizontal);
                    shape.Marks.Add(new PointMark(x * 100, y * 100, $"{model.ArgumentTexts[point.I]}, {data.Name}: {model.ValueTexts[s][point.I]}"));
                }
            }
        }

        return model;
    }

    private static (double, double) Nice(double min, double max, int count, ChartInput input)
    {
        var (a, b) = ChartScale.Nice(min, max, count);
        return (input.ValueMin ?? a, input.ValueMax ?? b);
    }

    private static void StackValues(ChartInput input, bool stacked, bool full, double?[][] lows, double?[][] highs)
    {
        var n = input.Arguments.Count;
        for (var s = 0; s < input.Series.Count; s++)
        {
            lows[s] = new double?[n];
            highs[s] = input.Series[s].Values.Select(v => v is { } x && double.IsFinite(x) ? (double?)x : null).ToArray();
        }

        if (!stacked)
        {
            return;
        }

        foreach (var type in new[] { ChartType.Bar, ChartType.Area })
        {
            var group = Enumerable.Range(0, input.Series.Count).Where(s => input.Series[s].Type == type).ToList();
            for (var i = 0; i < n; i++)
            {
                var total = full ? group.Sum(s => Math.Abs(highs[s][i] ?? 0)) : 1;
                if (total == 0) total = 1;
                double up = 0, down = 0;
                foreach (var s in group)
                {
                    var v = (highs[s][i] ?? 0) / total;
                    var missing = highs[s][i] is null;
                    if (v >= 0)
                    {
                        lows[s][i] = up;
                        up += v;
                        highs[s][i] = missing && type == ChartType.Bar ? null : up;
                    }
                    else
                    {
                        lows[s][i] = down;
                        down += v;
                        highs[s][i] = missing && type == ChartType.Bar ? null : down;
                    }
                }
            }
        }
    }

    // The value labels' column: as wide as its longest label (HTML lays it out), estimated here
    // to know how much width is left for the plot. Labels down the side of a horizontal chart are
    // capped at a third of the width by CSS.
    private static double EstimateValueColumn(ChartInput input, ChartModel model, ArgumentKind kind)
    {
        if (input.Horizontal)
        {
            var longest = model.ArgumentTexts.Select(t => Width(t)).DefaultIfEmpty(0).Max();
            return Math.Min(longest, 320) + GapPx;
        }

        var all = input.Series.SelectMany(s => s.Values).Where(v => v is not null).Select(v => v!.Value).DefaultIfEmpty(0).ToArray();
        var sample = input.Stacked == ChartStack.Full ? "100%" : Math.Max(Math.Abs(all.Max()), Math.Abs(all.Min())).ToString(input.ValueFormat ?? "N0", input.Culture);
        return Width(sample) + (all.Min() < 0 ? Width("-") : 0) + GapPx;
    }

    private static double PlotWidth(int k, double columnPx) => Math.Max(60, FitWidth(k) - columnPx - GapPx * 2);

    private static double Width(string text) => TextWidths.AtSize(text, FontPx);

    // Bands: label i shows at stride 1, 2, 4… when i is a multiple of it.
    private static void CategoryLabels(ChartModel model, double[] positions, double columnPx)
    {
        var n = positions.Length;
        if (n == 0) return;
        var widest = model.ArgumentTexts.Select(t => Math.Min(Width(t), 120)).Max();
        var strides = new int[Widths.Length];
        model.LabelRoom = new double[Widths.Length];
        for (var k = 0; k < Widths.Length; k++)
        {
            var slot = PlotWidth(k, columnPx) / n;
            var stride = 1;
            while (stride < n && slot * stride < widest + LabelGapPx) stride *= 2;
            strides[k] = stride;
            model.LabelRoom[k] = Math.Min(100, stride * 100.0 / n);
        }

        for (var i = 0; i < n; i++)
        {
            var from = Array.FindIndex(strides, s => i % s == 0);
            if (from >= 0)
            {
                var text = model.ArgumentTexts[i];
                model.ArgumentLabels.Add(new AxisLabel(positions[i], text, From: from, Title: text));
            }
        }
    }

    // Numbers: D3 ticks at the widest fit, and coarser multiples of the step at narrower ones.
    private static void NumberLabels(List<AxisLabel> labels, double min, double max, double count, string? format, CultureInfo culture, double columnPx, List<GridLine>? grid)
    {
        var spec = ChartScale.TickSpec(min, max, count);
        format ??= "N" + ChartScale.Decimals(spec.Step);
        var ladder = ChartScale.Ladder(spec.Factor, Math.Max(1, spec.I2 - spec.I1));
        var ticks = new List<(long I, double V, string Text)>();
        for (var i = spec.I1; i <= spec.I2; i++)
        {
            var v = spec.Value(i);
            ticks.Add((i, v, v.ToString(format, culture)));
        }

        if (ticks.Count == 0) return;
        var widest = ticks.Max(t => Width(t.Text));
        var span = max - min;
        var needed = new int[Widths.Length];
        for (var k = 0; k < Widths.Length; k++)
        {
            var level = 0;
            while (level < ladder.Count - 1 && ladder[level] * spec.Step / span * PlotWidth(k, columnPx) < widest + LabelGapPx) level++;
            needed[k] = level;
        }

        foreach (var (i, v, text) in ticks)
        {
            var level = 0;
            while (level < ladder.Count - 1 && i % ladder[level + 1] == 0) level++;
            var from = Array.FindIndex(needed, l => level >= l);
            if (from < 0) continue;
            var pos = (v - min) / span;
            labels.Add(new AxisLabel(pos, text, From: from));
            grid?.Add(new GridLine(pos, from, v == 0));
        }
    }

    private static void TimeLabels(ChartModel model, DateTime min, DateTime max, double count, string? format, CultureInfo culture, double columnPx)
    {
        var b = ChartTime.BaseIndex((max - min).Ticks, count);
        var unit = ChartTime.Ladder[b].Unit;
        var ticks = ChartTime.Ticks(min, max, ChartTime.Ladder[b]);
        if (ticks.Count == 0) return;
        var levels = ticks.Select(t =>
        {
            var level = 0;
            while (b + level + 1 < ChartTime.Ladder.Length && ChartTime.IsOn(t, ChartTime.Ladder[b + level + 1])) level++;
            return level;
        }).ToArray();

        string Text(DateTime t) => format is not null ? t.ToString(format, culture) : unit switch
        {
            ChartTime.Unit.Second => t.ToString("T", culture),
            ChartTime.Unit.Minute or ChartTime.Unit.Hour => t.ToString("t", culture),
            ChartTime.Unit.Day => t.Day.ToString(culture),
            ChartTime.Unit.Month => t.ToString("MMM", culture),
            _ => t.ToString("yyyy", culture),
        };

        // The second line: the month (and year) under days, the date under times, the year under months.
        string? Context(DateTime t, DateTime? previous) => format is not null ? null : unit switch
        {
            ChartTime.Unit.Second or ChartTime.Unit.Minute or ChartTime.Unit.Hour =>
                previous?.Date == t.Date ? null : t.ToString(previous?.Year == t.Year ? "MMM d" : "MMM d, yyyy", culture),
            ChartTime.Unit.Day =>
                previous is { } p && p.Year == t.Year && p.Month == t.Month ? null : t.ToString(previous?.Year == t.Year ? "MMM" : "MMM yyyy", culture),
            ChartTime.Unit.Month => previous?.Year == t.Year ? null : t.ToString("yyyy", culture),
            _ => null,
        };

        var texts = ticks.Select(Text).ToArray();
        var widest = texts.Max(Width) + (unit is ChartTime.Unit.Day ? 8 : 0);
        var span = (double)(max - min).Ticks;
        var needed = new int[Widths.Length];
        for (var k = 0; k < Widths.Length; k++)
        {
            var level = 0;
            while (b + level + 1 < ChartTime.Ladder.Length && ChartTime.Ladder[b + level].ApproxTicks / span * PlotWidth(k, columnPx) < widest + LabelGapPx) level++;
            needed[k] = level;
        }

        for (var i = 0; i < ticks.Count; i++)
        {
            var from = Array.FindIndex(needed, l => levels[i] >= l);
            if (from < 0) continue;

            // Compared with the tick before it at the narrowest width it shows at. Wider, more
            // ticks show between them, and any that starts a month or year says so itself.
            DateTime? previous = null;
            for (var j = i - 1; j >= 0; j--)
            {
                if (levels[j] >= needed[from]) { previous = ticks[j]; break; }
            }

            var context = Context(ticks[i], previous);
            model.TwoLine |= context is not null;
            model.ArgumentLabels.Add(new AxisLabel((ticks[i] - min).Ticks / span, texts[i], context, from));
        }
    }

    private static List<List<(double A, double V, int I)>> Runs(List<(double A, double V, int I)?> points)
    {
        var runs = new List<List<(double A, double V, int I)>>();
        List<(double, double, int)>? run = null;
        foreach (var p in points)
        {
            if (p is { } point)
            {
                if (run is null) runs.Add(run = []);
                run.Add(point);
            }
            else
            {
                run = null;
            }
        }

        return runs;
    }

    /// <summary>A curve: its first point, then segments, each straight (no controls) or a cubic.</summary>
    internal readonly record struct Segment((double A, double V)? C1, (double A, double V)? C2, (double A, double V) To);

    internal static ((double A, double V) Start, List<Segment> Segments) Curve(List<(double A, double V)> p, ChartCurve curve)
    {
        var segments = new List<Segment>();
        if (curve == ChartCurve.Step)
        {
            for (var i = 1; i < p.Count; i++)
            {
                var mid = (p[i - 1].A + p[i].A) / 2;
                segments.Add(new Segment(null, null, (mid, p[i - 1].V)));
                segments.Add(new Segment(null, null, (mid, p[i].V)));
                segments.Add(new Segment(null, null, p[i]));
            }
        }
        else if (curve == ChartCurve.Smooth && p.Count > 2)
        {
            // Monotone cubic (Fritsch–Carlson, as D3's curveMonotoneX): never overshoots a value.
            var t = new double[p.Count];
            for (var i = 1; i < p.Count - 1; i++)
            {
                double h0 = p[i].A - p[i - 1].A, h1 = p[i + 1].A - p[i].A;
                double s0 = h0 == 0 ? 0 : (p[i].V - p[i - 1].V) / h0, s1 = h1 == 0 ? 0 : (p[i + 1].V - p[i].V) / h1;
                var m = (s0 * h1 + s1 * h0) / (h0 + h1);
                t[i] = (Math.Sign(s0) + Math.Sign(s1)) * Math.Min(Math.Min(Math.Abs(s0), Math.Abs(s1)), 0.5 * Math.Abs(m));
            }

            double End(int a, int b, double tb)
            {
                var h = p[b].A - p[a].A;
                return h == 0 ? tb : (3 * (p[b].V - p[a].V) / h - tb) / 2;
            }

            t[0] = End(0, 1, t[1]);
            t[^1] = End(p.Count - 2, p.Count - 1, t[^2]);
            for (var i = 1; i < p.Count; i++)
            {
                var dx = (p[i].A - p[i - 1].A) / 3;
                segments.Add(new Segment((p[i - 1].A + dx, p[i - 1].V + dx * t[i - 1]), (p[i].A - dx, p[i].V - dx * t[i]), p[i]));
            }
        }
        else
        {
            for (var i = 1; i < p.Count; i++) segments.Add(new Segment(null, null, p[i]));
        }

        return (p[0], segments);
    }

    internal static ((double A, double V) Start, List<Segment> Segments) Reverse(((double A, double V) Start, List<Segment> Segments) curve)
    {
        var points = new List<(double A, double V)> { curve.Start };
        points.AddRange(curve.Segments.Select(s => s.To));
        var reversed = new List<Segment>();
        for (var i = curve.Segments.Count - 1; i >= 0; i--)
        {
            var s = curve.Segments[i];
            reversed.Add(new Segment(s.C2, s.C1, points[i]));
        }

        return (points[^1], reversed);
    }

    private static (double X, double Y) Map(double a, double v, bool horizontal) => horizontal ? (v, a) : (a, 1 - v);

    private static void AppendPath(StringBuilder sb, ((double A, double V) Start, List<Segment> Segments) curve, bool horizontal, bool move)
    {
        void Point((double A, double V) p)
        {
            var (x, y) = Map(p.A, p.V, horizontal);
            sb.Append((x * 1000).ToString("0.#", CultureInfo.InvariantCulture)).Append(',').Append((y * 1000).ToString("0.#", CultureInfo.InvariantCulture));
        }

        sb.Append(move ? 'M' : 'L');
        Point(curve.Start);
        foreach (var s in curve.Segments)
        {
            if (s.C1 is { } c1 && s.C2 is { } c2)
            {
                sb.Append('C');
                Point(c1);
                sb.Append(' ');
                Point(c2);
                sb.Append(' ');
            }
            else
            {
                sb.Append('L');
            }

            Point(s.To);
        }
    }

    internal static ArgumentKind KindOf(IReadOnlyList<object?> arguments)
    {
        var first = arguments.FirstOrDefault(a => a is not null);
        return first switch
        {
            DateTime or DateOnly or DateTimeOffset => ArgumentKind.Time,
            byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => ArgumentKind.Number,
            _ => ArgumentKind.Category,
        };
    }

    private static double? ToDouble(object? value, ArgumentKind kind) => value switch
    {
        null => null,
        DateTime d => d.Ticks,
        DateOnly d => d.ToDateTime(TimeOnly.MinValue).Ticks,
        DateTimeOffset d => d.DateTime.Ticks,
        _ when kind == ArgumentKind.Number => Convert.ToDouble(value, CultureInfo.InvariantCulture) is var x && double.IsFinite(x) ? x : null,
        _ => null,
    };

    internal static string ArgumentText(object? value, string? format, CultureInfo culture) => value switch
    {
        null => "",
        DateTime d when format is null => d.TimeOfDay == TimeSpan.Zero ? d.ToString("d", culture) : d.ToString("g", culture),
        DateTimeOffset d when format is null => d.TimeOfDay == TimeSpan.Zero ? d.ToString("d", culture) : d.ToString("g", culture),
        DateOnly d when format is null => d.ToString("d", culture),
        Enum e => DataFields.EnumChoices(e.GetType())?.FirstOrDefault(c => c.Value == e.ToString()).Text ?? e.ToString(),
        IFormattable f => f.ToString(format ?? (value is float or double or decimal ? "#,##0.##" : null), culture),
        _ => value.ToString() ?? "",
    };
}
