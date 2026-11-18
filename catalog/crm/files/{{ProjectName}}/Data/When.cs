using System.Globalization;

namespace {{RootNamespace}}.Data;

/// <summary>
/// Dates and times as the app writes them, against the workspace's today (<see cref="Clock"/>): "Today · 11:00 AM"
/// for a task, "Yesterday" in a timeline, "2:00–2:45 PM" for a meeting.
/// </summary>
public static class When
{
    /// <summary>A day by name when it's near: "Today", "Tomorrow", "Yesterday", else "Nov 14".</summary>
    public static string Day(DateOnly day, DateOnly today) => (day.DayNumber - today.DayNumber) switch
    {
        0 => "Today",
        1 => "Tomorrow",
        -1 => "Yesterday",
        _ => Date(day, today),
    };

    /// <summary>"Nov 14", with the year when it isn't this one.</summary>
    public static string Date(DateOnly day, DateOnly today) =>
        day.ToString(day.Year == today.Year ? "MMM d" : "MMM d, yyyy", CultureInfo.CurrentCulture);

    /// <summary>"11:00 AM", in the app's culture.</summary>
    public static string Time(DateTime time) => time.ToString("t", CultureInfo.CurrentCulture);

    /// <summary>When a task is due: "Overdue · Nov 16" before today, "Today · 11:00 AM", then "Nov 19".</summary>
    public static string Due(DateTime start, DateOnly today)
    {
        var day = DateOnly.FromDateTime(start);
        return day < today ? $"Overdue · {Date(day, today)}"
            : day == today ? $"Today · {Time(start)}"
            : Date(day, today);
    }

    /// <summary>When something happened: "Today, 9:15 AM", "Yesterday", then "Nov 14".</summary>
    public static string Happened(DateTime time, DateOnly today)
    {
        var day = DateOnly.FromDateTime(time);
        return day == today ? $"Today, {Time(time)}" : Day(day, today);
    }

    /// <summary>A meeting's hours: "2:00–2:45 PM", or "11:30 AM–12:15 PM" across noon.</summary>
    public static string Hours(DateTime start, DateTime end)
    {
        var (from, to) = (Time(start), Time(end));
        var designator = start.ToString("tt", CultureInfo.CurrentCulture);
        // Both in the morning or both after noon: say which once, at the end.
        if (designator.Length > 0 && designator == end.ToString("tt", CultureInfo.CurrentCulture) && from.EndsWith(designator, StringComparison.Ordinal))
            from = from[..^designator.Length].TrimEnd();
        return $"{from}–{to}";
    }

    /// <summary>A time the database keeps in UTC (when something was done), on the workspace's clock.</summary>
    public static DateTime Local(DateTime utc, Workspace? workspace) =>
        workspace is null ? utc : TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById(workspace.TimeZone));
}
