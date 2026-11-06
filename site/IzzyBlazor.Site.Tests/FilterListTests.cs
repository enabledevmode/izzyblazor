using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A list filter in Chromium: ticks after a pause, Back, Select all, the search, and with no script.</summary>
public sealed class FilterListTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private const string Prefix = "lists-";

    private async Task<(TestBrowser, IPage, ILocator)> OpenAsync(string mode, string caption = "Support tickets", bool script = true, string query = "", string path = "docs/components/table/filtering")
    {
        var browser = await TestBrowser.OpenAsync();
        var context = await browser.NewContextAsync(new() { JavaScriptEnabled = script, Locale = "en-US", ViewportSize = new() { Width = 1400, Height = 900 } });
        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"{path}?mode={mode}{query}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var table = page.Locator("ib-table", new() { HasText = caption });
        await table.ScrollIntoViewSettledAsync();
        if (mode == "interactive") await page.Locator("ib-table[data-ib-interactive]", new() { HasText = caption }).WaitForAsync();
        if (script) await page.WaitForFunctionAsync("() => !!customElements.get('ib-table') && !!customElements.get('ib-filter-list')", null, new() { PollingInterval = 100 });
        return (browser, page, table);
    }

    private static async Task<ILocator> OpenPanelAsync(IPage page, ILocator table, string column)
    {
        await table.Locator($"th .ib-table-filter-button[aria-label^='Filter {column}']").ClickAsync();
        var panel = page.Locator("[popover]:popover-open .ib-table-filter-panel");
        await panel.WaitForAsync();
        return panel;
    }

    private static ILocator Box(ILocator panel, string name) => panel.GetByRole(AriaRole.Checkbox, new() { Name = name, Exact = true });

    private static string Own(IPage page, string prefix) => string.Join("&",
        new Uri(page.Url).Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.StartsWith($"{prefix}f-", StringComparison.Ordinal))
            .Select(p => p[prefix.Length..])
            .Order(StringComparer.Ordinal));

    // The address settles on these of the table's filter keys, in any order.
    private static async Task AddressIsAsync(IPage page, string expected, string prefix = Prefix)
    {
        expected = string.Join("&", expected.Split('&', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal));
        for (var i = 0; i < 100 && Own(page, prefix) != expected; i++)
        {
            await page.WaitForTimeoutAsync(50);
        }

        Assert.Equal(expected, Own(page, prefix));
    }

    private static async Task RowsAreAsync(ILocator table, int count)
    {
        var rows = table.Locator("tbody tr:not(.ib-table-empty)");
        for (var i = 0; i < 100 && await rows.CountAsync() != count; i++)
        {
            await Task.Delay(50);
        }

        Assert.Equal(count, await rows.CountAsync());
    }

    [Theory]
    [InlineData("interactive")]
    public async Task Ticks_apply_after_a_pause_and_leave_the_panel_open(string mode)
    {
        var (browser, page, table) = await OpenAsync(mode);
        await using (browser)
        {
            var panel = await OpenPanelAsync(page, table, "Priority");
            await Box(panel, "Low").UncheckAsync();
            await Box(panel, "(Blank)").UncheckAsync();

            await AddressIsAsync(page, "f-priority=High&f-priority=Medium");
            await RowsAreAsync(table, 5);
            Assert.True(await panel.IsVisibleAsync());
            Assert.Equal("(Blank)", await page.EvaluateAsync<string>("() => document.activeElement.closest('label').textContent.trim()"));
            Assert.False(await Box(panel, "Low").IsCheckedAsync());

            await Box(panel, "Low").CheckAsync();
            await AddressIsAsync(page, "f-priority=High&f-priority=Low&f-priority=Medium");

            // The last row ticked again is no filter at all, so the address is clean.
            await Box(panel, "(Blank)").CheckAsync();
            await AddressIsAsync(page, "");
        }
    }

    [Fact]
    public async Task With_no_script_ticks_post_with_Apply()
    {
        var (browser, page, table) = await OpenAsync("static", script: false);
        await using (browser)
        {
            var panel = await OpenPanelAsync(page, table, "Priority");
            Assert.False(await panel.Locator("[data-ib-list-all]").IsVisibleAsync());

            await Box(panel, "Low").UncheckAsync();
            await panel.GetByRole(AriaRole.Button, new() { Name = "Apply" }).ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await RowsAreAsync(table, 5);
            await Assertions.Expect(table.Locator("th .ib-table-filter-button[aria-label='Filter Priority: 3 selected']")).ToBeVisibleAsync();
            await Assertions.Expect(table.Locator("th .ib-table-filter-button[data-active]")).ToHaveCountAsync(1);
            Assert.DoesNotContain("f-priority=Low", page.Url, StringComparison.Ordinal);

            // Everything ticked and posted reads as no filter, and draws no tag.
            panel = await OpenPanelAsync(page, table, "Priority");
            await Box(panel, "Low").CheckAsync();
            await panel.GetByRole(AriaRole.Button, new() { Name = "Apply" }).ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await RowsAreAsync(table, 5);
            await Assertions.Expect(table.Locator("th .ib-table-filter-button[data-active]")).ToHaveCountAsync(0);

            // A search box and Select all aren't there to post.
            panel = await OpenPanelAsync(page, table, "Team");
            Assert.False(await panel.Locator("[data-ib-list-search]").IsVisibleAsync());
        }
    }

    [Fact]
    public async Task A_bound_list_applies_in_C_sharp()
    {
        var (browser, page, table) = await OpenAsync("interactive", "Order history", path: "docs/components/table");
        await using (browser)
        {
            var url = page.Url;
            var panel = await OpenPanelAsync(page, table, "Status");
            var all = Box(panel, "(Select all)");

            await Box(panel, "Refunded").UncheckAsync();
            await Assertions.Expect(table.Locator("th .ib-table-filter-button[aria-label='Filter Status: 3 selected']")).ToBeVisibleAsync();
            Assert.True(await all.EvaluateAsync<bool>("b => b.indeterminate"));

            await all.ClickAsync();
            await Assertions.Expect(table.Locator("th .ib-table-filter-button[data-active]")).ToHaveCountAsync(0);
            await all.ClickAsync();
            await Assertions.Expect(table.Locator("th .ib-table-filter-button[aria-label='Filter Status: none']")).ToBeVisibleAsync();
            await Assertions.Expect(table.Locator(".ib-table-empty-content")).ToContainTextAsync("Nothing is selected under Status.");
            Assert.Equal(url, page.Url);
        }
    }
}
