using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>Rearranging a DataGrid's columns in Chromium, and the cookie that keeps it, on the second example.</summary>
public sealed class DataGridArrangeTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private string Url(string mode) => new Uri(site.Client.BaseAddress!, $"docs/components/data-grid?mode={mode}").ToString();

    private async Task<(IPlaywright, IBrowser, IPage, ILocator)> OpenAsync(string mode)
    {
        Assert.Equal(0, Microsoft.Playwright.Program.Main(["install", "chromium"]));
        var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 } });
        await page.GotoAsync(Url(mode), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var grid = page.Locator(".ib-datagrid", new() { HasText = "Status" }).Nth(1);
        await grid.Locator("ib-grid-columns").WaitForAsync(new() { State = WaitForSelectorState.Attached });
        if (mode == "interactive") await grid.Locator("ib-grid-columns[data-ib-interactive]").WaitForAsync(new() { State = WaitForSelectorState.Attached });
        await grid.Locator("table[role=grid]").WaitForAsync();
        await grid.Locator("thead").ScrollIntoViewIfNeededAsync();
        await page.WaitForTimeoutAsync(200);
        return (playwright, browser, page, grid);
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
        var (playwright, browser, page, grid) = await OpenAsync(mode);
        using (playwright)
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
        var (playwright, browser, page, grid) = await OpenAsync(mode);
        using (playwright)
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
        var (playwright, browser, page, grid) = await OpenAsync(mode);
        using (playwright)
        await using (browser)
        {
            await grid.Locator("thead th", new() { HasText = "Status" }).Locator("[data-ib-menu]").ClickAsync();
            await page.GetByRole(AriaRole.Menuitem, new() { Name = "Hide column" }).ClickAsync();
            await Until(grid, ["Order", "Customer", "Date", "Total"]);

            await grid.Locator(".ib-col-picker button").First.ClickAsync();
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
        var (playwright, browser, page, grid) = await OpenAsync(mode);
        using (playwright)
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

    [Fact]
    public async Task A_bad_cookie_is_ignored_and_tools_sit_at_the_far_edge()
    {
        var (playwright, browser, page, grid) = await OpenAsync("static");
        using (playwright)
        await using (browser)
        {
            var title = await grid.Locator("thead th", new() { HasText = "Customer" }).Locator(".ib-table-sort").BoundingBoxAsync();
            var tool = await grid.Locator("thead th", new() { HasText = "Customer" }).Locator("[data-ib-menu]").BoundingBoxAsync();
            var cell = await grid.Locator("thead th", new() { HasText = "Customer" }).BoundingBoxAsync();
            Assert.True(tool!.X - (title!.X + title.Width) > 40, "the menu is away from the title");
            Assert.True(cell!.X + cell.Width - (tool.X + tool.Width) < 24, "the menu is at the cell's far edge");

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
        var (playwright, browser, page, grid) = await OpenAsync("static");
        using (playwright)
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
