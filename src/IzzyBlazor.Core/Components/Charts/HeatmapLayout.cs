using System.Globalization;

namespace IzzyBlazor;

/// <summary>What a heatmap is laid out from: each item's column, row and value, read by the chart.</summary>
internal sealed class HeatmapInput
{
    public required IReadOnlyList<object?> Columns { get; init; }
    public required IReadOnlyList<object?> Rows { get; init; }
    public required IReadOnlyList<double?> Values { get; init; }
    public string? ColumnFormat { get; init; }
    public string? RowFormat { get; init; }
    public string? ValueFormat { get; init; }
    public CultureInfo Culture { get; init; } = CultureInfo.CurrentCulture;

    /// <summary>Classes: 2 to 9, or 0 for a continuous blend. Ignored with <see cref="Ranges"/> or a palette of fixed colors.</summary>
    public int Steps { get; init; } = 5;

    /// <summary>Fixed colors' count (ChartPalette.Of): that many classes.</summary>
    public int? FixedColors { get; init; }

    public bool Diverging { get; init; }
    public double Midpoint { get; init; }
    public double? Min { get; init; }
    public double? Max { get; init; }

    /// <summary>HeatmapRange classes, in order.</summary>
    public IReadOnlyList<(double From, double To)>? Ranges { get; init; }

    public ChartLabels Labels { get; init; } = ChartLabels.Auto;

    /// <summary>A cell's height in pixels, when known (at a 16px root), for thinning row names.</summary>
    public double CellHeightPx { get; init; } = 32;
}

/// <summary>One cell of the grid, in row-major order.</summary>
/// <param name="Item">The item drawn there (the last for its column and row), or -1 for none.</param>
/// <param name="Value">Its value, or null for no data.</param>
/// <param name="Text">The value as text, or null.</param>
/// <param name="Class">The color class from 0, or -1 for no data (or, with ranges, a value in none of them).</param>
/// <param name="T">On a continuous scale, 0 to 1 along it (a diverging one's side: <see cref="Low"/>).</param>
/// <param name="Low">On a diverging scale, below the midpoint.</param>
/// <param name="Ink">0 for the usual text, 1 for light text in light mode, 2 for light in light mode and dark in dark.</param>
/// <param name="Out">On a calendar, a day outside its range in the first or last week: not drawn.</param>
internal readonly record struct HeatmapCell(int Item, double? Value, string? Text, int Class, double T, bool Low, int Ink, bool Out = false);

/// <summary>A column name along the bottom: its place (0 to 1, the cell's middle) and the narrowest width it shows at.</summary>
internal readonly record struct HeatmapTick(double Pos, string Text, int From);

/// <summary>A class in the legend: its bounds as text, or a range's label.</summary>
internal readonly record struct HeatmapClass(string? From, string? To, string? Label);

internal sealed class HeatmapModel
{
    public string[] ColumnTexts { get; init; } = [];
    public string[] RowTexts { get; init; } = [];
    public HeatmapCell[] Cells { get; set; } = [];
    public List<HeatmapTick> Ticks { get; } = [];

    /// <summary>Which row names show: every one, or every second, fourth… when rows are shorter than a line.</summary>
    public bool[] RowShown { get; init; } = [];

    /// <summary>The narrowest of <see cref="ChartLayout.Widths"/> cell labels show at (8: never, unless the script finds room).</summary>
    public int LabelFrom { get; set; }

    /// <summary>The narrowest width the gaps between cells show at (8: never).</summary>
    public int GapFrom { get; set; }

    /// <summary>The classes, or empty for a continuous scale.</summary>
    public List<HeatmapClass> Classes { get; } = [];

    /// <summary>A class's bound, in place between classes, for the legend (n + 1 of them).</summary>
    public List<string> Bounds { get; } = [];

    /// <summary>A continuous scale's ends and (diverging) middle as text.</summary>
    public string? LowText { get; set; }
    public string? MidText { get; set; }
    public string? HighText { get; set; }

    public int ClassCount => Classes.Count;
    public bool Continuous => Classes.Count == 0;
}

