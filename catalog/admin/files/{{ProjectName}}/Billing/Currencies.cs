using System.Globalization;

namespace {{RootNamespace}}.Billing;

/// <summary>A currency an invoice can be in: its ISO code, its name, the symbol amounts show and its decimals.</summary>
public sealed record Currency(string Code, string Name, string Symbol, int Decimals = 2)
{
    /// <summary>"USD — US dollar", as the currency select lists it.</summary>
    public string Label => $"{Code} — {Name}";
}

/// <summary>
/// The currencies invoices can be in, the company's first. Add yours here. A symbol another currency shares
/// says whose it is (CA$, A$), so a list that mixes them stays clear.
/// </summary>
public static class Currencies
{
    public static IReadOnlyList<Currency> All { get; } =
    [
        new("USD", "US dollar", "$"),
        new("EUR", "Euro", "€"),
        new("GBP", "British pound", "£"),
        new("CAD", "Canadian dollar", "CA$"),
        new("AUD", "Australian dollar", "A$"),
        new("JPY", "Japanese yen", "¥", 0),
        new("CHF", "Swiss franc", "CHF "),
        new("INR", "Indian rupee", "₹"),
    ];

    /// <summary>The currency with this code, or the company's.</summary>
    public static Currency Of(string? code) =>
        All.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase)) ?? Of(Company.Currency);

    /// <summary>An amount in a currency, in the page's number format: $1,234.50, €1,234.50, ¥1,235.</summary>
    public static string Format(decimal amount, string? code)
    {
        var currency = Of(code);
        var format = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();
        format.CurrencySymbol = currency.Symbol;
        format.CurrencyDecimalDigits = currency.Decimals;
        return amount.ToString("C", format);
    }
}
