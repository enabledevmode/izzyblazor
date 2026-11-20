using IzzyBlazor;

namespace {{RootNamespace}}.Data;

/// <summary>What an entry in a record's activity feed is: something that happened, a note, a meeting, a call or a task.</summary>
public enum FeedKind { Event, Note, Meeting, Call, Task }

/// <summary>
/// One entry in a record's activity feed (Components/Activities/ActivityFeed.razor): what, more about it, who,
/// and when, as written and as a time on the workspace's clock. A note's entry carries the note.
/// </summary>
public sealed record FeedItem(FeedKind Kind, string Title, string? Text, string? By, string Time, DateTime When)
{
    /// <summary>The note this entry is, with its Contact, Deal, Company and Activity loaded.</summary>
    public Note? Note { get; init; }

    /// <summary>A finished or scheduled activity's kind.</summary>
    public static FeedKind KindOf(Activity activity) => activity.Type switch
    {
        ActivityType.Meeting => FeedKind.Meeting,
        ActivityType.Call => FeedKind.Call,
        _ => FeedKind.Task,
    };

    /// <summary>A note as an entry: its title, or none for a note that is only its text.</summary>
    public static FeedItem Of(Note note, Workspace? workspace, DateOnly today) =>
        new(FeedKind.Note, note.Title ?? "", note.Title is null ? note.Body : Lead(note), note.Author?.Name, Written(note.Created, workspace, today), Data.When.Local(note.Created, workspace)) { Note = note };

    /// <summary>When something was written or done, as the feed writes it: "Today, 9:15 AM", "Nov 14, 12:10 PM".</summary>
    public static string Written(DateTime utc, Workspace? workspace, DateOnly today) => Data.When.At(Data.When.Local(utc, workspace), today);

    /// <summary>How a titled note opens: its first paragraph.</summary>
    public static string Lead(Note note) =>
        note.Body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault(line => !line.StartsWith('#')) ?? "";

    /// <summary>The records a note is on, as (what kind, its icon, its address, its name): a contact, a deal, a company, an activity.</summary>
    public static IEnumerable<(string Kind, IconData Icon, string Href, string Name)> Records(Note note)
    {
        if (note.Contact is { } contact) yield return ("contact", Lucide.User, $"contacts/{contact.Id}", contact.Name);
        if (note.Deal is { } deal) yield return ("deal", Lucide.Handshake, $"deals/{deal.Id}", deal.Name);
        if (note.Company is { } company) yield return ("company", Lucide.BuildingComplex, $"companies/{company.Id}", company.Name);
        if (note.Activity is { } activity) yield return ("activity", Lucide.CalendarDays, $"activities/{activity.Id}", activity.Title);
    }
}
