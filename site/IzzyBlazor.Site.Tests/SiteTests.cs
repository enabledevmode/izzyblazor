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
                    href.StartsWith("_", StringComparison.Ordinal) || href.Contains("/_izzy/", StringComparison.Ordinal))
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

    public static readonly TheoryData<string> Modes = new() { "off", "static", "live" };

    // Every page that shows examples: component pages, articles, the docs overview and home.
    private IEnumerable<string> ExamplePages =>
        site.Registry.Components.Select(Registry.OverviewHref)
            .Concat(Articles.Sections.SelectMany(s => s.Articles.Select(s.HrefOf)))
            .Concat(["docs", ""]);

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
            // A demo (components.json "page") is a page of its own, in a window: the only frames.
            Assert.Equal(Regex.Count(html, "<iframe"), Regex.Count(html, "ex-window\"><iframe"));
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
    // Off page a Live-only example's link to Live.
    private static void AssertRendered(string section, RegistryExample example, string pageMode, string page)
    {
        if (example.Page)
        {
            Assert.Contains($"src=\"frame/{example.Id}?mode=", section);
            return;
        }

        var mode = example.Modes.Contains(pageMode) ? pageMode : example.StartMode;
        if (mode == "live" && pageMode == "off")
        {
            Assert.Contains("an Off page doesn't have", section);
            return;
        }

        var preview = Regex.Match(section, "ib-example-preview[^\"]*\">(?<content>.*)", RegexOptions.Singleline).Groups["content"].Value;
        Assert.True(Regex.IsMatch(preview, "^\\s*(<!--Blazor:[^>]*-->\\s*)?<(?!/)"), $"/{page} ({pageMode}): {example.Id} renders nothing");
        Assert.True((mode == "live") == section.Contains("<!--Blazor:", StringComparison.Ordinal), $"/{page} ({pageMode}): {example.Id} should {(mode == "live" ? "" : "not ")}be a Live island");
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

                if (mode == "off")
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
        var off = await site.Client.GetStringAsync("docs/components/button?mode=off");
        var live = await site.Client.GetStringAsync("docs/components/button?mode=live");

        Assert.DoesNotContain("blazor.web.js", off);
        Assert.DoesNotContain("importmap", off);
        Assert.Contains("data-site-off", off);
        Assert.Contains("blazor.web.js", live);
        Assert.DoesNotContain("data-site-off", live);

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
    }

    [Fact]
    public async Task A_page_follows_off_exactly_when_it_has_the_switch()
    {
        // Picking Off is about the examples on pages with the switch: any other page keeps its
        // script, since it has no switch to turn it back on. Those pages are [FollowsModeSwitch].
        foreach (var page in ExamplePages.Concat(["docs/components/button/api", "docs/components", "docs/get-started", "docs/components/app-shell"]))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, page);
            request.Headers.Add("Cookie", "site-mode=off");
            var html = await (await site.Client.SendAsync(request)).Content.ReadAsStringAsync();
            var hasSwitch = html.Contains("class=\"site-modeswitch ", StringComparison.Ordinal);

            Assert.True(hasSwitch == html.Contains("data-site-off", StringComparison.Ordinal),
                $"/{page}: {(hasSwitch ? "has the switch but isn't [FollowsModeSwitch]" : "is Off with no switch")}");
        }
    }

    [Fact]
    public void The_off_stylesheet_reads_the_scripting_media_feature_as_javascript_off()
    {
        var css = NoScriptCss.Transform("""
            @media (scripting: none) { a { b: c } }
            @media (pointer: coarse), (scripting:none) { d { e: f } }
            @media (scripting: enabled) { g { h: i } }
            @media(scripting:none){j{k:l}}@media(pointer:coarse),(scripting:none){m{n:o}}
            """);

        Assert.DoesNotContain("scripting", css);
        Assert.Matches(@"@media\s+all \{ a", css);
        Assert.Matches(@"@media \(pointer: coarse\),\s+all \{ d", css);
        Assert.Matches(@"@media\s+not all \{ g", css);

        // The minified stylesheet, as the site serves it in Release: still valid media queries.
        Assert.Contains("@media all{j", css);
        Assert.Contains("@media(pointer:coarse), all{m", css);
    }

    [Fact]
    public async Task Examples_on_one_page_read_different_keys_from_the_address()
    {
        // In the page, examples share its address: two reading ?page= would page together. Rename
        // one (QueryKey, QueryPrefix, a field's Name). A page of its own (AppShell's) is in a frame
        // with its own address.
        var clashes = new List<string>();
        foreach (var page in ExamplePages)
        {
            var html = await site.Client.GetStringAsync($"{page}?mode=static");
            var readers = Regex.Matches(html, "data-ex=\"([^\"]+)\"").Select(m => m.Groups[1].Value)
                .Distinct()
                .Where(id => site.Registry.Example(id)?.Example.Page != true)
                .SelectMany(id => QueryKeys(id).Distinct().Select(key => (key, id)))
                .GroupBy(r => r.key)
                .Where(g => g.Count() > 1);
            clashes.AddRange(readers.Select(g => $"/{page}: ?{g.Key}= in {string.Join(", ", g.Select(r => r.id))}"));
        }

        Assert.True(clashes.Count == 0, string.Join("; ", clashes));
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
            foreach (Match m in Regex.Matches(source, "<Table\\b[^>]*>"))
            {
                var prefix = Regex.Match(m.Value, "QueryPrefix=\"([^\"]*)\"").Groups[1].Value;
                yield return prefix + "sort";
                yield return prefix + "page";
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
    [InlineData("docs/components/button?mode=off", null, "off")]
    [InlineData("docs/components/button", null, "static")]
    [InlineData("docs/components/button", "live", "live")]
    [InlineData("docs/components/button?mode=off", "live", "off")]
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

    [Fact]
    public async Task A_link_with_a_mode_remembers_it_for_the_next_page()
    {
        // With JavaScript off too: an example's own links and forms drop ?mode= from the address.
        var response = await site.Client.GetAsync("docs/components/button?mode=live");

        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("site-mode=live;", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_troubleshooting_article_is_linked_from_each_component_it_is_about()
    {
        // Hand-written See also lists need TroubleshootingLinks added by hand: this catches one that doesn't.
        var missing = new List<string>();
        foreach (var section in Articles.Sections)
        {
            foreach (var article in section.Articles)
            {
                foreach (var name in article.Components)
                {
                    var html = await site.Client.GetStringAsync(Registry.OverviewHref(site.Registry.ByName(name)!));
                    if (!html.Contains($"href=\"{section.HrefOf(article)}\"", StringComparison.Ordinal))
                    {
                        missing.Add($"{name}: {section.HrefOf(article)}");
                    }
                }
            }
        }

        Assert.True(missing.Count == 0, string.Join(", ", missing));
    }

    [Fact]
    public async Task A_page_that_is_one_example_has_one_switch_in_it()
    {
        var html = await site.Client.GetStringAsync("docs");

        Assert.Equal(["example"], Regex.Matches(html, "site-modeswitch site-modeswitch-(\\w+)").Select(m => m.Groups[1].Value));
    }

    [Fact]
    public async Task An_example_pinned_to_live_runs_live_on_a_static_page_and_links_to_live_from_off()
    {
        // Button's OnClick example demonstrates Live behavior: Live whatever the page's mode, and an
        // Off page has no Blazor to run it.
        var onStatic = Example(await site.Client.GetStringAsync("docs/components/button?mode=static"), "button-on-click");
        var onOff = Example(await site.Client.GetStringAsync("docs/components/button?mode=off"), "button-on-click");

        Assert.Contains("Always Live", onStatic);
        Assert.Contains("<!--Blazor:", onStatic);
        Assert.Contains("an Off page doesn't have", onOff);
        Assert.DoesNotContain("<!--Blazor:", onOff);
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

    [GeneratedRegex("<a class=\"site-modeswitch-option site-mode-link\" href=\"[^\"]*\"(?<current> aria-current=\"true\")? data-mode=\"(?<mode>\\w+)\"")]
    private static partial Regex ModeLink();

    [GeneratedRegex("\\sid=\"([^\"]+)\"")]
    private static partial Regex Id();

    // Ids in a <template> aren't in the document.
    [GeneratedRegex("(?s)<template.*?</template>")]
    private static partial Regex Template();
}
