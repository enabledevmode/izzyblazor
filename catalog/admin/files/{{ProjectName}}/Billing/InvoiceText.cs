using System.Globalization;
using {{RootNamespace}}.Data;

namespace {{RootNamespace}}.Billing;

/// <summary>Where an invoice stands on a given day. Overdue isn't stored: it's a sent invoice past its due date.</summary>
public enum InvoiceState { Draft, Sent, Overdue, Paid, Void }

/// <summary>How the invoice pages word, color and date an invoice, so the list and the invoice page agree.</summary>
public static class InvoiceText
{
    /// <summary>Today, as invoices are dated (UTC, as the seeder dates them).</summary>
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public static InvoiceState State(Invoice invoice, DateOnly today) => invoice.Status switch
    {
        InvoiceStatus.Draft => InvoiceState.Draft,
        InvoiceStatus.Paid => InvoiceState.Paid,
        InvoiceStatus.Void => InvoiceState.Void,
        _ => invoice.Due < today ? InvoiceState.Overdue : InvoiceState.Sent,
    };

    /// <summary>Its badge's color: a draft gray, sent blue, overdue red, paid green, void gray.</summary>
    public static IzzyBlazor.Status Tone(InvoiceState state) => state switch
    {
        InvoiceState.Sent => IzzyBlazor.Status.Info,
        InvoiceState.Overdue => IzzyBlazor.Status.Danger,
        InvoiceState.Paid => IzzyBlazor.Status.Success,
        _ => IzzyBlazor.Status.Neutral,
    };

    /// <summary>The line under the due date: "Due in 14 days", "13 days late", "Paid Nov 13", "Not sent".</summary>
    public static string DueNote(Invoice invoice, DateOnly today)
    {
        if (invoice.Status == InvoiceStatus.Draft) return "Not sent";
        if (invoice.Status == InvoiceStatus.Void) return "Void";
        if (invoice.Paid is { } paid) return $"Paid {Short(paid)}";
        var days = invoice.Due.DayNumber - today.DayNumber;
        return days switch
        {
            0 => "Due today",
            1 => "Due tomorrow",
            > 1 => $"Due in {days} days",
            -1 => "1 day late",
            _ => $"{-days} days late",
        };
    }

    /// <summary>Days past due, or 0.</summary>
    public static int DaysLate(Invoice invoice, DateOnly today) => Math.Max(0, today.DayNumber - invoice.Due.DayNumber);

    /// <summary>A date as the pages write it: Nov 13, 2026.</summary>
    public static string Date(DateOnly date) => date.ToString("MMM d, yyyy", CultureInfo.CurrentCulture);

    /// <summary>A date in this year's short form: Nov 13.</summary>
    public static string Short(DateOnly date) => date.ToString("MMM d", CultureInfo.CurrentCulture);

    /// <summary>An amount in the company's currency, as totals across invoices are.</summary>
    public static string Money(decimal amount) => Currencies.Format(amount, Company.Currency);

    /// <summary>An amount in an invoice's own currency.</summary>
    public static string Money(decimal amount, string? currency) => Currencies.Format(amount, currency);

    /// <summary>
    /// Totals by currency, the company's first, then the rest by amount: "$9,542.88 · €1,200.00". Invoices in
    /// different currencies can't be added up as one number.
    /// </summary>
    public static string Money(IEnumerable<(string Currency, decimal Amount)> totals)
    {
        var sums = totals.GroupBy(t => t.Currency).Select(g => (Code: g.Key, Sum: g.Sum(t => t.Amount))).ToList();
        if (!sums.Any(s => s.Code == Company.Currency)) sums.Add((Company.Currency, 0));
        return string.Join(" · ", sums.Where(s => s.Code == Company.Currency || s.Sum != 0)
            .OrderBy(s => s.Code == Company.Currency ? 0 : 1).ThenByDescending(s => s.Sum)
            .Select(s => Currencies.Format(s.Sum, s.Code)));
    }

    /// <summary>Days from the issue date to the due date: 30 for Net 30.</summary>
    public static int TermsDays(Invoice invoice) => invoice.Due.DayNumber - invoice.Issued.DayNumber;

    /// <summary>Payment terms as invoices name them: "Net 30", or "Due on receipt" for none.</summary>
    public static string Terms(int days) => days <= 0 ? "Due on receipt" : $"Net {days}";

    public static string CustomerName(Invoice invoice) => invoice.Customer is { } c ? $"{c.FirstName} {c.LastName}" : "";

    /// <summary>Who an invoice is to, with their company: "Maya Chen — Northwind Studio".</summary>
    public static string BillToName(string name, string? company) =>
        string.IsNullOrWhiteSpace(company) ? name : $"{name} — {company}";

    /// <summary>The address invoices go to, on one line: "220 Harbor Street, San Francisco 94107"; empty without one.</summary>
    public static string AddressLine(string? street, string? city, string? postalCode) =>
        string.Join(", ", new[] { street, $"{city} {postalCode}".Trim() }.Where(p => !string.IsNullOrWhiteSpace(p)));

    /// <summary>The Bill to lines on the invoice itself: name, company, street, then city and postal code.</summary>
    public static IEnumerable<string> BillToLines(Customer customer) =>
        new[] { $"{customer.FirstName} {customer.LastName}", customer.Company, customer.StreetAddress, $"{customer.City} {customer.PostalCode}".Trim() }
            .Where(l => !string.IsNullOrWhiteSpace(l))!;
}
