using System.ComponentModel.DataAnnotations;
using {{RootNamespace}}.Auditing;

namespace {{RootNamespace}}.Data;

/// <summary>A note the whole team can find, linked to a contact, a deal or both. It has no title: it is its text, which is Markdown.</summary>
public class Note : IAudited
{
    public int Id { get; set; }

    public int? ContactId { get; set; }
    public Contact? Contact { get; set; }

    public int? DealId { get; set; }
    public Deal? Deal { get; set; }

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
