using System.ComponentModel.DataAnnotations;

namespace IzzyBlazor.Site.Frame;

/// <summary>The model the CRUD pages demo is generated from (izzy add crud Customer).</summary>
public sealed class DemoCustomer
{
    public int Id { get; set; }
    [Required, StringLength(40)] public string FirstName { get; set; } = "";
    [Required, StringLength(40)] public string LastName { get; set; } = "";
    [Required, EmailAddress] public string Email { get; set; } = "";
    public DemoCustomerStatus? Status { get; set; }
    [DataType(DataType.Currency)] public decimal LifetimeValue { get; set; }
    public DateOnly Joined { get; set; }
    public bool Newsletter { get; set; }
}

public enum DemoCustomerStatus { Active, Trial, Churned }

/// <summary>
/// The demo's customers, made fresh for each request: a save or delete posts but doesn't last,
/// so visitors never see each other's changes.
/// </summary>
public static class CrudDemoData
{
    public static List<DemoCustomer> Customers() =>
    [
        Make(1, "Amara", "Okafor", DemoCustomerStatus.Active, 18_420m, new(2023, 3, 14), true),
        Make(2, "Lucas", "Moreau", DemoCustomerStatus.Active, 9_860m, new(2023, 6, 2), false),
        Make(3, "Priya", "Raman", DemoCustomerStatus.Trial, 0m, new(2026, 10, 28), true),
        Make(4, "Jonas", "Becker", DemoCustomerStatus.Active, 24_150m, new(2022, 11, 9), true),
        Make(5, "Sofia", "Lindqvist", DemoCustomerStatus.Churned, 3_200m, new(2024, 1, 22), false),
        Make(6, "Mateo", "García", DemoCustomerStatus.Active, 12_775m, new(2024, 4, 5), true),
        Make(7, "Hannah", "Kim", DemoCustomerStatus.Active, 7_340m, new(2025, 2, 17), false),
        Make(8, "Daniel", "Novak", DemoCustomerStatus.Trial, 0m, new(2026, 11, 3), false),
        Make(9, "Chloe", "Dubois", DemoCustomerStatus.Active, 15_980m, new(2023, 9, 30), true),
        Make(10, "Kwame", "Mensah", DemoCustomerStatus.Active, 5_610m, new(2025, 7, 11), true),
        Make(11, "Elena", "Rossi", DemoCustomerStatus.Churned, 1_450m, new(2024, 8, 19), false),
        Make(12, "Noah", "Fischer", DemoCustomerStatus.Active, 21_300m, new(2022, 5, 3), true),
        Make(13, "Aiko", "Tanaka", DemoCustomerStatus.Active, 8_925m, new(2025, 3, 26), false),
        Make(14, "Liam", "O'Brien", DemoCustomerStatus.Trial, 0m, new(2026, 11, 10), true),
        Make(15, "Isabel", "Santos", DemoCustomerStatus.Active, 11_060m, new(2024, 10, 14), true),
        Make(16, "Omar", "Haddad", DemoCustomerStatus.Active, 6_480m, new(2025, 9, 8), false),
        Make(17, "Freya", "Jensen", DemoCustomerStatus.Churned, 2_890m, new(2024, 2, 27), false),
        Make(18, "Ravi", "Patel", DemoCustomerStatus.Active, 19_735m, new(2023, 1, 16), true),
    ];

    private static DemoCustomer Make(int id, string first, string last, DemoCustomerStatus status, decimal value, DateOnly joined, bool newsletter) => new()
    {
        Id = id, FirstName = first, LastName = last,
        Email = $"{first.ToLowerInvariant()}.{last.ToLowerInvariant().Replace("'", "")}@example.com".Replace("í", "i"),
        Status = status, LifetimeValue = value, Joined = joined, Newsletter = newsletter,
    };
}
