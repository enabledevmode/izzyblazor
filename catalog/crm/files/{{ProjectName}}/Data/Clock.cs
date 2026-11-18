namespace {{RootNamespace}}.Data;

/// <summary>The workspace's clock: activities are scheduled, and days begin and end, in its time zone.</summary>
public static class Clock
{
    /// <summary>The time now in the workspace's zone (UTC when there's no workspace yet).</summary>
    public static DateTime Now(Workspace? workspace) =>
        workspace is null ? DateTime.UtcNow : TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(workspace.TimeZone));

    /// <summary>Today's date there.</summary>
    public static DateOnly Today(Workspace? workspace) => DateOnly.FromDateTime(Now(workspace));
}
