using System.ComponentModel.DataAnnotations;
using IzzyBlazor.Site.Examples.Table;
using Microsoft.Extensions.Caching.Memory;

namespace IzzyBlazor.Site.Examples.DataGrid;

/// <summary>An order that can be edited: public settable properties, and rules a form can check.</summary>
public sealed class EditableOrder
{
    public int Number { get; set; }

    [Required, StringLength(60)]
    public string? Customer { get; set; }

    public DateOnly Date { get; set; }

    public OrderStatus Status { get; set; }

    [Range(0, 1_000_000)]
    public decimal Total { get; set; }

    [StringLength(200), DataType(DataType.MultilineText)]
    public string? Notes { get; set; }
}

/// <summary>
/// Stands in for the database in the DataGrid's editing example. This is a public site, so each visitor
/// gets their own copy of the rows, kept for a while, and no one sees what anyone else types.
/// </summary>
public static class EditableOrders
{
    private const string Cookie = "ib-example-orders";
    private static readonly MemoryCache Copies = new(new MemoryCacheOptions { SizeLimit = 2_000 });

    /// <summary>The visitor's rows on a static page, or a new copy with no request to go by, on an interactive one.</summary>
    public static List<EditableOrder> For(HttpContext? http)
    {
        if (http is null)
        {
            return Fresh();
        }

        if (!http.Request.Cookies.TryGetValue(Cookie, out var visitor) || visitor.Length > 40)
        {
            // Only a save needs to find these again; no cookie for someone who only reads the page.
            if (!HttpMethods.IsPost(http.Request.Method) || http.Response.HasStarted)
            {
                return Fresh();
            }

            visitor = Guid.NewGuid().ToString("N");
            http.Response.Cookies.Append(Cookie, visitor, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromHours(1), Secure = http.Request.IsHttps });
        }

        return Copies.GetOrCreate(visitor, entry =>
        {
            entry.Size = 1;
            entry.SlidingExpiration = TimeSpan.FromMinutes(30);
            return Fresh();
        })!;
    }

    private static List<EditableOrder> Fresh() => Orders.All
        .Select(o => new EditableOrder { Number = o.Number, Customer = o.Customer, Date = o.Date, Status = o.Status, Total = o.Total })
        .ToList();
}
