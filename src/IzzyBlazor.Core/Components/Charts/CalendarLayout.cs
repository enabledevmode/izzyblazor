using System.Globalization;

namespace IzzyBlazor;

/// <summary>What a calendar heatmap is laid out from: each item's day and value, the range, and the scale (as a heatmap's).</summary>
internal sealed class CalendarInput
{
    /// <summary>Each item's day, or null for an item with none (left out).</summary>
    public required IReadOnlyList<DateOnly?> Dates { get; init; }
    public required IReadOnlyList<double?> Values { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public DayOfWeek? FirstDayOfWeek { get; init; }

    /// <summary>The largest a day grows to, in pixels at a 16px root (CellSize).</summary>
    public double MaxCellPx { get; init; } = 32;

    /// <summary>The color scale's settings (Steps, palette, Midpoint, Min, Max, ranges, ValueFormat, Culture); its columns and rows are unused.</summary>
    public required HeatmapInput Scale { get; init; }
}

/// <summary>A calendar heatmap laid out: a heatmap's grid, seven rows of weekdays by a column per week.</summary>
internal sealed class CalendarModel
{
    public required HeatmapModel Heat { get; init; }

    /// <summary>Columns: one per week, from the week holding the first day.</summary>
    public int Weeks { get; init; }

    /// <summary>The first column's first day (before the range's first day when that isn't on the week's first day).</summary>
    public DateOnly Start { get; init; }
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }

    /// <summary>Each cell's day as text, such as "Tuesday, March 4, 2025", or null outside the range.</summary>
    public string?[] DateTexts { get; init; } = [];

    /// <summary>Which weekday names stay when cells are shorter than a line: Monday, Wednesday and Friday.</summary>
    public bool[] RowKept { get; init; } = [];

    /// <summary>The narrowest of <see cref="ChartLayout.Widths"/> every weekday name shows at (8: only the kept ones).</summary>
    public int RowsFrom { get; init; }

    /// <summary>The narrowest width the kept names (two rows apart) show at; below it, none (8: never).</summary>
    public int NamesFrom { get; init; }

    /// <summary>The months in the range, in order: the first day of each (or the range's first day), for the data table.</summary>
    public List<DateOnly> Months { get; } = [];

    public DateOnly DateOf(int cell) => Start.AddDays(cell % Weeks * 7 + cell / Weeks);

    /// <summary>A day's cell, in row order (rows are weekdays, columns weeks).</summary>
    public int CellOf(DateOnly day)
    {
        var n = day.DayNumber - Start.DayNumber;
        return n % 7 * Weeks + n / 7;
    }
}

/// <summary>
/// Lays a calendar heatmap out with no width known: a column per week, a row per weekday from the
/// culture's first day (or the one given), cells square and at most CellSize (2rem); months named along the
/// top at the first week that starts in them, thinning to every second, third, sixth or twelfth
/// month where they crowd. The colors are a heatmap's (<see cref="HeatmapLayout.Scale"/>).
/// </summary>
internal static class CalendarLayout
{
    private static readonly int[] MonthStrides = [1, 2, 3, 6, 12];

