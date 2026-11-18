using System.ComponentModel.DataAnnotations;
using {{RootNamespace}}.Auditing;

namespace {{RootNamespace}}.Data;

/// <summary>A company: the account its people and deals belong to.</summary>
public class Company : IAudited
{
    public int Id { get; set; }

    [Required, StringLength(120)] public string Name { get; set; } = "";

    [StringLength(200)] public string? Website { get; set; }

    public Industry? Industry { get; set; }

    public CompanySize? Size { get; set; }

    [StringLength(80)] public string? City { get; set; }

    /// <summary>One of <see cref="Countries"/>.</summary>
    [StringLength(80)] public string? Country { get; set; }

    /// <summary>The team member who looks after the account.</summary>
    public string? OwnerId { get; set; }
    public ApplicationUser? Owner { get; set; }

    public CompanyStatus Status { get; set; }

    [StringLength(2000)] public string? Notes { get; set; }

    public DateOnly Added { get; set; }

    public List<Contact> Contacts { get; set; } = [];
    public List<Deal> Deals { get; set; } = [];

    /// <summary>The countries the forms offer.</summary>
    public static IReadOnlyList<string> Countries { get; } = ["United States", "Canada", "United Kingdom", "Other"];

    string IAudited.AuditName => Name;
}

public enum Industry
{
    [Display(Name = "Design & technology")] DesignTechnology,
    [Display(Name = "Professional services")] ProfessionalServices,
    Retail,
    Other,
}

public enum CompanySize
{
    [Display(Name = "1–10 people")] UpTo10,
    [Display(Name = "11–50 people")] UpTo50,
    [Display(Name = "51–200 people")] UpTo200,
    [Display(Name = "201+ people")] Over200,
}

public enum CompanyStatus { Prospect, Customer, Inactive }
