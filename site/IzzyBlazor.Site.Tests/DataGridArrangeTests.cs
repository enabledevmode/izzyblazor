using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>Rearranging a DataGrid's columns in Chromium, and the cookie that keeps it, on the second example.</summary>
public sealed class DataGridArrangeTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private string Url(string mode) => new Uri(site.Client.BaseAddress!, $"docs/components/data-grid/columns?mode={mode}").ToString();

    private async Task<(TestBrowser, IPage, ILocator)> OpenAsync(string mode)
    {
        var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 } });
        await page.GotoAsync(Url(mode), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var grid = page.Locator(".ib-datagrid:has(.ib-table-scroll[aria-label='Orders you arrange'])");
        await grid.Locator("ib-grid-columns").WaitForAsync(new() { State = WaitForSelectorState.Attached });
        if (mode == "interactive") await grid.Locator("ib-grid-columns[data-ib-interactive]").WaitForAsync(new() { State = WaitForSelectorState.Attached });
        await grid.Locator("table[role=grid]").WaitForAsync();
        await grid.Locator("thead").ScrollIntoViewSettledAsync();
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
            Assert.Equal("/docs/components/data-grid/columns", cookies.First(c => c.Name == "ib-grid-arrange-example").Path);

            // The server draws it: the width is in the first HTML, with no script.
            var html = await page.Context.APIRequest.GetAsync(Url(mode));
            Assert.Contains("px;", await html.TextAsync());
            await page.ReloadAsync(new() { WaitUntil = WaitUntilState.NetworkIdle });
            Assert.Equal(before + 40, await WidthAsync(grid, "Customer"), 1.5);
        }
    }

    [Theory]
    [InlineData("static")]
    public async Task The_picker_hides_shows_and_resets(string mode)
    {
        var (browser, page, grid) = await OpenAsync(mode);
        await using (browser)
        {
            await grid.Locator(".ib-col-picker button").First.ClickAsync();
            await grid.GetByRole(AriaRole.Checkbox, new() { Name = "Status" }).UncheckAsync();
            await Until(grid, ["Order", "Customer", "Date", "Total"]);

            var box = await grid.GetByRole(AriaRole.Checkbox, new() { Name = "Status" }).BoundingBoxAsync();
            Assert.Equal(16, box!.Width, 1);
            // Hiding a column leaves its row as it was: its handle's room stays, empty.
            var row = grid.Locator(".ib-col-picker-list li", new() { HasText = "Status" });
            Assert.Equal(0, await row.Locator("[data-ib-picker-handle]").CountAsync());
            Assert.Equal(1, await row.Locator(".ib-col-picker-gap").CountAsync());
            var hiddenRow = await row.BoundingBoxAsync();
            var shownRow = await grid.Locator(".ib-col-picker-list li", new() { HasText = "Date" }).BoundingBoxAsync();
            Assert.Equal(shownRow!.Width, hiddenRow!.Width, 0.5);

            await grid.GetByRole(AriaRole.Checkbox, new() { Name = "Status" }).CheckAsync();
            await Until(grid, ["Order", "Customer", "Date", "Status", "Total"]);

            // One that starts hidden: the picker shows it, and it stays shown.
            await grid.GetByRole(AriaRole.Checkbox, new() { Name = "Weekday" }).CheckAsync();
            await Until(grid, ["Order", "Customer", "Date", "Status", "Total", "Weekday"]);
            await page.ReloadAsync(new() { WaitUntil = WaitUntilState.NetworkIdle });
            await Until(grid, ["Order", "Customer", "Date", "Status", "Total", "Weekday"]);

            await grid.Locator(".ib-col-picker button").First.ClickAsync();
            await grid.Locator(".ib-col-picker [data-ib-col-action=reset]").ClickAsync();
            await page.WaitForFunctionAsync("() => !document.cookie.includes('ib-grid-arrange-example')");
            await Until(grid, ["Order", "Customer", "Date", "Status", "Total"]);
        }
    }

    [Theory]
    [InlineData("static")]
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
    public async Task Search_filters_as_you_type_and_the_selection_bar_takes_the_toolbars_place(string mode)
    {
        var browser = await TestBrowser.OpenAsync();
        await using (browser)
        {
            var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 } });
            await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/data-grid?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
            var grid = page.Locator(".ib-datagrid", new() { HasText = "New order" });
            var toolbar = grid.Locator(".ib-grid-toolbar");
            await toolbar.ScrollIntoViewSettledAsync();
            var box = grid.GetByRole(AriaRole.Searchbox);
            await box.FillAsync("ada");
            await page.WaitForFunctionAsync("() => new URLSearchParams(location.search).get('tools-search') === 'ada'");
            await page.WaitForFunctionAsync("() => [...document.querySelectorAll('.ib-datagrid')].find(g => g.textContent.includes('Export')).querySelectorAll('tbody tr').length === 3");
            Assert.Equal("ada", await box.InputValueAsync());

            var top = await grid.Locator("table").EvaluateAsync<double>("t => t.getBoundingClientRect().top");
            await grid.Locator("tbody .ib-table-select").First.CheckAsync();
            await grid.Locator(".ib-selection-bar:not([hidden])").WaitForAsync();
            await Assertions.Expect(grid.Locator(".ib-selection-count")).ToBeVisibleAsync();
            Assert.False(await toolbar.IsVisibleAsync());
            Assert.Equal(top, await grid.Locator("table").EvaluateAsync<double>("t => t.getBoundingClientRect().top"), 1);
            await grid.Locator(".ib-selection-clear").ClickAsync();
            await toolbar.WaitForAsync();
        }
    }
}