/// <summary>
/// Lays a heatmap out with no width known, as <see cref="ChartLayout"/> does a chart: the cells are
/// an HTML grid that stretches, and what crowds at a narrow width (column names, the values in
/// cells, the gaps between cells) is marked with the narrowest of <see cref="ChartLayout.Widths"/>
/// at which it fits, estimated from Inter's widths, for container queries in heatmap.css.
/// </summary>
internal static class HeatmapLayout
{
    internal const double FontPx = 12, LabelGapPx = 8, GapPx = 2, CellPadPx = 8, LinePx = 16, DenseCellPx = 6;

    public static HeatmapModel Build(HeatmapInput input)
    {
        var culture = input.Culture;

        // Columns and rows in first-seen order.
        var columnKeys = Distinct(input.Columns);
        var rowKeys = Distinct(input.Rows);
        var columnIndex = Index(columnKeys);
        var rowIndex = Index(rowKeys);
        var dates = columnKeys.Count > 0 && columnKeys.All(k => k is DateTime or DateOnly or DateTimeOffset);
        var columnTexts = dates && input.ColumnFormat is null
            ? DateTexts(columnKeys.Select(ToDate).ToArray(), culture)
            : columnKeys.Select(k => ChartLayout.ArgumentText(k, input.ColumnFormat, culture)).ToArray();
        var rowTexts = rowKeys.Select(k => ChartLayout.ArgumentText(k, input.RowFormat, culture)).ToArray();
        var cols = columnKeys.Count;
        var rows = rowKeys.Count;

        // Each cell's item: the last for its column and row.
        var items = new int[cols * rows];
        Array.Fill(items, -1);
        for (var i = 0; i < input.Values.Count; i++)
        {
            items[rowIndex[Key(input.Rows[i])] * cols + columnIndex[Key(input.Columns[i])]] = i;
        }

        var values = items.Select(i => i >= 0 && input.Values[i] is { } v && double.IsFinite(v) ? v : (double?)null).ToArray();
        var texts = values.Select(v => v?.ToString(input.ValueFormat ?? "#,##0.##", culture)).ToArray();

        var model = new HeatmapModel
        {
            ColumnTexts = columnTexts,
            RowTexts = rowTexts,
            RowShown = RowNames(rows, input.CellHeightPx),
        };

        var scale = Scale(input, model, values.OfType<double>().ToArray());
        model.Cells = new HeatmapCell[cols * rows];
        for (var c = 0; c < model.Cells.Length; c++)
        {
            model.Cells[c] = values[c] is { } v ? scale(items[c], v, texts[c]) : new HeatmapCell(items[c], null, null, -1, 0, false, 0);
        }

        var rowColumnPx = rowTexts.Select(Width).DefaultIfEmpty(0).Max() + LabelGapPx;
        if (dates && input.ColumnFormat is null)
        {
            DateTicks(model, columnKeys.Select(ToDate).ToArray(), rowColumnPx);
        }
        else
        {
            CategoryTicks(model, rowColumnPx);
        }

        // Cell labels: shown where the widest value fits the narrowest cell, a line tall.
        var widest = texts.Where(t => t is not null).Select(t => Width(t!)).DefaultIfEmpty(0).Max() + CellPadPx;
        model.LabelFrom = input.Labels switch
        {
            ChartLabels.Inside => 0,
            ChartLabels.None => 8,
            _ when input.CellHeightPx < LinePx => 8,
            _ => FirstFit(k => CellPx(k, cols, rowColumnPx) >= widest),
        };
        model.GapFrom = FirstFit(k => CellPx(k, cols, rowColumnPx) >= DenseCellPx);
        return model;
    }

    /// <summary>
    /// Throws for settings a heatmap would ignore: HeatmapRanges set their own colors and bounds,
    /// and ChartPalette.Of's colors set how many classes there are.
    /// </summary>
    internal static void CheckScale(string chart, int ranges, ChartPalette? palette, int steps, double? min, double? max, double? midpoint)
    {
        if (ranges > 0 && (palette is not null || steps != 5 || min is not null || max is not null || midpoint is not null))
        {
            throw new InvalidOperationException($"A {chart}'s HeatmapRanges set their own colors and bounds: leave out Palette, Steps, Min, Max and Midpoint.");
        }

        if (palette?.FixedCount > 9)
        {
            throw new InvalidOperationException($"A {chart}'s ChartPalette.Of takes up to nine colors: past that, they're hard to tell apart.");
        }

        if (palette?.FixedCount is { } count && steps != 5)
        {
            throw new InvalidOperationException($"A {chart} with ChartPalette.Of has a class per color ({count} here): leave out Steps.");
        }
    }

