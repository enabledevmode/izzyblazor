using System.ComponentModel.DataAnnotations;
using {{RootNamespace}}.Auditing;

namespace {{RootNamespace}}.Data;

/// <summary>A note the whole team can find, linked to a contact, a deal or both. Its text is Markdown.</summary>
public class Note : IAudited
{
    public int Id { get; set; }

    [Required, StringLength(120)] public string Title { get; set; } = "";

    public int? ContactId { get; set; }
    public Contact? Contact { get; set; }

    public int? DealId { get; set; }
    public Deal? Deal { get; set; }

    [Required, StringLength(20000)] public string Body { get; set; } = "";

    public string? AuthorId { get; set; }
    public ApplicationUser? Author { get; set; }

    /// <summary>When it was written, in UTC.</summary>
    public DateTime Created { get; set; }

    /// <summary>When it was last changed, in UTC.</summary>
    public DateTime Edited { get; set; }

    string IAudited.AuditName => Title;
}
