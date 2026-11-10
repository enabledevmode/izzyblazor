using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>DataGrid in Chromium: widths the server set, and Shift+click adding to the sort.</summary>
public sealed class DataGridTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private async Task<(TestBrowser, IPage)> OpenAsync(string mode, int width = 1400, string topic = "")
    {
        var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = width, Height = 900 } });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/data-grid{topic}?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        if (mode == "interactive") await page.Locator("ib-table[data-ib-interactive]").First.WaitForAsync();
        return (browser, page);
    }

    [Theory]
    [InlineData("static")]
    public async Task Export_downloads_what_the_screen_shows_without_leaving_the_page(string mode)
    {
        var (browser, page) = await OpenAsync(mode, topic: "/export");
        await using (browser)
        {
            var grid = page.Locator(".ib-datagrid", new() { HasText = "Orders to export" });
            await grid.ScrollIntoViewSettledAsync();
            var url = page.Url;
            var menu = grid.GetByRole(AriaRole.Button, new() { Name = "Export" });

            // As wide as the toolbar reckons on the first paint.
            Assert.Equal(52, (await menu.BoundingBoxAsync())!.Width);

            // Sorted by total, highest first: the links follow.
            var total = grid.Locator("thead th", new() { HasText = "Total" }).Locator("a, button").First;
            var csv = grid.Locator(".ib-toolbar-end > .ib-toolbar-unit a[download]", new() { HasText = "Export CSV" });
            await total.ClickAsync();
            await Expect(csv, "sort=total");
            await total.ClickAsync();
            await Expect(csv, "sort=-total");

            await menu.ClickAsync();
            var download = await page.RunAndWaitForDownloadAsync(() => csv.ClickAsync());
            Assert.Equal("orders.csv", download.SuggestedFilename);
            var text = await File.ReadAllTextAsync((await download.PathAsync())!);
            var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("Number,Customer,Total", lines[0].TrimStart('\uFEFF'));
            Assert.Equal(24, lines.Length);
            var totals = lines.Skip(1).Select(l => decimal.Parse(l.Split(',')[^1].Trim('"'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture)).ToList();
            Assert.Equal(totals.OrderDescending(), totals);

            var excel = grid.Locator(".ib-toolbar-end > .ib-toolbar-unit a[download]", new() { HasText = "Export Excel" });
            // Choosing an item closes the menu, which takes a moment.
            await excel.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
            await menu.ClickAsync();
            download = await page.RunAndWaitForDownloadAsync(() => excel.ClickAsync());
            Assert.Equal("orders.xlsx", download.SuggestedFilename);
            using var zip = System.IO.Compression.ZipFile.OpenRead((await download.PathAsync())!);
            Assert.NotNull(zip.GetEntry("xl/worksheets/sheet1.xml"));

            // Still on the page, its grid intact.
            Assert.StartsWith(url.Split('?')[0], page.Url);
            Assert.True(await grid.IsVisibleAsync());
        }
    }

    private static readonly string Focused = """
        () => {
          const a = document.activeElement;
          return a?.closest?.('tr')?.getAttribute('aria-rowindex') ?? (a?.matches?.('.ib-table-scroll') ? 'box' : 'none');
        }
        """;

    private static async Task WaitForFocusAsync(IPage page, string row) =>
        await page.WaitForFunctionAsync($"() => ({Focused})() === '{row}'");

    [Fact]
    public async Task On_a_static_page_virtual_rows_load_on_scroll()
    {
        var (browser, page) = await OpenAsync("static");
        await using (browser)
        {
            var grid = page.Locator(".ib-datagrid", new() { HasText = "Every order" });
            Assert.Equal(20, await grid.Locator("tbody tr").CountAsync());
            Assert.Equal(0, await grid.Locator("table[aria-rowcount]").CountAsync());
            Assert.NotNull(await grid.GetAttributeAsync("data-ib-load-on-scroll"));
        }
    }

    private static async Task Expect(ILocator link, string part)
    {
        for (var i = 0; i < 50 && !((await link.GetAttributeAsync("href")) ?? "").Contains(part, StringComparison.Ordinal); i++)
        {
            await Task.Delay(100);
        }

        Assert.Contains(part, await link.GetAttributeAsync("href"));
    }

    [Fact]
    public async Task Columns_keep_their_widths_share_the_rest_and_scroll_past_their_minimums()
    {
        var (browser, page) = await OpenAsync("static");
        await using (browser)
        {
            const string measure = """
                () => {
                  const table = document.querySelector(`.ib-datagrid:has(.ib-table-scroll[aria-label='Orders']) table`);
                  const box = table.closest('.ib-table-scroll');
                  const widths = [...table.querySelectorAll('thead th')].map(th => th.getBoundingClientRect().width);
                  return { widths, scrolls: box.scrollWidth > box.clientWidth + 1, rem: parseFloat(getComputedStyle(document.documentElement).fontSize) };
                }
                """;

            var wide = await page.EvaluateAsync<Measured>(measure);
            Assert.False(wide.Scrolls);
            // Select, Order, Customer, Date, Status, Total.
            Assert.Equal(6 * wide.Rem, wide.Widths[1], 0.5);
            Assert.Equal(8 * wide.Rem, wide.Widths[3], 0.5);
            Assert.Equal(wide.Widths[2], wide.Widths[4], 0.5);
            Assert.True(wide.Widths[2] > 12 * wide.Rem);

            await page.SetViewportSizeAsync(500, 900);
            var narrow = await page.EvaluateAsync<Measured>(measure);
            Assert.True(narrow.Scrolls);
            Assert.Equal(6 * narrow.Rem, narrow.Widths[1], 0.5);
            // Status shares equally, so it gets Customer's minimum too.
            Assert.Equal(12 * narrow.Rem, narrow.Widths[4], 0.5);
        }
    }

    // The Frozen columns example: the first two columns frozen at the start, the last at the end.
    private const string Frozen = """
        () => {
          const table = document.querySelector(`.ib-datagrid:has(.ib-table-scroll[aria-label='Orders by column']) table`);
          const box = table.closest('.ib-table-scroll');
          const row = table.tBodies[0].rows[0];
          const edges = (c) => { const r = c.getBoundingClientRect(); return [r.left, r.right]; };
          return { box: edges(box), first: edges(row.cells[0]), second: edges(row.cells[1]), last: edges(row.cells[row.cells.length - 1]), unfrozen: table.hasAttribute('data-ib-unfrozen'), scrolls: box.scrollWidth > box.clientWidth };
        }
        """;

    private sealed class FrozenBoxes
    {
        public double[] Box { get; set; } = [];
        public double[] First { get; set; } = [];
        public double[] Second { get; set; } = [];
        public double[] Last { get; set; } = [];
        public bool Unfrozen { get; set; }
        public bool Scrolls { get; set; }
    }

    [Theory]
    [InlineData("static")]
    public async Task Frozen_columns_stay_while_the_rest_scroll_and_focus_stops_clear_of_them(string mode)
    {
        var (browser, page) = await OpenAsync(mode, topic: "/columns");
        await using (browser)
        {
            var at = await page.EvaluateAsync<FrozenBoxes>(Frozen);
            Assert.True(at.Scrolls);
            Assert.False(at.Unfrozen);
            await page.Locator(".ib-datagrid:has(.ib-table-scroll[aria-label='Orders by column']) table").EvaluateAsync("t => { t.closest('.ib-table-scroll').scrollLeft = 300; }");
            var moved = await page.EvaluateAsync<FrozenBoxes>(Frozen);
            Assert.Equal(at.First[0], moved.First[0], 0.5);
            Assert.Equal(at.Second[0], moved.Second[0], 0.5);
            Assert.Equal(at.Second[0], moved.First[1], 0.5);
            Assert.Equal(moved.Box[1], moved.Last[1], 1.5);

            // Opaque on every row, stripes included.
            var colors = await page.Locator(".ib-datagrid:has(.ib-table-scroll[aria-label='Orders by column']) table").EvaluateAsync<string[]>("t => [...t.tBodies[0].rows].map(r => getComputedStyle(r.cells[0]).backgroundColor)");
            Assert.All(colors, c => Assert.DoesNotContain("/", c));
            Assert.All(colors, c => Assert.DoesNotContain("rgba", c));

            // Focus on a frozen cell leaves the grid where it is.
            await page.Locator(".ib-datagrid:has(.ib-table-scroll[aria-label='Orders by column']) table").Locator("tbody tr").First.Locator("td").First.FocusAsync();
            await page.Locator(".ib-datagrid:has(.ib-table-scroll[aria-label='Orders by column']) table").EvaluateAsync("t => { t.closest('.ib-table-scroll').scrollLeft = 300; }");
            await page.Keyboard.PressAsync("ArrowDown");
            Assert.Equal(300, await page.Locator(".ib-datagrid:has(.ib-table-scroll[aria-label='Orders by column']) table").EvaluateAsync<double>("t => t.closest('.ib-table-scroll').scrollLeft"), 1);
            await page.Locator(".ib-datagrid:has(.ib-table-scroll[aria-label='Orders by column']) table").Locator("tbody tr").Nth(2).Locator("td").First.ClickAsync();
            Assert.Equal(300, await page.Locator(".ib-datagrid:has(.ib-table-scroll[aria-label='Orders by column']) table").EvaluateAsync<double>("t => t.closest('.ib-table-scroll').scrollLeft"), 1);

            // A cell the arrows reach isn't left under them.
            await page.Locator(".ib-datagrid:has(.ib-table-scroll[aria-label='Orders by column']) table").Locator("tbody tr").First.Locator("td").Nth(3).FocusAsync();
            await page.Keyboard.PressAsync("ArrowLeft");
            var clear = await page.EvaluateAsync<double[]>("() => { const r = document.activeElement.getBoundingClientRect(); const t = document.activeElement.closest('tr').cells[1].getBoundingClientRect(); return [r.left, t.right]; }");
            Assert.True(clear[0] >= clear[1] - 1, $"focused cell starts at {clear[0]}, frozen columns end at {clear[1]}");
        }
    }

    private sealed class Measured
    {
        public double[] Widths { get; set; } = [];
        public bool Scrolls { get; set; }
        public double Rem { get; set; }
    }

    [Theory]
    [InlineData("static")]
    public async Task Shift_click_adds_to_the_sort_and_a_plain_click_replaces_it(string mode)
    {
        var (browser, page) = await OpenAsync(mode);
        await using (browser)
        {
            var grid = page.Locator(".ib-datagrid:has(.ib-table-scroll[aria-label='Orders'])");
            var customer = grid.Locator("th a.ib-table-sort", new() { HasText = "Customer" });
            var total = grid.Locator("th a.ib-table-sort", new() { HasText = "Total" });

            // The address changes before the page does: wait for the header that shows the new sort.
            var customerHeader = grid.Locator("th", new() { HasText = "Customer" });
            var totalHeader = grid.Locator("th", new() { HasText = "Total" });
            Task SortIs(string sort) => page.WaitForFunctionAsync(
                "sort => new URLSearchParams(location.search).get('sort') === sort", sort);

            await customer.ClickAsync();
            await SortIs("customer");
            await grid.Locator("th[aria-sort=ascending]", new() { HasText = "Customer" }).WaitForAsync();

            await total.ClickAsync(new() { Modifiers = [KeyboardModifier.Shift] });
            await SortIs("customer,total");
            await totalHeader.Locator(".ib-table-sort-order").WaitForAsync();
            Assert.Single(page.Context.Pages);
            Assert.Equal("ascending", await customerHeader.GetAttributeAsync("aria-sort"));
            Assert.Equal("none", await totalHeader.GetAttributeAsync("aria-sort"));
            Assert.Contains("Sort 2, ascending", await totalHeader.TextContentAsync());

            await total.ClickAsync(new() { Modifiers = [KeyboardModifier.Shift] });
            await SortIs("customer,-total");
            await grid.Locator("th", new() { HasText = "Sort 2, descending" }).WaitForAsync();

            await total.ClickAsync(new() { Modifiers = [KeyboardModifier.Shift] });
            await SortIs("customer");
            await totalHeader.Locator(".ib-table-sort-order").WaitForAsync(new() { State = WaitForSelectorState.Detached });

            await total.ClickAsync();
            await SortIs("total");
            await grid.Locator(".ib-table-sort-order").First.WaitForAsync(new() { State = WaitForSelectorState.Detached });
        }
    }

    // Touch has no Shift: the column menu's Add to sort keeps the sort and adds the column.
    [Fact]
    public async Task The_column_menu_adds_to_the_sort()
    {
        var browser = await TestBrowser.OpenAsync();
        await using (browser)
        {
            var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 } });
            await page.GotoAsync(new Uri(site.Client.BaseAddress!, "frame/data-grid-arrange?mode=static&sort=total").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
            await page.Locator("button[title='Options for Customer']").ClickAsync();
            await page.GetByRole(AriaRole.Menuitem, new() { Name = "Add to sort, descending" }).ClickAsync();
            await page.WaitForFunctionAsync("() => new URLSearchParams(location.search).get('sort') === 'total,-customer'");
            await page.Locator("th", new() { HasText = "Sort 2, descending" }).WaitForAsync();
        }
    }
}