    internal static int FirstFit(Func<int, bool> fits)
    {
        for (var k = 0; k < ChartLayout.Widths.Length; k++)
        {
            if (fits(k)) return k;
        }

        return 8;
    }

    // A cell's width at each fitted width: the grid is the chart less the row names.
    internal static double CellPx(int k, int cols, double rowColumnPx) =>
        cols == 0 ? 0 : (Math.Max(60, (k == 0 ? 280 : ChartLayout.Widths[k]) - rowColumnPx) - GapPx * (cols - 1)) / cols;

    internal static double Width(string text) => TextWidths.AtSize(text, FontPx);

    /// <summary>
    /// The color scale, and a function that gives each value its cell. Classes are equal steps
    /// from round bounds (a diverging scale's centered on the midpoint, as wide on both sides), or
    /// the ranges given; a continuous scale is a place along the same span.
    /// </summary>
    internal static Func<int, double, string?, HeatmapCell> Scale(HeatmapInput input, HeatmapModel model, double[] values)
    {
        var culture = input.Culture;
        string Text(double v) => v.ToString(input.ValueFormat ?? "#,##0.##", culture);

        // Where contrast-color() isn't supported: inked light where the shade is strong (light
        // mode, from a little under the middle), and dark in dark mode only at the strongest,
        // where the shades turn light.
        static int Ink(double strength) => strength >= 0.65 ? 2 : strength >= 0.4 ? 1 : 0;

        if (input.Ranges is { Count: > 0 } ranges)
        {
            foreach (var (from, to) in ranges)
            {
                model.Classes.Add(new HeatmapClass(Text(from), Text(to), null));
            }

            return (item, v, text) =>
            {
                // From is in its range and To isn't, but the last range keeps its To.
                var k = -1;
                for (var i = 0; i < ranges.Count && k < 0; i++)
                {
                    if (v >= ranges[i].From && (v < ranges[i].To || (i == ranges.Count - 1 && v == ranges[i].To))) k = i;
                }

                // Ranges go light to dark, so their order stands in for their strength.
                return new HeatmapCell(item, v, text, k, 0, false, k < 0 ? 0 : Ink((k + 0.5) / ranges.Count));
            };
        }

        var dataMin = values.Length > 0 ? values.Min() : 0;
        var dataMax = values.Length > 0 ? values.Max() : 0;
        var n = input.FixedColors ?? input.Steps;
        double lo, hi;
        if (input.Diverging)
        {
            var mid = input.Midpoint;
            var reach = Math.Max(Math.Abs((input.Min ?? dataMin) - mid), Math.Abs((input.Max ?? dataMax) - mid));
            if (!(reach > 0)) reach = 1;
            if (n > 0 && input.Min is null && input.Max is null && input.FixedColors is null)
            {
                // Round classes, as many on each side, about Steps in all.
                var (width, count) = Nice(reach / (n / 2.0), w => 2 * (int)Math.Ceiling(reach / w - 1e-9), n);
                n = count;
                reach = width * n / 2;
            }

            lo = mid - reach;
            hi = mid + reach;
        }
        else
        {
            lo = input.Min ?? dataMin;
            hi = input.Max ?? dataMax;
            if (!(hi > lo))
            {
                // Only Min, with every value at or under it: they all take the first class.
                // Only Max, with every value at or over it: the last. One value (or none) with
                // neither: a span around it, so it takes the middle of the scale.
                if (input.Min is not null && input.Max is null) hi = lo + 1;
                else if (input.Max is not null && input.Min is null) lo = hi - 1;
                else
                {
                    lo -= 1;
                    hi += 1;
                }
            }
            else if (n > 0 && input.Min is null && input.Max is null && input.FixedColors is null)
            {
                // Round bounds around the values, about Steps classes of a round width (one
                // more or fewer, as d3's nice quantize scales give): 6 to 55 in five steps is
                // 0 to 60 in six classes of 10, not 0 to 100 in five of 20.
                var low = lo;
                var high = hi;
                var (width, count) = Nice((hi - lo) / n, w => (int)Math.Ceiling((high - Math.Floor(low / w + 1e-9) * w) / w - 1e-9), n);
                var start = Math.Floor(lo / width + 1e-9) * width;
                n = Math.Max(2, count);
                lo = start;
                hi = start + width * n;
            }
        }

        if (n == 0)
        {
            model.LowText = Text(lo);
            model.HighText = Text(hi);
            if (input.Diverging) model.MidText = Text(input.Midpoint);
            return (item, v, text) =>
            {
                var c = Math.Clamp(v, lo, hi);
                if (input.Diverging)
                {
                    var p = (c - input.Midpoint) / (hi - input.Midpoint);
                    return new HeatmapCell(item, v, text, 0, Math.Abs(p), p < 0, Ink(Math.Abs(p)));
                }

                var t = (c - lo) / (hi - lo);
                return new HeatmapCell(item, v, text, 0, t, false, Ink(t));
            };
        }

        var step = (hi - lo) / n;
        for (var i = 0; i < n; i++)
        {
            model.Classes.Add(new HeatmapClass(Text(lo + step * i), Text(lo + step * (i + 1)), null));
        }

        for (var i = 0; i <= n; i++)
        {
            model.Bounds.Add(Text(lo + step * i));
        }

        return (item, v, text) =>
        {
            var k = Math.Clamp((int)Math.Floor((Math.Clamp(v, lo, hi) - lo) / step), 0, n - 1);
            var strength = input.Diverging
                ? Math.Abs((k + 0.5) / n * 2 - 1)
                : (k + 0.5) / n;
            return new HeatmapCell(item, v, text, k, 0, false, Ink(strength));
        };
    }

