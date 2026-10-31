using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>Rearranging a DataGrid's columns in Chromium, and the cookie that keeps it, on the second example.</summary>
public sealed class DataGridArrangeTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private string Url(string mode) => new Uri(site.Client.BaseAddress!, $"docs/components/data-grid?mode={mode}").ToString();

    private async Task<(TestBrowser, IPage, ILocator)> OpenAsync(string mode)
    {
        var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 } });
        await page.GotoAsync(Url(mode), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var grid = page.Locator(".ib-datagrid:has(.ib-table-scroll[aria-label='Orders you arrange'])");
        await grid.Locator("ib-grid-columns").WaitForAsync(new() { State = WaitForSelectorState.Attached });
        if (mode == "interactive") await grid.Locator("ib-grid-columns[data-ib-interactive]").WaitForAsync(new() { State = WaitForSelectorState.Attached });
        await grid.Locator("table[role=grid]").WaitForAsync();
        await grid.Locator("thead").ScrollIntoViewIfNeededAsync();
        await page.WaitForTimeoutAsync(200);
        return (browser, page, grid);
    }

    private static Task<string[]> TitlesAsync(ILocator grid) =>
        grid.Locator("thead th").EvaluateAllAsync<string[]>("ths => ths.map(th => th.querySelector('.ib-table-title')?.textContent.trim())");

    private static async Task Until(ILocator grid, string[] titles)
    {
        for (var i = 0; i < 50; i++)
        {
            if ((await TitlesAsync(grid)).SequenceEqual(titles)) return;
            await Task.Delay(100);
        }

        Assert.Equal(titles, await TitlesAsync(grid));
    }

    private static Task<double> WidthAsync(ILocator grid, string title) =>
        grid.Locator("thead th", new() { HasText = title }).EvaluateAsync<double>("th => th.getBoundingClientRect().width");

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task Dragging_a_handle_resizes_and_a_reload_keeps_it(string mode)
    {
        var (browser, page, grid) = await OpenAsync(mode);
        await using (browser)
        {
            var before = await WidthAsync(grid, "Customer");
            var handle = await grid.Locator("thead th", new() { HasText = "Customer" }).Locator(".ib-col-resize").BoundingBoxAsync();
            await page.Mouse.MoveAsync(handle!.X + 2, handle.Y + 10);
            await page.Mouse.DownAsync();
            await page.Mouse.MoveAsync(handle.X + 42, handle.Y + 10, new() { Steps = 4 });
            Assert.Equal(before + 40, await WidthAsync(grid, "Customer"), 1.5);
            await page.Mouse.UpAsync();

            var cookies = await page.Context.CookiesAsync();
            Assert.Contains(cookies, c => c.Name == "ib-grid-arrange-example" && c.Value.Contains("customer"));
            Assert.Equal("/docs/components/data-grid", cookies.First(c => c.Name == "ib-grid-arrange-example").Path);

            // The server draws it: the width is in the first HTML, with no script.
            var html = await page.Context.APIRequest.GetAsync(Url(mode));
            Assert.Contains("px;", await html.TextAsync());
            await page.ReloadAsync(new() { WaitUntil = WaitUntilState.NetworkIdle });
            Assert.Equal(before + 40, await WidthAsync(grid, "Customer"), 1.5);
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task Keys_move_and_resize_a_header_and_focus_stays_on_it(string mode)
    {
        var (browser, page, grid) = await OpenAsync(mode);
        await using (browser)
        {
            await grid.Locator("thead th", new() { HasText = "Customer" }).FocusAsync();
            var before = await WidthAsync(grid, "Customer");
            await page.Keyboard.PressAsync("Shift+ArrowRight");
            for (var i = 0; i < 30 && Math.Abs(await WidthAsync(grid, "Customer") - before - 16) > 1; i++) await Task.Delay(100);
            Assert.Equal(before + 16, await WidthAsync(grid, "Customer"), 1.5);
            Assert.Contains("Customer", await grid.Locator("[data-ib-col-say]").TextContentAsync());

            await page.Keyboard.PressAsync("Alt+ArrowRight");
            await Until(grid, ["Order", "Date", "Customer", "Status", "Total"]);
            await page.WaitForFunctionAsync("() => document.activeElement.closest('th')?.textContent.includes('Customer')");
            Assert.Equal("Customer", (await TitlesAsync(grid))[2]);
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task The_menu_and_the_picker_hide_show_and_reset(string mode)
    {
        var (browser, page, grid) = await OpenAsync(mode);
        await using (browser)
        {
            await grid.Locator("thead th", new() { HasText = "Status" }).Locator("[data-ib-menu]").ClickAsync();
            await page.GetByRole(AriaRole.Menuitem, new() { Name = "Hide column" }).ClickAsync();
            await Until(grid, ["Order", "Customer", "Date", "Total"]);

            await grid.Locator(".ib-col-picker button").First.ClickAsync();
            var box = await grid.GetByRole(AriaRole.Checkbox, new() { Name = "Status" }).BoundingBoxAsync();
            Assert.Equal(16, box!.Width, 1);
            // Hiding a column leaves its row as it was: the arrows stay, greyed out.
            var row = grid.Locator(".ib-col-picker-list li", new() { HasText = "Status" });
            Assert.Equal(2, await row.Locator(".ib-col-picker-move button:disabled").CountAsync());
            var hiddenRow = await row.BoundingBoxAsync();
            var shownRow = await grid.Locator(".ib-col-picker-list li", new() { HasText = "Date" }).BoundingBoxAsync();
            Assert.Equal(shownRow!.Width, hiddenRow!.Width, 0.5);

            await grid.GetByRole(AriaRole.Checkbox, new() { Name = "Status" }).CheckAsync();
            await Until(grid, ["Order", "Customer", "Date", "Status", "Total"]);

            await grid.Locator("[data-ib-col-action=reset]").ClickAsync();
            await page.WaitForFunctionAsync("() => !document.cookie.includes('ib-grid-arrange-example')");
            await Until(grid, ["Order", "Customer", "Date", "Status", "Total"]);
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task Dragging_a_header_moves_its_column_and_doesnt_sort(string mode)
    {
        var (browser, page, grid) = await OpenAsync(mode);
        await using (browser)
        {
            var from = await grid.Locator("thead th", new() { HasText = "Customer" }).BoundingBoxAsync();
            var to = await grid.Locator("thead th", new() { HasText = "Status" }).BoundingBoxAsync();
            await page.Mouse.MoveAsync(from!.X + 12, from.Y + from.Height / 2);
            await page.Mouse.DownAsync();
            await page.Mouse.MoveAsync(to!.X + to.Width - 4, from.Y + from.Height / 2, new() { Steps = 6 });
            await page.Mouse.UpAsync();
            await Until(grid, ["Order", "Date", "Status", "Customer", "Total"]);
            Assert.DoesNotContain("sort=", page.Url);

            // Released away from the header, nothing is left to eat the next real click.
            var date = await grid.Locator("thead th", new() { HasText = "Date" }).BoundingBoxAsync();
            await page.Mouse.MoveAsync(date!.X + 12, date.Y + date.Height / 2);
            await page.Mouse.DownAsync();
            await page.Mouse.MoveAsync(date.X + 12, date.Y + 120, new() { Steps = 3 });
            await page.Mouse.MoveAsync(date.X + 40, date.Y + 120, new() { Steps = 3 });
            await page.Keyboard.PressAsync("Escape");
            await page.Mouse.UpAsync();
            await grid.Locator("thead th a.ib-table-sort", new() { HasText = "Date" }).ClickAsync();
            await page.WaitForFunctionAsync("() => new URLSearchParams(location.search).get('sort')?.includes('date')");
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task Search_filters_as_you_type_and_the_selection_bar_takes_the_toolbars_place(string mode)
    {
        var (browser, page, _) = await OpenAsync(mode);
        await using (browser)
        {
            var grid = page.Locator(".ib-datagrid", new() { HasText = "New order" });
            var toolbar = grid.Locator(".ib-grid-toolbar");
            await toolbar.ScrollIntoViewIfNeededAsync();
            var box = grid.GetByRole(AriaRole.Searchbox);
            await box.FillAsync("ada");
            await page.WaitForFunctionAsync("() => new URLSearchParams(location.search).get('tools-search') === 'ada'");
            await page.WaitForFunctionAsync("() => [...document.querySelectorAll('.ib-datagrid')].find(g => g.textContent.includes('Export')).querySelectorAll('tbody tr').length === 3");
            Assert.Equal("ada", await box.InputValueAsync());

            var top = await grid.Locator("table").EvaluateAsync<double>("t => t.getBoundingClientRect().top");
            await grid.Locator("tbody .ib-table-select").First.CheckAsync();
            await grid.Locator(".ib-selection-bar:not([hidden])").WaitForAsync();
            Assert.False(await toolbar.IsVisibleAsync());
            Assert.Equal(top, await grid.Locator("table").EvaluateAsync<double>("t => t.getBoundingClientRect().top"), 1);
            await grid.Locator(".ib-selection-clear").ClickAsync();
            await toolbar.WaitForAsync();
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task The_last_key_always_applies_and_back_follows_the_address(string mode)
    {
        var (browser, page, _) = await OpenAsync(mode);
        await using (browser)
        {
            var grid = page.Locator(".ib-datagrid", new() { HasText = "New order" });
            var box = grid.GetByRole(AriaRole.Searchbox);
            await box.ClickAsync();
            // Near the pause that applies a search, so a key lands while the last one's page is on its way.
            foreach (var key in "alax")
            {
                await page.Keyboard.PressAsync(key.ToString());
                await page.WaitForTimeoutAsync(347);
            }

            await page.WaitForFunctionAsync("() => new URLSearchParams(location.search).get('tools-search') === 'alax'");
            await grid.GetByText("No items").WaitForAsync();

            await box.FillAsync("ada");
            await page.Keyboard.PressAsync("Enter");
            await page.WaitForFunctionAsync("() => new URLSearchParams(location.search).get('tools-search') === 'ada'");
            await page.GoBackAsync();
            await page.WaitForFunctionAsync("() => new URLSearchParams(location.search).get('tools-search') !== 'ada'");
            await page.WaitForFunctionAsync("() => { const v = new URLSearchParams(location.search).get('tools-search') ?? ''; return [...document.querySelectorAll('input[type=search]')].some(i => i.name === 'tools-search' && i.value === v); }");
        }
    }

    [Fact]
    public async Task What_was_typed_while_a_response_was_on_its_way_stays_typed()
    {
        var (browser, page, _) = await OpenAsync("static");
        await using (browser)
        {
            await page.RouteAsync("**/docs/components/data-grid*", async route =>
            {
                // Back before the next key's pause is up, which would replace this navigation.
                await Task.Delay(50);
                await route.ContinueAsync();
            });
            var grid = page.Locator(".ib-datagrid", new() { HasText = "New order" });
            var box = grid.GetByRole(AriaRole.Searchbox);
            await box.ClickAsync();
            await page.Keyboard.PressAsync("a");
            await page.WaitForFunctionAsync("() => new URLSearchParams(location.search).get('tools-search') === 'a'");

            // Typed with the response for "a" still to come, then typing on after it has.
            await page.Keyboard.PressAsync("l");
            await page.WaitForFunctionAsync("() => document.querySelector('input[name=tools-search]').defaultValue !== ''");
            await page.Keyboard.PressAsync("a");
            Assert.Equal("ala", await box.InputValueAsync());
            await page.WaitForFunctionAsync("() => new URLSearchParams(location.search).get('tools-search') === 'ala'");
        }
    }

    [Fact]
    public async Task At_phone_width_the_search_box_stops_short_of_the_tools()
    {
        var (browser, page, _) = await OpenAsync("static");
        await using (browser)
        {
            foreach (var width in new[] { 390, 320 })
            {
                await page.SetViewportSizeAsync(width, 800);
                await page.WaitForTimeoutAsync(400);
                var grid = page.Locator(".ib-datagrid", new() { HasText = "New order" });
                var search = await grid.GetByRole(AriaRole.Searchbox).BoundingBoxAsync();
                var end = await grid.Locator(".ib-toolbar-end").BoundingBoxAsync();
                Assert.True(search!.X + search.Width <= end!.X + 1, $"{width}: the box ends at {search.X + search.Width}, the tools start at {end.X}");
            }
        }
    }

    [Fact]
    public async Task A_bad_cookie_is_ignored_and_tools_sit_at_the_far_edge()
    {
        var (browser, page, grid) = await OpenAsync("static");
        await using (browser)
        {
            var title = await grid.Locator("thead th", new() { HasText = "Customer" }).Locator(".ib-table-sort").BoundingBoxAsync();
            var tool = await grid.Locator("thead th", new() { HasText = "Customer" }).Locator("[data-ib-menu]").BoundingBoxAsync();
            var cell = await grid.Locator("thead th", new() { HasText = "Customer" }).BoundingBoxAsync();
            Assert.True(tool!.X - (title!.X + title.Width) > 40, "the menu is away from the title");
            Assert.True(cell!.X + cell.Width - (tool.X + tool.Width) < 24, "the menu is at the cell's far edge");

            // An end-aligned title lines up with its values.
            var total = await grid.Locator("thead th", new() { HasText = "Total" }).Locator(".ib-table-title").EvaluateAsync<double>("t => t.getBoundingClientRect().right");
            var value = await grid.Locator("tbody tr").First.Locator("td").Last.EvaluateAsync<double>("c => { const r = document.createRange(); r.selectNodeContents(c); return r.getBoundingClientRect().right; }");
            Assert.Equal(value, total, 1.5);

            foreach (var bad in new[] { "{}", "%7B%22c%22%3Anull%7D", "nonsense" })
            {
                await page.Context.AddCookiesAsync([new() { Name = "ib-grid-arrange-example", Value = bad, Url = page.Url }]);
                var response = await page.ReloadAsync();
                Assert.Equal(200, response!.Status);
            }
        }
    }

    [Fact]
    public async Task A_column_that_opted_out_has_no_controls_and_frozen_keeps_it_at_its_edge()
    {
        var (browser, page, grid) = await OpenAsync("static");
        await using (browser)
        {
            Assert.Equal(0, await grid.Locator("thead th", new() { HasText = "Order" }).Locator(".ib-col-resize, [data-ib-menu]").CountAsync());

            await grid.Locator("thead th", new() { HasText = "Total" }).Locator("[data-ib-menu]").ClickAsync();
            await page.GetByRole(AriaRole.Menuitem, new() { Name = "Freeze at start" }).ClickAsync();
            await Until(grid, ["Total", "Order", "Customer", "Date", "Status"]);
            Assert.Contains("ib-frozen-start", await grid.Locator("thead th", new() { HasText = "Total" }).GetAttributeAsync("class"));
        }
    }
}
