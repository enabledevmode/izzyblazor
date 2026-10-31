using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A DataGrid's FilterRow in Chromium: a field that filters, and with no script.</summary>
public sealed class FilterRowTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private async Task<(TestBrowser, IPage, ILocator)> OpenAsync(string mode, bool script = true)
    {
        var browser = await TestBrowser.OpenAsync();
        var context = await browser.NewContextAsync(new() { JavaScriptEnabled = script, Locale = "en-US", ViewportSize = new() { Width = 1400, Height = 900 } });
        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/data-grid?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        if (mode == "interactive") await page.Locator("ib-table[data-ib-interactive]", new() { HasText = "Orders by row filter" }).WaitForAsync();
        var grid = page.Locator("ib-table", new() { HasText = "Orders by row filter" });
        await grid.ScrollIntoViewSettledAsync();
        if (script) await page.WaitForFunctionAsync("() => !!customElements.get('ib-table')", null, new() { PollingInterval = 100 });
        return (browser, page, grid);
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task Typing_in_a_field_filters_and_the_panel_says_the_same(string mode)
    {
        var (browser, page, grid) = await OpenAsync(mode);
        await using var _ = browser;
        var field = grid.GetByRole(AriaRole.Searchbox, new() { Name = "Filter Customer" }).Locator("visible=true");

        await field.FillAsync("ada");

        await page.WaitForURLAsync("**/*row-f-customer=ada*");
        await Assertions.Expect(grid.Locator("tbody tr").First).ToContainTextAsync("Ada");
        await Assertions.Expect(field).ToHaveValueAsync("ada");
        await Assertions.Expect(grid.Locator("th .ib-table-filter-button[aria-label^='Filter Customer']")).ToHaveAttributeAsync("data-active", "");
        Assert.Equal("ada", await grid.Locator("[popover] input[name='row-f-customer']").InputValueAsync());
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task Choosing_between_opens_the_panel_on_its_first_value(string mode)
    {
        var (browser, page, grid) = await OpenAsync(mode);
        await using var _ = browser;

        await grid.Locator(".ib-table-filter-row [data-ib-menu][aria-label^='Total']").ClickAsync();
        await page.Locator("[popover]:popover-open").GetByRole(AriaRole.Menuitemradio, new() { Name = "Between" })
            .Or(page.Locator("[popover]:popover-open").GetByRole(AriaRole.Menuitemcheckbox, new() { Name = "Between" })).ClickAsync();

        var from = grid.Locator("[popover]:popover-open input[name='row-f-total']");
        await from.WaitForAsync();
        await Assertions.Expect(grid.Locator(".ib-table-filter-row .ib-filter-cell-summary[aria-label^='Filter Total']")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task With_no_script_enter_in_a_field_posts_it_with_the_rest_of_the_state()
    {
        var (browser, page, grid) = await OpenAsync("static", script: false);
        await using var _ = browser;
        await page.GotoAsync(page.Url + "&row-f-total-op=between&row-f-total=1&row-sort=customer");
        grid = page.Locator("ib-table", new() { HasText = "Orders by row filter" });

        await grid.Locator("input[data-ib-row][name='row-f-customer']").FillAsync("a");
        await page.Keyboard.PressAsync("Enter");

        await page.WaitForURLAsync("**/*row-f-customer=a*");
        Assert.Contains("row-f-total-op=between", page.Url, StringComparison.Ordinal);
        Assert.Contains("row-sort=customer", page.Url, StringComparison.Ordinal);
    }
}
