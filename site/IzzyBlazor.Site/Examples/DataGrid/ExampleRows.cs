using Microsoft.Extensions.Caching.Memory;

namespace IzzyBlazor.Site.Examples.DataGrid;

/// <summary>
/// Stands in for the database in the DataGrid's editing examples. This is a public site, so each visitor
/// gets their own copy of the rows, kept for a while, and no one sees what anyone else types.
/// </summary>
public static class ExampleRows
{
    private const string Cookie = "ib-example-rows";
    private static readonly MemoryCache Copies = new(new MemoryCacheOptions { SizeLimit = 2_000 });

    /// <summary>The visitor's rows on a static page, or a new copy with no request to go by, on an interactive one.</summary>
    public static List<TRow> For<TRow>(HttpContext? http, string example, Func<List<TRow>> rows)
    {
        if (http is null)
        {
            return rows();
        }

        // Several examples on one page ask during the same request.
        var visitor = http.Items[Cookie] as string;
        if (visitor is null && (!http.Request.Cookies.TryGetValue(Cookie, out visitor) || visitor.Length > 40))
        {
            // Only a save needs to find these again; no cookie for someone who only reads the page.
            if (!HttpMethods.IsPost(http.Request.Method) || http.Response.HasStarted)
            {
                return rows();
            }

            visitor = Guid.NewGuid().ToString("N");
            http.Items[Cookie] = visitor;
            http.Response.Cookies.Append(Cookie, visitor, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromHours(1), Secure = http.Request.IsHttps });
        }

        return Copies.GetOrCreate($"{visitor}:{example}", entry =>
        {
            entry.Size = 1;
            entry.SlidingExpiration = TimeSpan.FromMinutes(30);
            return rows();
        })!;
    }
}
