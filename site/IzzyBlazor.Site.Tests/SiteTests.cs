using System.Net;
using System.Text.RegularExpressions;
using IzzyBlazor.Site.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Site.Tests;

/// <summary>Runs the real site on a free port for the tests in this class.</summary>
public sealed class SiteFixture : IAsyncLifetime
{
    private WebApplication? _app;

    public HttpClient Client { get; private set; } = null!;

    public Registry Registry { get; private set; } = null!;

    public ExampleCatalog Catalog { get; private set; } = null!;

    public SearchIndex Search { get; private set; } = null!;

    public ApiMetadata Api { get; private set; } = null!;

    public IServiceProvider Services => _app!.Services;

    public async Task InitializeAsync()
    {
        _app = SiteApp.Build([], b => b.WebHost.UseUrls("http://127.0.0.1:0"));
        await _app.StartAsync();
        var address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        // No cookie jar: the site remembers the mode in a cookie, and each test sends its own.
        Client = new HttpClient(new HttpClientHandler { UseCookies = false }) { BaseAddress = new Uri(address) };
        Registry = _app.Services.GetRequiredService<Registry>();
        Catalog = _app.Services.GetRequiredService<ExampleCatalog>();
        Search = _app.Services.GetRequiredService<SearchIndex>();
        Api = _app.Services.GetRequiredService<ApiMetadata>();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}

public partial class SiteTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Fact]
    public async Task Every_internal_link_from_the_home_page_resolves()
    {
        var seen = new HashSet<string> { "/" };
        var queue = new Queue<string>(seen);
        var broken = new List<string>();
        var pages = new Dictionary<string, string>();
        var anchors = new List<(string From, string Page, string Id)>();

        while (queue.Count > 0)
        {
            var path = queue.Dequeue();
            var response = await site.Client.GetAsync(path);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                broken.Add($"{path}: {(int)response.StatusCode}");
                continue;
            }

            if (response.Content.Headers.ContentType?.MediaType != "text/html")
            {
                continue;
            }

            var html = await response.Content.ReadAsStringAsync();
            pages[path] = html;
            foreach (Match match in Href().Matches(html))
            {
                var href = WebUtility.HtmlDecode(match.Groups[1].Value);
                if (href.StartsWith("http", StringComparison.Ordinal) || href.StartsWith('#') || href.StartsWith("mailto:", StringComparison.Ordinal) ||
                    href.StartsWith("_", StringComparison.Ordinal))
                {
                    continue;
                }

                var hash = href.IndexOf('#');
                var target = "/" + (hash < 0 ? href : href[..hash]).TrimStart('/');
                target = target.Replace("/./", "/");

                // The library's own fragments (the drawer's close link targets nothing on purpose).
                if (hash > 0 && !target.Contains('?') && !href[(hash + 1)..].StartsWith("ib-", StringComparison.Ordinal))
                {
                    anchors.Add((path, target, href[(hash + 1)..]));
                }

                if (target.Contains('?') || target.EndsWith(".css", StringComparison.Ordinal) || target.EndsWith(".js", StringComparison.Ordinal) ||
                    target.EndsWith(".ico", StringComparison.Ordinal) || target.EndsWith(".svg", StringComparison.Ordinal) || target.EndsWith(".png", StringComparison.Ordinal))
                {
                    continue;
                }

                if (seen.Add(target))
                {
                    queue.Enqueue(target);
                }
            }
        }

        // Links to a section or member point at an element that's there.
        broken.AddRange(anchors
            .Where(a => pages.TryGetValue(a.Page, out var html) && !html.Contains($"id=\"{a.Id}\"", StringComparison.Ordinal))
            .Select(a => $"{a.From}: no #{a.Id} on {a.Page}")
            .Distinct());
        Assert.True(broken.Count == 0, string.Join("\n", broken));
        Assert.Contains("/docs/components/button/api", seen);
        Assert.Contains("/demos/button/toolbar", seen);
        Assert.Contains("/docs/get-started/existing-app", seen);
        Assert.All(Articles.Sections.SelectMany(s => s.Articles.Select(a => "/" + s.HrefOf(a))), href => Assert.Contains(href, seen));
    }

    public static readonly TheoryData<string> Modes = new() { "noscript", "static", "interactive" };

    // Every page that shows examples: component pages, articles, the docs overview and home.
    private IEnumerable<string> ExamplePages =>
        site.Registry.AllComponents.Select(Registry.OverviewHref)
            .Concat(site.Registry.AllComponents.SelectMany(c => c.Topics.Select(t => Registry.TopicHref(c, t))))
            .Concat(Articles.Sections.SelectMany(s => s.Articles.Select(s.HrefOf)))
            .Concat(["docs", ""]);

    [Fact]
    public async Task A_components_topics_are_pages_under_it_in_the_sidebar()
    {
        var html = await site.Client.GetStringAsync("docs/components/data-grid/editing");
        Assert.Contains("data-ex=\"data-grid-edit-in-row\"", html);
        Assert.DoesNotContain("data-ex=\"data-grid-columns\"", html);
        Assert.Contains("href=\"docs/components/data-grid/filtering\"", html);

        var overview = await site.Client.GetStringAsync("docs/components/data-grid");
        Assert.Contains("data-ex=\"data-grid-columns\"", overview);
        Assert.DoesNotContain("data-ex=\"data-grid-edit-in-row\"", overview);
        Assert.Contains("href=\"docs/components/data-grid/editing\"", overview);

        // The API page keeps its address, and a topic that isn't one isn't found.
        Assert.Contains("docs/components/data-grid/api", await site.Client.GetStringAsync("docs/components/data-grid/api"));
        Assert.Equal(HttpStatusCode.NotFound, (await site.Client.GetAsync("docs/components/data-grid/nonsense")).StatusCode);
    }

    // An example's state in the address, asked for from outside the site (a crawler), goes to the
    // plain page; a click on the site keeps it. Only through Fly's proxy (Fly-Client-IP).
    [Fact]
    public async Task A_query_page_from_outside_the_site_goes_to_the_plain_page()
    {
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { BaseAddress = site.Client.BaseAddress };

        async Task<HttpResponseMessage> Get(string? fetchSite)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "docs/components/pagination?page=3");
            request.Headers.Add("Fly-Client-IP", "203.0.113.7");
            if (fetchSite is not null)
            {
                request.Headers.Add("Sec-Fetch-Site", fetchSite);
            }

            return await client.SendAsync(request);
        }

        var crawled = await Get(null);
        Assert.Equal(HttpStatusCode.Redirect, crawled.StatusCode);
        Assert.Equal("/docs/components/pagination", crawled.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, (await Get("cross-site")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Get("same-origin")).StatusCode);
    }

    // A first visit from outside (no cookie, through Fly's proxy) can be served a copy rendered for
    // someone else (FirstVisitCache): each visitor still gets an antiforgery cookie and token of
    // their own, and a form on the page posts.
    [Fact]
    public async Task A_first_visit_served_from_the_cache_has_its_own_token_and_its_form_posts()
    {
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { BaseAddress = site.Client.BaseAddress };

        async Task<(string Cookie, string Token)> Visit()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "docs/components/card");
            request.Headers.Add("Fly-Client-IP", "203.0.113.8");
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            var tokens = Regex.Matches(html, "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"").Select(m => m.Groups[1].Value).Distinct().ToList();
            var token = Assert.Single(tokens);
            var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(".AspNetCore.Antiforgery", StringComparison.Ordinal));
            return (cookie.Split(';')[0], token);
        }

        var first = await Visit();
        var second = await Visit();
        Assert.NotEqual(first.Token, second.Token);
        Assert.NotEqual(first.Cookie, second.Cookie);

        using var post = new HttpRequestMessage(HttpMethod.Post, "docs/components/card")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["_handler"] = "card-settings",
                ["__RequestVerificationToken"] = second.Token,
                ["Model.Name"] = "Platform team",
            }),
        };
        post.Headers.Add("Fly-Client-IP", "203.0.113.8");
        post.Headers.Add("Cookie", second.Cookie);
        using var saved = await client.SendAsync(post);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Contains("Saved: Platform team", await saved.Content.ReadAsStringAsync());
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task Every_example_renders_in_a_page_in_each_mode(string mode)
    {
        // Examples render in the page itself, in its mode; only demos have a frame.
        var shown = new HashSet<string>();
        foreach (var page in ExamplePages)
        {
            var html = await site.Client.GetStringAsync($"{page}?mode={mode}");
            shown.UnionWith(Regex.Matches(html, "data-ex=\"([^\"]+)\"").Select(m => m.Groups[1].Value));
            // A demo (components.json "page") is a page of its own, in a window: the only frames,
            // with the home page's hero, which shows one.
            Assert.Equal(Regex.Count(html, "<iframe"), Regex.Count(html, "ex-window\"><iframe") + Regex.Count(html, "<iframe class=\"ex-frame\" src=\"frame/home-store"));
            var ids = Id().Matches(Template().Replace(html, "")).Select(m => m.Groups[1].Value);
            var repeated = ids.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.True(repeated.Count == 0, $"/{page} ({mode}) repeats ids: {string.Join(", ", repeated)}");

            // Forms on one page post to it: two with one FormName fail the post ("more than one
            // named submit event").
            var handlers = Regex.Matches(html, "name=\"_handler\" value=\"([^\"]+)\"").Select(m => m.Groups[1].Value);
            var shared = handlers.GroupBy(h => h).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.True(shared.Count == 0, $"/{page} ({mode}) has two forms named {string.Join(", ", shared)}");

            // A page without the switch shows its examples in Static.
            var pageMode = html.Contains("class=\"site-modeswitch ", StringComparison.Ordinal) ? mode : "static";
            foreach (var id in Regex.Matches(html, "data-ex=\"([^\"]+)\"").Select(m => m.Groups[1].Value).Distinct())
            {
                AssertRendered(Example(html, id), site.Registry.Example(id)!.Value.Example, pageMode, page);
            }
        }

        var missing = site.Registry.AllExamples.Select(e => e.Example).Where(e => !e.Page && !shown.Contains(e.Id)).Select(e => e.Id).ToList();
        Assert.True(missing.Count == 0, $"Not on any page: {string.Join(", ", missing)}");
    }

    // The preview holds the example: rendered with the page, a prerendered island in Live, or on an
    // no-script page a Live-only example's link to Live.
    private static void AssertRendered(string section, RegistryExample example, string pageMode, string page)
    {
        if (example.Page)
        {
            Assert.Contains($"src=\"frame/{example.Id}?mode=", section);
            return;
        }

        var mode = example.Modes.Contains(pageMode) ? pageMode : example.StartMode;
        if (mode == "interactive" && pageMode == "noscript")
        {
            Assert.Contains("a no-script page doesn't have", section);
            return;
        }

        var preview = Regex.Match(section, "ib-example-preview[^\"]*\">(?<content>.*)", RegexOptions.Singleline).Groups["content"].Value;
        Assert.True(Regex.IsMatch(preview, "^\\s*(<!--Blazor:[^>]*-->\\s*)?<(?!/)"), $"/{page} ({pageMode}): {example.Id} renders nothing");
        Assert.True((mode == "interactive") == section.Contains("<!--Blazor:", StringComparison.Ordinal), $"/{page} ({pageMode}): {example.Id} should {(mode == "interactive" ? "" : "not ")}be a Live island");
    }

    [Fact]
    public async Task Every_demo_renders_in_its_frame_in_each_mode_it_claims()
    {
        foreach (var (_, example) in site.Registry.AllExamples.Where(e => e.Example.Page))
        {
            foreach (var mode in example.Modes)
            {
                var html = await site.Client.GetStringAsync($"frame/{example.Id}?mode={mode}");
                Assert.Contains("class=\"frame", html);

                if (mode == "noscript")
                {
                    // The sandbox runs no script anyway; the page shouldn't even ask for any.
                    Assert.DoesNotContain("<script", html);
                }
            }
        }
    }

    [Fact]
    public async Task An_off_page_sends_no_blazor_and_no_library_script()
    {
        var off = await site.Client.GetStringAsync("docs/components/button?mode=noscript");
        var live = await site.Client.GetStringAsync("docs/components/button?mode=interactive");

        Assert.DoesNotContain("_framework/blazor.web.", off);
        Assert.DoesNotContain("importmap", off);
        Assert.Contains("data-site-noscript", off);
        Assert.Contains("_framework/blazor.web.", live);
        Assert.DoesNotContain("data-site-noscript", live);

        // Its stylesheet: the library's with the scripting rules as JavaScript off reads them.
        var href = Regex.Match(off, "href=\"(_content/IzzyBlazor.Core/izzyblazor-noscript.css\\?v=\\w+)\"").Groups[1].Value;
        Assert.NotEmpty(href);
        var css = await site.Client.GetStringAsync(href);
        Assert.Contains(".ib-btn", css);
        Assert.DoesNotContain("scripting", css);
    }

    [Fact]
    public async Task The_library_lays_out_by_whether_the_browser_runs_script_not_by_its_script_arriving()
    {
        // IzzyBlazor's script arrives after the first paint: a rule on :defined would change the
        // page when it does. @media (scripting: ...) decides from the first paint (CONTRIBUTING).
        var css = await site.Client.GetStringAsync(NoScriptCss.LibraryPath);

        Assert.Contains(".ib-btn", css);
        Assert.DoesNotContain(":defined", css);

        // The calendar's sheet too, and no scripting rules there: Off pages rewrite only the main sheet.
        var date = await site.Client.GetStringAsync("_content/IzzyBlazor.Core/date.css");
        Assert.Contains(".ib-calendar", date);
        Assert.DoesNotContain(":defined", date);
        Assert.DoesNotContain("scripting:", date);
        var carousel = await site.Client.GetStringAsync("_content/IzzyBlazor.Core/carousel.css");
        Assert.Contains(".ib-carousel", carousel);
        Assert.DoesNotContain("scripting:", carousel);
        var chart = await site.Client.GetStringAsync("_content/IzzyBlazor.Core/chart.css");
        Assert.Contains(".ib-chart", chart);
        Assert.DoesNotContain("scripting:", chart);
    }

    [Fact]
    public async Task A_page_follows_off_exactly_when_it_has_the_switch()
    {
        // Picking Off is about the examples on pages with the switch: any other page keeps its
        // script, since it has no switch to turn it back on. Those pages are [FollowsModeSwitch].
        foreach (var page in ExamplePages.Concat(["docs/components/button/api", "docs/components", "docs/get-started", "docs/components/app-shell"]))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, page);
            request.Headers.Add("Cookie", "site-mode=noscript");
            var html = await (await site.Client.SendAsync(request)).Content.ReadAsStringAsync();
            var hasSwitch = html.Contains("class=\"site-modeswitch ", StringComparison.Ordinal);

            Assert.True(hasSwitch == html.Contains("data-site-noscript", StringComparison.Ordinal),
                $"/{page}: {(hasSwitch ? "has the switch but isn't [FollowsModeSwitch]" : "is Off with no switch")}");
        }
    }

    // The keys an example reads from the address, from its source: its own query parameters and
    // the library components that keep state there.
    private IEnumerable<string> QueryKeys(string id)
    {
        var source = site.Catalog.SourceOf(id)?.Source ?? "";
        foreach (Match m in Regex.Matches(source, "SupplyParameterFromQuery\\(Name\\s*=\\s*\"([^\"]+)\""))
        {
            yield return m.Groups[1].Value;
        }

        foreach (Match m in Regex.Matches(source, "GetUriWithQueryParameter\\(\"([^\"]+)\""))
        {
            yield return m.Groups[1].Value;
        }

        if (Regex.IsMatch(source, "Sortable|PageSize|Filterable"))
        {
            // Quoted values may hold a ">" (ItemKey="e => e.Id").
            foreach (Match m in Regex.Matches(source, "<Table\\b(?:[^>\"]|\"[^\"]*\")*>"))
            {
                var prefix = Regex.Match(m.Value, "QueryPrefix=\"([^\"]*)\"").Groups[1].Value;
                yield return prefix + "sort";
                yield return prefix + "page";
                yield return prefix + "cursor";
            }
        }

        foreach (Match m in Regex.Matches(source, "<Pagination\\b[^>]*>"))
        {
            var key = Regex.Match(m.Value, "QueryKey=\"([^\"]*)\"");
            yield return key.Success ? key.Groups[1].Value : "page";
        }

        foreach (Match m in Regex.Matches(source, "<(?:Tabs|MenuRadioGroup)\\b[^>]*QueryKey=\"([^\"]*)\""))
        {
            yield return m.Groups[1].Value;
        }

        foreach (Match m in Regex.Matches(source, "<FilterSearch\\b[^>]*>"))
        {
            var name = Regex.Match(m.Value, "\\bName=\"([^\"]*)\"");
            yield return name.Success ? name.Groups[1].Value : "q";
        }

        foreach (Match m in Regex.Matches(source, "<Filter(?!Search|Form)\\w+\\b[^>]*\\bName=\"([^\"]*)\""))
        {
            yield return m.Groups[1].Value;
        }
    }

    [Theory]
    [InlineData("docs/components/button?mode=noscript", null, "noscript")]
    [InlineData("docs/components/button", null, "static")]
    [InlineData("docs/components/button", "interactive", "interactive")]
    [InlineData("docs/components/button?mode=noscript", "interactive", "noscript")]
    [InlineData("docs/components/button?mode=nope", "nope", "static")]
    public async Task A_page_opens_in_the_linked_then_remembered_mode(string path, string? cookie, string expected)
    {
        // ?mode= (a link can open a page in Off), then the cookie, then Static. Both copies of the
        // switch mark it.
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", $"site-mode={cookie}");
        }

        var html = await (await site.Client.SendAsync(request)).Content.ReadAsStringAsync();
        var current = ModeLink().Matches(html).Where(m => m.Groups["current"].Success).Select(m => m.Groups["mode"].Value).ToList();

        Assert.Equal([expected, expected], current);
    }

    private static string Example(string html, string id)
    {
        var start = html.IndexOf($"data-ex=\"{id}\"", StringComparison.Ordinal);
        return html[start..html.IndexOf("</section>", start, StringComparison.Ordinal)];
    }

    [Fact]
    public async Task Unknown_pages_are_not_found()
    {
        foreach (var path in new[] { "docs/components/nope", "docs/components/nope/api", "demos/button/nope", "frame/nope" })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await site.Client.GetAsync(path)).StatusCode);
        }
    }

    [GeneratedRegex("href=\"([^\"]*)\"")]
    private static partial Regex Href();

    // A mode link (ModeSwitch's TabLinks carry data-mode), and whether it's marked current.
    [GeneratedRegex("<a\\b(?=[^>]*\\sdata-mode=\"(?<mode>\\w+)\")(?:(?=[^>]*\\saria-current=\"(?<current>true)\"))?[^>]*>")]
    private static partial Regex ModeLink();

    [GeneratedRegex("\\sid=\"([^\"]+)\"")]
    private static partial Regex Id();

    // Ids in a <template> aren't in the document.
    [GeneratedRegex("(?s)<template.*?</template>")]
    private static partial Regex Template();
}
