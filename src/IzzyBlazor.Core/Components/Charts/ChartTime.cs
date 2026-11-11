namespace IzzyBlazor;

/// <summary>
/// Ticks for a date and time axis: intervals from a second to centuries, each one's ticks a subset
/// of the one before (a month starts on a day, a quarter on a month), so the coarser levels that
/// show at narrow widths are always among the finer ones. D3's interval ladder (d3-scale, ISC),
/// less the week and two-day steps, which months don't line up with, plus 5-day and half-month
/// steps (the 1st, 6th, 11th… and the 1st and 16th).
/// </summary>
internal static class ChartTime
{
    public enum Unit { Second, Minute, Hour, Day, Month, Year }

    public readonly record struct Interval(Unit Unit, int Count, double ApproxTicks);

    private const double Sec = TimeSpan.TicksPerSecond, Min = TimeSpan.TicksPerMinute, Hr = TimeSpan.TicksPerHour, Dy = TimeSpan.TicksPerDay;

    public static readonly Interval[] Ladder =
    [
        new(Unit.Second, 1, Sec), new(Unit.Second, 5, 5 * Sec), new(Unit.Second, 15, 15 * Sec), new(Unit.Second, 30, 30 * Sec),
        new(Unit.Minute, 1, Min), new(Unit.Minute, 5, 5 * Min), new(Unit.Minute, 15, 15 * Min), new(Unit.Minute, 30, 30 * Min),
        new(Unit.Hour, 1, Hr), new(Unit.Hour, 3, 3 * Hr), new(Unit.Hour, 6, 6 * Hr), new(Unit.Hour, 12, 12 * Hr),
        new(Unit.Day, 1, Dy), new(Unit.Day, 5, 5 * Dy), new(Unit.Day, 15, 15.2 * Dy),
        new(Unit.Month, 1, 30.4 * Dy), new(Unit.Month, 3, 91.3 * Dy),
        new(Unit.Year, 1, 365.25 * Dy), new(Unit.Year, 2, 730.5 * Dy), new(Unit.Year, 10, 3652.5 * Dy),
        new(Unit.Year, 20, 7305 * Dy), new(Unit.Year, 100, 36525 * Dy), new(Unit.Year, 200, 73050 * Dy),
        new(Unit.Year, 1000, 365250 * Dy),
    ];

    /// <summary>The finest interval that gives at most <paramref name="count"/> ticks over the span.</summary>
    public static int BaseIndex(long spanTicks, double count)
    {
        for (var i = 0; i < Ladder.Length; i++)
        {
            if (spanTicks / Ladder[i].ApproxTicks <= Math.Max(1, count))
            {
                return i;
            }
        }

        return Ladder.Length - 1;
    }

    /// <summary>Whether a moment falls on a tick of this interval.</summary>
    public static bool IsOn(DateTime t, Interval interval)
    {
        if (t.Millisecond != 0 || t.Ticks % TimeSpan.TicksPerMillisecond != 0)
        {
            return false;
        }

        return interval.Unit switch
        {
            Unit.Second => t.Second % interval.Count == 0,
            Unit.Minute => t.Second == 0 && t.Minute % interval.Count == 0,
            Unit.Hour => t.Second == 0 && t.Minute == 0 && t.Hour % interval.Count == 0,
            Unit.Day => t.TimeOfDay == TimeSpan.Zero && interval.Count switch
            {
                1 => true,
                5 => t.Day <= 26 && (t.Day - 1) % 5 == 0,
                _ => t.Day is 1 or 16,
            },
            Unit.Month => t.TimeOfDay == TimeSpan.Zero && t.Day == 1 && (t.Month - 1) % interval.Count == 0,
            _ => t.TimeOfDay == TimeSpan.Zero && t.Day == 1 && t.Month == 1 && t.Year % interval.Count == 0,
        };
    }

    /// <summary>
    /// Which of an interval's spans a moment falls in (the day, the 5-day stretch, the quarter…):
    /// two moments share a span when their keys are the same.
    /// </summary>
    public static long Key(DateTime t, Interval interval) => interval.Unit switch
    {
        Unit.Second => new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, t.Second - t.Second % interval.Count).Ticks,
        Unit.Minute => new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute - t.Minute % interval.Count, 0).Ticks,
        Unit.Hour => new DateTime(t.Year, t.Month, t.Day, t.Hour - t.Hour % interval.Count, 0, 0).Ticks,
        Unit.Day => interval.Count switch
        {
            1 => t.Date.Ticks,
            5 => new DateTime(t.Year, t.Month, Math.Min(26, t.Day - (t.Day - 1) % 5)).Ticks,
            _ => new DateTime(t.Year, t.Month, t.Day >= 16 ? 16 : 1).Ticks,
        },
        Unit.Month => new DateTime(t.Year, t.Month - (t.Month - 1) % interval.Count, 1).Ticks,
        _ => t.Year / interval.Count,
    };

    /// <summary>The ticks of an interval from min to max, inclusive.</summary>
    public static List<DateTime> Ticks(DateTime min, DateTime max, Interval interval)
    {
        var ticks = new List<DateTime>();
        var t = Floor(min, interval);
        for (var guard = 0; t <= max && guard < 100_000; guard++)
        {
            if (t >= min && IsOn(t, interval))
            {
                ticks.Add(t);
            }

            t = Next(t, interval);
        }

        return ticks;
    }

    private static DateTime Floor(DateTime t, Interval interval) => interval.Unit switch
    {
        Unit.Second => new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, t.Second, t.Kind),
        Unit.Minute => new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0, t.Kind),
        Unit.Hour => new DateTime(t.Year, t.Month, t.Day, t.Hour, 0, 0, t.Kind),
        Unit.Day => t.Date,
        Unit.Month => new DateTime(t.Year, t.Month, 1, 0, 0, 0, t.Kind),
        _ => new DateTime(t.Year, 1, 1, 0, 0, 0, t.Kind),
    };

    // Steps by the interval's unit; IsOn keeps only the ticks on the interval (the 1st, 6th…).
    private static DateTime Next(DateTime t, Interval interval) => interval.Unit switch
    {
        Unit.Second => t.AddSeconds(1),
        Unit.Minute => t.AddMinutes(1),
        Unit.Hour => t.AddHours(1),
        Unit.Day => t.AddDays(1),
        Unit.Month => t.AddMonths(1),
        _ => t.Year >= 9999 ? DateTime.MaxValue : t.AddYears(1),
    };
}
