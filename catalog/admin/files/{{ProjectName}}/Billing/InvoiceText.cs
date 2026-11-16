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

    public static string Money(decimal amount) => amount.ToString("C", CultureInfo.CurrentCulture);

    /// <summary>Days from the issue date to the due date: 30 for Net 30.</summary>
    public static int TermsDays(Invoice invoice) => invoice.Due.DayNumber - invoice.Issued.DayNumber;

    /// <summary>Payment terms as invoices name them: "Net 30", or "Due on receipt" for none.</summary>
    public static string Terms(int days) => days <= 0 ? "Due on receipt" : $"Net {days}";

    public static string CustomerName(Invoice invoice) => invoice.Customer is { } c ? $"{c.FirstName} {c.LastName}" : "";
}
