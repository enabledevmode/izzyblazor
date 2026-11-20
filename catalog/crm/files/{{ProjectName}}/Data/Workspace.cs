using System.ComponentModel.DataAnnotations;
using {{RootNamespace}}.Auditing;

namespace {{RootNamespace}}.Data;

/// <summary>The team's workspace: its name and the defaults every page shares. There's one row.</summary>
public class Workspace : IAudited
{
    public int Id { get; set; }

    [Required, StringLength(80)] public string Name { get; set; } = "";

    [Url, StringLength(200)] public string? Website { get; set; }

    /// <summary>The ISO code of the currency amounts are in: one of <see cref="Currencies"/>.</summary>
    [Required, StringLength(3)] public string Currency { get; set; } = "USD";

    /// <summary>The IANA time zone activities are scheduled in: one of <see cref="TimeZones"/>.</summary>
    [Required, StringLength(60)] public string TimeZone { get; set; } = "America/New_York";

    public DayOfWeek WeekStart { get; set; } = DayOfWeek.Monday;

    /// <summary>The .NET format dates are shown in, such as "MMM d, yyyy".</summary>
    [Required, StringLength(20)] public string DateFormat { get; set; } = "MMM d, yyyy";

    /// <summary>The currencies the settings offer: code and name.</summary>
    public static IReadOnlyList<(string Code, string Name)> Currencies { get; } =
        [("USD", "US dollar"), ("GBP", "British pound"), ("EUR", "Euro")];

    /// <summary>The time zones the settings offer.</summary>
    public static IReadOnlyList<string> TimeZones { get; } = ["America/New_York", "America/Chicago"];

    /// <summary>An amount in the workspace's currency, in whole units: "$18,000".</summary>
    public string Money(decimal amount) => CurrencySymbol + amount.ToString("N0");

    /// <summary>The currency's sign: "$".</summary>
    public string CurrencySymbol => Currency switch { "GBP" => "£", "EUR" => "€", _ => "$" };

    string IAudited.AuditName => Name;
}
