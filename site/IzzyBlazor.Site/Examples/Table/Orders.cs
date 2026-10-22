namespace IzzyBlazor.Site.Examples.Table;

/// <summary>Sample orders for the Table examples: the same 23 rows in each, so sorting, paging and filtering have something to do.</summary>
public static class Orders
{
    private static readonly string[] Customers =
        ["Ada Lovelace", "Grace Hopper", "Alan Turing", "Katherine Johnson", "Linus Torvalds", "Margaret Hamilton", "Barbara Liskov"];

    public static readonly IReadOnlyList<Order> All = Enumerable.Range(1, 23)
        .Select(i => new Order(
            1000 + i,
            Customers[i * 5 % Customers.Length],
            new DateOnly(2026, 9, 1).AddDays(i * 7 % 50),
            (OrderStatus)(i * 3 % 4),
            Math.Round(24.5m + i * 41.75m % 460m, 2)))
        .ToList();

    /// <summary>The badge status for an order status.</summary>
    public static Status StatusOf(OrderStatus status) => status switch
    {
        OrderStatus.Paid => Status.Success,
        OrderStatus.Pending => Status.Warning,
        OrderStatus.Failed => Status.Danger,
        _ => Status.Neutral,
    };
}

public sealed record Order(int Number, string Customer, DateOnly Date, OrderStatus Status, decimal Total);

public enum OrderStatus { Paid, Pending, Refunded, Failed }
