using System.ComponentModel.DataAnnotations;
using {{RootNamespace}}.Auditing;

namespace {{RootNamespace}}.Data;

/// <summary>A note the whole team can find, linked to a contact, a deal or both. Its text is Markdown.</summary>
public class Note : IAudited
{
    public int Id { get; set; }

    /// <summary>Its title; empty for a short note written on a record's page, which shows whole there.</summary>
    [StringLength(120)] public string Title { get; set; } = "";

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

    /// <summary>
    /// What a list of notes shows: a short note whole, and a titled one's opening paragraph (cut at 160
    /// characters), beside a link to the rest.
    /// </summary>
    public string Summary
    {
        get
        {
            if (Title.Length == 0) return Body.Trim();
            var first = Body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault(line => !line.StartsWith('#')) ?? "";
            return first.Length <= 160 ? first : first[..157].TrimEnd() + "…";
        }
    }

    string IAudited.AuditName => Title.Length > 0 ? Title : "Note";
}
