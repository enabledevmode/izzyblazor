namespace IzzyBlazor;

/// <summary>What a <see cref="Scheduler{TItem}"/> shows: days in time columns, a month, or a list.</summary>
public enum SchedulerView
{
    /// <summary>One day, in a column of hours.</summary>
    Day,

    /// <summary>Seven days from the week's first day, a column of hours each.</summary>
    Week,

    /// <summary>The working days of the week (Monday to Friday), a column of hours each.</summary>
    WorkWeek,

    /// <summary>The month's weeks, six rows of days, each listing its appointments.</summary>
    Month,

    /// <summary>The appointments of the coming days as a list, by day.</summary>
    Agenda,
}

/// <summary>An appointment as the layout sees it: its place in the items, its times, and whether it's all day.</summary>
internal readonly record struct SchedulerEntry(int Index, DateTime Start, DateTime End, bool AllDay)
{
    /// <summary>
    /// A bar across days rather than a box in a day's column: all day, or a day long or more. A
    /// shorter one that crosses midnight is a box in each day it touches.
    /// </summary>
    public bool IsBar => AllDay || End - Start >= TimeSpan.FromDays(1);

    /// <summary>The first day it covers.</summary>
    public DateOnly FirstDay => DateOnly.FromDateTime(Start);

    /// <summary>The last day it covers: an end at midnight belongs to the day before.</summary>
    public DateOnly LastDay => End <= Start ? FirstDay : DateOnly.FromDateTime(End == End.Date ? End.AddTicks(-1) : End);
}

/// <summary>A timed appointment's box in a day's column, in shares of the day and of the column.</summary>
internal readonly record struct SchedulerBox(int Index, int Day, double Top, double Height, double Left, double Width, bool StartsBefore, bool EndsAfter);

/// <summary>An all-day or several-day appointment's bar across a row of days, in a lane under the day's header.</summary>
internal readonly record struct SchedulerBar(int Index, int FromDay, int ToDay, int Lane, bool StartsBefore, bool EndsAfter);

/// <summary>Where a <see cref="Scheduler{TItem}"/>'s appointments go: pure, so its tests need no rendering.</summary>
internal static class SchedulerLayout
{
    /// <summary>The days a view shows around a date: its first day and how many.</summary>
    public static (DateOnly First, int Days) Range(SchedulerView view, DateOnly date, DayOfWeek firstDay, int agendaDays) => view switch
    {
        SchedulerView.Day => (date, 1),
        SchedulerView.Week => (WeekStart(date, firstDay), 7),
        SchedulerView.WorkWeek => (WeekStart(date, DayOfWeek.Monday), 5),
        SchedulerView.Month => (WeekStart(new DateOnly(date.Year, date.Month, 1), firstDay), 42),
        _ => (date, Math.Max(1, agendaDays)),
    };

    /// <summary>The date a view's previous or next page opens on.</summary>
    public static DateOnly Step(SchedulerView view, DateOnly date, int by, int agendaDays) => view switch
    {
        SchedulerView.Day => date.AddDays(by),
        SchedulerView.Week or SchedulerView.WorkWeek => date.AddDays(7 * by),
        SchedulerView.Month => new DateOnly(date.Year, date.Month, 1).AddMonths(by),
        _ => date.AddDays(Math.Max(1, agendaDays) * by),
    };

    public static DateOnly WeekStart(DateOnly date, DayOfWeek firstDay) => date.AddDays(-(((int)date.DayOfWeek - (int)firstDay + 7) % 7));