    /// <summary>
    /// A round class width near <paramref name="raw"/> and the classes it makes: of the round
    /// widths either side of it, the one whose count is nearest <paramref name="target"/> (the
    /// fewer on a tie), at most nine.
    /// </summary>
    private static (double Width, int Count) Nice(double raw, Func<double, int> count, int target)
    {
        var up = ChartLayout.NiceStep(raw);
        var down = ChartLayout.NiceStep(up * 0.4);
        while (ChartLayout.NiceStep(down, up: true) < up) down = ChartLayout.NiceStep(down, up: true);
        var best = (Width: up, Count: count(up));
        while (best.Count > 9)
        {
            var wider = ChartLayout.NiceStep(best.Width, up: true);
            best = (wider, count(wider));
        }

        var finer = count(down);
        if (finer <= 9 && Math.Abs(finer - target) < Math.Abs(best.Count - target)) best = (down, finer);
        return (best.Width, Math.Max(2, best.Count));
    }

    // Row names a line apart at the least: every one, or every second, fourth….
    private static bool[] RowNames(int rows, double cellPx)
    {
        var stride = 1;
        while (stride < rows && stride * (cellPx + GapPx) < LinePx) stride *= 2;
        return Enumerable.Range(0, rows).Select(r => r % stride == 0).ToArray();
    }

    // Names: every one where they fit, else every second, fourth… from the first.
    private static void CategoryTicks(HeatmapModel model, double rowColumnPx)
    {
        var n = model.ColumnTexts.Length;
        if (n == 0) return;
        var widest = model.ColumnTexts.Select(t => Math.Min(Width(t), 120)).Max() + LabelGapPx;
        var strides = new int[ChartLayout.Widths.Length];
        for (var k = 0; k < strides.Length; k++)
        {
            var slot = CellPx(k, n, rowColumnPx) + GapPx;
            var stride = 1;
            while (stride < n && slot * stride < widest) stride *= 2;
            strides[k] = stride;
        }

        for (var i = 0; i < n; i++)
        {
            var from = Array.FindIndex(strides, s => i % s == 0);
            if (from >= 0) model.Ticks.Add(new HeatmapTick((i + 0.5) / n, model.ColumnTexts[i], from));
        }
    }

