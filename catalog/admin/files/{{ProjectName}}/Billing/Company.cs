namespace {{RootNamespace}}.Billing;

/// <summary>Who the invoices are from, as the invoice page and its printout show it. Change it to your own.</summary>
public static class Company
{
    public const string Name = "{{ProjectName}}, Inc.";

    public static readonly string[] Address = ["100 Market Street", "San Francisco, CA 94105"];

    /// <summary>The currency new invoices are in, and the one totals across invoices add up (Billing/Currencies.cs).</summary>
    public const string Currency = "USD";

    /// <summary>The sales tax new invoices charge, as a fraction: 0.08 is 8%.</summary>
    public const decimal TaxRate = 0.08m;
}