    /// <summary>
    /// The bars across a row of days: each in the first lane free over its days, the longest
    /// first among those starting together, so a long bar stays above the short ones.
    /// </summary>
    public static List<SchedulerBar> Bars(IEnumerable<SchedulerEntry> entries, DateOnly first, int days)
    {
        var last = first.AddDays(days - 1);
        var lanes = new List<bool[]>();
        var bars = new List<SchedulerBar>();
        var order = entries.Where(e => e.IsBar)
            .Select(e => (Entry: e, From: e.FirstDay, To: e.LastDay))
            .Where(e => e.To >= first && e.From <= last)
            .OrderBy(e => e.From).ThenByDescending(e => e.To.DayNumber - e.From.DayNumber).ThenBy(e => e.Entry.Index);
        foreach (var (entry, from, to) in order)
        {
            var a = Math.Max(0, from.DayNumber - first.DayNumber);
            var b = Math.Min(days - 1, to.DayNumber - first.DayNumber);
            var lane = lanes.FindIndex(l => !l.Skip(a).Take(b - a + 1).Any(x => x));
            if (lane < 0)
            {
                lanes.Add(new bool[days]);
                lane = lanes.Count - 1;
            }

            for (var d = a; d <= b; d++) lanes[lane][d] = true;
            bars.Add(new SchedulerBar(entry.Index, a, b, lane, from < first, to > last));
        }

        return bars;
    }

    /// <summary>
    /// The timed appointments in each day's column: overlapping ones side by side, each in the
    /// first column free when it starts, as wide as its group of overlaps allows.
    /// </summary>
    public static List<SchedulerBox> Boxes(IEnumerable<SchedulerEntry> entries, DateOnly first, int days)
    {
        var boxes = new List<SchedulerBox>();
        var timed = entries.Where(e => !e.IsBar).ToList();
        for (var d = 0; d < days; d++)
        {
            var dayStart = first.AddDays(d).ToDateTime(TimeOnly.MinValue);
            var dayEnd = dayStart.AddDays(1);
            var own = timed
                .Where(e => e.Start < dayEnd && (e.End > dayStart || e.End == e.Start && e.Start >= dayStart))
                .Select(e => (e.Index, From: e.Start < dayStart ? dayStart : e.Start, To: e.End > dayEnd ? dayEnd : e.End, Before: e.Start < dayStart, After: e.End > dayEnd))
                // A box is at least a quarter hour tall, so a zero-length or short one can be seen and overlaps as drawn.
                .Select(e => e with { To = e.To < e.From.AddMinutes(15) ? e.From.AddMinutes(15) : e.To })
                .OrderBy(e => e.From).ThenByDescending(e => e.To).ThenBy(e => e.Index)
                .ToList();

            var group = new List<(int Box, int Column)>();
            var columnEnds = new List<DateTime>();
            var groupEnd = DateTime.MinValue;
            void Close()
            {
                foreach (var (box, column) in group)
                {
                    var b = boxes[box];
                    boxes[box] = b with { Left = (double)column / columnEnds.Count, Width = 1.0 / columnEnds.Count };
                }

                group.Clear();
                columnEnds.Clear();
            }

            foreach (var e in own)
            {
                if (e.From >= groupEnd) Close();
                var column = columnEnds.FindIndex(end => end <= e.From);
                if (column < 0)
                {
                    columnEnds.Add(e.To);
                    column = columnEnds.Count - 1;
                }
                else
                {
                    columnEnds[column] = e.To;
                }

                groupEnd = e.To > groupEnd ? e.To : groupEnd;
                var top = (e.From - dayStart).TotalMinutes / 1440;
                boxes.Add(new SchedulerBox(e.Index, d, top, Math.Min(1 - top, (e.To - e.From).TotalMinutes / 1440), 0, 1, e.Before, e.After));
                group.Add((boxes.Count - 1, column));
            }

            Close();
            groupEnd = DateTime.MinValue;
        }

        return boxes;
    }

    /// <summary>
    /// The appointments on a day, the all-day and several-day ones first, then by time: a month's
    /// cell takes the timed ones (its bars are drawn across the row), the agenda all of them.
    /// </summary>
    public static List<SchedulerEntry> OnDay(IEnumerable<SchedulerEntry> entries, DateOnly day, bool withBars)
    {
        var start = day.ToDateTime(TimeOnly.MinValue);
        var end = start.AddDays(1);
        return entries
            .Where(e => withBars || !e.IsBar)
            .Where(e => e.IsBar
                ? e.FirstDay <= day && e.LastDay >= day
                : e.Start < end && (e.End > start || e.Start >= start))
            .OrderBy(e => e.IsBar ? 0 : 1).ThenBy(e => e.Start).ThenBy(e => e.Index)
            .ToList();
    }
}
