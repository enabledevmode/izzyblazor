using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A list of looked-up values in Chromium: on an interactive page, and the usual panel in its place elsewhere.</summary>
public sealed class FilterValuesTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private const string Caption = "Orders by customer";

    private async Task<(TestBrowser, IPage, ILocator)> OpenAsync(string mode, bool script = true, string query = "")
    {
        var browser = await TestBrowser.OpenAsync();
        var context = await browser.NewContextAsync(new() { JavaScriptEnabled = script, Locale = "en-US", ViewportSize = new() { Width = 1400, Height = 900 } });
        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/table?mode={mode}{query}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var table = page.Locator("ib-table", new() { HasText = Caption });
        await table.ScrollIntoViewIfNeededAsync();
        if (mode == "interactive") await page.Locator("ib-table[data-ib-interactive]", new() { HasText = Caption }).WaitForAsync();
        return (browser, page, table);
    }

    private static async Task<ILocator> OpenPanelAsync(IPage page, ILocator table, string column)
    {
        await table.Locator($"th .ib-table-filter-button[aria-label^='Filter {column}']").ClickAsync();
        var panel = page.Locator("[popover]:popover-open .ib-table-filter-panel");
        await panel.WaitForAsync();
        return panel;
    }

    private static ILocator Rows(ILocator panel) => panel.Locator(".ib-filter-list-items > label");

    private static ILocator Box(ILocator panel, string name) => panel.GetByRole(AriaRole.Checkbox, new() { Name = name, Exact = true });

    private static ILocator All(ILocator panel) => panel.Locator("[data-ib-list-all]");

    private static async Task CountIsAsync(ILocator rows, int count)
    {
        for (var i = 0; i < 100 && await rows.CountAsync() != count; i++)
        {
            await Task.Delay(50);
        }

        Assert.Equal(count, await rows.CountAsync());
    }

    private static async Task AddressHasAsync(IPage page, string part, bool has = true)
    {
        for (var i = 0; i < 100 && page.Url.Contains(part, StringComparison.Ordinal) != has; i++)
        {
            await page.WaitForTimeoutAsync(50);
        }

        Assert.Equal(has, page.Url.Contains(part, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Opening_lists_a_page_and_scrolling_loads_the_next()
    {
        var (browser, page, table) = await OpenAsync("interactive");
        await using var _ = browser;

        var panel = await OpenPanelAsync(page, table, "Customer");
        await CountIsAsync(Rows(panel), 50);
        Assert.True(await All(panel).IsCheckedAsync());

        await Rows(panel).Last.ScrollIntoViewIfNeededAsync();
        await CountIsAsync(Rows(panel), 100);
    }

    [Fact]
    public async Task Unticking_writes_what_is_unticked_and_Back_undoes_it()
    {
        var (browser, page, table) = await OpenAsync("interactive");
        await using var _ = browser;
        var panel = await OpenPanelAsync(page, table, "Customer");
        await CountIsAsync(Rows(panel), 50);

        await Box(panel, "Customer 001").UncheckAsync();

        await AddressHasAsync(page, "values-f-customer-op=notin");
        await AddressHasAsync(page, "values-f-customer=Customer%20001");
        Assert.False(await Box(panel, "Customer 001").IsCheckedAsync());
        Assert.True(await All(panel).EvaluateAsync<bool>("b => b.indeterminate"));
        Assert.True(await panel.IsVisibleAsync());

        await page.GoBackAsync();

        await AddressHasAsync(page, "values-f-customer", has: false);
        await Assertions.Expect(Box(panel, "Customer 001")).ToBeCheckedAsync();
    }

    [Fact]
    public async Task Clearing_select_all_shows_no_rows_and_a_tick_shows_that_one()
    {
        var (browser, page, table) = await OpenAsync("interactive");
        await using var _ = browser;
        var panel = await OpenPanelAsync(page, table, "Customer");
        await CountIsAsync(Rows(panel), 50);

        await All(panel).UncheckAsync();
        await AddressHasAsync(page, "values-f-customer-none=1");
        await CountIsAsync(table.Locator("tbody tr:not(.ib-table-empty)"), 0);

        await Box(panel, "Customer 002").CheckAsync();
        await AddressHasAsync(page, "values-f-customer-none", has: false);
        await AddressHasAsync(page, "values-f-customer-op=in");
        await CountIsAsync(table.Locator("tbody tr:not(.ib-table-empty)"), 1);
    }

    [Fact]
    public async Task The_search_narrows_and_keeps_what_is_ticked_through_a_close()
    {
        var (browser, page, table) = await OpenAsync("interactive");
        await using var _ = browser;
        var panel = await OpenPanelAsync(page, table, "Customer");
        await CountIsAsync(Rows(panel), 50);

        var search = panel.Locator("[data-ib-bound-search]");
        Assert.True(await search.EvaluateAsync<bool>("s => document.activeElement === s"));
        await search.PressSequentiallyAsync("Customer 00");
        await CountIsAsync(Rows(panel), 9);
        Assert.Contains("Select all 9 results", await panel.Locator(".ib-filter-list-all").InnerTextAsync(), StringComparison.Ordinal);

        await All(panel).UncheckAsync();
        await AddressHasAsync(page, "values-f-customer-op=notin");
        Assert.Contains("Customer%20009", page.Url, StringComparison.Ordinal);

        await page.Keyboard.PressAsync("Escape");
        await panel.WaitForAsync(new() { State = WaitForSelectorState.Hidden });

        panel = await OpenPanelAsync(page, table, "Customer");
        Assert.Equal("", await panel.Locator("[data-ib-bound-search]").InputValueAsync());
        await CountIsAsync(Rows(panel), 50);

        // What's unticked comes first, to be ticked again.
        Assert.Equal("Customer 001", (await Rows(panel).First.InnerTextAsync()).Trim());
        Assert.False(await Box(panel, "Customer 001").IsCheckedAsync());
        Assert.True(await Box(panel, "Customer 010").IsCheckedAsync());
    }

    [Fact]
    public async Task The_other_filters_narrow_the_list()
    {
        var (browser, page, table) = await OpenAsync("interactive", query: "&values-f-region=East");
        await using var _ = browser;

        var panel = await OpenPanelAsync(page, table, "Customer");
        await CountIsAsync(Rows(panel), 50);
        Assert.Equal("Customer 002", (await Rows(panel).First.InnerTextAsync()).Trim());
    }

    [Fact]
    public async Task A_short_list_has_its_blank_row_and_all_ticked_is_no_filter()
    {
        var (browser, page, table) = await OpenAsync("interactive");
        await using var _ = browser;
        var panel = await OpenPanelAsync(page, table, "City");
        await CountIsAsync(Rows(panel), 13);

        await Box(panel, "(Blank)").UncheckAsync();
        await AddressHasAsync(page, "values-f-city-blank=1");

        await Box(panel, "(Blank)").CheckAsync();
        await AddressHasAsync(page, "values-f-city", has: false);
    }

    [Fact]
    public async Task The_keyboard_moves_through_the_list_and_on_to_the_next_page()
    {
        var (browser, page, table) = await OpenAsync("interactive");
        await using var _ = browser;
        var panel = await OpenPanelAsync(page, table, "Customer");
        await CountIsAsync(Rows(panel), 50);

        await page.Keyboard.PressAsync("ArrowDown");
        await page.Keyboard.PressAsync("ArrowDown");
        Assert.True(await Box(panel, "Customer 001").EvaluateAsync<bool>("b => document.activeElement === b"));

        // One Tab stop among the rows, then the button that's the keyboard's way on.
        await page.Keyboard.PressAsync("Tab");
        var more = panel.Locator("[data-ib-list-more]");
        Assert.True(await more.EvaluateAsync<bool>("b => document.activeElement === b"));

        // Reaching the button brings the last row into view, so a page may be on its way already. Wait
        // for it to arrive and leave the button pressable.
        await more.Locator("xpath=self::*[not(@disabled)]").WaitForAsync();
        var before = await Rows(panel).CountAsync();
        await page.Keyboard.PressAsync("Enter");
        var first = Rows(panel).Nth(before).Locator("input");
        await first.WaitForAsync(new() { State = WaitForSelectorState.Attached });
        await Assertions.Expect(first).ToBeFocusedAsync();

        await page.Keyboard.PressAsync("Escape");
        await panel.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        Assert.True(await table.Locator("th .ib-table-filter-button[aria-label^='Filter Customer']").EvaluateAsync<bool>("b => document.activeElement === b"));
    }

    [Fact]
    public async Task A_bound_list_tells_C_sharp_and_leaves_the_address_alone()
    {
        var (browser, page, _) = await OpenAsync("interactive");
        await using var _1 = browser;
        var table = page.Locator("ib-table[data-ib-interactive]:has(caption:text-is('Order history'))");
        await table.ScrollIntoViewIfNeededAsync();
        var shown = table.Locator("xpath=..").Locator("code").Last;
        var url = page.Url;

        var panel = await OpenPanelAsync(page, table, "Customer");
        await Assertions.Expect(Rows(panel).First).ToBeVisibleAsync();
        var name = (await Rows(panel).First.InnerTextAsync()).Trim();

        await Box(panel, name).UncheckAsync();
        await Assertions.Expect(shown).ToContainTextAsync($"not (customer in ('{name}'))");
        Assert.Equal(url, page.Url);
        await Assertions.Expect(All(panel)).Not.ToBeCheckedAsync();
        Assert.True(await All(panel).EvaluateAsync<bool>("b => b.indeterminate"));

        await All(panel).CheckAsync();
        await Assertions.Expect(shown).ToHaveTextAsync("none");
        await Assertions.Expect(Box(panel, name)).ToBeCheckedAsync();
    }

    [Theory]
    [InlineData("static", true)]
    [InlineData("static", false)]
    public async Task Where_there_is_no_list_the_usual_filter_works_and_what_a_list_wrote_stands(string mode, bool script)
    {
        var (browser, page, table) = await OpenAsync(mode, script, "&values-f-customer-op=notin&values-f-customer=Customer%20001");
        await using var _ = browser;

        Assert.Equal(0, await table.Locator("ib-filter-list[data-ib-list-remote]").CountAsync());
        var button = table.Locator("th .ib-table-filter-button[aria-label^='Filter Customer']");
        Assert.Contains("All but Customer 001", await button.GetAttributeAsync("aria-label") ?? "", StringComparison.Ordinal);

        // Applying another column keeps it.
        var panel = await OpenPanelAsync(page, table, "City");
        await panel.Locator("input[name='values-f-city']").FillAsync("City 01");
        if (script) await page.Keyboard.PressAsync("Enter");
        else await panel.Locator(".ib-table-filter-apply").ClickAsync();

        await AddressHasAsync(page, "values-f-city=City");
        await AddressHasAsync(page, "values-f-customer-op=notin");
        await AddressHasAsync(page, "values-f-customer=Customer");
    }
}