    // Dates a day apart or less over more than a month: only the months are named (and coarser
    // steps of them, quarters and years, where months crowd). Otherwise each column, as names.
    private static void DateTicks(HeatmapModel model, DateTime[] dates, double rowColumnPx)
    {
        var n = dates.Length;
        // Hours over two days are many columns but not a month: they're named as times.
        var span = (dates[^1] - dates[0]).TotalDays;
        var daily = n > 31 && span > 31 && span / (n - 1) <= 7.5;
        if (!daily)
        {
            CategoryTicks(model, rowColumnPx);
            return;
        }

        var month = Array.FindIndex(ChartTime.Ladder, l => l.Unit == ChartTime.Unit.Month);
        var starts = Enumerable.Range(0, n).Where(i => i == 0 ? dates[0].Day <= 7 : dates[i].Month != dates[i - 1].Month).ToArray();
        var levels = starts.Select(i =>
        {
            var first = new DateTime(dates[i].Year, dates[i].Month, 1);
            var level = 0;
            while (month + level + 1 < ChartTime.Ladder.Length && ChartTime.IsOn(first, ChartTime.Ladder[month + level + 1])) level++;
            return level;
        }).ToArray();
        var texts = starts.Select(i => dates[i].Month == 1 && i > 0 ? dates[i].ToString("yyyy", CultureInfo.CurrentCulture) : dates[i].ToString("MMM", CultureInfo.CurrentCulture)).ToArray();
        var need = texts.Select(Width).DefaultIfEmpty(0).Max() + LabelGapPx;
        var needed = new int[ChartLayout.Widths.Length];
        for (var k = 0; k < needed.Length; k++)
        {
            var px = (CellPx(k, n, rowColumnPx) + GapPx) * 30.4 * n / Math.Max(1, (dates[^1] - dates[0]).TotalDays + 1);
            var level = 0;
            while (month + level + 1 < ChartTime.Ladder.Length && ChartTime.Ladder[month + level].ApproxTicks / TimeSpan.TicksPerDay / 30.4 * px < need) level++;
            needed[k] = level;
        }

        for (var j = 0; j < starts.Length; j++)
        {
            var from = Array.FindIndex(needed, l => levels[j] >= l);
            if (from >= 0) model.Ticks.Add(new HeatmapTick((starts[j] + 0.5) / n, texts[j], from));
        }
    }

    // Each date as a column name, for the tooltip and the table: the time for hours, the month
    // and day for days and weeks, the month for months, the year for years.
    private static string[] DateTexts(DateTime[] dates, CultureInfo culture)
    {
        var n = dates.Length;
        var spacing = n > 1 ? (dates[^1] - dates[0]).TotalDays / (n - 1) : 1;
        return dates.Select((d, i) => spacing switch
        {
            < 0.99 => d.ToString("t", culture),
            < 20 => d.ToString("MMM d", culture),
            < 300 => d.ToString(i == 0 || d.Year != dates[i - 1].Year && d.Month == 1 ? "MMM yyyy" : "MMM", culture),
            _ => d.ToString("yyyy", culture),
        }).ToArray();
    }

    private static DateTime ToDate(object? value) => value switch
    {
        DateTime d => d,
        DateOnly d => d.ToDateTime(TimeOnly.MinValue),
        DateTimeOffset d => d.DateTime,
        _ => default,
    };

    // Keys compared by value; null as a key of its own.
    private static object Key(object? value) => value ?? DBNull.Value;

    private static List<object?> Distinct(IReadOnlyList<object?> keys)
    {
        var seen = new HashSet<object>();
        var list = new List<object?>();
        foreach (var k in keys)
        {
            if (seen.Add(Key(k))) list.Add(k);
        }

        return list;
    }

    private static Dictionary<object, int> Index(List<object?> keys)
    {
        var index = new Dictionary<object, int>();
        for (var i = 0; i < keys.Count; i++) index[Key(keys[i])] = i;
        return index;
    }
}
