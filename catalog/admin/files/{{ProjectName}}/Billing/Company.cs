namespace {{RootNamespace}}.Billing;

/// <summary>Who the invoices are from, as the invoice page and its printout show it. Change it to your own.</summary>
public static class Company
{
    public const string Name = "{{ProjectName}}, Inc.";

    public static readonly string[] Address = ["100 Market Street", "San Francisco, CA 94105"];
}
