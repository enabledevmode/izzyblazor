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
    public ChartLineStyle? LineStyle { get; init; }
    public bool? Markers { get; init; }
    public bool ConnectGaps { get; init; }
    public bool Hidden { get; init; }

    /// <summary>The series' name written past the plot's end, level with its last value.</summary>
    public bool EndLabel { get; init; }
    public ChartLabels Labels { get; init; }
    public string? LabelFormat { get; init; }

    /// <summary>Each item's label text in place of its value (a series' LabelText), or null.</summary>
    public string?[]? LabelTexts { get; init; }
    public ChartLabelOverlap LabelOverlap { get; init; }
    public ChartTrend Trend { get; init; }
    public int TrendOrder { get; init; } = 2;
    public int TrendPeriod { get; init; } = 3;
    public string? TrendColor { get; init; }

    /// <summary>For a trend: the series it's drawn from, whose color it takes.</summary>
    public int? ColorFrom { get; init; }

    /// <summary>On the second value axis, at the end side.</summary>
    public bool Secondary { get; init; }

    /// <summary>The name of the value axis the series asked for (null: the first).</summary>
    public string? ValueAxis { get; init; }

    /// <summary>A bar series' own fill strength, 0 to 1.</summary>
    public double? Opacity { get; init; }

    /// <summary>A bubble series' sizes by item, and what they measure (its property's name).</summary>
    public double?[]? Sizes { get; init; }

    public string? SizeName { get; init; }

    /// <summary>A .NET format for a bubble series' sizes, in the tooltip, data table and size key.</summary>
    public string? SizeFormat { get; init; }

    /// <summary>Shows the size key (a bubble series' ShowSizeKey).</summary>
    public bool ShowSizeKey { get; init; }

    /// <summary>A waterfall's totals by item (null for other series); a total's value is everything before it.</summary>
    public bool[]? Totals { get; init; }

    public string? IncreaseColor { get; init; }

    public string? DecreaseColor { get; init; }

    public bool IsWaterfall => Totals is not null;

    public bool IsPoint => Type is ChartType.Scatter or ChartType.Bubble;
}

/// <summary>Everything a chart's layout depends on. No widths: the chart fills its container.</summary>
internal sealed record ChartInput(IReadOnlyList<object?> Arguments, IReadOnlyList<ChartSeriesData> Series)
{
    public ChartStacking Stacked { get; init; }
    public bool Horizontal { get; init; }
    public double HeightPx { get; init; } = 288;
    public string? ValueFormat { get; init; }
    public string? ArgumentFormat { get; init; }
    public double? ValueMin { get; init; }
    public double? ValueMax { get; init; }
    public double? ValueStep { get; init; }
    public bool ValueLog { get; init; }
    public bool ArgumentLog { get; init; }
    public bool ValueGrid { get; init; } = true;
    public bool ArgumentGrid { get; init; }
    public ChartLabelFit ArgumentLabels { get; init; }
    public Func<double, string>? ValueFormatter { get; init; }
    public Func<object?, string>? ArgumentFormatter { get; init; }
    public bool HideValueAxis { get; init; }
    public bool HideArgumentAxis { get; init; }
    public IReadOnlyList<ChartMarkInput> Marks { get; init; } = [];
    public IzzyText Text { get; init; } = new();
    public string? SecondaryFormat { get; init; }
    public double? SecondaryMin { get; init; }
    public double? SecondaryMax { get; init; }
    public CultureInfo Culture { get; init; } = CultureInfo.CurrentCulture;
}

/// <summary>
/// A label on an axis. <c>Pos</c> runs 0 to 1 from the axis's start (left, or top for the
/// arguments of a horizontal chart; bottom for upright values). <c>From</c> is the narrowest
/// container width, as an index into <see cref="ChartLayout.Widths"/>, at which it shows.
/// </summary>
internal sealed record AxisLabel(double Pos, string Text, string? Context = null, int From = 0, string? Title = null);

/// <summary>
/// A grid line across the plot, shown from a container width as its label is: at a value, or
/// (<c>Argument</c>) at an argument's tick or between two bands.
/// </summary>
internal sealed record GridLine(double Pos, int From, bool Zero, bool Argument = false);

/// <summary>
/// A bar, in percent of the plot. <c>End</c> is set on a bar whose value end is rounded: the side
/// it's on, <c>t</c>, <c>b</c>, <c>l</c> or <c>r</c> (chart.css rounds that side's corners).
/// </summary>
internal sealed record BarRect(double X, double Y, double Width, double Height, string Title, char? End = null)
{
    /// <summary>The item it's for, and which way it grows from its base: t, b, l or r.</summary>
    public int Index { get; init; }

    public char Dir { get; init; } = 't';

    /// <summary>A waterfall bar's kind: "up", "down" or "total" (null for other bars).</summary>
    public string? Change { get; init; }
}

/// <summary>
/// A value written beside its bar or point. Its center is <c>AX</c>/<c>AY</c> (0 to 1 of the plot)
/// moved by <c>DX</c>/<c>DY</c> pixels; it shows from the <c>From</c>th of
/// <see cref="ChartLayout.Widths"/>. <c>Inside</c> is on the bar. <c>Justify</c>, when over 0, is
/// how far its center keeps from the plot's sides, in pixels: a label above or below a point near
/// an edge slides in rather than being left out.
/// </summary>
internal sealed record DataLabel(int Series, string Text, double AX, double DX, double AY, double DY, int From, bool Inside, double Justify = 0)
{
    /// <summary>On a waterfall's bar: its kind ("up", "down" or "total"), for the fill its text color is picked against.</summary>
    public string? Change { get; init; }
}

/// <summary>A series' name past the plot's end: its center, 0 to 1 down the plot, nudged clear of the others.</summary>
internal sealed record LineEndLabel(int Series, string Text, double Pos);

/// <summary>A marker at a point, in percent of the plot; a bubble's has its radius in pixels.</summary>
internal sealed record PointMark(double X, double Y, string Title, int Index, double? R = null);

/// <summary>A series drawn: bars, or a line and its area in a 1000 by 1000 box that stretches, with markers.</summary>
internal sealed class SeriesShape
{
    public required ChartSeriesData Data { get; init; }
    public required int Slot { get; init; }
    public required int Index { get; init; }
    public int Style { get; init; }

    /// <summary>The line's dash: 0 solid, 1 dashed, 2 dotted, 3 dash-dot.</summary>
    public int Dash => Data.LineStyle switch
    {
        ChartLineStyle.Solid => 0,
        ChartLineStyle.Dashed => 1,
        ChartLineStyle.Dotted => 2,
        _ => Style % 4,
    };
    public List<BarRect> Bars { get; } = [];


    public string? Line { get; set; }
    public string? Area { get; set; }

    /// <summary>A waterfall's thin lines from each bar's end across to the next bar, in the 1000 by 1000 box.</summary>
    public string? Connectors { get; set; }
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
    public List<DataLabel> DataLabels { get; } = [];

    /// <summary>
    /// A bubble chart's key to its sizes: what they measure, and reference circles (a round value
    /// near the largest size, and a quarter of it) at the radius a bubble of that size has.
    /// </summary>
    public (string Name, List<(string Text, double R)> Circles)? SizeKey { get; set; }

    /// <summary>The size key's width, estimated, in pixels: the room the title and legend keep clear of it.</summary>
    public double SizeKeyPx { get; set; }

    /// <summary>The series' names past the plot's end (a series' EndLabel), and the room they take, in pixels.</summary>
    public List<LineEndLabel> EndLabels { get; } = [];

    public double EndLabelsPx { get; set; }

    /// <summary>
    /// Room past the plot for labels past their bars' ends, in pixels: beside bars across (X), or
    /// above upright columns (Y). The chart's frame leaves it, so the longest bar's label fits.
    /// </summary>
    public (double X, double Y) OutsideRoom { get; set; }
    public List<MarkShape> Marks { get; } = [];

    /// <summary>Each mark in words, such as "Target: $150,000", for the chart's description.</summary>
    public List<string> MarkTexts { get; } = [];

    /// <summary>The value-axis marks' values, which the value axis always takes in.</summary>
    public List<double> MarkValues { get; } = [];

    /// <summary>Each series' label texts by item (null with no labels), for the script's redraw.</summary>
    public string?[]?[] LabelTexts { get; set; } = [];

    /// <summary>For bands: each width's room for a label, in percent of the plot, by <see cref="ChartLayout.Widths"/>.</summary>
    public double[]? LabelRoom { get; set; }

