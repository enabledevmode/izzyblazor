using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A DataGrid's column filters in Chromium: the panel's shape, as you go, and with no script.</summary>
public sealed class DataGridFilterTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private const string Prefix = "filtered-";

    private async Task<(TestBrowser, IPage, ILocator)> OpenAsync(string mode, bool script = true, string query = "")
    {
        var browser = await TestBrowser.OpenAsync();
        var context = await browser.NewContextAsync(new() { JavaScriptEnabled = script, Locale = "en-US", ViewportSize = new() { Width = 1400, Height = 900 } });
        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/data-grid/filtering?mode={mode}{query}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var grid = page.Locator(".ib-datagrid", new() { HasText = "Orders to filter" });
        await grid.ScrollIntoViewSettledAsync();
        if (mode == "interactive") await grid.Locator("xpath=ancestor-or-self::ib-table[@data-ib-interactive]").WaitForAsync();
        // The on-demand element's listeners are on once it has set up.
        if (script) await page.WaitForFunctionAsync("() => !!customElements.get('ib-table')", null, new() { PollingInterval = 100 });
        return (browser, page, grid);
    }

    private static async Task<ILocator> OpenPanelAsync(IPage page, ILocator grid, string column)
    {
        await grid.Locator($"th .ib-table-filter-button[title^='Filter {column}']").ClickAsync();
        var panel = page.Locator("[popover]:popover-open .ib-table-filter-panel");
        await panel.WaitForAsync();
        return panel;
    }

    private static Dictionary<string, string> Own(IPage page) =>
        new Uri(page.Url).Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .Where(p => p[0].StartsWith($"{Prefix}f-", StringComparison.Ordinal))
            .ToDictionary(p => p[0][Prefix.Length..], p => Uri.UnescapeDataString(p.Length > 1 ? p[1] : ""));

    private static string Say(IReadOnlyDictionary<string, string> keys) => string.Join("&", keys.Select(k => $"{k.Key}={k.Value}").Order(StringComparer.Ordinal));

    // The address settles on these of the table's filter keys, and nothing else of theirs.
    private static async Task AddressIsAsync(IPage page, string expected)
    {
        expected = string.Join("&", expected.Split('&', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal));
        for (var i = 0; i < 100 && Say(Own(page)) != expected; i++)
        {
            await page.WaitForTimeoutAsync(50);
        }

        Assert.Equal(expected, Say(Own(page)));
    }

    // The address changes first, and the rows arrive after it.
    private static async Task RowsMatchAsync(ILocator grid, Func<decimal, bool> test)
    {
        var cells = grid.Locator("tbody tr td:last-child");
        List<decimal> totals = [];
        for (var i = 0; i < 100; i++)
        {
            totals = (await cells.AllTextContentsAsync())
                .Select(t => decimal.TryParse(t, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.GetCultureInfo("en-US"), out var d) ? d : (decimal?)null)
                .OfType<decimal>().ToList();
            if (totals.Count > 0 && totals.All(test))
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.NotEmpty(totals);
        Assert.All(totals, t => Assert.True(test(t), $"{t} shouldn't be here."));
    }

    private static ILocator FilterRow(ILocator grid) => grid.Locator(".ib-table-filter-tags");

    [Theory]
    [InlineData("static", true)]
    [InlineData("static", false)]
    public async Task FilterPanel_removes_one_then_the_rest(string mode, bool script)
    {
        var (browser, page, grid) = await OpenAsync(mode, script, $"&{Prefix}f-status=Paid&{Prefix}f-total=100&{Prefix}f-customer=a");
        await using (browser)
        {
            var panel = FilterRow(grid);
            await panel.Locator(".ib-tag", new() { HasText = "Status" }).Locator("a").ClickAsync();
            await AddressIsAsync(page, "f-total=100&f-customer=a");

            await panel.Locator("[data-ib-filter-clear]").ClickAsync();
            await AddressIsAsync(page, "");
            await Assertions.Expect(grid.Locator("th .ib-table-filter-button[data-active]")).ToHaveCountAsync(0);
        }
    }

    [Theory]
    [InlineData("static", true)]
    [InlineData("static", false)]
    public async Task The_panel_follows_its_operator(string mode, bool script)
    {
        var (browser, page, grid) = await OpenAsync(mode, script);
        await using (browser)
        {
            var panel = await OpenPanelAsync(page, grid, "Total");
            var first = panel.Locator($"input[name='{Prefix}f-total']");
            var second = panel.Locator($"input[name='{Prefix}f-total-to']");
            var min = panel.GetByText("Min", new() { Exact = true });

            Assert.True(await first.IsVisibleAsync());
            Assert.False(await second.IsVisibleAsync());
            Assert.False(await min.IsVisibleAsync());
            // With one value it's named for screen readers; with two, for its end.
            Assert.Equal("Value", (await page.Locator($"label[for='{await first.GetAttributeAsync("id")}']").TextContentAsync())!.Replace("Min", "", StringComparison.Ordinal));

            foreach (var _ in new[] { 1, 2 })
            {
                await panel.Locator("select").SelectOptionAsync("between");
                await second.WaitForAsync();
                Assert.True(await first.IsVisibleAsync());
                Assert.True(await min.IsVisibleAsync());
                Assert.True(await panel.GetByText("Max", new() { Exact = true }).IsVisibleAsync());
                Assert.Equal("Min", await first.EvaluateAsync<string>("e => e.labels[0].innerText"));

                await panel.Locator("select").SelectOptionAsync("gt");
                await second.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
                Assert.Equal("Value", await first.EvaluateAsync<string>("e => e.labels[0].innerText"));
            }

            Assert.True(await panel.IsVisibleAsync());
            await page.Keyboard.PressAsync("Escape");
            await panel.WaitForAsync(new() { State = WaitForSelectorState.Hidden });

            // Is empty takes no value.
            var customer = await OpenPanelAsync(page, grid, "Customer");
            var value = customer.Locator($"input[name='{Prefix}f-customer']");
            Assert.True(await value.IsVisibleAsync());
            await customer.Locator("select").SelectOptionAsync("isempty");
            await value.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
            await customer.Locator("select").SelectOptionAsync("contains");
            await value.WaitForAsync();
        }
    }

    // A moment after a response or a render, nothing more is on its way.
    private static Task SettledAsync(IPage page) => page.WaitForTimeoutAsync(600);

    private static async Task OperatorAndValuesAreAsync(IPage page, string op, string value, string to)
    {
        Assert.Equal(op, await page.Locator($"select[name='{Prefix}f-total-op']").InputValueAsync());
        Assert.Equal(value, await page.Locator($"[popover] input[name='{Prefix}f-total']").InputValueAsync());
        Assert.Equal(to, await page.Locator($"[popover] input[name='{Prefix}f-total-to']").InputValueAsync());
    }

    // The panels show what the address says once everything has settled, and the next filter keeps it.
    [Theory]
    [InlineData("interactive")]
    public async Task Back_and_forward_leave_the_panels_showing_the_address(string mode)
    {
        var (browser, page, grid) = await OpenAsync(mode);
        await using (browser)
        {
            var panel = await OpenPanelAsync(page, grid, "Total");
            var select = panel.Locator("select");
            await panel.Locator($"input[name='{Prefix}f-total']").FillAsync("300");
            await AddressIsAsync(page, "f-total=300");
            await select.SelectOptionAsync("gt");
            await AddressIsAsync(page, "f-total-op=gt&f-total=300");
            await select.SelectOptionAsync("lt");
            await AddressIsAsync(page, "f-total-op=lt&f-total=300");
            await SettledAsync(page);

            await page.GoBackAsync();
            await AddressIsAsync(page, "f-total-op=gt&f-total=300");
            await RowsMatchAsync(grid, t => t > 300);
            await SettledAsync(page);
            await OperatorAndValuesAreAsync(page, "gt", "300", "");

            await page.GoForwardAsync();
            await AddressIsAsync(page, "f-total-op=lt&f-total=300");
            await RowsMatchAsync(grid, t => t < 300);
            await SettledAsync(page);
            await OperatorAndValuesAreAsync(page, "lt", "300", "");

            // And twice back, then a filter on another column, which keeps this one.
            await page.GoBackAsync();
            await AddressIsAsync(page, "f-total-op=gt&f-total=300");
            await SettledAsync(page);
            await page.Keyboard.PressAsync("Escape");
            var customer = await OpenPanelAsync(page, grid, "Customer");
            await customer.Locator($"input[name='{Prefix}f-customer']").FillAsync("a");
            await AddressIsAsync(page, "f-total-op=gt&f-total=300&f-customer=a");
        }
    }

    private static Task RowsChangedOrWaitAsync(IPage page) => page.WaitForTimeoutAsync(1500);

    private async Task<(TestBrowser, IPage)> OpenPageAsync(string address)
    {
        var browser = await TestBrowser.OpenAsync();
        var context = await browser.NewContextAsync(new() { Locale = "en-US", ViewportSize = new() { Width = 1400, Height = 900 } });
        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, address).ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => !!customElements.get('ib-table')", null, new() { PollingInterval = 100 });
        return (browser, page);
    }

    [Fact]
    public async Task With_no_script_the_form_posts_and_a_stray_operator_is_no_filter()
    {
        var (browser, page, grid) = await OpenAsync("static", script: false);
        await using (browser)
        {
            // An operator with no value: Apply changes the address, and filters nothing.
            var panel = await OpenPanelAsync(page, grid, "Total");
            await panel.Locator("select").SelectOptionAsync("gt");
            await panel.GetByRole(AriaRole.Button, new() { Name = "Apply" }).ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            grid = page.Locator(".ib-datagrid", new() { HasText = "Orders to filter" });
            Assert.Contains($"{Prefix}f-total-op=gt", page.Url, StringComparison.Ordinal);
            Assert.Equal(5, await grid.Locator("tbody tr").CountAsync());
            Assert.DoesNotContain("Total", await FilterRow(grid).TextContentAsync(), StringComparison.Ordinal);
            Assert.Equal(0, await grid.Locator("th .ib-table-filter-button[data-active]").CountAsync());
            Assert.Equal("gt", await page.Locator($"select[name='{Prefix}f-total-op']").InputValueAsync());

            // With a value, it's the filter, and every other column's empty fields still aren't.
            panel = await OpenPanelAsync(page, grid, "Total");
            await panel.Locator($"input[name='{Prefix}f-total']").FillAsync("300");
            await panel.GetByRole(AriaRole.Button, new() { Name = "Apply" }).ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            grid = page.Locator(".ib-datagrid", new() { HasText = "Orders to filter" });
            await RowsMatchAsync(grid, t => t > 300);
            Assert.Contains("Total: Greater than 300", await grid.Locator(".ib-table-filter-tags").TextContentAsync());
            Assert.Equal(1, await grid.Locator("th .ib-table-filter-button[data-active]").CountAsync());

            // Between's two ends, then a stale second that the operator doesn't take.
            panel = await OpenPanelAsync(page, grid, "Total");
            await panel.Locator("select").SelectOptionAsync("between");
            await panel.Locator($"input[name='{Prefix}f-total-to']").FillAsync("400");
            await panel.GetByRole(AriaRole.Button, new() { Name = "Apply" }).ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            grid = page.Locator(".ib-datagrid", new() { HasText = "Orders to filter" });
            await RowsMatchAsync(grid, t => t is >= 300 and <= 400);
            Assert.Contains("Total: 300", await grid.Locator(".ib-table-filter-tags").TextContentAsync());

            panel = await OpenPanelAsync(page, grid, "Total");
            await panel.Locator("select").SelectOptionAsync("ge");
            await panel.GetByRole(AriaRole.Button, new() { Name = "Apply" }).ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            grid = page.Locator(".ib-datagrid", new() { HasText = "Orders to filter" });
            Assert.Contains("Total: 300 or more", await grid.Locator(".ib-table-filter-tags").TextContentAsync());
            Assert.Equal("", await grid.Locator($"[popover] input[name='{Prefix}f-total-to']").InputValueAsync());
        }
    }
}
