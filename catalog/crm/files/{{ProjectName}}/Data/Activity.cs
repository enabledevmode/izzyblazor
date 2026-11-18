using System.ComponentModel.DataAnnotations;
using {{RootNamespace}}.Auditing;

namespace {{RootNamespace}}.Data;

/// <summary>
/// Something on the calendar or the task list, linked to a contact or a deal: a meeting, a call or a task. Its
/// time is the workspace's own (<see cref="Workspace.TimeZone"/>), as the forms show it.
/// </summary>
public class Activity : IAudited
{
    public int Id { get; set; }

    public ActivityType Type { get; set; }

    [Required, StringLength(120)] public string Title { get; set; } = "";

    public int? ContactId { get; set; }
    public Contact? Contact { get; set; }

    public int? DealId { get; set; }
    public Deal? Deal { get; set; }

    /// <summary>When it starts, or when a task is due, in the workspace's time zone.</summary>
    public DateTime Start { get; set; }

    /// <summary>How long it runs, in minutes: one of <see cref="Durations"/>.</summary>
    public int Duration { get; set; } = 30;

    public string? AssignedToId { get; set; }
    public ApplicationUser? AssignedTo { get; set; }

    /// <summary>Minutes before it starts to remind them: one of <see cref="Reminders"/>, or null for none.</summary>
    public int? Reminder { get; set; } = 30;

    /// <summary>Where it is, or a meeting link.</summary>
    [StringLength(300)] public string? Location { get; set; }

    /// <summary>The agenda, or notes about it.</summary>
    [StringLength(4000)] public string? Agenda { get; set; }

    /// <summary>When it was marked complete, in UTC; null while it's open.</summary>
    public DateTime? Completed { get; set; }

    public string? CreatedById { get; set; }
    public ApplicationUser? CreatedBy { get; set; }

    public DateTime End => Start.AddMinutes(Duration);

    /// <summary>The lengths the form offers, in minutes, in its order.</summary>
    public static IReadOnlyList<int> Durations { get; } = [30, 15, 45, 60];

    /// <summary>The reminders the form offers, in minutes before, in its order; "No reminder" is null.</summary>
    public static IReadOnlyList<int> Reminders { get; } = [30, 15, 1440];

    string IAudited.AuditName => Title;
}

public enum ActivityType { Meeting, Call, Task }
