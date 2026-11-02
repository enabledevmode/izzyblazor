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
        await table.ScrollIntoViewSettledAsync();
        if (mode == "interactive") await page.Locator("ib-table[data-ib-interactive]", new() { HasText = Caption }).WaitForAsync();
        return (browser, page, table);
    }

    private static async Task<ILocator> OpenPanelAsync(IPage page, ILocator table, string column)
    {
        await table.Locator($"th .ib-table-filter-button[aria-label^='Filter {column}']").ClickAsync();
        var panel = page.Locator("[popover]:popover-open .ib-table-filter-panel:visible");
        await panel.WaitForAsync();
        return panel;
    }

    // A static page's list waits for Apply, which closes it, so this opens it again; an interactive one's has applied.
    private static async Task<ILocator> AppliedAsync(IPage page, ILocator table, ILocator panel, string column, string mode)
    {
        if (mode != "static")
        {
            return panel;
        }

        await panel.Locator(".ib-table-filter-apply").ClickAsync();
        await panel.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        return await OpenPanelAsync(page, table, column);
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

    // A script writes a space as +, and C# as %20.
    private static string Address(IPage page) => page.Url.Replace("+", "%20", StringComparison.Ordinal);

    private static async Task AddressHasAsync(IPage page, string part, bool has = true)
    {
        for (var i = 0; i < 100 && Address(page).Contains(part, StringComparison.Ordinal) != has; i++)
        {
            await page.WaitForTimeoutAsync(50);
        }

        Assert.Equal(has, Address(page).Contains(part, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("interactive")]
    [InlineData("static")]
    public async Task Opening_lists_a_page_and_scrolling_loads_the_next(string mode)
    {
        var (browser, page, table) = await OpenAsync(mode);
        await using var _ = browser;

        var panel = await OpenPanelAsync(page, table, "Customer");
        await CountIsAsync(Rows(panel), 50);
        Assert.True(await All(panel).IsCheckedAsync());

        await Rows(panel).Last.ScrollIntoViewSettledAsync();
        await CountIsAsync(Rows(panel), 100);
    }

    [Theory]
    [InlineData("interactive")]
    [InlineData("static")]
    public async Task Unticking_writes_what_is_unticked_and_Back_undoes_it(string mode)
    {
        var (browser, page, table) = await OpenAsync(mode);
        await using var _ = browser;
        var panel = await OpenPanelAsync(page, table, "Customer");
        await CountIsAsync(Rows(panel), 50);

        await Box(panel, "Customer 001").UncheckAsync();
        panel = await AppliedAsync(page, table, panel, "Customer", mode);

        await AddressHasAsync(page, "values-f-customer-op=notin");
        await AddressHasAsync(page, "values-f-customer=Customer%20001");
        await Assertions.Expect(table.Locator("tbody tr").First).Not.ToContainTextAsync("Customer 001");
        Assert.False(await Box(panel, "Customer 001").IsCheckedAsync());
        Assert.True(await All(panel).EvaluateAsync<bool>("b => b.indeterminate"));
        Assert.True(await panel.IsVisibleAsync());

        await page.GoBackAsync();

        await AddressHasAsync(page, "values-f-customer", has: false);
        await Assertions.Expect(Box(panel, "Customer 001")).ToBeCheckedAsync();
    }

    [Theory]
    [InlineData("interactive")]
    [InlineData("static")]
    public async Task The_search_narrows_and_keeps_what_is_ticked_through_a_close(string mode)
    {
        var (browser, page, table) = await OpenAsync(mode);
        await using var _ = browser;
        var panel = await OpenPanelAsync(page, table, "Customer");
        await CountIsAsync(Rows(panel), 50);

        var search = panel.Locator("[data-ib-bound-search], [data-ib-list-search]");
        Assert.True(await search.EvaluateAsync<bool>("s => document.activeElement === s"));
        await search.PressSequentiallyAsync("Customer 00");
        await CountIsAsync(Rows(panel), 9);
        Assert.Contains("Select all 9 results", await panel.Locator(".ib-filter-list-all").InnerTextAsync(), StringComparison.Ordinal);

        await All(panel).UncheckAsync();
        if (mode == "static")
        {
            await panel.Locator(".ib-table-filter-apply").ClickAsync();
        }

        await AddressHasAsync(page, "values-f-customer-op=notin");
        Assert.Contains("Customer%20009", Address(page), StringComparison.Ordinal);

        if (mode != "static")
        {
            await page.Keyboard.PressAsync("Escape");
        }

        await panel.WaitForAsync(new() { State = WaitForSelectorState.Hidden });

        panel = await OpenPanelAsync(page, table, "Customer");
        Assert.Equal("", await panel.Locator("[data-ib-bound-search], [data-ib-list-search]").InputValueAsync());
        await CountIsAsync(Rows(panel), 50);

        // What's unticked comes first, to be ticked again.
        Assert.Equal("Customer 001", (await Rows(panel).First.InnerTextAsync()).Trim());
        Assert.False(await Box(panel, "Customer 001").IsCheckedAsync());
        Assert.True(await Box(panel, "Customer 010").IsCheckedAsync());
    }

    [Theory]
    [InlineData("interactive")]
    [InlineData("static")]
    public async Task The_other_filters_narrow_the_list(string mode)
    {
        var (browser, page, table) = await OpenAsync(mode, query: "&values-f-region=East");
        await using var _ = browser;

        var panel = await OpenPanelAsync(page, table, "Customer");
        await CountIsAsync(Rows(panel), 50);
        Assert.Equal("Customer 002", (await Rows(panel).First.InnerTextAsync()).Trim());
    }

    [Theory]
    [InlineData("static")]
    public async Task The_keyboard_moves_through_the_list_and_on_to_the_next_page(string mode)
    {
        var (browser, page, table) = await OpenAsync(mode);
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

        // Reaching the button brings the last row into view, so a page may be on its way already, and
        // which row is new depends on when: only that focus lands on a row is the keyboard's concern.
        await more.Locator("xpath=self::*[not(@disabled)]").WaitForAsync();
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(Rows(panel).Locator("input:focus")).ToHaveCountAsync(1);

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
        await table.ScrollIntoViewSettledAsync();
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

    [Fact]
    public async Task With_no_script_the_usual_filter_works_and_what_a_list_wrote_stands()
    {
        var (browser, page, table) = await OpenAsync("static", script: false, query: "&values-f-customer-op=notin&values-f-customer=Customer%20001");
        await using var _ = browser;

        var button = table.Locator("th .ib-table-filter-button[aria-label^='Filter Customer']");
        Assert.Contains("All but Customer 001", await button.GetAttributeAsync("aria-label") ?? "", StringComparison.Ordinal);

        // Applying another column keeps it.
        var panel = await OpenPanelAsync(page, table, "City");
        Assert.False(await panel.Locator("ib-filter-list").IsVisibleAsync());
        await panel.Locator("input[name='values-f-city']").FillAsync("City 01");
        await panel.Locator(".ib-table-filter-apply").ClickAsync();

        await AddressHasAsync(page, "values-f-city=City");
        await AddressHasAsync(page, "values-f-customer-op=notin");
        await AddressHasAsync(page, "values-f-customer=Customer");
    }

    private static Task HasFocusAsync(ILocator box) => Assertions.Expect(box).ToBeFocusedAsync();
}