    /// <summary>Room the last label along the bottom of a scale needs past the plot's end, in pixels.</summary>
    public double EndRoomPx { get; set; }

    /// <summary>
    /// For names turned 45° (<see cref="ChartLabelFit.Rotate"/>): the first of
    /// <see cref="ChartLayout.Widths"/> at which they all fit flat, or its length when they never
    /// do; 0 when they're never turned.
    /// </summary>
    public int FlatFrom { get; set; }

    /// <summary>The height turned names need, in pixels.</summary>
    public double TurnedPx { get; set; }

    /// <summary>How far the first turned name reaches past the plot's start, in pixels.</summary>
    public double LeadPx { get; set; }

    /// <summary>The value axis's ends, as drawn.</summary>
    public (double Min, double Max) ValueRange { get; set; }

    /// <summary>The second value axis's labels (none without one), and its ends.</summary>
    public List<AxisLabel> SecondaryLabels { get; } = [];

    public (double Min, double Max) SecondaryRange { get; set; } = (0, 1);

    /// <summary>Whether every name shows, wrapped in its band (<see cref="ChartLabelFit.Wrap"/>).</summary>
    public bool Wrap { get; set; }

    /// <summary>Whether the argument labels have a second line (a month under days, a year under months).</summary>
    public bool TwoLine { get; set; }

    /// <summary>Each item's place along the argument axis, 0 to 1 (NaN where it has none).</summary>
    public double[] ArgumentPositions { get; set; } = [];

    public required string[] ArgumentTexts { get; init; }
    public required string?[][] ValueTexts { get; init; }

    /// <summary>A bubble series' sizes as text, by item (null for other series).</summary>
    public string?[]?[] SizeTexts { get; init; } = [];
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

    private const double FontPx = 12, LineHeightPx = 16, LabelGapPx = 8, GapPx = 8, TurnedPitchPx = 24, TurnedMaxPx = 128;

    // The width each breakpoint is fitted at: its own, or 280 for anything under 320.
    private static double FitWidth(int k) => k == 0 ? 280 : Widths[k];

