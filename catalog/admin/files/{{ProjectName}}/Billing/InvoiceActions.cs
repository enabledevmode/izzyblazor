using Microsoft.EntityFrameworkCore;
using {{RootNamespace}}.Auditing;
using {{RootNamespace}}.Data;

namespace {{RootNamespace}}.Billing;

/// <summary>
/// What people do to an invoice: send it, remind, mark it paid, void, duplicate or delete a draft. Each is a form
/// post from the invoice list or page, and each lands in the audit log: sending and reminders as their own actions
/// (with what they changed), the rest as the record's own changes.
/// </summary>
/// <remarks>
/// No email goes out: sending and reminders only record that they happened. To mail the customer, send it here,
/// where the invoice is marked sent.
/// </remarks>
public sealed class InvoiceActions(ApplicationDbContext db, Audit audit)
{
    /// <summary>The terms a new invoice starts with: due 30 days after its issue date.</summary>
    public const int DefaultTerms = 30;

    /// <summary>One invoice with its customer and lines, or null.</summary>
    public Task<Invoice?> GetAsync(int id) =>
        db.Invoices.Include(i => i.Customer).Include(i => i.Lines).FirstOrDefaultAsync(i => i.Id == id);

    /// <summary>Sends a draft. It keeps the issue and due dates it was given: they're what the customer was billed with.</summary>
    public async Task<string?> SendAsync(Invoice invoice)
    {
        if (invoice.Status != InvoiceStatus.Draft) return "Only a draft can be sent.";
        if (invoice.Lines.Count == 0) return "Add an item before sending.";
        List<AuditChange> changes = [new("Status", nameof(InvoiceStatus.Draft), nameof(InvoiceStatus.Sent))];
        (invoice.Status, invoice.Sent) = (InvoiceStatus.Sent, InvoiceText.Today);
        await SaveQuietlyAsync();
        await audit.RecordAsync(AuditActions.InvoiceSent, nameof(Invoice), invoice.Id.ToString(), invoice.Number, changes, $"To {invoice.Customer?.Email}.");
        return null;
    }

    /// <summary>A new draft for a customer, numbered next in the series, dated today on the default terms, with no lines yet; returns it.</summary>
    public async Task<Invoice> StartDraftAsync(int customerId, string currency)
    {
        var today = InvoiceText.Today;
        var draft = new Invoice
        {
            Number = await NextNumberAsync(),
            CustomerId = customerId,
            Currency = currency,
            Status = InvoiceStatus.Draft,
            Issued = today,
            Due = today.AddDays(DefaultTerms),
            TaxRate = Company.TaxRate,
        };
        db.Invoices.Add(draft);
        await db.SaveChangesAsync();
        return draft;
    }

    /// <summary>Reminds the customer of a sent invoice they haven't paid.</summary>
    public async Task<string?> RemindAsync(Invoice invoice)
    {
        if (invoice.Status != InvoiceStatus.Sent) return "Only a sent invoice that isn't paid can be reminded.";
        invoice.LastReminder = InvoiceText.Today;
        await SaveQuietlyAsync();
        await audit.RecordAsync(AuditActions.ReminderSent, nameof(Invoice), invoice.Id.ToString(), invoice.Number, note: $"To {invoice.Customer?.Email}.");
        return null;
    }

    /// <summary>Marks a sent invoice paid today, and adds it to what the customer has paid in all.</summary>
    public async Task<string?> MarkPaidAsync(Invoice invoice)
    {
        if (invoice.Status != InvoiceStatus.Sent) return "Only a sent invoice can be marked paid.";
        (invoice.Status, invoice.Paid) = (InvoiceStatus.Paid, InvoiceText.Today);
        await db.SaveChangesAsync();
        if (invoice.Customer is { } customer && invoice.Currency == Company.Currency)
        {
            // A total kept for the Customers list (in the company's currency), not a change anyone made: left out of the log.
            customer.LifetimeValue += invoice.Total;
            await SaveQuietlyAsync();
        }
        return null;
    }

    /// <summary>Cancels a sent invoice: it stays on record, owing nothing.</summary>
    public async Task<string?> VoidAsync(Invoice invoice)
    {
        if (invoice.Status != InvoiceStatus.Sent) return "Only a sent invoice that isn't paid can be voided.";
        invoice.Status = InvoiceStatus.Void;
        await db.SaveChangesAsync();
        return null;
    }

    /// <summary>Deletes a draft. Sent invoices stay on record; void them instead.</summary>
    public async Task<string?> DeleteDraftAsync(Invoice invoice)
    {
        if (invoice.Status != InvoiceStatus.Draft) return "Only a draft can be deleted. Void a sent invoice instead.";
        db.Invoices.Remove(invoice);
        await db.SaveChangesAsync();
        return null;
    }

    /// <summary>A new draft with the same customer, lines, note and terms, dated today; returns it.</summary>
    public async Task<Invoice> DuplicateAsync(Invoice invoice)
    {
        var today = InvoiceText.Today;
        var copy = new Invoice
        {
            Number = await NextNumberAsync(),
            CustomerId = invoice.CustomerId,
            Status = InvoiceStatus.Draft,
            Issued = today,
            Due = today.AddDays(InvoiceText.TermsDays(invoice)),
            TaxRate = invoice.TaxRate,
            Note = invoice.Note,
            Lines = [.. invoice.Lines.Select(l => new InvoiceLine { Description = l.Description, Quantity = l.Quantity, UnitPrice = l.UnitPrice })],
        };
        db.Invoices.Add(copy);
        await db.SaveChangesAsync();
        return copy;
    }

    /// <summary>The next number in the INV-0042 series.</summary>
    public async Task<string> NextNumberAsync()
    {
        var numbers = await db.Invoices.Select(i => i.Number).ToListAsync();
        var last = numbers.Select(n => int.TryParse(n.AsSpan(n.LastIndexOf('-') + 1), out var v) ? v : 0).DefaultIfEmpty().Max();
        return $"INV-{last + 1:0000}";
    }

    // A save whose audit entry is written by hand, with more to say than the fields (who it went to).
    async Task SaveQuietlyAsync()
    {
        db.Auditing = false;
        try { await db.SaveChangesAsync(); }
        finally { db.Auditing = true; }
    }
}