    public static CalendarModel Build(CalendarInput input)
    {
        var culture = input.Scale.Culture;
        var firstDay = input.FirstDayOfWeek ?? culture.DateTimeFormat.FirstDayOfWeek;

        // Each day's item: the last one for it.
        var byDay = new Dictionary<DateOnly, int>();
        for (var i = 0; i < input.Dates.Count; i++)
        {
            if (input.Dates[i] is { } d) byDay[d] = i;
        }

        var from = input.From ?? (byDay.Count > 0 ? byDay.Keys.Min() : DateOnly.FromDateTime(DateTime.Today));
        var to = input.To ?? (byDay.Count > 0 ? byDay.Keys.Max() : from);
        // A To before the items' first day (or today, with none) moves the start back to it.
        if (to < from)
        {
            if (input.From is null) from = to;
            else to = from;
        }
        var start = from.AddDays(-(((int)from.DayOfWeek - (int)firstDay + 7) % 7));
        var weeks = (to.DayNumber - start.DayNumber) / 7 + 1;

        var rowTexts = Enumerable.Range(0, 7).Select(r => culture.DateTimeFormat.GetAbbreviatedDayName((DayOfWeek)(((int)firstDay + r) % 7))).ToArray();
        var rowKept = Enumerable.Range(0, 7).Select(r => (((int)firstDay + r) % 7) is 1 or 3 or 5).ToArray();

        var heat = new HeatmapModel
        {
            RowTexts = rowTexts,
            RowShown = rowKept,
            LabelFrom = 8,
        };

        var count = weeks * 7;
        var items = new int[count];
        var values = new double?[count];
        var texts = new string?[count];
        var dateTexts = new string?[count];
        var outside = new bool[count];
        var rowColumnPx = rowTexts.Select(HeatmapLayout.Width).DefaultIfEmpty(0).Max() + HeatmapLayout.LabelGapPx;
        // A row's height and gap: the gaps go under 6px days.
        double Pitch(int k)
        {
            var cell = Math.Min(input.MaxCellPx, HeatmapLayout.CellPx(k, weeks, rowColumnPx));
            return cell + (cell >= HeatmapLayout.DenseCellPx ? HeatmapLayout.GapPx : 0);
        }

        var model = new CalendarModel
        {
            Heat = heat,
            Weeks = weeks,
            Start = start,
            From = from,
            To = to,
            DateTexts = dateTexts,
            RowKept = rowKept,
            RowsFrom = HeatmapLayout.FirstFit(k => Pitch(k) >= HeatmapLayout.LinePx),
            NamesFrom = HeatmapLayout.FirstFit(k => 2 * Pitch(k) >= HeatmapLayout.FontPx + 1),
        };
        for (var c = 0; c < count; c++)
        {
            var day = model.DateOf(c);
            items[c] = -1;
            if (day < from || day > to)
            {
                outside[c] = true;
                continue;
            }

            dateTexts[c] = day.ToString("D", culture);
            if (byDay.TryGetValue(day, out var i))
            {
                items[c] = i;
                if (input.Values[i] is { } v && double.IsFinite(v))
                {
                    values[c] = v;
                    texts[c] = v.ToString(input.Scale.ValueFormat ?? "#,##0.##", culture);
                }
            }
        }

        var scale = HeatmapLayout.Scale(input.Scale, heat, values.OfType<double>().ToArray());
        heat.Cells = new HeatmapCell[count];
        for (var c = 0; c < count; c++)
        {
            heat.Cells[c] = outside[c]
                ? new HeatmapCell(-1, null, null, -1, 0, false, 0, Out: true)
                : values[c] is { } v ? scale(items[c], v, texts[c]) : new HeatmapCell(items[c], null, null, -1, 0, false, 0);
        }

        heat.GapFrom = HeatmapLayout.FirstFit(k => Math.Min(input.MaxCellPx, HeatmapLayout.CellPx(k, weeks, rowColumnPx)) >= HeatmapLayout.DenseCellPx);

        for (var m = new DateOnly(from.Year, from.Month, 1); m <= to; m = m.AddMonths(1))
        {
            model.Months.Add(m < from ? from : m);
        }

        MonthTicks(model, culture, Pitch);
        return model;
    }

    // Each month named at the first week that starts in it (the first month at the first week,
    // when its next is three weeks off or more); every month where they fit, else every second,
    // third (quarters), sixth or twelfth, counted from January. January names the year when it
    // isn't the first name.
    private static void MonthTicks(CalendarModel model, CultureInfo culture, Func<int, double> pitch)
    {
        var heat = model.Heat;
        var weeks = model.Weeks;
        var marks = new List<(int Week, DateOnly Month)>();
        foreach (var month in model.Months)
        {
            var first = new DateOnly(month.Year, month.Month, 1);
            var week = first <= model.Start ? 0 : (first.DayNumber - model.Start.DayNumber + 6) / 7;
            // No room for a name in the last column (or past the grid).
            if (week >= weeks || (week == weeks - 1 && weeks > 1)) continue;
            marks.Add((week, first));
        }

        if (marks.Count > 1 && marks[1].Week - marks[0].Week < 3) marks.RemoveAt(0);

        var texts = marks.Select((m, j) => m.Month.Month == 1 && j > 0 ? m.Month.ToString("yyyy", culture) : m.Month.ToString("MMM", culture)).ToArray();
        var need = texts.Select(HeatmapLayout.Width).DefaultIfEmpty(0).Max() + HeatmapLayout.LabelGapPx;
        var strides = new int[ChartLayout.Widths.Length];
        for (var k = 0; k < strides.Length; k++)
        {
            var s = Array.Find(MonthStrides, s => pitch(k) * 4.35 * s >= need);
            strides[k] = s == 0 ? 12 : s;
        }

        for (var j = 0; j < marks.Count; j++)
        {
            var month = marks[j].Month.Month - 1;
            var from = Array.FindIndex(strides, s => month % s == 0);
            if (from >= 0) heat.Ticks.Add(new HeatmapTick((double)marks[j].Week / weeks, texts[j], from));
        }
    }
}