    public static ChartModel Build(ChartInput input)
    {
        var n = input.Arguments.Count;
        var hasBars = input.Series.Any(s => s.Type == ChartType.Bar);
        var kind = hasBars ? ArgumentKind.Category : KindOf(input.Arguments);
        var culture = input.Culture;
        var full = input.Stacked == ChartStacking.Percent;
        var stacked = input.Stacked != ChartStacking.None;

        var model = new ChartModel
        {
            Horizontal = input.Horizontal,
            Kind = kind,
            ArgumentTexts = input.Arguments.Select(a => input.ArgumentFormatter?.Invoke(a) ?? ArgumentText(a, input.ArgumentFormat, culture)).ToArray(),
            SizeTexts = input.Series.Select(s => s.Sizes?.Select(v => v is { } x ? x.ToString(s.SizeFormat ?? "#,##0.##", culture) : null).ToArray()).ToArray(),
            ValueTexts = input.Series.Select(s => s.Values.Select((v, i) => v is not { } x ? null
                : s.Secondary ? x.ToString(input.SecondaryFormat ?? "#,##0.##", culture)
                : Change(s, i) ? Signed(x, y => input.ValueFormatter?.Invoke(y) ?? y.ToString(input.ValueFormat ?? "#,##0.##", culture))
                : input.ValueFormatter?.Invoke(x) ?? x.ToString(input.ValueFormat ?? "#,##0.##", culture)).ToArray()).ToArray(),
        };
        LabelTexts(input, model);
        model.OutsideRoom = OutsideRoom(input, model);

        // Stacks: bars with bars, areas with areas. Each point's base and top, before scaling.
        var lows = new double?[input.Series.Count][];
        var highs = new double?[input.Series.Count][];
        var log = input.ValueLog;

        // A tick's text: the formatter's, but a full stack's ticks are shares, so they keep P0.
        Func<double, string>? tickText = full ? null : input.ValueFormatter;
        StackValues(input, stacked, full, log, lows, highs);

        // The value domain, from the series that show: a hidden one is drawn as if it weren't there.
        // On a log scale, only values over zero have a place.
        var min = double.PositiveInfinity;
        var max = double.NegativeInfinity;
        for (var s = 0; s < input.Series.Count; s++)
        {
            if (input.Series[s].Hidden || input.Series[s].Secondary) continue;
            for (var i = 0; i < n; i++)
            {
                if (highs[s][i] is { } h && (!log || h > 0)) { min = Math.Min(min, h); max = Math.Max(max, h); }
                if (lows[s][i] is { } l && (!log || l > 0)) { min = Math.Min(min, l); max = Math.Max(max, l); }
            }
        }

        // Marks on the value axis are always in view: a target above every value still shows.
        var marks = ResolveMarks(input);
        foreach (var m in marks)
        {
            if (m.Axis != MarkAxis.Value) continue;
            foreach (var v in new[] { m.Value, m.To })
            {
                if (v is not { } x || !double.IsFinite(x) || (log && x <= 0)) continue;
                min = Math.Min(min, x);
                max = Math.Max(max, x);
                model.MarkValues.Add(x);
            }
        }

        if (!log && input.Series.Any(s => !s.Hidden && !s.Secondary && s.Type is ChartType.Bar or ChartType.Area))
        {
            min = Math.Min(min, 0);
            max = Math.Max(max, 0);
        }

        if (!double.IsFinite(min)) { (min, max) = log ? (1, 10) : (0, 1); }

        // Bubbles keep inside the plot: the scale reaches past the values by the largest radius.
        var bubbleR = input.Series.Any(s => s.Type == ChartType.Bubble && !s.Hidden) ? BubbleRadius(input.HeightPx) : 0;
        if (bubbleR > 0)
        {
            (min, max) = PadEnds(min, max, log, bubbleR, Math.Max(80, input.HeightPx));
        }

        // A set end the values cross moves the other end past it, so the range never runs backward.
        if (input.ValueMin is { } vmin) { min = vmin; max = Math.Max(max, vmin); }
        if (input.ValueMax is { } vmax) { max = vmax; min = Math.Min(min, vmax); }

        // Upright values get a tick per 56px of height; values across are fitted by width.
        var plotHeight = Math.Max(80, input.HeightPx);
        var argumentRoomPx = EstimateValueColumn(input, model, kind);
        double dmin, dmax;
        var step = log ? null : input.ValueStep;
        var stepDomain = step is { } st ? StepDomain(min, max, st, input) : null;
        if (stepDomain is null) step = null;
        if (log || step is not null)
        {
            (dmin, dmax) = log ? LogDomain(min, max, input) : stepDomain!.Value;
            var count = input.Horizontal ? Math.Max(2, PlotWidth(Widths.Length - 1, argumentRoomPx) / 80) : Math.Max(2, Math.Round(plotHeight / 56));
            var ticks = log ? LogTicks(dmin, dmax, count) : StepTicks(dmin, dmax, step!.Value);
            var format = full ? "P0" : input.ValueFormat ?? (log ? "#,##0.##########" : "N" + StepDecimals(step!.Value));
            var (l0, l1) = log ? (Math.Log10(dmin), Math.Log10(dmax)) : (0, 0);
            var placed = ticks.Select(t => (
                Pos: log ? (Math.Log10(t.V) - l0) / (l1 - l0) : (t.V - dmin) / (dmax - dmin),
                t.Level,
                Text: tickText?.Invoke(t.V) ?? t.V.ToString(format, culture))).ToList();
            var levels = placed.Select(t => (t.Pos, t.Level)).ToList();

            // Up the side, the ticks a line apart at the plot's height; across, those that fit each width.
            var froms = input.Horizontal
                ? FitLevels(levels, Widths.Length, k => PlotWidth(k, argumentRoomPx), placed.Select(t => Width(t.Text)).DefaultIfEmpty(0).Max() + LabelGapPx)
                : FitLevels(levels, 1, _ => plotHeight, LineHeightPx + 4);
            for (var j = 0; j < placed.Count; j++)
            {
                if (froms[j] < 0) continue;
                model.ValueLabels.Add(new AxisLabel(placed[j].Pos, placed[j].Text, From: froms[j]));
                model.Grid.Add(new GridLine(placed[j].Pos, froms[j], ticks[j].V == 0));
            }
        }
        else if (!input.Horizontal)
        {
            var count = Math.Max(2, (int)Math.Round(plotHeight / 56));
            (dmin, dmax) = input.ValueMin is not null && input.ValueMax is not null ? (min, max) : Nice(min, max, count, input);
            var spec = ChartScale.TickSpec(dmin, dmax, count);
            var format = full ? "P0" : input.ValueFormat ?? "N" + ChartScale.Decimals(spec.Step);
            for (var i = spec.I1; i <= spec.I2; i++)
            {
                var v = spec.Value(i);
                var pos = (v - dmin) / (dmax - dmin);
                model.ValueLabels.Add(new AxisLabel(pos, tickText?.Invoke(v) ?? v.ToString(format, culture)));
                model.Grid.Add(new GridLine(pos, 0, v == 0));
            }
        }
        else
        {
            var count = Math.Max(2, PlotWidth(Widths.Length - 1, argumentRoomPx) / 80);
            (dmin, dmax) = input.ValueMin is not null && input.ValueMax is not null ? (min, max) : Nice(min, max, (int)count, input);
            var format = full ? "P0" : input.ValueFormat;
            NumberLabels(model.ValueLabels, dmin, dmax, count, format, culture, argumentRoomPx, grid: model.Grid, tickText);
        }

        model.ValueRange = (dmin, dmax);
        var scale2 = SecondaryAxis(input, model, marks, highs, lows, plotHeight);
        if (tickText is null && !full) PlainZero(model.ValueLabels, input.ValueFormat, culture);
        PlainZero(model.SecondaryLabels, input.SecondaryFormat, culture);

        // The argument axis.
        double[] positions;
        double argMin = 0, argMax = 1;
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
                CategoryLabels(model, positions, argumentRoomPx, input.ArgumentLabels);
            }
        }
        else
        {
            var values = input.Arguments.Select(a => ToDouble(a, kind)).ToArray();
            if (input.ArgumentLog)
            {
                if (kind != ArgumentKind.Number)
                {
                    throw new InvalidOperationException("A logarithmic ChartArgumentAxis needs numbers as the Argument: names and dates have no log scale.");
                }

                // Only numbers over zero have a place on a log scale.
                values = values.Select(v => v > 0 ? v : null).ToArray();
            }

            var finite = values.Where(v => v is not null).Select(v => v!.Value).ToArray();
            var amin = finite.Length > 0 ? finite.Min() : 0;
            var amax = finite.Length > 0 ? finite.Max() : 1;
            if (amin == amax) { amin -= kind == ArgumentKind.Time ? TimeSpan.TicksPerDay : 1; amax += kind == ArgumentKind.Time ? TimeSpan.TicksPerDay : 1; }
            var count = Math.Max(2, PlotWidth(Widths.Length - 1, argumentRoomPx) / 90);

            // Bubbles keep inside the plot, at a middling width (560px) at least.
            if (kind == ArgumentKind.Number && bubbleR > 0 && finite.Length > 0)
            {
                (amin, amax) = PadEnds(amin, amax, input.ArgumentLog, bubbleR, PlotWidth(4, argumentRoomPx));
                finite = [.. finite, amin, amax];
            }

            // Points have round ends to the scale, so none sits on the plot's edge; lines run edge to edge.
            if (kind == ArgumentKind.Number && input.Series.Any(s => s.IsPoint))
            {
                (amin, amax) = ChartScale.Nice(amin, amax, (int)count);
            }

            positions = values.Select(v => v is { } x ? (x - amin) / (amax - amin) : double.NaN).ToArray();
            (argMin, argMax) = (amin, amax);
            if (input.ArgumentLog)
            {
                // The powers of ten around the arguments; ticks at each, with 2s and 5s when few,
                // as many as fit each width.
                // Bubbles' ends are already moved out for their radius: snapped to a power of ten,
                // they could take in a whole empty one.
                var lo = finite.Length == 0 ? 1 : bubbleR > 0 ? finite.Min() : Power(1, (int)Math.Floor(Math.Log10(finite.Min()) + 1e-9));
                var hi = finite.Length == 0 ? 10 : bubbleR > 0 ? finite.Max() : Power(1, (int)Math.Ceiling(Math.Log10(finite.Max()) - 1e-9));
                if (!(hi > lo)) hi = lo * 10;
                (argMin, argMax) = (lo, hi);
                var (g0, g1) = (Math.Log10(lo), Math.Log10(hi));
                positions = values.Select(v => v is { } x ? (Math.Log10(x) - g0) / (g1 - g0) : double.NaN).ToArray();
                var ticks = LogTicks(lo, hi, count);
                var placed = ticks.Select(t => (Pos: (Math.Log10(t.V) - g0) / (g1 - g0), t.Level,
                    Text: input.ArgumentFormatter?.Invoke(t.V) ?? t.V.ToString(input.ArgumentFormat ?? "#,##0.##########", culture))).ToList();
                var froms = FitLevels(placed.Select(t => (t.Pos, t.Level)).ToList(), Widths.Length, k => PlotWidth(k, argumentRoomPx),
                    placed.Select(t => Width(t.Text)).DefaultIfEmpty(0).Max() + LabelGapPx);
                for (var j = 0; j < placed.Count; j++)
                {
                    if (froms[j] >= 0) model.ArgumentLabels.Add(new AxisLabel(placed[j].Pos, placed[j].Text, From: froms[j]));
                }
            }
            else if (kind == ArgumentKind.Number)
            {
                NumberLabels(model.ArgumentLabels, amin, amax, count, input.ArgumentFormat, culture, argumentRoomPx, grid: null,
                    input.ArgumentFormatter is { } af ? v => af(v) : null);
            }
            else
            {
                // A formatter gets each tick as a date of the argument's own type.
                var first = input.Arguments.FirstOrDefault(a => a is not null);
                Func<DateTime, string>? timeText = input.ArgumentFormatter is { } tf
                    ? t => tf(first switch
                    {
                        DateOnly => DateOnly.FromDateTime(t),
                        DateTimeOffset o => new DateTimeOffset(t, o.Offset),
                        _ => t,
                    })
                    : null;
                TimeLabels(model, new DateTime((long)amin), new DateTime((long)amax), count, input.ArgumentFormat, culture, argumentRoomPx, timeText);
            }
        }

        model.ArgumentPositions = positions;

        // Grid lines along the arguments: between bands (where a label starts its band), or at a
        // scale's ticks.
        if (input.ArgumentGrid)
        {
            foreach (var label in model.ArgumentLabels)
            {
                var pos = kind == ArgumentKind.Category ? label.Pos - 0.5 / Math.Max(1, n) : label.Pos;
                if (pos > 1e-9 && pos < 1 - 1e-9) model.Grid.Add(new GridLine(pos, label.From, false, Argument: true));
            }
        }

        // A scale's last label is centered on the plot's end: leave room for its far half.
        var bottom = input.Horizontal ? model.ValueLabels : kind == ArgumentKind.Category ? null : model.ArgumentLabels;
        if (bottom is { Count: > 0 } && bottom[^1] is { Pos: > 0.9 } last)
        {
            var half = Math.Max(Width(last.Text), last.Context is { } c ? Width(c) : 0) / 2;
            model.EndRoomPx = Math.Ceiling(Math.Max(0, half - (1 - last.Pos) * 200));
        }

        // The series.
        var (lmin, lmax) = log ? (Math.Log10(dmin), Math.Log10(dmax)) : (0, 0);
        double Scale(double v) => log
            ? v > 0 ? Math.Clamp((Math.Log10(v) - lmin) / (lmax - lmin), 0, 1) : 0
            : Math.Clamp((v - dmin) / (dmax - dmin), 0, 1);
        var bars = input.Series.Where(s => s.Type == ChartType.Bar && !s.Hidden).ToList();
        var band = 1.0 / Math.Max(1, n);

        // Bubbles share one size scale, so they compare across series; the largest is an eighth
        // of the plot's height across, between 12 and 40 pixels in radius.
        var maxSize = input.Series.Where(s => s.Type == ChartType.Bubble && s.Sizes is not null)
            .SelectMany(s => s.Sizes!).Where(v => v is > 0 && double.IsFinite(v.Value)).Select(v => v!.Value).DefaultIfEmpty(1).Max();
        var maxRadius = BubbleRadius(plotHeight);
        if (input.Series.FirstOrDefault(s => s.Type == ChartType.Bubble && s.Sizes is not null && s.Sizes.Any(v => v is > 0)) is { ShowSizeKey: true } bubbles)
        {
            // Round values a reader can take in: near the largest size, and a quarter of it (half the radius).
            var circles = new[] { RoundDown(maxSize), RoundDown(maxSize / 4) }.Where(v => v > 0).Distinct()
                .Select(v => (v.ToString(bubbles.SizeFormat ?? "#,##0.##", culture), Math.Max(2, Math.Sqrt(v / maxSize) * maxRadius))).ToList();
            model.SizeKey = (bubbles.SizeName ?? input.Text.ChartSize, circles);

            // Its name at the legend's 14px, the circles and their leaders, the widest value, and the gaps.
            model.SizeKeyPx = Math.Ceiling(Width(model.SizeKey.Value.Name) * 14 / FontPx + circles.Max(c => c.Item2) * 2 + 8
                + circles.Max(c => Width(c.Item1)) + 12);
        }

        var lineStyle = 0;
        var palette = 0;
        var ends = new List<(int S, double Y)>();
        for (var s = 0; s < input.Series.Count; s++)
        {
            var data = input.Series[s];

            // A trend takes its series' color and leaves the palette and dashes to the others.
            var own = data.ColorFrom is null ? palette++ : -1;
            var shape = new SeriesShape
            {
                Data = data,
                Slot = data.ColorFrom is { } from ? model.Series[from].Slot : own % 8 + 1,
                Index = s,
                Style = data.Type == ChartType.Bar || data.ColorFrom is not null ? 0 : lineStyle++ + own / 8,
            };
            model.Series.Add(shape);
            if (data.Hidden) continue;

            // The second axis's series take its scale.
            Func<double, double> sc = data.Secondary ? scale2 : Scale;
            var zero = sc(0);

            if (data.Type == ChartType.Bar)
            {
                // Chart.js's proportions: the group takes 80% of its band, each bar 90% of its share.
                var j = bars.IndexOf(data);
                var slot = stacked ? band * 0.8 : band * 0.8 / bars.Count;
                var width = slot * 0.9;
                var steps = new List<(double A0, double A1, double V)>();
                for (var i = 0; i < n; i++)
                {
                    if (highs[s][i] is not { } top) continue;
                    var a0 = i * band + band * 0.1 + (stacked ? 0 : j * slot) + slot * 0.05;
                    var v0 = sc(lows[s][i] ?? 0);
                    var v1 = sc(top);
                    var title = $"{model.ArgumentTexts[i]}, {data.Name}: {model.ValueTexts[s][i]}";
                    var (lo, hi) = (Math.Min(v0, v1), Math.Max(v0, v1));
                    var rect = input.Horizontal
                        ? new BarRect(lo * 100, a0 * 100, (hi - lo) * 100, width * 100, title)
                        : new BarRect(a0 * 100, (1 - hi) * 100, width * 100, (hi - lo) * 100, title);

                    // The value end is rounded on the outermost bar: every bar side by side, the
                    // last segment of a stack each way from zero. Not a waterfall's: each bar's
                    // ends are where the last step ended and the next begins, joined by a line.
                    var below = v1 < v0;
                    rect = rect with { Index = i, Dir = (input.Horizontal, below) switch { (false, false) => 't', (false, true) => 'b', (true, false) => 'r', (true, true) => 'l' } };
                    var outermost = !stacked || !bars.Skip(j + 1).Any(other =>
                        other.Values[i] is { } x && double.IsFinite(x) && x != 0 && (x < 0) == ((highs[s][i] ?? 0) < (lows[s][i] ?? 0)));
                    if (outermost && hi > lo && !data.IsWaterfall)
                    {
                        rect = rect with { End = rect.Dir };
                    }

                    if (data.Totals is { } totals)
                    {
                        // The first bar is the amount the waterfall starts from: a total, on the baseline.
                        rect = rect with { Change = totals[i] || steps.Count == 0 ? "total" : top < (lows[s][i] ?? 0) ? "down" : "up" };
                        steps.Add((a0, a0 + width, v1));
                    }

                    shape.Bars.Add(rect);
                }

                // A waterfall's connectors: from each bar's end, at the running total, to the next bar.
                if (steps.Count > 1)
                {
                    var path = new StringBuilder();
                    for (var k = 1; k < steps.Count; k++)
                    {
                        var (x0, y0) = Map(steps[k - 1].A1, steps[k - 1].V, input.Horizontal);
                        var (x1, y1) = Map(steps[k].A0, steps[k - 1].V, input.Horizontal);
                        path.Append(CultureInfo.InvariantCulture, $"M{x0 * 1000:0.#},{y0 * 1000:0.#}L{x1 * 1000:0.#},{y1 * 1000:0.#}");
                    }

                    shape.Connectors = path.ToString();
                }

                continue;
            }

            // Lines and areas: runs of points, split at missing values unless they're joined.
            var points = new List<(double A, double V, int I)?>();
            for (var i = 0; i < n; i++)
            {
                if (highs[s][i] is { } v && !double.IsNaN(positions[i]))
                {
                    points.Add((positions[i], sc(v), i));
                }
                else if (!data.ConnectGaps)
                {
                    points.Add(null);
                }
            }

            var runs = data.IsPoint ? [] : Runs(points);
            var line = new StringBuilder();
            var area = new StringBuilder();
            foreach (var run in runs)
            {
                var upper = Curve(run.Select(p => (p.A, p.V)).ToList(), data.Curve);
                AppendPath(line, upper, input.Horizontal, move: true);
                if (data.Type == ChartType.Area)
                {
                    var lower = run.Select(p => (p.A, lows[s][p.I] is { } l ? sc(l) : zero)).ToList();
                    AppendPath(area, upper, input.Horizontal, move: true);
                    AppendPath(area, Reverse(Curve(lower, stacked ? data.Curve : ChartCurve.Straight)), input.Horizontal, move: false);
                    area.Append('Z');
                }
            }

            shape.Line = line.Length > 0 ? line.ToString() : null;
            if (data.EndLabel && !data.IsPoint && points.LastOrDefault(p => p is not null) is { } end)
            {
                ends.Add((s, (1 - end.V) * plotHeight));
            }

            shape.Area = area.Length > 0 ? area.ToString() : null;

            var count = points.Count(p => p is not null);
            if (data.IsPoint || (data.Markers ?? (data.Type == ChartType.Line && count <= 12)))
            {
                foreach (var p in points)
                {
                    if (p is not { } point) continue;
                    var (x, y) = Map(point.A, point.V, input.Horizontal);
                    var title = $"{model.ArgumentTexts[point.I]}, {data.Name}: {model.ValueTexts[s][point.I]}";
                    if (data.Type == ChartType.Bubble)
                    {
                        // By area: the radius goes with the square root of the size.
                        if (data.Sizes?[point.I] is not { } size || !(size > 0)) continue;
                        title += $", {data.SizeName ?? input.Text.ChartSize}: {model.SizeTexts[s]![point.I]}";
                        shape.Marks.Add(new PointMark(x * 100, y * 100, title, point.I, Math.Max(2, Math.Sqrt(size / maxSize) * maxRadius)));
                        continue;
                    }

                    shape.Marks.Add(new PointMark(x * 100, y * 100, title, point.I));
                }

                // Larger bubbles first, so none covers a smaller one of its series.
                if (data.Type == ChartType.Bubble) shape.Marks.Sort((a, b) => (b.R ?? 0).CompareTo(a.R ?? 0));
            }
        }

        // End labels: each level with its line's last value, nudged apart a line each.
        var centers = Spread(ends.Select(e => e.Y).ToList(), plotHeight, LineHeightPx);
        for (var e = 0; e < ends.Count; e++)
        {
            model.EndLabels.Add(new LineEndLabel(ends[e].S, input.Series[ends[e].S].Name, centers[e] / plotHeight));
        }

        model.EndLabelsPx = Math.Ceiling(EndsWidth(input));
        PlaceMarks(input, model, marks, kind, positions, argMin, argMax, Scale, scale2);
        PlaceLabels(input, model, argumentRoomPx, plotHeight);
        return model;
    }

    // ---- Reference lines, bands and notes. ----

    /// <summary>The marks with each statistic worked out from its series.</summary>
    private static List<ChartMarkInput> ResolveMarks(ChartInput input) => input.Marks.Select(m =>
    {
        if (m.Statistic is not { } stat) return m;
        var series = input.Series.FirstOrDefault(s => s.Name == m.Series)
            ?? throw new InvalidOperationException($"ChartReferenceLine's Series \"{m.Series}\" isn't a series on the chart: use a series' Name.");
        var values = series.Values.Where(v => v is { } x && double.IsFinite(x)).Select(v => v!.Value).Order().ToArray();
        double? value = values.Length == 0 ? null : stat == ChartStatistic.Average
            ? values.Average()
            : values.Length % 2 == 1 ? values[values.Length / 2] : (values[values.Length / 2 - 1] + values[values.Length / 2]) / 2;
        return m with { Value = value };
    }).ToList();

    /// <summary>
    /// Places the marks: value-axis ones by the scale, argument ones at their argument (a name's
    /// band, or a number or date on the scale). A mark at an argument the chart doesn't have is
    /// left out. Each is put in words for the chart's description.
    /// </summary>
    private static void PlaceMarks(ChartInput input, ChartModel model, List<ChartMarkInput> marks, ArgumentKind kind, double[] positions, double argMin, double argMax, Func<double, double> primary, Func<double, double> secondary)
    {
        var culture = input.Culture;
        var n = input.Arguments.Count;
        string ArgText(object? a) => input.ArgumentFormatter?.Invoke(a) ?? ArgumentText(a, input.ArgumentFormat, culture);

        // A name's index, or a number or date's place on the scale.
        int IndexOf(object? a)
        {
            for (var i = 0; i < n; i++)
            {
                if (Equals(input.Arguments[i], a) || model.ArgumentTexts[i] == ArgText(a)) return i;
            }

            return -1;
        }

        double? At(object? a) => kind == ArgumentKind.Category
            ? IndexOf(a) is var i and >= 0 ? positions[i] : null
            : ToDouble(a, kind) is not { } x ? null
            : input.ArgumentLog ? x > 0 ? Math.Clamp((Math.Log10(x) - Math.Log10(argMin)) / (Math.Log10(argMax) - Math.Log10(argMin)), 0, 1) : null
            : Math.Clamp((x - argMin) / (argMax - argMin), 0, 1);

        foreach (var m0 in marks)
        {
            // A mark on the second axis is placed and written by it, and drawn as a value-axis mark.
            var on2 = m0.Axis == MarkAxis.Secondary;
            var m = on2 ? m0 with { Axis = MarkAxis.Value } : m0;
            var scale = on2 ? secondary : primary;
            string ValueText(double v) => on2
                ? v.ToString(input.SecondaryFormat ?? "#,##0.##", culture)
                : input.ValueFormatter?.Invoke(v) ?? v.ToString(input.ValueFormat ?? "#,##0.##", culture);
            var count = model.Marks.Count;
            var series = m.Statistic is not null ? input.Series.ToList().FindIndex(s => s.Name == m.Series) : -1;
            switch (m.Kind, m.Axis)
            {
                case (ChartMarkKind.Line, MarkAxis.Value) when m.Value is { } v:
                {
                    var value = ValueText(v);
                    var name = m.Statistic is { } st ? st == ChartStatistic.Average ? input.Text.ChartAverage : input.Text.ChartMedian : null;
                    var text = m.Label ?? (name is not null ? $"{name}: {value}" : value);
                    model.Marks.Add(new MarkShape(m.Kind, m.Axis, scale(v), 0, 0, text) { V = v, Series = series, Align = m.Align });
                    model.MarkTexts.Add(m.Label is not null ? $"{m.Label}: {value}" : text);
                    break;
                }

                case (ChartMarkKind.Line, MarkAxis.Argument) when At(m.At) is { } p:
                {
                    var at = ArgText(m.At);
                    model.Marks.Add(new MarkShape(m.Kind, m.Axis, p, 0, 0, m.Label ?? at) { Align = m.Align });
                    model.MarkTexts.Add(m.Label is not null ? $"{m.Label}: {at}" : at);
                    break;
                }

                case (ChartMarkKind.Band, MarkAxis.Value) when m.Value is { } from && m.To is { } to:
                {
                    var range = $"{ValueText(Math.Min(from, to))} {input.Text.ChartRangeTo} {ValueText(Math.Max(from, to))}";
                    var (p, q) = (scale(Math.Min(from, to)), scale(Math.Max(from, to)));
                    model.Marks.Add(new MarkShape(m.Kind, m.Axis, p, q, 0, m.Label ?? range) { V = Math.Min(from, to), VTo = Math.Max(from, to) });
                    model.MarkTexts.Add(m.Label is not null ? $"{m.Label}: {range}" : range);
                    break;
                }

                case (ChartMarkKind.Band, MarkAxis.Argument):
                {
                    double? p, q;
                    if (kind == ArgumentKind.Category)
                    {
                        var (i0, i1) = (IndexOf(m.At), IndexOf(m.AtTo));
                        if (i0 < 0 || i1 < 0) break;
                        (p, q) = ((double)Math.Min(i0, i1) / n, (double)(Math.Max(i0, i1) + 1) / n);
                    }
                    else
                    {
                        (p, q) = (At(m.At), At(m.AtTo));
                        if (p is null || q is null) break;
                        (p, q) = (Math.Min(p.Value, q.Value), Math.Max(p.Value, q.Value));
                    }

                    var range = $"{ArgText(m.At)} {input.Text.ChartRangeTo} {ArgText(m.AtTo)}";
                    model.Marks.Add(new MarkShape(m.Kind, m.Axis, p!.Value, q!.Value, 0, m.Label ?? range));
                    model.MarkTexts.Add(m.Label is not null ? $"{m.Label}: {range}" : range);
                    break;
                }

                case (ChartMarkKind.Note, _) when m.Value is { } v && At(m.At) is { } a:
                    model.Marks.Add(new MarkShape(m.Kind, MarkAxis.Value, scale(v), 0, a, m.Label ?? "") { V = v });
                    model.MarkTexts.Add($"{m.Label}: {ArgText(m.At)}, {ValueText(v)}");
                    break;
            }

            // The second axis keeps its scale when the script rescales the first: no values to move by.
            if (on2 && model.Marks.Count > count) model.Marks[^1] = model.Marks[^1] with { V = null, VTo = null };
        }
    }

    // ---- The second value axis. ----

    /// <summary>
    /// The second value axis, for series with <see cref="MarkAxis.Secondary"/>: its range from
    /// those series (and its marks), and ticks at the first axis's grid lines, each a round step,
    /// so both read off the same lines. With its own SecondaryMin and SecondaryMax, or a first axis
    /// whose ticks don't reach both ends, it takes its own round ticks instead. Returns its scale.
    /// </summary>
    private static Func<double, double> SecondaryAxis(ChartInput input, ChartModel model, List<ChartMarkInput> marks, double?[][] highs, double?[][] lows, double plotHeight)
    {
        var on = Enumerable.Range(0, input.Series.Count).Where(s => input.Series[s].Secondary).ToList();
        if (on.Count == 0) return v => 0;
        var culture = input.Culture;
        var min = double.PositiveInfinity;
        var max = double.NegativeInfinity;
        foreach (var s in on.Where(s => !input.Series[s].Hidden))
        {
            foreach (var v in highs[s].Concat(lows[s]))
            {
                if (v is { } x) { min = Math.Min(min, x); max = Math.Max(max, x); }
            }
        }

        foreach (var m in marks.Where(m => m.Axis == MarkAxis.Secondary))
        {
            foreach (var v in new[] { m.Value, m.To })
            {
                if (v is { } x && double.IsFinite(x)) { min = Math.Min(min, x); max = Math.Max(max, x); }
            }
        }

        if (on.Any(s => !input.Series[s].Hidden && input.Series[s].Type is ChartType.Bar or ChartType.Area))
        {
            min = Math.Min(min, 0);
            max = Math.Max(max, 0);
        }

        if (!double.IsFinite(min)) (min, max) = (0, 1);
        if (input.SecondaryMin is { } smin) { min = smin; max = Math.Max(max, smin); }
        if (input.SecondaryMax is { } smax) { max = smax; min = Math.Min(min, smax); }

        // The first axis's ticks, if they run evenly from end to end: the steps to match.
        var first = model.ValueLabels;
        var steps = first.Count - 1;
        var even = steps >= 1 && Math.Abs(first[0].Pos) < 1e-9 && Math.Abs(first[^1].Pos - 1) < 1e-9 && input.SecondaryMin is null && input.SecondaryMax is null;
        double lo, hi;
        var ticks = new List<double>();
        if (even)
        {
            // The smallest round step (1, 2 or 5 times a power of ten) whose run of steps covers the range.
            var step = NiceStep(Math.Max(max - min, 1e-12) / steps);
            for (var tries = 0; ; tries++)
            {
                lo = Math.Floor(min / step + 1e-9) * step;
                if (lo + steps * step >= max - 1e-9 * Math.Abs(max) || tries > 20) break;
                step = NiceStep(step * 1.0001, up: true);
            }

            hi = lo + steps * step;
            for (var i = 0; i <= steps; i++) ticks.Add((double)((decimal)lo + i * (decimal)step));
            model.SecondaryRange = (lo, hi);
            var format = input.SecondaryFormat ?? "N" + ChartScale.Decimals(step);
            foreach (var v in ticks) model.SecondaryLabels.Add(new AxisLabel((v - lo) / (hi - lo), v.ToString(format, culture)));
        }
        else
        {
            var count = Math.Max(2, (int)Math.Round(plotHeight / 56));
            (lo, hi) = input.SecondaryMin is not null && input.SecondaryMax is not null ? (min, max) : ChartScale.Nice(min, max, count);
            if (input.SecondaryMin is { } a) lo = a;
            if (input.SecondaryMax is { } b) hi = b;
            var spec = ChartScale.TickSpec(lo, hi, count);
            var format = input.SecondaryFormat ?? "N" + ChartScale.Decimals(spec.Step);
            model.SecondaryRange = (lo, hi);
            for (var i = spec.I1; i <= spec.I2; i++)
            {
                var v = spec.Value(i);
                model.SecondaryLabels.Add(new AxisLabel((v - lo) / (hi - lo), v.ToString(format, culture)));
            }
        }

        var (l2, h2) = model.SecondaryRange;
        return v => Math.Clamp((v - l2) / (h2 - l2), 0, 1);
    }

    /// <summary>
    /// Under a format that scales values down, such as thousands as "#,##0,K", the zero tick reads
    /// "0": nothing has no unit. chart-draw.ts does the same.
    /// </summary>
    private static void PlainZero(List<AxisLabel> labels, string? format, CultureInfo culture)
    {
        if (format is null || NumberShape.Of(format, culture) is not { Scale: < 1 } shape) return;
        var zero = 0.0.ToString(format, culture);
        var i = labels.FindIndex(l => l.Text == zero);
        if (i >= 0) labels[i] = labels[i] with { Text = shape.Prefix + "0" };
    }

    /// <summary>The largest bubble's radius: an eighth of the plot's height, 12 to 40 pixels.</summary>
    private static double BubbleRadius(double plotHeightPx) => Math.Clamp(Math.Max(80, plotHeightPx) / 8, 12, 40);

    /// <summary>
    /// A scale's ends moved out so a circle of <paramref name="radiusPx"/> at either end stays on a
    /// plot <paramref name="lengthPx"/> long; on a log scale, by powers of ten.
    /// </summary>
    private static (double Min, double Max) PadEnds(double min, double max, bool log, double radiusPx, double lengthPx)
    {
        if (log && !(min > 0)) return (min, max);
        var (a, b) = log ? (Math.Log10(min), Math.Log10(max)) : (min, max);
        var span = b - a;
        if (!(span > 0)) span = log ? 1 : Math.Max(Math.Abs(b), 1);
        var pad = span * radiusPx / Math.Max(1, lengthPx - 2 * radiusPx);
        (a, b) = (a - pad, b + pad);
        return log ? (Math.Pow(10, a), Math.Pow(10, b)) : (a, b);
    }

    /// <summary><paramref name="x"/> rounded down to one significant digit: 46 million is 40 million.</summary>
    private static double RoundDown(double x)
    {
        if (!(x > 0) || !double.IsFinite(x)) return 0;
        var power = Math.Pow(10, Math.Floor(Math.Log10(x)));
        return Math.Floor(x / power * (1 + 1e-12)) * power;
    }

    /// <summary>The round step (1, 2 or 5 times a power of ten) at or above <paramref name="raw"/>; with <paramref name="up"/>, the next one past it.</summary>
    private static double NiceStep(double raw, bool up = false)
    {
        var power = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        foreach (var f in new[] { 1, 2, 5, 10 })
        {
            if (f * power >= raw * (1 - 1e-12) && (!up || f * power > raw)) return f * power;
        }

        return 10 * power;
    }

    // ---- Data labels. ----

    private const double MarkGapPx = 4, LabelPadPx = 2;

    // An end label's start past the plot (chart.css's --ib-space-3), clear of the last marker.
    private const double EndGapPx = 12;

    /// <summary>
    /// Places each series' labels at each of <see cref="Widths"/>, from the widest down: a label
    /// keeps the first spot its mode allows that stays in the plot (in the bar, for one inside)
    /// and clears the labels placed before it. A label shown at a width shows at every wider one,
    /// at the spot it has at the narrowest.
    /// </summary>
    private static void LabelTexts(ChartInput input, ChartModel model)
    {
        var culture = input.Culture;
        model.LabelTexts = new string?[]?[input.Series.Count];
        for (var si = 0; si < input.Series.Count; si++)
        {
            var s = input.Series[si];
            if (s.Labels == ChartLabels.None) continue;
            model.LabelTexts[si] = s.Values.Select((v, i) => v is not { } x ? null
                : s.LabelTexts is { } given ? (i < given.Length ? given[i] : null)
                : s.LabelFormat is not { } f ? model.ValueTexts[si][i]
                : Change(s, i) ? Signed(x, y => y.ToString(f, culture)) : x.ToString(f, culture)).ToArray();
        }
    }

    /// <summary>
    /// The room labels past their bars' ends take outside the plot (<see cref="ChartModel.OutsideRoom"/>):
    /// for bar series whose labels go there first (Outside, or Auto unstacked), the widest label
    /// beside bars across, or a line above upright columns. Hidden series count, which the legend can show.
    /// </summary>
    private static (double X, double Y) OutsideRoom(ChartInput input, ChartModel model)
    {
        var stacked = input.Stacked != ChartStacking.None;
        var texts = Enumerable.Range(0, input.Series.Count)
            .Where(s => input.Series[s].Type == ChartType.Bar && !input.Series[s].IsWaterfall
                && (input.Series[s].Labels == ChartLabels.Outside || (input.Series[s].Labels == ChartLabels.Auto && !stacked)))
            .SelectMany(s => model.LabelTexts[s] ?? []).OfType<string>().ToList();
        if (texts.Count == 0) return (0, 0);
        return input.Horizontal
            ? (Math.Ceiling(texts.Max(t => Width(t) * 1.04) + MarkGapPx + LabelPadPx), 0)
            : (0, LineHeightPx + MarkGapPx);
    }

    private static void PlaceLabels(ChartInput input, ChartModel model, double columnPx, double plotH)
    {
        var stacked = input.Stacked != ChartStacking.None;
        var weights = new List<double>();
        var candidates = new List<(int S, string Text, double W, (double AX, double DX, double AY, double DY, (double X0, double Y0, double X1, double Y1)? Bar)[] Spots, string? Change)>();
        foreach (var shape in model.Series)
        {
            var data = shape.Data;
            if (data.Hidden || data.Labels == ChartLabels.None || model.LabelTexts[shape.Index] is not { } texts) continue;
            foreach (var bar in shape.Bars)
            {
                if (texts[bar.Index] is not { } text) continue;
                var (x0, y0, x1, y1) = (bar.X / 100, bar.Y / 100, (bar.X + bar.Width) / 100, (bar.Y + bar.Height) / 100);
                var w = Width(text) * 1.04;
                var (mx, my) = ((x0 + x1) / 2, (y0 + y1) / 2);
                var (dw, dh) = (MarkGapPx + w / 2, MarkGapPx + LineHeightPx / 2);
                (double, double, double, double, (double, double, double, double)?) Out() => bar.Dir switch
                {
                    't' => (mx, 0, y0, -dh, null),
                    'b' => (mx, 0, y1, dh, null),
                    'r' => (x1, dw, my, 0, null),
                    _ => (x0, -dw, my, 0, null),
                };
                (double, double, double, double, (double, double, double, double)?) End() => bar.Dir switch
                {
                    't' => (mx, 0, y0, dh, (x0, y0, x1, y1)),
                    'b' => (mx, 0, y1, -dh, (x0, y0, x1, y1)),
                    'r' => (x1, -dw, my, 0, (x0, y0, x1, y1)),
                    _ => (x0, dw, my, 0, (x0, y0, x1, y1)),
                };
                (double, double, double, double, (double, double, double, double)?) Middle() => (mx, 0, my, 0, (x0, y0, x1, y1));
                var spots = (data.Labels, stacked) switch
                {
                    (ChartLabels.Inside, true) or (ChartLabels.Auto, true) => new[] { Middle() },
                    (ChartLabels.Inside, false) => [End()],
                    _ => [Out(), End()],
                };
                candidates.Add((shape.Index, text, w, spots, bar.Change));
                weights.Add(0);
            }

            foreach (var mark in shape.Marks)
            {
                var (px, py, i) = (mark.X / 100, mark.Y / 100, mark.Index);
                if (texts[i] is not { } text) continue;
                var w = Width(text) * 1.04;
                var d = (mark.R ?? 6) + MarkGapPx + LineHeightPx / 2;
                (double, double, double, double, (double, double, double, double)?) above = (px, 0, py, -d, null), below = (px, 0, py, d, null);
                candidates.Add((shape.Index, text, w, data.Labels == ChartLabels.Inside ? [below, above] : [above, below], null));
                weights.Add(mark.R ?? 0);
            }
        }

        if (candidates.Count == 0) return;
        var placed = new (double AX, double DX, double AY, double DY, bool Inside, double Justify)?[Widths.Length, candidates.Count];
        // Labels that show overlapping or not go first, so the others step around them.
        bool Forced(int c) => input.Series[candidates[c].S].LabelOverlap == ChartLabelOverlap.Show;
        // Then larger bubbles' labels, across series; the rest in order.
        var shown = Enumerable.Range(0, candidates.Count).OrderBy(c => Forced(c) ? 0 : 1).ThenByDescending(c => weights[c]).ToList();
        for (var k = Widths.Length - 1; k >= 0; k--)
        {
            var plotW = PlotWidth(k, columnPx);
            var boxes = new List<(double L, double T, double R, double B)>();
            var keep = new List<int>();
            foreach (var c in shown)
            {
                var (_, _, w, spots, _) = candidates[c];
                foreach (var (ax, dx, ay, dy, bar) in spots)
                {
                    var (cx, cy) = (ax * plotW + dx, ay * plotH + dy);

                    // Above or below its mark, a label near a side slides in to stay in the plot.
                    var justify = bar is null && dx == 0 ? w / 2 + LabelPadPx : 0;
                    if (justify > 0) cx = Math.Clamp(cx, justify, Math.Max(justify, plotW - justify));
                    var box = (L: cx - w / 2 - LabelPadPx, T: cy - LineHeightPx / 2, R: cx + w / 2 + LabelPadPx, B: cy + LineHeightPx / 2);
                    var room = bar is { } b
                        ? box.L >= b.X0 * plotW && box.R <= b.X1 * plotW && box.T >= b.Y0 * plotH - 0.5 && box.B <= b.Y1 * plotH + 0.5
                        : box.L >= 0 && box.R <= plotW + model.OutsideRoom.X + 0.5 && box.T >= -model.OutsideRoom.Y - 0.5 && box.B <= plotH;
                    if (!room) continue;
                    if (!Forced(c) && boxes.Any(o => box.L < o.R && o.L < box.R && box.T < o.B && o.T < box.B)) break;
                    boxes.Add(box);
                    keep.Add(c);
                    placed[k, c] = (ax, dx, ay, dy, bar is not null, justify);
                    break;
                }
            }

            shown = keep;
        }

        for (var c = 0; c < candidates.Count; c++)
        {
            var from = Enumerable.Range(0, Widths.Length).FirstOrDefault(k => placed[k, c] is not null, -1);
            if (from < 0 || placed[Widths.Length - 1, c] is null) continue;
            var (ax, dx, ay, dy, inside, justify) = placed[from, c]!.Value;
            model.DataLabels.Add(new DataLabel(candidates[c].S, candidates[c].Text, ax, dx, ay, dy, from, inside, Math.Ceiling(justify)) { Change = candidates[c].Change });
        }
    }

    /// <summary>
    /// Centers for labels a line tall, each as near its target as it can be with none overlapping
    /// and all within the height: labels that would overlap are put in a run, centered on their
    /// targets' average, and runs that then touch join. Returned in the targets' order.
    /// chart-draw.ts's spread does the same.
    /// </summary>
    internal static double[] Spread(IReadOnlyList<double> targets, double height, double line)
    {
        var order = Enumerable.Range(0, targets.Count).OrderBy(i => targets[i]).ToArray();
        var runs = new List<(double Sum, int N, double Top)>();
        double Top(double sum, int n) => Math.Clamp(sum / n - n * line / 2, 0, Math.Max(0, height - n * line));
        foreach (var i in order)
        {
            var run = (Sum: targets[i], N: 1, Top: Top(targets[i], 1));
            while (runs.Count > 0 && runs[^1].Top + runs[^1].N * line > run.Top)
            {
                var before = runs[^1];
                runs.RemoveAt(runs.Count - 1);
                run = (before.Sum + run.Sum, before.N + run.N, Top(before.Sum + run.Sum, before.N + run.N));
            }

            runs.Add(run);
        }

        var centers = new double[targets.Count];
        var k = 0;
        foreach (var run in runs)
        {
            for (var j = 0; j < run.N; j++) centers[order[k++]] = run.Top + j * line + line / 2;
        }

        return centers;
    }

    /// <summary>The room end labels take past the plot: the widest name (hidden ones too, which the legend can show), in its medium weight, and a gap.</summary>
    private static double EndsWidth(ChartInput input) =>
        input.Series.Where(s => s.EndLabel).Select(s => Width(s.Name) * 1.06 + EndGapPx).DefaultIfEmpty(0).Max();

    /// <summary>
    /// Whether a value is a waterfall's change, which reads signed: not a total, and not the first
    /// bar, which is the amount the waterfall starts from.
    /// </summary>
    private static bool Change(ChartSeriesData s, int i) =>
        s.Totals is { } totals && !totals[i] && Array.FindIndex(s.Values, v => v is not null) != i;

    /// <summary>A change with its sign, as "+12,000" or "−4,500" (a minus sign, not a hyphen).</summary>
    private static string Signed(double x, Func<double, string> text) =>
        x > 0 ? "+" + text(x) : x < 0 ? "−" + text(-x) : text(x);

    private static (double, double) Nice(double min, double max, int count, ChartInput input)
    {
        var (a, b) = ChartScale.Nice(min, max, count);
        return (input.ValueMin ?? a, input.ValueMax ?? b);
    }

    // A log scale's ends: the powers of ten around the values, unless set.
    private static (double, double) LogDomain(double min, double max, ChartInput input)
    {
        var lo = input.ValueMin ?? Power(1, (int)Math.Floor(Math.Log10(min) + 1e-9));
        var hi = input.ValueMax ?? Power(1, (int)Math.Ceiling(Math.Log10(max) - 1e-9));
        return hi > lo ? (lo, hi) : (lo, lo * 10);
    }

    // Ends on multiples of the step, unless set. Null when that would make more than 500 ticks.
    private static (double, double)? StepDomain(double min, double max, double step, ChartInput input)
    {
        var lo = input.ValueMin ?? Math.Floor(min / step + 1e-9) * step;
        var hi = input.ValueMax ?? Math.Ceiling(max / step - 1e-9) * step;
        if (!(hi > lo)) hi = lo + step;
        return (hi - lo) / step > 500 ? null : (Math.Round(lo, 10), Math.Round(hi, 10));
    }

    // m × 10^e, exactly as it's written (so 0.001 isn't 0.0010000000000000002).
    private static double Power(int m, int e) => double.Parse($"{m}e{e}", CultureInfo.InvariantCulture);

    /// <summary>
    /// A log scale's ticks: each power of ten, with 2 and 5 times it when the range spans few.
    /// Levels say which to keep when they crowd: the powers before the 2s and 5s, then every
    /// other power, every fourth….
    /// </summary>
    internal static List<(double V, int Level)> LogTicks(double min, double max, double count)
    {
        var lo = (int)Math.Floor(Math.Log10(min) + 1e-9);
        var hi = (int)Math.Ceiling(Math.Log10(max) - 1e-9);
        // 2s and 5s too when the range spans few powers of ten (measured, not counted whole).
        int[] multiples = (Math.Log10(max) - Math.Log10(min)) * 3 <= count + 1 ? [1, 2, 5] : [1];
        var ticks = new List<(double, int)>();
        for (var e = lo; e <= hi; e++)
        {
            foreach (var m in multiples)
            {
                var v = Power(m, e);
                if (v < min * (1 - 1e-9) || v > max * (1 + 1e-9)) continue;
                var level = m != 1 ? 0 : e == 0 ? 6 : 1 + Math.Min(4, System.Numerics.BitOperations.TrailingZeroCount(Math.Abs(e)));
                ticks.Add((v, level));
            }
        }

        return ticks;
    }

    /// <summary>Ticks at each multiple of a step, leveled as a number axis's are (every 1, 2, 10, 20… steps).</summary>
    internal static List<(double V, int Level)> StepTicks(double min, double max, double step)
    {
        var i1 = (long)Math.Ceiling(min / step - 1e-9);
        var i2 = (long)Math.Floor(max / step + 1e-9);
        var ladder = ChartScale.Ladder(1, Math.Max(1, i2 - i1));
        var exact = Math.Max(Math.Abs(min), Math.Abs(max)) < 1e20 && step >= 1e-20;
        var ticks = new List<(double, int)>();
        for (var i = i1; i <= i2; i++)
        {
            var level = 0;
            while (level < ladder.Count - 1 && i % ladder[level + 1] == 0) level++;
            ticks.Add((exact ? (double)((decimal)i * (decimal)step) : i * step, level));
        }

        return ticks;
    }

    /// <summary>The decimals a step is written with, so a step of 0.25 reads 0.25, not 0.3.</summary>
    internal static int StepDecimals(double step)
    {
        if (step >= 1 || step < 1e-10) return 0;
        var d = (decimal)step / 1.000000000000000000000000000000m;
        return Math.Min(10, (decimal.GetBits(d)[3] >> 16) & 0xFF);
    }

    /// <summary>
    /// For each tick, the first of <paramref name="widths"/> widths at which it shows (−1: never):
    /// at each, the finest level whose ticks are <paramref name="needPx"/> apart on a plot that size.
    /// </summary>
    private static int[] FitLevels(IReadOnlyList<(double Pos, int Level)> ticks, int widths, Func<int, double> plotPx, double needPx)
    {
        if (ticks.Count == 0) return [];
        var top = ticks.Max(t => t.Level);
        double Gap(int level)
        {
            var pos = ticks.Where(t => t.Level >= level).Select(t => t.Pos).Order().ToArray();
            var gap = double.PositiveInfinity;
            for (var i = 1; i < pos.Length; i++) gap = Math.Min(gap, pos[i] - pos[i - 1]);
            return gap;
        }

        var needed = new int[widths];
        for (var k = 0; k < widths; k++)
        {
            var level = 0;
            while (level < top && Gap(level) * plotPx(k) < needPx) level++;
            needed[k] = level;
        }

        return ticks.Select(t => Array.FindIndex(needed, l => t.Level >= l)).ToArray();
    }

    private static void StackValues(ChartInput input, bool stacked, bool full, bool log, double?[][] lows, double?[][] highs)
    {
        var n = input.Arguments.Count;
        for (var s = 0; s < input.Series.Count; s++)
        {
            lows[s] = new double?[n];
            highs[s] = input.Series[s].Values.Select(v => v is { } x && double.IsFinite(x) && (!log || x > 0) ? (double?)x : null).ToArray();

            // A waterfall's change starts where the running total was; a total starts from zero.
            if (input.Series[s].Totals is { } totals)
            {
                var sum = 0.0;
                for (var i = 0; i < n; i++)
                {
                    if (highs[s][i] is not { } v) continue;
                    lows[s][i] = totals[i] ? 0 : sum;
                    highs[s][i] = totals[i] ? v : sum + v;
                    sum = highs[s][i]!.Value;
                }
            }
        }

        if (!stacked)
        {
            return;
        }

        foreach (var type in new[] { ChartType.Bar, ChartType.Area })
        {
            var group = Enumerable.Range(0, input.Series.Count).Where(s => input.Series[s].Type == type && !input.Series[s].Hidden).ToList();
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
        if (input.Horizontal ? input.HideArgumentAxis : input.HideValueAxis)
        {
            return GapPx + model.OutsideRoom.X + (input.Horizontal ? 0 : EndsWidth(input));
        }

        if (input.Horizontal)
        {
            var longest = model.ArgumentTexts.Select(t => Width(t)).DefaultIfEmpty(0).Max();
            return Math.Min(longest, 320) + GapPx + model.OutsideRoom.X;
        }

        // The second axis's column, after the plot, takes room as well.
        var second = input.Series.Where(s => s.Secondary).SelectMany(s => s.Values).Where(v => v is not null).Select(v => Math.Abs(v!.Value)).DefaultIfEmpty(-1).Max();
        var secondPx = second < 0 ? 0 : Width(second.ToString(input.SecondaryFormat ?? "N0", input.Culture)) + GapPx;
        var all = input.Series.Where(s => !s.Secondary).SelectMany(s => s.Values).Where(v => v is not null).Select(v => v!.Value).DefaultIfEmpty(0).ToArray();
        var widest = Math.Max(Math.Abs(all.Max()), Math.Abs(all.Min()));
        var sample = input.Stacked == ChartStacking.Percent ? "100%" : input.ValueFormatter?.Invoke(widest) ?? widest.ToString(input.ValueFormat ?? "N0", input.Culture);
        return Width(sample) + (all.Min() < 0 ? Width("-") : 0) + GapPx + secondPx + EndsWidth(input);
    }

    private static double PlotWidth(int k, double columnPx) => Math.Max(60, FitWidth(k) - columnPx - GapPx * 2);

    private static double Width(string text) => TextWidths.AtSize(text, FontPx);

    // Bands: label i shows at stride 1, 2, 4… when i is a multiple of it.
    private static void CategoryLabels(ChartModel model, double[] positions, double columnPx, ChartLabelFit mode)
    {
        var n = positions.Length;
        if (n == 0) return;
        if (mode == ChartLabelFit.Wrap)
        {
            // Every name, wrapped in its band by CSS.
            model.Wrap = true;
            for (var i = 0; i < n; i++)
            {
                model.ArgumentLabels.Add(new AxisLabel(positions[i], model.ArgumentTexts[i], Title: model.ArgumentTexts[i]));
            }

            return;
        }

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

        // Turned 45° at the widths where they don't all fit flat: a name then needs about a line
        // and a half along the axis, and the axis is as tall as the longest turned name.
        if (mode == ChartLabelFit.Rotate && strides[0] > 1)
        {
            var flatFrom = Array.IndexOf(strides, 1);
            model.FlatFrom = flatFrom < 0 ? Widths.Length : flatFrom;
            for (var k = 0; k < model.FlatFrom; k++)
            {
                var slot = PlotWidth(k, columnPx) / n;
                var stride = 1;
                while (stride < n && slot * stride < TurnedPitchPx) stride *= 2;
                strides[k] = stride;
            }

            var turned = model.ArgumentTexts.Select(t => Math.Min(Width(t), TurnedMaxPx)).Max();
            model.TurnedPx = Math.Ceiling((turned + LineHeightPx / 2) * Math.Sqrt(0.5) + LineHeightPx / 2);
            model.LeadPx = Math.Ceiling(Math.Max(0, Math.Min(Width(model.ArgumentTexts[0]), TurnedMaxPx) * Math.Sqrt(0.5) - columnPx));
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
    private static void NumberLabels(List<AxisLabel> labels, double min, double max, double count, string? format, CultureInfo culture, double columnPx, List<GridLine>? grid, Func<double, string>? tickText = null)
    {
        var spec = ChartScale.TickSpec(min, max, count);
        format ??= "N" + ChartScale.Decimals(spec.Step);
        var ladder = ChartScale.Ladder(spec.Factor, Math.Max(1, spec.I2 - spec.I1));
        var ticks = new List<(long I, double V, string Text)>();
        for (var i = spec.I1; i <= spec.I2; i++)
        {
            var v = spec.Value(i);
            ticks.Add((i, v, tickText?.Invoke(v) ?? v.ToString(format, culture)));
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

    private static void TimeLabels(ChartModel model, DateTime min, DateTime max, double count, string? format, CultureInfo culture, double columnPx, Func<DateTime, string>? text = null)
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

        string Text(DateTime t) => text is not null ? text(t) : format is not null ? t.ToString(format, culture) : unit switch
        {
            ChartTime.Unit.Second => t.ToString("T", culture),
            ChartTime.Unit.Minute or ChartTime.Unit.Hour => t.ToString("t", culture),
            ChartTime.Unit.Day => t.Day.ToString(culture),
            ChartTime.Unit.Month => t.ToString("MMM", culture),
            _ => t.ToString("yyyy", culture),
        };

        // The second line: the month (and year) under days, the date under times, the year under months.
        string? Context(DateTime t, DateTime? previous) => format is not null || text is not null ? null : unit switch
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
                // Each value holds until the next one's argument, where it steps.
                segments.Add(new Segment(null, null, (p[i].A, p[i - 1].V)));
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

    internal static double? ToDouble(object? value, ArgumentKind kind) => value switch
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
