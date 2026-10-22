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

    public async Task InitializeAsync()
    {
        _app = SiteApp.Build([], b => b.WebHost.UseUrls("http://127.0.0.1:0"));
        await _app.StartAsync();
        var address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        Client = new HttpClient { BaseAddress = new Uri(address) };
        Registry = _app.Services.GetRequiredService<Registry>();
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
    }

    [Fact]
    public async Task Every_example_renders_in_each_mode_it_claims()
    {
        foreach (var (_, example) in site.Registry.AllExamples)
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

    [Theory]
    [InlineData("docs/components/button?mode=off", null, "off")]
    [InlineData("docs/components/button", null, "static")]
    [InlineData("docs/components/button", "live", "live")]
    [InlineData("docs/components/button?mode=off", "live", "off")]
    [InlineData("docs/components/button?mode=nope", "nope", "static")]
    public async Task A_page_opens_in_the_linked_then_remembered_mode(string path, string? cookie, string expected)
    {
        // ?mode= (a link can open a page in Off), then the cookie the script sets, then Static. Each
        // copy of the switch is its own radio group with that option checked, so the visible one is
        // right for screen readers and the keyboard.
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", $"site-mode={cookie}");
        }

        var html = await (await site.Client.SendAsync(request)).Content.ReadAsStringAsync();
        var checkedRadios = ModeRadio().Matches(html).Where(m => m.Groups["checked"].Success).ToList();

        Assert.Equal(["site-mode-aside", "site-mode-top"], checkedRadios.Select(m => m.Groups["name"].Value).Order());
        Assert.All(checkedRadios, m => Assert.Equal(expected, m.Groups["value"].Value));
    }

    [Fact]
    public async Task A_page_that_is_one_example_has_one_switch_in_it()
    {
        var html = await site.Client.GetStringAsync("docs");

        Assert.Equal(["site-mode-example"], ModeRadio().Matches(html).Select(m => m.Groups["name"].Value).Distinct());
    }

    [Fact]
    public async Task An_example_pinned_to_a_mode_has_only_that_frame()
    {
        // Button's OnClick example demonstrates Live behavior: Live whatever the page's mode.
        var html = await site.Client.GetStringAsync("docs/components/button?mode=off");
        var start = html.IndexOf("data-ex=\"button-on-click\"", StringComparison.Ordinal);
        var example = html[start..html.IndexOf("</section>", start, StringComparison.Ordinal)];

        Assert.Equal(["live"], Regex.Matches(example, "class=\"ex-mode\" data-mode=\"(\\w+)\"").Select(m => m.Groups[1].Value));
        Assert.Contains("Always Live", example);
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

    [GeneratedRegex("<input type=\"radio\" class=\"site-mode-input\" name=\"(?<name>[^\"]+)\" value=\"(?<value>\\w+)\"(?<checked> checked)?")]
    private static partial Regex ModeRadio();
}
