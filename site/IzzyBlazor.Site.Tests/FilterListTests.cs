using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A list filter in Chromium: ticks after a pause, Back, Select all, the search, and with no script.</summary>
public sealed class FilterListTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private const string Prefix = "lists-";

    private async Task<(TestBrowser, IPage, ILocator)> OpenAsync(string mode, string caption = "Support tickets", bool script = true, string query = "")
    {
        var browser = await TestBrowser.OpenAsync();
        var context = await browser.NewContextAsync(new() { JavaScriptEnabled = script, Locale = "en-US", ViewportSize = new() { Width = 1400, Height = 900 } });
        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/table?mode={mode}{query}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var table = page.Locator("ib-table", new() { HasText = caption });
        await table.ScrollIntoViewIfNeededAsync();
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
    [InlineData("static")]
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

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task Select_all_clears_to_no_rows_and_back(string mode)
    {
        var (browser, page, table) = await OpenAsync(mode);
        await using (browser)
        {
            var panel = await OpenPanelAsync(page, table, "Priority");
            var all = Box(panel, "Select all");
            Assert.True(await all.IsCheckedAsync());

            await Box(panel, "Low").UncheckAsync();
            Assert.True(await all.EvaluateAsync<bool>("b => b.indeterminate"));

            await all.ClickAsync();
            Assert.True(await Box(panel, "Low").IsCheckedAsync());
            await AddressIsAsync(page, "");

            await all.ClickAsync();
            Assert.False(await Box(panel, "High").IsCheckedAsync());
            await AddressIsAsync(page, "f-priority-none=1");
            await RowsAreAsync(table, 0);
            await Assertions.Expect(table.Locator(".ib-table-empty-content")).ToContainTextAsync("Nothing is selected under Priority.");
            await Assertions.Expect(table.Locator("th .ib-table-filter-button[aria-label='Filter Priority: none']")).ToBeVisibleAsync();

            await all.ClickAsync();
            await AddressIsAsync(page, "");
            await RowsAreAsync(table, 5);
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task A_panels_Clear_removes_its_filter_and_closes(string mode)
    {
        var (browser, page, table) = await OpenAsync(mode, query: "&lists-f-priority=High&lists-f-open=true");
        await using (browser)
        {
            var panel = await OpenPanelAsync(page, table, "Priority");
            await Box(panel, "Medium").CheckAsync();
            await panel.Locator("[data-ib-filter-clear]").ClickAsync();

            await AddressIsAsync(page, "f-open=true");
            await page.WaitForTimeoutAsync(700);
            await AddressIsAsync(page, "f-open=true");
            await Assertions.Expect(page.Locator("[popover]:popover-open .ib-table-filter-panel")).ToHaveCountAsync(0);
            await Assertions.Expect(table.Locator("th .ib-table-filter-button[aria-label='Filter Priority']")).ToBeFocusedAsync();

            panel = await OpenPanelAsync(page, table, "Priority");
            await Assertions.Expect(Box(panel, "Low")).ToBeCheckedAsync();
            await Assertions.Expect(panel.GetByRole(AriaRole.Button, new() { Name = "Clear" })).ToBeDisabledAsync();
        }
    }

    [Fact]
    public async Task A_bound_panels_Clear_removes_its_filter_and_closes()
    {
        var (browser, page, table) = await OpenAsync("interactive", "Order history");
        await using (browser)
        {
            var panel = await OpenPanelAsync(page, table, "Status");
            await Assertions.Expect(panel.GetByRole(AriaRole.Button, new() { Name = "Clear" })).ToBeDisabledAsync();
            await Box(panel, "Refunded").UncheckAsync();
            await panel.Locator("[data-ib-filter-clear]").ClickAsync();

            await Assertions.Expect(table.Locator("th .ib-table-filter-button[data-active]")).ToHaveCountAsync(0);
            await Assertions.Expect(page.Locator("[popover]:popover-open .ib-table-filter-panel")).ToHaveCountAsync(0);
            await Assertions.Expect(table.Locator("th .ib-table-filter-button[aria-label='Filter Status']")).ToBeFocusedAsync();
        }
    }

    [Fact]
    public async Task With_no_script_a_panels_Clear_is_a_link()
    {
        var (browser, page, table) = await OpenAsync("static", script: false, query: "&lists-f-priority=High");
        await using (browser)
        {
            var panel = await OpenPanelAsync(page, table, "Priority");
            await panel.Locator("[data-ib-filter-clear]").ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await AddressIsAsync(page, "");
        }
    }

    [Fact]
    public async Task A_tick_made_while_a_Clear_is_on_its_way_doesnt_bring_it_back()
    {
        var (browser, page, table) = await OpenAsync("static", query: "&lists-f-open=true");
        await using (browser)
        {
            await page.RouteAsync("**/docs/components/table**", async route =>
            {
                await Task.Delay(1200);
                await route.ContinueAsync();
            });
            var open = await OpenPanelAsync(page, table, "Open");
            await open.Locator("[data-ib-filter-clear]").ClickAsync();
            var panel = await OpenPanelAsync(page, table, "Priority");
            await Box(panel, "Low").UncheckAsync();

            await AddressIsAsync(page, "f-priority=High&f-priority=Medium&f-priority-blank=1");
            await page.WaitForTimeoutAsync(1800);
            await AddressIsAsync(page, "f-priority=High&f-priority=Medium&f-priority-blank=1");
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task Back_during_the_pause_puts_the_box_back(string mode)
    {
        var (browser, page, table) = await OpenAsync(mode);
        await using (browser)
        {
            var panel = await OpenPanelAsync(page, table, "Priority");
            await Box(panel, "Low").UncheckAsync();
            await AddressIsAsync(page, "f-priority=High&f-priority=Medium&f-priority-blank=1");
            await Box(panel, "Medium").UncheckAsync();
            await page.GoBackAsync();

            await AddressIsAsync(page, "");
            await page.WaitForTimeoutAsync(700);
            await AddressIsAsync(page, "");
            await Assertions.Expect(table.Locator("input[name='lists-f-priority'][value='Medium']")).ToBeCheckedAsync();
            await Assertions.Expect(table.Locator("input[name='lists-f-priority'][value='Low']")).ToBeCheckedAsync();
        }
    }

    [Fact]
    public async Task A_tick_waiting_on_its_pause_stays_off_the_next_page()
    {
        var (browser, page, table) = await OpenAsync("static");
        await using (browser)
        {
            await page.RouteAsync("**/docs/components/card**", async route =>
            {
                await Task.Delay(800);
                await route.ContinueAsync();
            });
            var panel = await OpenPanelAsync(page, table, "Priority");
            await Box(panel, "Low").UncheckAsync();
            await page.Keyboard.PressAsync("Escape");
            await page.Locator("a[href$='docs/components/card']").First.ClickAsync();
            await page.WaitForURLAsync("**/docs/components/card**");
            await page.WaitForTimeoutAsync(1200);

            Assert.DoesNotContain("lists-", page.Url, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task Filtering_doesnt_move_the_table(string mode)
    {
        var (browser, page, table) = await OpenAsync(mode);
        await using (browser)
        {
            var head = table.Locator("thead");
            const string top = "h => h.getBoundingClientRect().top";
            var before = await head.EvaluateAsync<double>(top);
            var panel = await OpenPanelAsync(page, table, "Priority");
            await Box(panel, "Low").UncheckAsync();
            await AddressIsAsync(page, "f-priority=High&f-priority=Medium&f-priority-blank=1");
            await page.WaitForTimeoutAsync(500);

            Assert.Equal(before, await head.EvaluateAsync<double>(top));
        }
    }

    [Fact]
    public async Task A_tick_made_while_a_response_is_on_its_way_stays()
    {
        var (browser, page, table) = await OpenAsync("static");
        await using (browser)
        {
            await page.RouteAsync("**/docs/components/table**", async route =>
            {
                await Task.Delay(900);
                await route.ContinueAsync();
            });
            var panel = await OpenPanelAsync(page, table, "Priority");
            await Box(panel, "Low").UncheckAsync();
            await AddressIsAsync(page, "f-priority=High&f-priority=Medium&f-priority-blank=1");

            await Box(panel, "Medium").UncheckAsync();
            await page.WaitForTimeoutAsync(100);
            await AddressIsAsync(page, "f-priority=High&f-priority-blank=1");

            await page.WaitForTimeoutAsync(2000);
            Assert.False(await Box(panel, "Low").IsCheckedAsync());
            Assert.False(await Box(panel, "Medium").IsCheckedAsync());
            Assert.True(await Box(panel, "High").IsCheckedAsync());
            await RowsAreAsync(table, 5);
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task Back_puts_the_ticks_back(string mode)
    {
        var (browser, page, table) = await OpenAsync(mode);
        await using (browser)
        {
            var panel = await OpenPanelAsync(page, table, "Priority");
            await Box(panel, "Low").UncheckAsync();
            await AddressIsAsync(page, "f-priority=High&f-priority=Medium&f-priority-blank=1");
            await Box(panel, "Medium").UncheckAsync();
            await AddressIsAsync(page, "f-priority=High&f-priority-blank=1");

            await page.GoBackAsync();
            await AddressIsAsync(page, "f-priority=High&f-priority=Medium&f-priority-blank=1");
            await Assertions.Expect(table.Locator("input[name='lists-f-priority'][value='Medium']")).ToBeCheckedAsync();
            await Assertions.Expect(table.Locator("input[name='lists-f-priority'][value='Low']")).Not.ToBeCheckedAsync();

            await page.GoBackAsync();
            await AddressIsAsync(page, "");
            await Assertions.Expect(table.Locator("input[name='lists-f-priority'][value='Low']")).ToBeCheckedAsync();

            // What was ticked before Back doesn't come back after a pause.
            await page.WaitForTimeoutAsync(600);
            await AddressIsAsync(page, "");
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task The_search_narrows_the_rows_and_Select_all_follows_it(string mode)
    {
        var (browser, page, table) = await OpenAsync(mode);
        await using (browser)
        {
            var panel = await OpenPanelAsync(page, table, "Team");
            var search = panel.GetByRole(AriaRole.Searchbox);
            await Assertions.Expect(search).ToBeFocusedAsync();

            await search.PressSequentiallyAsync("se");
            await Assertions.Expect(panel.Locator(".ib-filter-list-items label:visible")).ToHaveCountAsync(2);   // Research, Security
            await Assertions.Expect(Box(panel, "Select all 2 results")).ToBeVisibleAsync();
            await page.WaitForTimeoutAsync(500);
            await AddressIsAsync(page, "");

            // Select all takes what's listed and leaves the rest.
            await panel.Locator("[data-ib-list-all]").UncheckAsync();
            await AddressIsAsync(page, string.Join("&", new[] { "Billing", "Design", "Engineering", "Finance", "Legal", "Marketing", "Operations", "Product", "Sales", "Support" }.Select(t => $"f-team={t}")));
            await RowsAreAsync(table, 5);

            // The response hasn't undone the search.
            await Assertions.Expect(search).ToHaveValueAsync("se");
            await Assertions.Expect(panel.Locator(".ib-filter-list-items label:visible")).ToHaveCountAsync(2);

            // Closing ends it.
            await search.PressAsync("Escape");
            await Assertions.Expect(table.Locator("th .ib-table-filter-button[aria-label^='Filter Team']")).ToBeFocusedAsync();
            await page.WaitForFunctionAsync("() => !document.querySelector('[popover]:popover-open .ib-table-filter-panel')");
            panel = await OpenPanelAsync(page, table, "Team");
            await Assertions.Expect(panel.GetByRole(AriaRole.Searchbox)).ToHaveValueAsync("");
            await Assertions.Expect(panel.Locator(".ib-filter-list-items label:visible")).ToHaveCountAsync(12);
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task Keys_move_tick_and_close(string mode)
    {
        var (browser, page, table) = await OpenAsync(mode);
        await using (browser)
        {
            var panel = await OpenPanelAsync(page, table, "Priority");
            var all = Box(panel, "Select all");
            await Assertions.Expect(all).ToBeFocusedAsync();

            await page.Keyboard.PressAsync("ArrowDown");
            await Assertions.Expect(Box(panel, "High")).ToBeFocusedAsync();
            await page.Keyboard.PressAsync("ArrowDown");
            await Assertions.Expect(Box(panel, "Medium")).ToBeFocusedAsync();
            await page.Keyboard.PressAsync("ArrowUp");
            await Assertions.Expect(Box(panel, "High")).ToBeFocusedAsync();

            await page.Keyboard.PressAsync("Space");
            Assert.False(await Box(panel, "High").IsCheckedAsync());

            // Home and End go to the ends, and the rows are one Tab stop.
            await page.Keyboard.PressAsync("End");
            await Assertions.Expect(Box(panel, "(Blank)")).ToBeFocusedAsync();
            await page.Keyboard.PressAsync("Home");
            await Assertions.Expect(Box(panel, "High")).ToBeFocusedAsync();
            await page.Keyboard.PressAsync("Shift+Tab");
            await Assertions.Expect(all).ToBeFocusedAsync();
            await page.Keyboard.PressAsync("Tab");
            await Assertions.Expect(Box(panel, "High")).ToBeFocusedAsync();
            Assert.Equal(1, await panel.Locator(".ib-filter-list-items input").EvaluateAllAsync<int>("boxes => boxes.filter(b => b.tabIndex === 0).length"));

            // Enter applies at once and closes, and focus goes back to the button.
            await page.Keyboard.PressAsync("Enter");
            await AddressIsAsync(page, "f-priority=Medium&f-priority=Low&f-priority-blank=1");
            await Assertions.Expect(table.Locator("th .ib-table-filter-button[aria-label^='Filter Priority']")).ToBeFocusedAsync();
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
    public async Task A_bound_list_applies_in_C_sharp_and_Clear_redraws_it()
    {
        var (browser, page, table) = await OpenAsync("interactive", "Order history");
        await using (browser)
        {
            var url = page.Url;
            var panel = await OpenPanelAsync(page, table, "Status");
            var all = Box(panel, "Select all");

            await Box(panel, "Refunded").UncheckAsync();
            await Assertions.Expect(table.Locator("th .ib-table-filter-button[aria-label='Filter Status: 3 selected']")).ToBeVisibleAsync();
            Assert.True(await all.EvaluateAsync<bool>("b => b.indeterminate"));

            await all.ClickAsync();
            await Assertions.Expect(table.Locator("th .ib-table-filter-button[data-active]")).ToHaveCountAsync(0);
            await all.ClickAsync();
            await Assertions.Expect(table.Locator("th .ib-table-filter-button[aria-label='Filter Status: none']")).ToBeVisibleAsync();
            await Assertions.Expect(table.Locator(".ib-table-empty-content")).ToContainTextAsync("Nothing is selected under Status.");
            Assert.Equal(url, page.Url);

            await panel.Locator("[data-ib-filter-clear]").ClickAsync();
            await Assertions.Expect(table.Locator("th .ib-table-filter-button[data-active]")).ToHaveCountAsync(0);
            panel = await OpenPanelAsync(page, table, "Status");
            all = Box(panel, "Select all");
            await Assertions.Expect(all).ToBeCheckedAsync();
            Assert.False(await all.EvaluateAsync<bool>("b => b.indeterminate"));
        }
    }
}
