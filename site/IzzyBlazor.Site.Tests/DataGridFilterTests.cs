using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A DataGrid's column filters in Chromium: the panel's shape, as you go, and with no script.</summary>
public sealed class DataGridFilterTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private const string Prefix = "filtered-";

    private async Task<(TestBrowser, IPage, ILocator)> OpenAsync(string mode, bool script = true, string query = "")
    {
        var browser = await TestBrowser.OpenAsync();
        var context = await browser.NewContextAsync(new() { JavaScriptEnabled = script, ViewportSize = new() { Width = 1400, Height = 900 } });
        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/data-grid?mode={mode}{query}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var grid = page.Locator(".ib-datagrid", new() { HasText = "Orders to filter" });
        await grid.ScrollIntoViewIfNeededAsync();
        if (mode == "interactive") await grid.Locator("xpath=ancestor-or-self::ib-table[@data-ib-interactive]").WaitForAsync();
        // The on-demand element's listeners are on once it has set up.
        if (script) await page.WaitForFunctionAsync("() => customElements.get('ib-table')");
        return (browser, page, grid);
    }

    private static async Task<ILocator> OpenPanelAsync(IPage page, ILocator grid, string column)
    {
        await grid.Locator($"th .ib-table-filter-button[aria-label^='Filter {column}']").ClickAsync();
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

    [Theory]
    [InlineData("static", true)]
    [InlineData("interactive", true)]
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

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task The_address_gets_an_operator_only_with_a_value(string mode)
    {
        var (browser, page, grid) = await OpenAsync(mode);
        await using (browser)
        {
            var panel = await OpenPanelAsync(page, grid, "Total");
            var select = panel.Locator("select");
            var first = panel.Locator($"input[name='{Prefix}f-total']");
            var second = panel.Locator($"input[name='{Prefix}f-total-to']");

            // An operator alone changes nothing but the panel.
            await select.SelectOptionAsync("gt");
            await page.WaitForTimeoutAsync(500);
            await AddressIsAsync(page, "");

            // A value brings it along, after the pause.
            await first.FocusAsync();
            await page.Keyboard.TypeAsync("300");
            await AddressIsAsync(page, "f-total-op=gt&f-total=300");
            await RowsMatchAsync(grid, t => t > 300);
            Assert.True(await panel.IsVisibleAsync());

            // With a value, each new operator applies at once, focus staying on it, and the default
            // isn't written.
            await select.FocusAsync();
            foreach (var (word, address, test) in new (string, string, Func<decimal, bool>)[]
            {
                ("lt", "f-total-op=lt&f-total=300", t => t < 300),
                ("ge", "f-total-op=ge&f-total=300", t => t >= 300),
                ("lt", "f-total-op=lt&f-total=300", t => t < 300),
            })
            {
                await select.SelectOptionAsync(word);
                await AddressIsAsync(page, address);
                await RowsMatchAsync(grid, test);
                Assert.Equal(word, await select.InputValueAsync());
                Assert.Equal(await select.GetAttributeAsync("id"), await page.EvaluateAsync<string>("document.activeElement.id"));
            }

            await select.SelectOptionAsync("eq");
            await AddressIsAsync(page, "f-total=300");

            // Between's second value, and it stops counting when the operator doesn't take one.
            await select.SelectOptionAsync("between");
            await AddressIsAsync(page, "f-total-op=between&f-total=300");
            await second.FocusAsync();
            await page.Keyboard.TypeAsync("400");
            await AddressIsAsync(page, "f-total-op=between&f-total-to=400&f-total=300");
            await RowsMatchAsync(grid, t => t is >= 300 and <= 400);
            await select.SelectOptionAsync("ge");
            await AddressIsAsync(page, "f-total-op=ge&f-total=300");

            // Clearing the value takes the operator out of the address too.
            await first.FillAsync("");
            await AddressIsAsync(page, "");
            await grid.Locator("th .ib-table-filter-button[data-active]").WaitForAsync(new() { State = WaitForSelectorState.Detached });

            // Typing replaces its history entry, so Back goes to the Between before it, operator and
            // both ends, and Forward on again.
            await page.GoBackAsync();
            await AddressIsAsync(page, "f-total-op=between&f-total-to=400&f-total=300");
            await page.WaitForFunctionAsync($"document.querySelector(\"select[name='{Prefix}f-total-op']\").value === 'between' && document.querySelector(\"[popover] input[name='{Prefix}f-total']\").value === '300' && document.querySelector(\"[popover] input[name='{Prefix}f-total-to']\").value === '400'");
            await RowsMatchAsync(grid, t => t is >= 300 and <= 400);
            await page.GoForwardAsync();
            await AddressIsAsync(page, "");
            await page.WaitForFunctionAsync($"document.querySelector(\"select[name='{Prefix}f-total-op']\").value === 'eq' && document.querySelector(\"[popover] input[name='{Prefix}f-total']\").value === ''");
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task Keys_in_a_panel_filter_and_close_and_leave_the_filter(string mode)
    {
        var (browser, page, grid) = await OpenAsync(mode);
        await using (browser)
        {
            var panel = await OpenPanelAsync(page, grid, "Total");
            var select = panel.Locator("select");
            var first = panel.Locator($"input[name='{Prefix}f-total']");
            var button = grid.Locator("th .ib-table-filter-button[aria-label^='Filter Total']");

            // The field has focus as it opens; Enter filters now and closes.
            Assert.Equal(await first.GetAttributeAsync("id"), await page.EvaluateAsync<string>("document.activeElement.id"));
            await page.Keyboard.TypeAsync("300");
            await page.Keyboard.PressAsync("Enter");
            await AddressIsAsync(page, "f-total=300");
            await panel.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
            await page.WaitForFunctionAsync("document.activeElement?.classList.contains('ib-table-filter-button')");

            // On the select, arrows change the operator, not the grid's cell. Each new operator applies.
            panel = await OpenPanelAsync(page, grid, "Total");
            select = panel.Locator("select");
            await select.FocusAsync();
            await select.SelectOptionAsync("gt");
            await AddressIsAsync(page, "f-total-op=gt&f-total=300");

            // Enter in the open list picks an option and leaves the panel open.
            await select.ClickAsync();
            await page.Locator("select:open").WaitForAsync();
            await page.Keyboard.PressAsync("ArrowDown");
            await page.Keyboard.PressAsync("Enter");
            await AddressIsAsync(page, "f-total-op=ge&f-total=300");
            Assert.True(await panel.IsVisibleAsync());

            // Enter on the closed select is for its list, not for closing the panel.
            await select.FocusAsync();
            await page.Keyboard.PressAsync("Enter");
            await page.Locator("select:open").WaitForAsync();
            Assert.True(await panel.IsVisibleAsync());
            await page.Keyboard.PressAsync("Escape");
            await page.Locator("select:open").WaitForAsync(new() { State = WaitForSelectorState.Detached });

            // A mouse's choice too.
            await select.ClickAsync();
            await page.Locator("select:open option[value='lt']").ClickAsync();
            await AddressIsAsync(page, "f-total-op=lt&f-total=300");
            Assert.True(await panel.IsVisibleAsync());

            // Escape with the list open closes the list; again closes the panel, and the filter stays.
            await select.ClickAsync();
            await page.Locator("select:open").WaitForAsync();
            await page.Keyboard.PressAsync("Escape");
            await page.Locator("select:open").WaitForAsync(new() { State = WaitForSelectorState.Detached });
            Assert.True(await panel.IsVisibleAsync());
            await select.FocusAsync();
            await page.Keyboard.PressAsync("Escape");
            await panel.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
            await page.WaitForFunctionAsync("document.activeElement?.classList.contains('ib-table-filter-button')");
            Assert.StartsWith("Filter Total", await page.EvaluateAsync<string>("document.activeElement.getAttribute('aria-label')"));
            await AddressIsAsync(page, "f-total-op=lt&f-total=300");
            Assert.True(await button.IsVisibleAsync());
        }
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
            Assert.True(await grid.Locator(".ib-table-filter-tags").IsHiddenAsync());
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
