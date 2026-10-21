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

    [Fact]
    public async Task A_link_opens_a_page_in_its_mode()
    {
        // The page's mode switch starts on ?mode=, so a link can show the examples with JavaScript off.
        var off = await site.Client.GetStringAsync("docs/components/button?mode=off");
        var plain = await site.Client.GetStringAsync("docs/components/button");

        Assert.Matches("value=\"off\" checked", off);
        Assert.Matches("value=\"static\" checked", plain);
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
}
