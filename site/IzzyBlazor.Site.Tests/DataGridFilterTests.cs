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
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/data-grid?mode={mode}{query}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var grid = page.Locator(".ib-datagrid", new() { HasText = "Orders to filter" });
        await grid.ScrollIntoViewSettledAsync();
        if (mode == "interactive") await grid.Locator("xpath=ancestor-or-self::ib-table[@data-ib-interactive]").WaitForAsync();
        // The on-demand element's listeners are on once it has set up.
        if (script) await page.WaitForFunctionAsync("() => !!customElements.get('ib-table')", null, new() { PollingInterval = 100 });
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

    private static ILocator FilterRow(ILocator grid) => grid.Locator(".ib-table-filter-tags");

    [Theory]
    [InlineData("static", true)]
    [InlineData("interactive", true)]
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
    [InlineData("interactive")]
    public async Task FilterPanel_sits_below_the_rows_and_nothing_moves(string mode)
    {
        var (browser, page, grid) = await OpenAsync(mode);
        await using (browser)
        {
            const string top = "e => e.getBoundingClientRect().top";
            var head = grid.Locator("thead");
            var before = await head.EvaluateAsync<double>(top);
            await Assertions.Expect(FilterRow(grid)).ToContainTextAsync("No filters");

            var panel = await OpenPanelAsync(page, grid, "Status");
            await panel.GetByRole(AriaRole.Checkbox, new() { Name = "Paid", Exact = true }).UncheckAsync();
            await Assertions.Expect(FilterRow(grid)).ToContainTextAsync("Status: 3 selected");
            Assert.Equal(before, await head.EvaluateAsync<double>(top));

            var rowsEnd = await grid.Locator(".ib-table-scroll").EvaluateAsync<double>("e => e.getBoundingClientRect().bottom");
            Assert.True(await FilterRow(grid).EvaluateAsync<double>(top) >= rowsEnd);
        }
    }

    [Theory]
    [InlineData("interactive")]
    public async Task A_change_in_one_panel_leaves_what_a_Clear_just_removed(string mode)
    {
        var (browser, page, grid) = await OpenAsync(mode, query: $"&{Prefix}f-total=100&{Prefix}f-status=Paid&{Prefix}f-status=Failed&{Prefix}f-status=Refunded");
        await using (browser)
        {
            // A response that takes its time, whichever way this mode gets one.
            if (mode == "interactive")
            {
                await page.Context.RouteWebSocketAsync("**/_blazor**", ws =>
                {
                    var server = ws.ConnectToServer();
                    ws.OnMessage(async message =>
                    {
                        await Task.Delay(900);
                        if (message.Binary is { } binary) server.Send(binary);
                        else server.Send(message.Text!);
                    });
                });
                await page.ReloadAsync(new() { WaitUntil = WaitUntilState.NetworkIdle });
                await grid.Locator("xpath=ancestor-or-self::ib-table[@data-ib-interactive]").WaitForAsync();
                await page.WaitForTimeoutAsync(2000);
            }
            else
            {
                await page.RouteAsync("**/docs/components/data-grid**", async route =>
                {
                    await Task.Delay(900);
                    await route.ContinueAsync();
                });
            }

            var total = await OpenPanelAsync(page, grid, "Total");
            await total.Locator("[data-ib-filter-clear]").ClickAsync();
            var status = await OpenPanelAsync(page, grid, "Status");
            await status.GetByRole(AriaRole.Checkbox, new() { Name = "Paid", Exact = true }).UncheckAsync();

            await page.WaitForTimeoutAsync(3000);
            Assert.DoesNotContain("f-total", page.Url, StringComparison.Ordinal);
            Assert.Contains("f-status=Failed", page.Url, StringComparison.Ordinal);
            Assert.DoesNotContain("f-status=Paid", page.Url, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task A_long_tag_stays_in_its_list_with_its_remove_button(string mode)
    {
        var (browser, page, grid) = await OpenAsync(mode, query: $"&{Prefix}f-customer=Ada%20Lovelace%20and%20Grace%20Hopper%20Consulting%20Group%20International&{Prefix}f-total=100");
        await using (browser)
        {
            var row = FilterRow(grid);
            Assert.True(await row.EvaluateAsync<bool>("r => r.scrollWidth <= r.clientWidth"));
            var edge = await row.EvaluateAsync<double>("r => r.getBoundingClientRect().right");
            var remove = await row.Locator(".ib-tag a").First.EvaluateAsync<double>("a => a.getBoundingClientRect().right");
            Assert.True(remove <= edge, $"The remove button ends at {remove}, the row at {edge}.");
        }
    }

    [Theory]
    [InlineData("static", "tag")]
    [InlineData("interactive", "tag")]
    [InlineData("static", "clear")]
    [InlineData("interactive", "clear")]
    public async Task Removing_drops_what_waits_on_its_pause(string mode, string how)
    {
        var (browser, page, grid) = await OpenAsync(mode, query: $"&{Prefix}f-status=Paid&{Prefix}f-status=Pending&{Prefix}f-total=100");
        await using (browser)
        {
            var status = await OpenPanelAsync(page, grid, "Status");
            await status.GetByRole(AriaRole.Checkbox, new() { Name = "Pending", Exact = true }).UncheckAsync();
            var panel = FilterRow(grid);
            await page.Keyboard.PressAsync("Escape");
            if (how == "tag") await panel.Locator(".ib-tag", new() { HasText = "Status" }).Locator("a").ClickAsync();
            else await panel.Locator("[data-ib-filter-clear]").ClickAsync();

            var left = how == "tag" ? "f-total=100" : "";
            await AddressIsAsync(page, left);
            await page.WaitForTimeoutAsync(700);
            await AddressIsAsync(page, left);
        }
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

    [Theory]
    [InlineData("interactive")]
    public async Task Back_over_a_sort_leaves_a_Tables_fields_and_the_next_filter_keeps_them(string mode)
    {
        var (browser, page) = await OpenPageAsync($"docs/components/table?mode={mode}&orders-f-customer=a&orders-f-total=100");
        await using (browser)
        {
            var table = page.Locator(".ib-table:has(caption:text-is('Filtered orders'))");
            await table.ScrollIntoViewSettledAsync();
            if (mode == "interactive") await page.Locator("ib-table[data-ib-interactive]:has(caption:text-is('Filtered orders'))").WaitForAsync();
            await table.Locator("thead a.ib-table-sort, thead button.ib-table-sort").First.ClickAsync();
            await page.WaitForURLAsync(u => u.Contains("sort=", StringComparison.Ordinal));
            await SettledAsync(page);
            await page.GoBackAsync();
            await page.WaitForURLAsync(u => !u.Contains("sort=", StringComparison.Ordinal));
            await SettledAsync(page);

            Assert.Equal("a", await table.Locator("[popover] input[name='orders-f-customer']").InputValueAsync());
            Assert.Equal("100", await table.Locator("[popover] input[name='orders-f-total']").InputValueAsync());

            await table.Locator("th .ib-table-filter-button[aria-label^='Filter Total']").ClickAsync();
            await table.Locator("[popover]:popover-open input[name='orders-f-total-to']").FillAsync("400");
            await page.WaitForURLAsync(u => u.Contains("orders-f-total-to=400", StringComparison.Ordinal));
            Assert.Contains("orders-f-customer=a", page.Url, StringComparison.Ordinal);
            Assert.Contains("orders-f-total=100", page.Url, StringComparison.Ordinal);
        }
    }

    // Script mustn't change what a link means, on load or at the next filter.
    [Theory]
    [InlineData("interactive")]
    public async Task A_repeated_key_keeps_every_value_with_script(string mode)
    {
        const string Both = "f-customer=Ada%20Lovelace&f-customer=Grace%20Hopper";
        var (browser, page) = await OpenPageAsync($"docs/components/data-grid?mode={mode}&{Both}");
        await using (browser)
        {
            var grid = page.Locator(".ib-datagrid:has(caption:text-is('Orders'))");
            await grid.ScrollIntoViewSettledAsync();
            var entries = await page.EvaluateAsync<int>("history.length");
            await SettledAsync(page);

            var keys = () => new Uri(page.Url).Query.Split(['?', '&'], StringSplitOptions.RemoveEmptyEntries).Where(k => k.StartsWith("f-customer=", StringComparison.Ordinal)).ToArray();
            Assert.Equal(2, keys().Length);
            Assert.Equal(entries, await page.EvaluateAsync<int>("history.length"));
            Assert.Equal(1, await grid.Locator("th .ib-table-filter-button[aria-label='Filter Customer: 2 selected']").CountAsync());
            var names = (await grid.Locator("tbody tr").AllTextContentsAsync()).Select(r => r.Contains("Ada Lovelace", StringComparison.Ordinal) ? "Ada" : r.Contains("Grace Hopper", StringComparison.Ordinal) ? "Grace" : r).ToHashSet();
            Assert.Equal(new HashSet<string> { "Ada", "Grace" }, names);
        }

        (browser, page) = await OpenPageAsync($"docs/components/table?mode={mode}&orders-{Both.Replace("&f-", "&orders-f-", StringComparison.Ordinal)}");
        await using (browser)
        {
            var table = page.Locator(".ib-table:has(caption:text-is('Filtered orders'))");
            await table.ScrollIntoViewSettledAsync();
            if (mode == "interactive") await page.Locator("ib-table[data-ib-interactive]:has(caption:text-is('Filtered orders'))").WaitForAsync();
            await SettledAsync(page);
            await table.Locator("th .ib-table-filter-button[aria-label^='Filter Total']").ClickAsync();
            await table.Locator("[popover]:popover-open input[name='orders-f-total']").FillAsync("100");
            await page.WaitForURLAsync(u => u.Contains("orders-f-total=100", StringComparison.Ordinal));
            Assert.Contains("orders-f-customer=Ada", page.Url, StringComparison.Ordinal);
            Assert.Contains("orders-f-customer=Grace", page.Url, StringComparison.Ordinal);
        }
    }

    // A date applies when it's whole: not year 0002 on the way to 2026, and not a field half edited.
    [Theory]
    [InlineData("interactive")]
    public async Task A_date_typed_by_keyboard_applies_once_when_whole(string mode)
    {
        var (browser, page, grid) = await OpenAsync(mode);
        await using (browser)
        {
            await page.EvaluateAsync("""
                () => {
                  window.addresses = [];
                  for (const name of ["pushState", "replaceState"]) {
                    const was = history[name];
                    history[name] = function (state, title, url) { if (url) window.addresses.push(String(url)); return was.apply(this, arguments); };
                  }
                }
                """);
            var panel = await OpenPanelAsync(page, grid, "Date");
            var input = panel.Locator($"input[name='{Prefix}f-date']");
            await input.FocusAsync();
            var entries = await page.EvaluateAsync<int>("history.length");
            await page.Keyboard.TypeAsync("09122026", new() { Delay = 180 });

            await AddressIsAsync(page, "f-date=2026-09-12");
            await SettledAsync(page);
            Assert.Equal("2026-09-12", await input.InputValueAsync());
            var addresses = (await page.EvaluateAsync<string[]>("window.addresses")).Where(a => a.Contains("f-date", StringComparison.Ordinal)).Distinct().ToArray();
            Assert.DoesNotContain(addresses, a => a.Contains("f-date=0", StringComparison.Ordinal));
            Assert.Equal(entries + 1, await page.EvaluateAsync<int>("history.length"));

            // One segment emptied: a partly edited date applies nothing, and the filter stays.
            await page.Keyboard.PressAsync("Backspace");
            await SettledAsync(page);
            await AddressIsAsync(page, "f-date=2026-09-12");
            Assert.Contains("2026-09-12", await grid.Locator(".ib-table-filter-tags").TextContentAsync());

            // Nor does a filter on another column drop it.
            await page.Keyboard.PressAsync("Escape");
            var customer = await OpenPanelAsync(page, grid, "Customer");
            await customer.Locator($"input[name='{Prefix}f-customer']").FillAsync("a");
            await AddressIsAsync(page, "f-date=2026-09-12&f-customer=a");
            await page.Keyboard.PressAsync("Escape");

            // Emptied on purpose, it goes.
            panel = await OpenPanelAsync(page, grid, "Date");
            await panel.Locator($"input[name='{Prefix}f-date']").FillAsync("");
            await AddressIsAsync(page, "f-customer=a");
        }
    }

    [Fact]
    public async Task A_bound_date_typed_by_keyboard_is_the_date_typed()
    {
        var (browser, page) = await OpenPageAsync("docs/components/table?mode=interactive");
        await using (browser)
        {
            var table = page.Locator("ib-table[data-ib-interactive]:has(caption:text-is('Order history'))");
            await table.WaitForAsync();
            await table.ScrollIntoViewSettledAsync();
            var shown = table.Locator("xpath=..").Locator("code").Last;
            await table.Locator("th .ib-table-filter-button[aria-label^='Filter Date']").ClickAsync();
            var input = table.Locator("[popover]:popover-open input[type=date]");
            await input.FocusAsync();
            await shown.EvaluateAsync("code => { window.filters = []; new MutationObserver(() => window.filters.push(code.textContent)).observe(code, { characterData: true, childList: true, subtree: true }); }");
            await page.Keyboard.TypeAsync("09122026", new() { Delay = 180 });

            await Assertions.Expect(shown).ToHaveTextAsync("date eq 2026-09-12");
            await SettledAsync(page);
            Assert.Equal("date eq 2026-09-12", await shown.TextContentAsync());
            Assert.Equal("2026-09-12", await input.InputValueAsync());

            // C# heard the whole date, and nothing on the way to it.
            Assert.Equal(["date eq 2026-09-12"], await page.EvaluateAsync<string[]>("window.filters"));

            // One segment emptied: the filter stays until the date is whole again.
            await page.Keyboard.PressAsync("Backspace");
            await SettledAsync(page);
            Assert.Equal("date eq 2026-09-12", await shown.TextContentAsync());
            await page.Keyboard.TypeAsync("2026", new() { Delay = 180 });
            await SettledAsync(page);
            Assert.Equal("date eq 2026-09-12", await shown.TextContentAsync());
        }
    }

    // A response already on its way draws the panel as it was when that was asked for.
    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task An_operator_chosen_with_a_response_on_its_way_stays(string mode)
    {
        var (browser, page, grid) = await OpenAsync(mode);
        await using (browser)
        {
            await page.RouteAsync("**/docs/components/data-grid*", async route =>
            {
                await Task.Delay(700);
                await route.ContinueAsync();
            });
            var customer = await OpenPanelAsync(page, grid, "Customer");
            await customer.Locator($"input[name='{Prefix}f-customer']").FillAsync("a");
            await page.Keyboard.PressAsync("Enter");
            await AddressIsAsync(page, "f-customer=a");

            var panel = await OpenPanelAsync(page, grid, "Total");
            var select = panel.Locator("select");
            await select.SelectOptionAsync("gt");
            await RowsChangedOrWaitAsync(page);
            Assert.Equal("gt", await select.InputValueAsync());
            await AddressIsAsync(page, "f-customer=a");

            await panel.Locator($"input[name='{Prefix}f-total']").FillAsync("300");
            if (mode == "static") await panel.Locator(".ib-table-filter-apply").ClickAsync();
            await AddressIsAsync(page, "f-customer=a&f-total-op=gt&f-total=300");
        }
    }

    // Enter closes the panel, and the change that fires isn't something still to apply.
    [Fact]
    public async Task A_panel_reopened_before_the_response_keeps_what_was_applied()
    {
        var (browser, page, grid) = await OpenAsync("static");
        await using (browser)
        {
            await page.RouteAsync("**/docs/components/data-grid*", async route =>
            {
                await Task.Delay(700);
                await route.ContinueAsync();
            });
            var panel = await OpenPanelAsync(page, grid, "Total");
            await panel.Locator($"input[name='{Prefix}f-total']").FillAsync("300");
            await page.Keyboard.PressAsync("Enter");
            await AddressIsAsync(page, "f-total=300");

            panel = await OpenPanelAsync(page, grid, "Total");
            Assert.Equal("300", await panel.Locator($"input[name='{Prefix}f-total']").First.InputValueAsync());
            await panel.Locator("select").SelectOptionAsync("gt");
            await panel.Locator(".ib-table-filter-apply").ClickAsync();
            await AddressIsAsync(page, "f-total-op=gt&f-total=300");
        }
    }

    // What the address says wins over a panel's last Apply.
    [Fact]
    public async Task Back_on_a_static_page_leaves_a_panel_showing_the_address()
    {
        var (browser, page, grid) = await OpenAsync("static");
        await using (browser)
        {
            var panel = await OpenPanelAsync(page, grid, "Total");
            await panel.Locator($"input[name='{Prefix}f-total']").FillAsync("300");
            await page.Keyboard.PressAsync("Enter");
            await AddressIsAsync(page, "f-total=300");

            panel = await OpenPanelAsync(page, grid, "Total");
            await panel.Locator("select").SelectOptionAsync("gt");
            await panel.Locator(".ib-table-filter-apply").ClickAsync();
            await AddressIsAsync(page, "f-total-op=gt&f-total=300");

            await page.GoBackAsync();
            await AddressIsAsync(page, "f-total=300");
            await SettledAsync(page);
            panel = await OpenPanelAsync(page, grid, "Total");
            Assert.NotEqual("gt", await panel.Locator("select").InputValueAsync());
            Assert.Equal("300", await panel.Locator($"input[name='{Prefix}f-total']").First.InputValueAsync());
        }
    }

    private static Task RowsChangedOrWaitAsync(IPage page) => page.WaitForTimeoutAsync(1500);

    // The design's rule: an operator with no value doesn't outlast the next filter.
    [Theory]
    [InlineData("interactive")]
    public async Task An_operator_with_no_value_resets_when_another_column_filters(string mode)
    {
        var (browser, page, grid) = await OpenAsync(mode);
        await using (browser)
        {
            var panel = await OpenPanelAsync(page, grid, "Total");
            await panel.Locator("select").SelectOptionAsync("gt");
            await page.Keyboard.PressAsync("Escape");
            var customer = await OpenPanelAsync(page, grid, "Customer");
            await customer.Locator($"input[name='{Prefix}f-customer']").FillAsync("a");
            await AddressIsAsync(page, "f-customer=a");
            await SettledAsync(page);
            Assert.Equal("eq", await page.Locator($"select[name='{Prefix}f-total-op']").InputValueAsync());
        }
    }

    private async Task<(TestBrowser, IPage)> OpenPageAsync(string address)
    {
        var browser = await TestBrowser.OpenAsync();
        var context = await browser.NewContextAsync(new() { Locale = "en-US", ViewportSize = new() { Width = 1400, Height = 900 } });
        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, address).ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => !!customElements.get('ib-table')", null, new() { PollingInterval = 100 });
        return (browser, page);
    }

    [Theory]
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

            // On the select, an arrow opens its list; the grid's cell stays where it was.
            panel = await OpenPanelAsync(page, grid, "Total");
            select = panel.Locator("select");
            await select.FocusAsync();
            await page.Keyboard.PressAsync("ArrowDown");
            await page.Locator("select:open").WaitForAsync();
            Assert.True(await panel.IsVisibleAsync());
            await page.Keyboard.PressAsync("Escape");
            await page.Locator("select:open").WaitForAsync(new() { State = WaitForSelectorState.Detached });
            await AddressIsAsync(page, "f-total=300");

            // Each new operator applies.
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
