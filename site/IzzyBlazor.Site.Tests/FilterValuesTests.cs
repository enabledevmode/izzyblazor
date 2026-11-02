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
    public async Task Clearing_select_all_shows_no_rows_and_a_tick_shows_that_one(string mode)
    {
        var (browser, page, table) = await OpenAsync(mode);
        await using var _ = browser;
        var panel = await OpenPanelAsync(page, table, "Customer");
        await CountIsAsync(Rows(panel), 50);

        await All(panel).UncheckAsync();
        panel = await AppliedAsync(page, table, panel, "Customer", mode);
        await AddressHasAsync(page, "values-f-customer-none=1");
        await CountIsAsync(table.Locator("tbody tr:not(.ib-table-empty)"), 0);

        await Box(panel, "Customer 002").CheckAsync();
        await AppliedAsync(page, table, panel, "Customer", mode);
        await AddressHasAsync(page, "values-f-customer-none", has: false);
        await AddressHasAsync(page, "values-f-customer-op=in");
        await CountIsAsync(table.Locator("tbody tr:not(.ib-table-empty)"), 1);
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
    [InlineData("interactive")]
    public async Task A_short_list_has_its_blank_row_and_all_ticked_is_no_filter(string mode)
    {
        var (browser, page, table) = await OpenAsync(mode);
        await using var _ = browser;
        var panel = await OpenPanelAsync(page, table, "City");
        await CountIsAsync(Rows(panel), 13);

        await Box(panel, "(Blank)").UncheckAsync();
        panel = await AppliedAsync(page, table, panel, "City", mode);
        await AddressHasAsync(page, "values-f-city-blank=1");

        await Box(panel, "(Blank)").CheckAsync();
        await AppliedAsync(page, table, panel, "City", mode);
        await AddressHasAsync(page, "values-f-city", has: false);
    }

    [Theory]
    [InlineData("interactive")]
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

    [Fact]
    public async Task A_static_lists_fetch_never_shows_in_the_address_or_the_history()
    {
        var (browser, page, table) = await OpenAsync("static");
        await using var _ = browser;
        var length = await page.EvaluateAsync<int>("() => history.length");

        var panel = await OpenPanelAsync(page, table, "Customer");
        await CountIsAsync(Rows(panel), 50);

        Assert.DoesNotContain("fv", new Uri(page.Url).Query, StringComparison.Ordinal);
        Assert.Equal(length, await page.EvaluateAsync<int>("() => history.length"));
    }

    [Fact]
    public async Task A_static_search_cancels_the_one_before_and_shows_the_last()
    {
        var (browser, page, table) = await OpenAsync("static");
        await using var _ = browser;
        var panel = await OpenPanelAsync(page, table, "Customer");
        await CountIsAsync(Rows(panel), 50);

        // The first answer held back past the second, which only an abort keeps off the screen.
        var aborted = false;
        page.RequestFailed += (_, request) => aborted |= request.Url.Contains("Customer+1", StringComparison.Ordinal);
        await page.RouteAsync("**/*fv-search=Customer+1*", async route =>
        {
            await Task.Delay(1500);
            await route.ContinueAsync();
        });

        var search = panel.Locator("[data-ib-list-search]");
        await search.FillAsync("Customer 1");
        await page.WaitForTimeoutAsync(500);
        await search.FillAsync("Customer 59");
        await CountIsAsync(Rows(panel), 10);
        Assert.StartsWith("Customer 59", (await Rows(panel).First.InnerTextAsync()).Trim(), StringComparison.Ordinal);
        await page.WaitForTimeoutAsync(1500);
        await CountIsAsync(Rows(panel), 10);
        Assert.True(aborted);
    }

    private static Task HasFocusAsync(ILocator box) => Assertions.Expect(box).ToBeFocusedAsync();

    [Fact]
    public async Task A_later_page_that_fails_asks_once_and_offers_Retry()
    {
        var (browser, page, table) = await OpenAsync("static");
        await using var _ = browser;
        var panel = await OpenPanelAsync(page, table, "Customer");
        await CountIsAsync(Rows(panel), 50);

        var asked = 0;
        var fail = true;
        await page.RouteAsync("**/*fv-skip=*", route =>
        {
            asked++;
            return fail ? route.FulfillAsync(new() { Status = 500, Body = "down" }) : route.ContinueAsync();
        });

        await Rows(panel).Last.ScrollIntoViewSettledAsync();
        var retry = panel.Locator("[data-ib-list-retry]");
        await retry.WaitForAsync();
        await page.WaitForTimeoutAsync(600);
        Assert.Equal(1, asked);
        await Assertions.Expect(panel.Locator(".ib-filter-list-status")).ToHaveTextAsync("Couldn't load the values.");

        fail = false;
        await retry.ClickAsync();
        await CountIsAsync(Rows(panel), 100);
        Assert.Equal(50, await Rows(panel).Locator("text=/Customer 0(5[1-9]|[6-9]\\d)|Customer 100/").CountAsync());
    }

    [Theory]
    [InlineData("interactive")]
    [InlineData("static")]
    public async Task Focus_stays_on_its_row_as_the_next_page_arrives(string mode)
    {
        var (browser, page, table) = await OpenAsync(mode);
        await using var _ = browser;
        var panel = await OpenPanelAsync(page, table, "Customer");
        await CountIsAsync(Rows(panel), 50);

        await Box(panel, "Customer 001").FocusAsync();
        await page.Keyboard.PressAsync("End");
        await HasFocusAsync(Box(panel, "Customer 050"));
        await CountIsAsync(Rows(panel), 100);

        await HasFocusAsync(Box(panel, "Customer 050"));
        await page.Keyboard.PressAsync("ArrowDown");
        await HasFocusAsync(Box(panel, "Customer 051"));
    }

    [Fact]
    public async Task Space_ticks_the_row_focused_when_a_search_answers_late()
    {
        var (browser, page, table) = await OpenAsync("static");
        await using var _ = browser;
        var panel = await OpenPanelAsync(page, table, "Customer");
        await CountIsAsync(Rows(panel), 50);
        await page.RouteAsync("**/*fv-search=*", async route =>
        {
            await Task.Delay(800);
            await route.ContinueAsync();
        });

        await panel.Locator("[data-ib-list-search]").FillAsync("Customer 00");
        await Box(panel, "Customer 003").FocusAsync();
        await CountIsAsync(Rows(panel), 9);

        await HasFocusAsync(Box(panel, "Customer 003"));
        await page.Keyboard.PressAsync("Space");
        await panel.Locator(".ib-table-filter-apply").ClickAsync();
        await AddressHasAsync(page, "values-f-customer=Customer%20003");
    }

    [Fact]
    public async Task A_focused_row_a_search_takes_away_hands_focus_to_the_box()
    {
        var (browser, page, table) = await OpenAsync("static");
        await using var _ = browser;
        var panel = await OpenPanelAsync(page, table, "Customer");
        await CountIsAsync(Rows(panel), 50);
        await page.RouteAsync("**/*fv-search=*", async route =>
        {
            await Task.Delay(800);
            await route.ContinueAsync();
        });

        var search = panel.Locator("[data-ib-list-search]");
        await search.FillAsync("Customer 59");
        await Box(panel, "Customer 003").FocusAsync();
        await CountIsAsync(Rows(panel), 10);

        await HasFocusAsync(search);
    }

    [Fact]
    public async Task Scrolling_doesnt_drop_a_search_on_its_way()
    {
        var (browser, page, table) = await OpenAsync("static");
        await using var _ = browser;
        var panel = await OpenPanelAsync(page, table, "Customer");
        await CountIsAsync(Rows(panel), 50);

        await panel.Locator("[data-ib-list-search]").PressSequentiallyAsync("Customer 59");
        await Rows(panel).Last.ScrollIntoViewSettledAsync();

        await CountIsAsync(Rows(panel), 10);
    }

    [Fact]
    public async Task Opening_again_after_a_search_shows_none_of_what_it_found()
    {
        var (browser, page, table) = await OpenAsync("static");
        await using var _ = browser;
        var panel = await OpenPanelAsync(page, table, "Customer");
        await CountIsAsync(Rows(panel), 50);
        await panel.Locator("[data-ib-list-search]").FillAsync("Customer 59");
        await CountIsAsync(Rows(panel), 10);
        await page.Keyboard.PressAsync("Escape");
        await panel.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        Assert.False(await table.Locator("ib-filter-list[data-ib-list-fetch]").First.EvaluateAsync<bool>("l => l.hasAttribute('aria-busy')"));

        await page.RouteAsync("**/*fv=*", async route =>
        {
            await Task.Delay(500);
            await route.ContinueAsync();
        });
        panel = await OpenPanelAsync(page, table, "Customer");

        Assert.Equal(0, await Rows(panel).CountAsync());
        await CountIsAsync(Rows(panel), 50);
    }

    [Fact]
    public async Task An_interactive_list_keeps_a_tab_stop_after_a_search()
    {
        var (browser, page, table) = await OpenAsync("interactive");
        await using var _ = browser;
        var panel = await OpenPanelAsync(page, table, "City");
        await CountIsAsync(Rows(panel), 13);
        await Box(panel, "City 05").FocusAsync();
        await panel.Locator("[data-ib-bound-search]").FillAsync("City 01");
        await CountIsAsync(Rows(panel), 1);

        Assert.Equal(1, await panel.Locator(".ib-filter-list-items input[tabindex='0']").CountAsync());
    }
}
