using System.ComponentModel.DataAnnotations;
using {{RootNamespace}}.Auditing;

namespace {{RootNamespace}}.Data;

/// <summary>An invoice: what a customer owes for the lines on it.</summary>
public class Invoice : IAudited
{
    public int Id { get; set; }

    /// <summary>The number the customer sees, such as INV-0042.</summary>
    [Required, StringLength(20)] public string Number { get; set; } = "";

    /// <summary>The customer's purchase order number, when they gave one: shown on the invoice so they can match it.</summary>
    [StringLength(40)] public string? PoNumber { get; set; }

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public InvoiceStatus Status { get; set; }
    public DateOnly Issued { get; set; }
    public DateOnly Due { get; set; }
    public DateOnly? Sent { get; set; }
    public DateOnly? Paid { get; set; }
    public DateOnly? LastReminder { get; set; }

    /// <summary>The tax rate, as a fraction: 0.08 is 8%.</summary>
    public decimal TaxRate { get; set; }

    [StringLength(500)] public string? Note { get; set; }
    public List<InvoiceLine> Lines { get; set; } = [];

    public decimal Subtotal => Lines.Sum(l => l.Amount);
    public decimal Tax => Math.Round(Subtotal * TaxRate, 2, MidpointRounding.AwayFromZero);
    public decimal Total => Subtotal + Tax;

    /// <summary>Sent and past its due date, unpaid. Overdue isn't stored: it's a sent invoice's state on a given day.</summary>
    public bool IsOverdue(DateOnly today) => Status == InvoiceStatus.Sent && Due < today;

    string IAudited.AuditName => Number;
}

public enum InvoiceStatus { Draft, Sent, Paid, Void }

/// <summary>One line on an invoice.</summary>
public class InvoiceLine
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    [Required, StringLength(200)] public string Description { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal Amount => Quantity * UnitPrice;
}
