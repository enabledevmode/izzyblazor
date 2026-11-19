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

    /// <summary>A day and its time, in a record's history: "Today, 9:15 AM", "Nov 14, 11:00 AM".</summary>
    public static string At(DateTime time, DateOnly today) => $"{Day(DateOnly.FromDateTime(time), today)}, {Time(time)}";

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

    /// <summary>
    /// The workspace's time zone at a time on its clock, as people write it: "EST", "CDT". Where the zone has no
    /// such name, how far it is from UTC: "UTC+1".
    /// </summary>
    public static string Zone(DateTime local, Workspace? workspace)
    {
        if (workspace is null) return "UTC";
        var zone = TimeZoneInfo.FindSystemTimeZoneById(workspace.TimeZone);
        // "Eastern Standard Time" is EST; names that don't shorten that way (most outside North America) don't.
        var words = (zone.IsDaylightSavingTime(local) ? zone.DaylightName : zone.StandardName).Split(' ');
        if (workspace.TimeZone.StartsWith("America/", StringComparison.Ordinal) && words is [{ Length: > 0 }, "Standard" or "Daylight", "Time"])
            return string.Concat(words.Select(word => word[0]));
        var offset = zone.GetUtcOffset(local);
        return offset == TimeSpan.Zero ? "UTC" : $"UTC{(offset < TimeSpan.Zero ? "−" : "+")}{offset:h\\:mm}".Replace(":00", "");
    }

    /// <summary>A time the database keeps in UTC, as a time in the workspace's zone: for components that take one, such as Comment.</summary>
    public static DateTimeOffset Zoned(DateTime utc, Workspace? workspace)
    {
        var at = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
        return workspace is null ? at : TimeZoneInfo.ConvertTime(at, TimeZoneInfo.FindSystemTimeZoneById(workspace.TimeZone));
    }

    /// <summary>A time the database keeps in UTC (when something was done), on the workspace's clock.</summary>
    public static DateTime Local(DateTime utc, Workspace? workspace) =>
        workspace is null ? utc : TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById(workspace.TimeZone));
}
