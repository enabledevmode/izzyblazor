namespace IzzyBlazor.Site.Examples.DataGrid;

/// <summary>Sample lines for the orders in the Table examples: two to four for each, made from its number.</summary>
public static class OrderLines
{
    private static readonly (string Product, decimal Price)[] Products =
        [("Notebook", 4.50m), ("Pen set", 12m), ("Desk lamp", 38m), ("Stapler", 9.25m), ("Monitor arm", 64m), ("Cable tray", 21m)];

    public static IReadOnlyList<OrderLine> For(int order) => Enumerable.Range(0, 2 + order % 3)
        .Select(i =>
        {
            var (product, price) = Products[(order + i * 4) % Products.Length];
            return new OrderLine(product, 1 + (order + i) % 4, price);
        })
        .ToList();
}

public sealed record OrderLine(string Product, int Quantity, decimal Price)
{
    public decimal Amount => Quantity * Price;
}
