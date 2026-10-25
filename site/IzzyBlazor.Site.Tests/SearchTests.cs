using System.Net;
using System.Text.RegularExpressions;
using IzzyBlazor.Site.Services;

namespace IzzyBlazor.Site.Tests;

/// <summary>Site search: what the index holds, what a query finds first, and the search page.</summary>
public partial class SearchTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Fact]
    public void The_index_has_every_component_article_and_demo()
    {
        var hrefs = site.Search.Entries.Select(e => e.Href).ToHashSet();
        var missing = site.Registry.Components.Select(Registry.OverviewHref)
            .Concat(site.Registry.Components.SelectMany(c => c.Parts.Select(p => site.Registry.ApiHref(p, null)!)))
            .Concat(site.Registry.Components.SelectMany(c => c.Scenarios.Select(s => Registry.ScenarioHref(c, s))))
            .Concat(Articles.Sections.SelectMany(s => s.Articles.Select(s.HrefOf)))
            .Where(href => !hrefs.Contains(href))
            .ToList();

        Assert.True(missing.Count == 0, "Not in the index: " + string.Join(", ", missing));
        Assert.All(Articles.Section("troubleshooting")!.Articles, a =>
            Assert.Contains(site.Search.Entries, e => e.Kind == SearchKind.Troubleshooting && e.Title == a.Title));
    }

    [Fact]
    public async Task The_index_has_each_page_s_sections_as_its_outline_lists_them()
    {
        // The index reads headings from the pages' sources; the outline (On this page) is what
        // rendered. Standard sections ("Mode support") may be left out of the index, nothing else.
        var pages = site.Registry.Components.Select(Registry.OverviewHref)
            .Concat(Articles.Sections.SelectMany(s => s.Articles.Select(s.HrefOf)));
        var wrong = new List<string>();
        foreach (var page in pages)
        {
            var html = await site.Client.GetStringAsync($"{page}?mode=static");
            var outline = TocLink().Matches(html)
                .Select(m => (Id: WebUtility.HtmlDecode(m.Groups["id"].Value), Text: WebUtility.HtmlDecode(m.Groups["text"].Value)))
                .ToList();
            var indexed = site.Search.Entries
                .Where(e => e.Kind == SearchKind.Section && e.Href.StartsWith(page + "#", StringComparison.Ordinal))
                .Select(e => (Id: e.Href[(page.Length + 1)..], e.Title))
                .ToList();

            wrong.AddRange(indexed.Where(i => !outline.Any(o => o.Id == i.Id && o.Text == i.Title))
                .Select(i => $"/{page}: indexed #{i.Id} \"{i.Title}\" isn't in the outline"));
            wrong.AddRange(outline.Where(o => !SearchIndex.StandardSections.Contains(o.Text) && !indexed.Any(i => i.Id == o.Id))
                .Select(o => $"/{page}: #{o.Id} \"{o.Text}\" isn't indexed"));
        }

        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    [Fact]
    public async Task Every_result_links_to_a_page_and_place_that_exist()
    {
        var broken = new List<string>();
        foreach (var page in site.Search.Entries.GroupBy(e => e.Href.Split('#')[0]))
        {
            var response = await site.Client.GetAsync(page.Key);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                broken.Add($"/{page.Key}: {(int)response.StatusCode}");
                continue;
            }

            var html = await response.Content.ReadAsStringAsync();
            broken.AddRange(page.Select(e => e.Href).Where(h => h.Contains('#')).Distinct()
                .Where(h => !Regex.IsMatch(html, $"\\sid=\"{Regex.Escape(h.Split('#')[1])}\""))
                .Select(h => $"/{h}: no such id"));
        }

        Assert.True(broken.Count == 0, string.Join("\n", broken));
    }

    [Theory]
    [InlineData("dialog", 1, "docs/components/dialog")]
    [InlineData("Dialog", 1, "docs/components/dialog")]
    [InlineData("text field", 1, "docs/components/text-field")]
    [InlineData("textfield", 1, "docs/components/text-field")]
    [InlineData("modal", 1, "docs/components/dialog")]
    [InlineData("toggle buttons", 1, "docs/components/button#toggle-buttons")]
    [InlineData("button loading", 3, "docs/components/button/api#loading")]
    [InlineData("loading", 1, "docs/components/button/api#loading")]
    [InlineData("cardheader", 1, "docs/components/card/api#cardheader")]
    [InlineData("padding", 1, "docs/common/style-props#styledcomponentbase-padding")]
    [InlineData("form name", 1, "docs/components/form/api#formname")]
    [InlineData("dark", 1, "docs/theming/color-scheme")]
    [InlineData("dialog accessibility", 1, "docs/components/dialog#accessibility")]
    public void A_query_finds_what_it_names_first(string query, int within, string href)
    {
        var top = site.Search.Search(query).Groups.SelectMany(g => g.Entries).Take(within).Select(e => e.Href).ToList();

        Assert.True(top.Contains(href), $"\"{query}\": expected {href} in the first {within}, got {string.Join(", ", top)}");
    }

    [Theory]
    [InlineData("form name", "docs/troubleshooting/form-name")]
    [InlineData("password", "docs/troubleshooting/password-after-post")]
    [InlineData("spinner", "docs/components/spinner")]
    [InlineData("several submit", "docs/components/button#several-submit-buttons")]
    public void A_query_leads_its_group_with_what_it_names(string query, string href)
    {
        var leaders = site.Search.Search(query).Groups.Select(g => g.Entries[0].Href).ToList();

        Assert.True(leaders.Contains(href), $"\"{query}\": expected {href} to lead a group, got {string.Join(", ", leaders)}");
    }

    [Theory]
    [InlineData("accessibility")]
    [InlineData("button")]
    [InlineData("parameters")]
    public void Standard_sections_need_their_own_name_and_their_page_s(string query)
    {
        var results = site.Search.Search(query);

        Assert.DoesNotContain(results.Groups.SelectMany(g => g.Entries), e => e.Standard && e.Href.StartsWith("docs/components/", StringComparison.Ordinal));
        Assert.Equal("docs/common/accessibility", site.Search.Search("accessibility").Groups[0].Entries[0].Href);
        Assert.Equal("docs/components/button#parameters", site.Search.Search("button parameters").Groups[0].Entries[0].Href);
    }

    [Fact]
    public async Task The_search_page_works_as_a_get_form()
    {
        // Plain requests: what a browser with JavaScript off gets.
        var html = WebUtility.HtmlDecode(await site.Client.GetStringAsync("search?q=dialog"));
        Assert.Contains("<form method=\"get\"", html);
        Assert.Matches("<p role=\"status\"[^>]*>\\d+ results for “dialog”</p>", html);
        Assert.Contains("id=\"results-components\"", html);
        Assert.Contains("<a class=\"site-search-link\" href=\"docs/components/dialog\"><span class=\"site-search-title\"><span class=\"ib-highlight\"><mark class=\"ib-mark\">Dialog</mark></span>", html);
        Assert.Contains("<meta name=\"robots\" content=\"noindex\"", html);

        var empty = await site.Client.GetStringAsync("search");
        Assert.Contains("Start here", empty);
        Assert.Contains("autofocus", empty);

        // "buttons" finds Button, and marks it.
        Assert.Contains("<mark class=\"ib-mark\">Button</mark>", await site.Client.GetStringAsync("search?q=buttons"));

        var none = WebUtility.HtmlDecode(await site.Client.GetStringAsync("search?q=zqxjv"));
        Assert.Contains("No results for “zqxjv”", none);
    }

    [GeneratedRegex("<a class=\"ib-toc-link\" href=\"[^\"#]*#(?<id>[^\"]+)\">(?<text>[^<]*)</a>")]
    private static partial Regex TocLink();
}
