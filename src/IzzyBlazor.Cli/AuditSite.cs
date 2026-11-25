using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using IzzyBlazor.Generation;

namespace IzzyBlazor.Cli;

// A whole site's pages for an audit: from its sitemap.xml, else by following its links.
static partial class Audit
{
    /// <summary>The pages of a site to audit.</summary>
    /// <param name="Pages">Their addresses, at most the limit asked for.</param>
    /// <param name="Found">How many the site has; with <paramref name="FromSitemap"/> false, how many were found before the limit stopped the search.</param>
    /// <param name="FromSitemap">Whether they are the sitemap's; else they were found by following links.</param>
    public sealed record SitePages(List<string> Pages, int Found, bool FromSitemap);

    /// <summary>
    /// The pages under <paramref name="start"/>: those its site's sitemap.xml lists, or without one, every page
    /// reached by links from it on the same site. A page is a path: an address that differs by its query or
    /// fragment alone is the same page. <paramref name="start"/> with a path keeps the search inside it
    /// (<c>/docs</c> is /docs and what is under it).
    /// </summary>
    public static async Task<SitePages> Discover(Uri start, int max, IReadOnlyList<KeyValuePair<string, string>> cookies)
    {
        using var client = new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.All }) { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("izzy-audit");
        var origin = new Uri(start.GetLeftPart(UriPartial.Authority));
        // Redirects are followed here, so the cookies go to the site being audited and nowhere it leads.
        async Task<HttpResponseMessage> http(Uri address)
        {
            for (var hops = 0; ; hops++)
            {
                var request = new HttpRequestMessage(HttpMethod.Get, address);
                if (cookies.Count > 0 && SameSite(address, origin)) request.Headers.Add("Cookie", string.Join("; ", cookies.Select(c => $"{c.Key}={c.Value}")));
                var response = await client.SendAsync(request);
                if ((int)response.StatusCode is not (301 or 302 or 303 or 307 or 308) || response.Headers.Location is not { } to || hops == 10) return response;
                response.Dispose();
                address = new Uri(address, to);
            }
        }
        var scope = start.AbsolutePath.TrimEnd('/');
        bool Inside(Uri page) => SameSite(page, origin) && (scope.Length == 0 || page.AbsolutePath.Equals(scope, StringComparison.OrdinalIgnoreCase)
            || page.AbsolutePath.StartsWith(scope + "/", StringComparison.OrdinalIgnoreCase));

        // The sitemap's addresses are read as paths of the site being audited: an app run on this machine
        // lists the addresses it has in production.
        var listed = new List<Uri>();
        foreach (var loc in await Sitemap(http, new Uri(origin, "/sitemap.xml"), depth: 0))
            if (Uri.TryCreate(loc, UriKind.Absolute, out var there) && new Uri(origin, there.AbsolutePath) is var here && Inside(here)) listed.Add(here);
        if (listed.Count > 0)
        {
            var all = listed.Select(Address).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return new SitePages([.. all.Take(max)], all.Count, FromSitemap: true);
        }

        var pages = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Address(start) };
        var queue = new Queue<Uri>([start]);
        while (queue.Count > 0 && pages.Count < max)
        {
            var page = queue.Dequeue();
            string html;
            Uri landed;
            try
            {
                using var response = await http(page);
                landed = response.RequestMessage?.RequestUri ?? page;
                // A page that leads off the site, or to a file, isn't one of its pages.
                if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType != "text/html" || !Inside(landed)) continue;
                html = await response.Content.ReadAsStringAsync();
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                // The start itself: nothing can be found without it.
                if (pages.Count == 0 && queue.Count == 0) throw new GenerationException($"{page} couldn't be opened to look for the site's pages: {e.Message}");
                continue;
            }
            // Where it led is the page (/ that leads to /signin is /signin), once.
            if (!SameSite(landed, page) || Address(landed) != Address(page))
            {
                if (!seen.Add(Address(landed)) && pages.Contains(Address(landed), StringComparer.OrdinalIgnoreCase)) continue;
                if (queue.Any(waiting => Address(waiting).Equals(Address(landed), StringComparison.OrdinalIgnoreCase))) continue;
            }
            pages.Add(Address(landed));
            // Links are relative to the page's <base>, which a Blazor app sets to its root.
            var from = BaseHref().Match(html) is { Success: true } b && Uri.TryCreate(landed, WebUtility.HtmlDecode(b.Groups[1].Value), out var set) ? set : landed;
            foreach (Match link in Anchor().Matches(html))
            {
                var href = WebUtility.HtmlDecode(link.Groups[1].Success ? link.Groups[1].Value : link.Groups[2].Success ? link.Groups[2].Value : link.Groups[3].Value).Trim();
                if (href.Length == 0 || href[0] == '#' || !Uri.TryCreate(from, href, out var target) || target.Scheme is not ("http" or "https") || !Inside(target)) continue;
                if (Path.GetExtension(target.AbsolutePath) is { Length: > 0 } extension && extension.ToLowerInvariant() is not (".html" or ".htm")) continue;
                if (seen.Add(Address(target))) queue.Enqueue(new Uri(Address(target)));
            }
        }
        if (pages.Count == 0) throw new GenerationException($"{start} didn't answer with a page, so the site's pages couldn't be found.");
        // More than these only when the search stopped with addresses still to open.
        return new SitePages(pages, pages.Count + queue.Count, FromSitemap: false);
    }

    // localhost and 127.0.0.1 with one port are one site; so are a name and the same name in another case.
    static bool SameSite(Uri a, Uri b) => a.Scheme == b.Scheme && a.Port == b.Port && string.Equals(a.Host, b.Host, StringComparison.OrdinalIgnoreCase);

    // A page's address without its query and fragment, and without a slash at its end.
    static string Address(Uri page) => page.GetLeftPart(UriPartial.Authority) + (page.AbsolutePath.TrimEnd('/') is { Length: > 0 } path ? path : "/");

    // The addresses a sitemap lists; a sitemap of sitemaps is followed one level down.
    static async Task<List<string>> Sitemap(Func<Uri, Task<HttpResponseMessage>> http, Uri address, int depth)
    {
        var found = new List<string>();
        try
        {
            using var response = await http(address);
            if (!response.IsSuccessStatusCode) return found;
            var root = XDocument.Parse(await response.Content.ReadAsStringAsync()).Root;
            var locations = root?.Descendants().Where(e => e.Name.LocalName == "loc").Select(e => e.Value.Trim()).Where(l => l.Length > 0).ToList() ?? [];
            if (root?.Name.LocalName == "sitemapindex")
            {
                if (depth == 0)
                    foreach (var child in locations.Take(50))
                        if (Uri.TryCreate(child, UriKind.Absolute, out var there)) found.AddRange(await Sitemap(http, new Uri(address, there.PathAndQuery), depth + 1));
            }
            else if (root?.Name.LocalName == "urlset") found.AddRange(locations);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Xml.XmlException) { }
        return found;
    }

    [GeneratedRegex("""<a\b[^>]*?\bhref\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s>]+))""", RegexOptions.IgnoreCase)]
    private static partial Regex Anchor();

    [GeneratedRegex("""<base\b[^>]*?\bhref\s*=\s*["']([^"']*)["']""", RegexOptions.IgnoreCase)]
    private static partial Regex BaseHref();
}
