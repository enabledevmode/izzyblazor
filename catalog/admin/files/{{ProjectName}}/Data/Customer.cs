using System.ComponentModel.DataAnnotations;
using {{RootNamespace}}.Auditing;

namespace {{RootNamespace}}.Data;

/// <summary>A customer: someone the business sends invoices to.</summary>
public class Customer : IAudited
{
    public int Id { get; set; }
    [Required, StringLength(40)] public string FirstName { get; set; } = "";
    [Required, StringLength(40)] public string LastName { get; set; } = "";
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
    [DataType(DataType.PhoneNumber), StringLength(30)] public string? Phone { get; set; }
    [StringLength(100)] public string? Company { get; set; }

    // Where invoices are sent: the Bill to on each one.
    [StringLength(200)] public string? StreetAddress { get; set; }
    [StringLength(100)] public string? City { get; set; }
    [StringLength(20)] public string? PostalCode { get; set; }
    [StringLength(60)] public string? Country { get; set; }
    public CustomerStatus Status { get; set; }
    public CustomerPlan Plan { get; set; }

    /// <summary>What they've paid in all, in the company's currency, kept up to date as their invoices are paid.</summary>
    [DataType(DataType.Currency), Editable(false)] public decimal LifetimeValue { get; set; }

    public DateOnly Joined { get; set; }

    string IAudited.AuditName => $"{FirstName} {LastName}";
}

public enum CustomerStatus { Active, Trial, Churned }

public enum CustomerPlan { Starter, Growth, Business }
