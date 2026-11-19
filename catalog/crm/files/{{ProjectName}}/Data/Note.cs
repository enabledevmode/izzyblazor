using System.ComponentModel.DataAnnotations;
using {{RootNamespace}}.Auditing;

namespace {{RootNamespace}}.Data;

/// <summary>
/// A note the whole team can find, about a contact, a deal (or both) or a company, written on that record's page,
/// or on an activity's: what a meeting decided, which is also about the meeting's contact and deal.
/// It has no title: it is its text, which is Markdown.
/// </summary>
public class Note : IAudited
{
    public int Id { get; set; }

    public int? ContactId { get; set; }
    public Contact? Contact { get; set; }

    public int? DealId { get; set; }
    public Deal? Deal { get; set; }

    /// <summary>The company it's about, for a note written on a company's page. A company's page also shows the notes about its people and deals.</summary>
    public int? CompanyId { get; set; }
    public Company? Company { get; set; }

    /// <summary>The meeting, call or task it was written on, whose page lists it.</summary>
    public int? ActivityId { get; set; }
    public Activity? Activity { get; set; }

    [Required(ErrorMessage = "Enter a note."), StringLength(20000)] public string Body { get; set; } = "";

    public string? AuthorId { get; set; }
    public ApplicationUser? Author { get; set; }

    /// <summary>When it was written, in UTC.</summary>
    public DateTime Created { get; set; }

    /// <summary>When it was last changed, in UTC.</summary>
    public DateTime Edited { get; set; }

    /// <summary>How it opens, for a place with room for a line: the delete dialog, the audit log.</summary>
    public string Opening
    {
        get
        {
            var line = Body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "";
            return line.Length <= 80 ? line : line[..77].TrimEnd() + "…";
        }
    }

    string IAudited.AuditName => Opening;
}
