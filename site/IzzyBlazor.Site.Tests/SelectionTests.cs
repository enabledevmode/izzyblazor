using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A view's selection in Chromium: the count in the footer, the bar of actions, and what select-all reaches.</summary>
public sealed class SelectionTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private async Task<(TestBrowser, IPage, ILocator)> OpenAsync(string mode, string caption, bool script = true)
    {
        var browser = await TestBrowser.OpenAsync();
        var context = await browser.NewContextAsync(new() { JavaScriptEnabled = script, Locale = "en-US", ViewportSize = new() { Width = 1400, Height = 900 } });
        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/table?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var table = page.Locator("ib-table", new() { HasText = caption });
        await table.ScrollIntoViewIfNeededAsync();
        if (mode == "interactive") await page.Locator("ib-table[data-ib-interactive]", new() { HasText = caption }).WaitForAsync();
        if (script) await page.Locator("ib-table[data-ready]", new() { HasText = caption }).WaitForAsync();
        return (browser, page, table);
    }

    private static Task<double> TopAsync(ILocator table) => table.Locator("thead").EvaluateAsync<double>("h => h.getBoundingClientRect().top");

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task Selecting_moves_nothing_and_the_footer_counts(string mode)
    {
        var (browser, page, table) = await OpenAsync(mode, "Orders to archive");
        await using (browser)
        {
            var top = await TopAsync(table);
            var archive = table.GetByRole(AriaRole.Button, new() { Name = "Archive" });
            await Assertions.Expect(archive).ToBeDisabledAsync();
            await Assertions.Expect(table.Locator(".ib-selection-summary")).ToBeHiddenAsync();

            await table.Locator("tbody .ib-table-select").First.CheckAsync();

            await Assertions.Expect(table.Locator(".ib-data-footer .ib-selection-count")).ToHaveTextAsync("1 selected");
            await Assertions.Expect(archive).ToBeEnabledAsync();
            Assert.Equal(top, await TopAsync(table));

            await table.Locator(".ib-selection-clear").ClickAsync();
            await Assertions.Expect(table.Locator(".ib-selection-summary")).ToBeHiddenAsync();
            await Assertions.Expect(archive).ToBeDisabledAsync();
            await Assertions.Expect(table.Locator("tbody .ib-table-select").First).ToBeFocusedAsync();
            Assert.Equal(top, await TopAsync(table));
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task SelectAllMode_All_reaches_every_match_on_any_page(string mode)
    {
        var (browser, page, table) = await OpenAsync(mode, "Orders to archive");
        await using (browser)
        {
            var header = table.Locator(".ib-table-select-all");
            await header.CheckAsync();
            await Assertions.Expect(table.Locator(".ib-selection-count")).ToHaveTextAsync(new System.Text.RegularExpressions.Regex(@"^All \d+ selected$"));
            await Assertions.Expect(table.Locator("tbody .ib-table-select").First).ToBeCheckedAsync();

            // The next page's rows come selected.
            await table.Locator(".ib-pagination a, .ib-pagination button").Filter(new() { HasText = "2" }).First.ClickAsync();
            await Assertions.Expect(table.Locator("tbody .ib-table-select").First).ToBeCheckedAsync();

            // A row left out shows on the header, and clicking it there selects them all again.
            await table.Locator("tbody .ib-table-select").First.UncheckAsync();
            await Assertions.Expect(table.Locator(".ib-selection-count")).ToHaveTextAsync(new System.Text.RegularExpressions.Regex(@"^All \d+ selected$"));
            Assert.True(await header.EvaluateAsync<bool>("b => b.indeterminate"));

            await header.ClickAsync();
            await header.ClickAsync();
            await Assertions.Expect(table.Locator(".ib-selection-summary")).ToBeHiddenAsync();
            await Assertions.Expect(table.Locator("tbody .ib-table-select").First).Not.ToBeCheckedAsync();
        }
    }

    [Fact]
    public async Task SelectAllMode_All_posts_everything_for_Apply_to_find()
    {
        var (browser, page, table) = await OpenAsync("static", "Orders to archive");
        await using (browser)
        {
            await table.Locator(".ib-table-select-all").CheckAsync();
            await table.GetByRole(AriaRole.Button, new() { Name = "Archive" }).ClickAsync();

            var status = page.Locator("[role=status]", new() { HasText = "Archived" });
            await Assertions.Expect(status).ToBeVisibleAsync();
            Assert.DoesNotContain("Archived 0", await status.TextContentAsync(), StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task With_no_actions_there_is_no_bar_and_the_header_selects_the_page(string mode)
    {
        var (browser, page, table) = await OpenAsync(mode, "Recent orders");
        await using (browser)
        {
            var top = await TopAsync(table);
            await table.Locator(".ib-table-select-all").CheckAsync();

            await Assertions.Expect(table.Locator(".ib-selection-count")).ToHaveTextAsync(new System.Text.RegularExpressions.Regex(@"^\d+ selected$"));
            await Assertions.Expect(table.Locator(".ib-selection-bar")).ToBeHiddenAsync();
            Assert.Equal(top, await TopAsync(table));
        }
    }

    [Fact]
    public async Task With_no_script_the_actions_are_there_and_post_what_is_ticked()
    {
        var (browser, page, table) = await OpenAsync("static", "Orders to archive", script: false);
        await using (browser)
        {
            var archive = table.GetByRole(AriaRole.Button, new() { Name = "Archive" });
            await Assertions.Expect(archive).ToBeEnabledAsync();
            Assert.False(await table.Locator(".ib-table-select-all").IsVisibleAsync());

            await table.Locator("tbody .ib-table-select").First.CheckAsync();
            await archive.ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Assertions.Expect(page.Locator("[role=status]", new() { HasText = "Archived" })).ToBeVisibleAsync();
        }
    }
}
