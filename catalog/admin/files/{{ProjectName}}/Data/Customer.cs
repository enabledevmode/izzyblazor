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
    public CustomerStatus Status { get; set; }
    public CustomerPlan Plan { get; set; }

    /// <summary>What they've paid in all, kept up to date as their invoices are paid.</summary>
    [DataType(DataType.Currency), Editable(false)] public decimal LifetimeValue { get; set; }

    public DateOnly Joined { get; set; }

    string IAudited.AuditName => $"{FirstName} {LastName}";
}

public enum CustomerStatus { Active, Trial, Churned }

public enum CustomerPlan { Starter, Growth, Business }
