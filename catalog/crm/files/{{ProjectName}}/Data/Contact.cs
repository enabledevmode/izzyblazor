using System.ComponentModel.DataAnnotations;
using {{RootNamespace}}.Auditing;

namespace {{RootNamespace}}.Data;

/// <summary>A person the team has a relationship with, usually at a <see cref="Company"/>.</summary>
public class Contact : IAudited
{
    public int Id { get; set; }

    [Required, StringLength(60)] public string FirstName { get; set; } = "";

    [Required, StringLength(60)] public string LastName { get; set; } = "";

    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = "";

    [Phone, StringLength(40)] public string? Phone { get; set; }

    [StringLength(80)] public string? JobTitle { get; set; }

    public int? CompanyId { get; set; }
    public Company? Company { get; set; }

    /// <summary>The team member who owns the relationship.</summary>
    public string? OwnerId { get; set; }
    public ApplicationUser? Owner { get; set; }

    public ContactStatus Status { get; set; }

    public LeadSource? Source { get; set; }

    public DateOnly? NextFollowUp { get; set; }

    [StringLength(2000)] public string? Notes { get; set; }

    public DateOnly Added { get; set; }

    /// <summary>The last time anything happened with them, in UTC: the Contacts list sorts by it.</summary>
    public DateTime? LastActivity { get; set; }

    public string Name => $"{FirstName} {LastName}";

    string IAudited.AuditName => Name;
}

public enum ContactStatus { Lead, Prospect, Customer }

public enum LeadSource { Referral, Website, Event, Outbound, Other }
