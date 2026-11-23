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
        var (browser, page) = await OpenAsync("static", topic: "/server-data");
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
