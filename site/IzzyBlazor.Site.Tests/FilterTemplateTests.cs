using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A column's own filter controls in Chromium, on each kind of page.</summary>
public sealed class FilterTemplateTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private async Task<(TestBrowser, IPage, ILocator)> OpenAsync(string mode, bool script)
    {
        var browser = await TestBrowser.OpenAsync();
        var context = await browser.NewContextAsync(new() { JavaScriptEnabled = script, Locale = "en-US", ViewportSize = new() { Width = 1400, Height = 900 } });
        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/data-grid/filtering?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var grid = page.Locator("ib-table", new() { HasText = "Orders by custom filter" });
        await grid.ScrollIntoViewSettledAsync();
        if (mode == "interactive") await page.Locator("ib-table[data-ib-interactive]", new() { HasText = "Orders by custom filter" }).WaitForAsync();
        return (browser, page, grid);
    }

    [Theory]
    [InlineData("static", true)]
    [InlineData("noscript", false)]
    public async Task A_library_field_in_a_panel_filters_and_clears(string mode, bool script)
    {
        var (browser, page, grid) = await OpenAsync(mode, script);
        await using var _ = browser;
        var summary = grid.Locator(".ib-filter-cell-summary[aria-label^='Filter Total']");

        await summary.ClickAsync();
        var panel = page.Locator("[popover]:popover-open");
        await panel.Locator("select").SelectOptionAsync("400");
        if (mode != "interactive") await panel.Locator("button:has-text('Apply')").ClickAsync();

        await page.WaitForURLAsync("**/*own-f-total=400*");
        await Assertions.Expect(summary).ToHaveTextAsync("$400 or more");
        foreach (var total in await grid.Locator("tbody tr td:last-child").AllInnerTextsAsync())
        {
            Assert.True(decimal.Parse(total, System.Globalization.NumberStyles.Currency, System.Globalization.CultureInfo.GetCultureInfo("en-US")) >= 400, total);
        }

        await grid.Locator(".ib-filter-cell-clear").ClickAsync();
        await Assertions.Expect(summary).ToHaveTextAsync("");
        Assert.DoesNotContain("own-f-total", page.Url, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("static", true)]
    [InlineData("noscript", false)]
    public async Task Checkboxes_of_its_own_filter_by_any_of_them(string mode, bool script)
    {
        var (browser, page, grid) = await OpenAsync(mode, script);
        await using var _ = browser;
        var summary = grid.Locator(".ib-filter-cell-summary[aria-label^='Filter Customer']");

        await summary.ClickAsync();
        var panel = page.Locator("[popover]:popover-open");
        await panel.GetByLabel("Ada Lovelace").CheckAsync();
        await panel.GetByLabel("Alan Turing").CheckAsync();
        await panel.Locator("button:has-text('Apply')").ClickAsync();

        await page.WaitForURLAsync("**/*own-f-customer=*");
        await Assertions.Expect(summary).ToHaveTextAsync("Ada Lovelace, Alan Turing");
        var names = await grid.Locator("tbody tr td:nth-child(2)").AllInnerTextsAsync();
        Assert.NotEmpty(names);
        Assert.All(names, name => Assert.Contains(name.Trim(), new[] { "Ada Lovelace", "Alan Turing" }));
    }
}
