using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using IzzyBlazor.Site.Services;

namespace IzzyBlazor.Site.Tests;

// What search engines read: robots.txt, the sitemap, and each page's canonical address and
// description.
public partial class SeoTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private async Task<List<string>> SitemapPaths()
    {
        var xml = XDocument.Parse(await site.Client.GetStringAsync("sitemap.xml"));
        return xml.Descendants(XName.Get("loc", "http://www.sitemaps.org/schemas/sitemap/0.9"))
            .Select(loc => loc.Value)
            .Select(url => url.StartsWith("https://izzyblazor.com/", StringComparison.Ordinal) ? url["https://izzyblazor.com/".Length..] : $"not canonical: {url}")
            .ToList();
    }

    [Fact]
    public async Task A_client_past_sixty_pages_a_minute_gets_a_429_in_text_and_others_keep_reading()
    {
        // Its own addresses, so other tests keep theirs.
        async Task<HttpResponseMessage> Get(string client)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "docs");
            request.Headers.Add("Fly-Client-IP", client);
            return await site.Client.SendAsync(request);
        }

        HttpResponseMessage? last = null;
        for (var i = 0; i < 61; i++)
        {
            last = await Get("203.0.113.10");
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        Assert.NotNull(last.Headers.RetryAfter);
        Assert.DoesNotContain("<html", await last.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, (await Get("203.0.113.11")).StatusCode);
    }

    [Fact]
    public async Task Robots_txt_keeps_frames_and_search_out_and_points_at_a_sitemap_of_every_page()
    {
        var robots = await site.Client.GetStringAsync("robots.txt");
        Assert.Contains("Sitemap: https://izzyblazor.com/sitemap.xml", robots);
        Assert.Contains("Disallow: /frame/", robots);
        Assert.Contains("Disallow: /search", robots);
        Assert.Contains("Disallow: /mcp", robots);
        Assert.Contains("Disallow: /*?", robots);
        foreach (var file in new[] { "robots.txt", "sitemap.xml" })
        {
            using var head = new HttpRequestMessage(HttpMethod.Head, file);
            Assert.Equal(HttpStatusCode.OK, (await site.Client.SendAsync(head)).StatusCode);
        }

        var paths = await SitemapPaths();
        Assert.Equal(paths.Count, paths.Distinct().Count());
        Assert.DoesNotContain(paths, p => p.Contains('?') || p.Contains('#') || p.StartsWith("not canonical", StringComparison.Ordinal));
        string[] expected =
        [
            "", "docs", "docs/components", "demos",
            .. Articles.Sections.SelectMany(s => s.Articles.Select(s.HrefOf).Prepend(s.Href)),
            .. site.Registry.Components.SelectMany(c => c.Scenarios.Select(s => Registry.ScenarioHref(c, s)).Prepend(Registry.ApiPageHref(c)).Prepend(Registry.OverviewHref(c))),
        ];
        Assert.Empty(expected.Except(paths));

        var broken = new List<string>();
        foreach (var path in paths)
        {
            var response = await site.Client.GetAsync("/" + path);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                broken.Add($"/{path}: {(int)response.StatusCode}");
            }
        }

        Assert.True(broken.Count == 0, string.Join("\n", broken));
    }

    [Fact]
    public async Task Every_page_names_its_address_without_the_query_and_a_short_description()
    {
        var wrong = new List<string>();
        foreach (var path in await SitemapPaths())
        {
            // Any query is a variant of the same page.
            var html = await site.Client.GetStringAsync($"/{path}?mode=noscript&page=2");
            var canonical = Canonical().Matches(html).Select(m => m.Groups[1].Value).ToList();
            var url = $"https://izzyblazor.com/{path}";
            if (canonical.Count != 1 || canonical[0] != url)
            {
                wrong.Add($"/{path}: canonical {string.Join(", ", canonical)}");
            }

            if (Meta(html, "og:url") != url)
            {
                wrong.Add($"/{path}: og:url {Meta(html, "og:url")}");
            }

            var title = WebUtility.HtmlDecode(Title().Match(html).Groups[1].Value);
            if (Meta(html, "og:title") != title)
            {
                wrong.Add($"/{path}: og:title {Meta(html, "og:title")}, title {title}");
            }

            var description = Meta(html, "description", "name");
            if (description is not { Length: > 0 and <= 160 } || Meta(html, "og:description") != description)
            {
                wrong.Add($"/{path}: description ({description?.Length}) {description}");
            }
        }

        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    [Fact]
    public async Task Demo_frames_search_and_missing_pages_are_not_indexed()
    {
        var demo = site.Registry.AllExamples.First(e => e.Example.Page).Example;
        var frame = await site.Client.GetStringAsync($"/frame/{demo.Id}?mode=static");
        Assert.Contains("<meta name=\"robots\" content=\"noindex\"", frame);
        Assert.DoesNotContain("rel=\"canonical\"", frame);

        var search = await site.Client.GetStringAsync("/search?q=button");
        Assert.Contains("<meta name=\"robots\" content=\"noindex\"", search);

        foreach (var path in new[] { "/docs/components/no-such-thing", "/no/such/page" })
        {
            var missing = await site.Client.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            Assert.DoesNotContain("rel=\"canonical\"", await missing.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task Behind_fly_absolute_addresses_keep_https()
    {
        // Fly ends TLS and forwards plain HTTP, naming the original scheme in X-Forwarded-Proto.
        using var request = new HttpRequestMessage(HttpMethod.Get, "/docs");
        request.Headers.Add("X-Forwarded-Proto", "https");
        var html = await (await site.Client.SendAsync(request)).Content.ReadAsStringAsync();
        Assert.StartsWith("https://", Meta(html, "og:image"));
    }

    private static string? Meta(string html, string name, string attribute = "property")
    {
        var match = Regex.Match(html, $"<meta {attribute}=\"{Regex.Escape(name)}\" content=\"([^\"]*)\"");
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value) : null;
    }

    [GeneratedRegex("<link rel=\"canonical\" href=\"([^\"]*)\"")]
    private static partial Regex Canonical();

    [GeneratedRegex("<title>([^<]*)</title>")]
    private static partial Regex Title();
}
