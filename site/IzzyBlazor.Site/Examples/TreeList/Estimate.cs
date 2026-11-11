namespace IzzyBlazor.Site.Examples.TreeList;

/// <summary>A project estimate for the TreeList totals example and its export endpoints: hours on the tasks, phases above them.</summary>
public static class Estimate
{
    public static readonly IReadOnlyList<WorkItem> All =
    [
        new(1, null, "Discovery", "Nadia Park", 0),
        new(2, 1, "Stakeholder interviews", "Nadia Park", 16),
        new(3, 1, "Requirements", "Owen Clarke", 24),
        new(4, null, "Design", "Iris Novak", 0),
        new(5, 4, "Wireframes", "Iris Novak", 20),
        new(6, 4, "Visual design", "Iris Novak", 0),
        new(7, 6, "Style guide", "Iris Novak", 12),
        new(8, 6, "Screens", "Sam Ortiz", 32),
        new(9, null, "Build", "Sam Ortiz", 0),
        new(10, 9, "Front end", "Sam Ortiz", 0),
        new(11, 10, "Sign-up flow", "Sam Ortiz", 28),
        new(12, 10, "Dashboard", "Leo Brandt", 40),
        new(13, 9, "API", "Leo Brandt", 0),
        new(14, 13, "Accounts", "Leo Brandt", 24),
        new(15, 13, "Billing", "Leo Brandt", 36),
        new(16, null, "Launch", "Owen Clarke", 0),
        new(17, 16, "Testing", "Owen Clarke", 30),
        new(18, 16, "Release notes", "Nadia Park", 6),
    ];
}

public sealed record WorkItem(int Id, int? ParentId, string Task, string Owner, decimal Hours);
