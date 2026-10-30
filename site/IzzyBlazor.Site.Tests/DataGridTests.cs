using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>DataGrid in Chromium: widths the server set, and Shift+click adding to the sort.</summary>
public sealed class DataGridTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private async Task<(IPlaywright, IBrowser, IPage)> OpenAsync(string mode, int width = 1400)
    {
        Assert.Equal(0, Microsoft.Playwright.Program.Main(["install", "chromium"]));
        var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = width, Height = 900 } });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/data-grid?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        if (mode == "interactive") await page.Locator("ib-table[data-ib-interactive]").First.WaitForAsync();
        return (playwright, browser, page);
    }

    [Fact]
    public async Task Columns_keep_their_widths_share_the_rest_and_scroll_past_their_minimums()
    {
        var (playwright, browser, page) = await OpenAsync("static");
        using (playwright)
        await using (browser)
        {
            const string measure = """
                () => {
                  const table = document.querySelector('.ib-datagrid table');
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

    [Fact]
    public async Task What_doesnt_fit_stays_in_its_cell()
    {
        var (playwright, browser, page) = await OpenAsync("static");
        using (playwright)
        await using (browser)
        {
            // Date and Total, both sortable, far narrower than their text; Total also sorted second.
            await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/data-grid?mode=static&sort=customer,total").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
            var overflow = await page.EvaluateAsync<string[]>("""
                () => {
                  const table = document.querySelector('.ib-datagrid table');
                  table.style.setProperty('--ib-col-2', '3rem');
                  table.style.setProperty('--ib-col-4', '3rem');
                  const problems = [];
                  for (const cell of table.querySelectorAll('th, td')) {
                    const box = cell.getBoundingClientRect();
                    for (const part of cell.querySelectorAll('.ib-table-sort, .ib-table-filter-button')) {
                      const r = part.getBoundingClientRect();
                      if (r.right > box.right + 1 || r.left < box.left - 1) problems.push(cell.textContent.trim() + ': ' + part.className);
                    }
                  }
                  const scroll = table.closest('.ib-table-scroll');
                  if (scroll.scrollWidth > table.getBoundingClientRect().width + 1) problems.push('scrolls past the table');
                  const money = table.querySelector('tbody td.ib-cell-end');
                  if (getComputedStyle(money).whiteSpace !== 'nowrap') problems.push('numbers wrap');
                  return problems;
                }
                """);
            Assert.Empty(overflow);
        }
    }

    // The Frozen columns example: the first two columns frozen at the start, the last at the end.
    private const string Frozen = """
        () => {
          const table = document.querySelectorAll('.ib-datagrid table')[2];
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
    [InlineData("interactive")]
    public async Task Frozen_columns_stay_while_the_rest_scroll_and_focus_stops_clear_of_them(string mode)
    {
        var (playwright, browser, page) = await OpenAsync(mode);
        using (playwright)
        await using (browser)
        {
            var at = await page.EvaluateAsync<FrozenBoxes>(Frozen);
            Assert.True(at.Scrolls);
            Assert.False(at.Unfrozen);
            await page.Locator(".ib-datagrid table").Nth(2).EvaluateAsync("t => { t.closest('.ib-table-scroll').scrollLeft = 300; }");
            var moved = await page.EvaluateAsync<FrozenBoxes>(Frozen);
            Assert.Equal(at.First[0], moved.First[0], 0.5);
            Assert.Equal(at.Second[0], moved.Second[0], 0.5);
            Assert.Equal(at.Second[0], moved.First[1], 0.5);
            Assert.Equal(moved.Box[1], moved.Last[1], 1.5);

            // Opaque on every row, stripes included.
            var colors = await page.Locator(".ib-datagrid table").Nth(2).EvaluateAsync<string[]>("t => [...t.tBodies[0].rows].map(r => getComputedStyle(r.cells[0]).backgroundColor)");
            Assert.All(colors, c => Assert.DoesNotContain("/", c));
            Assert.All(colors, c => Assert.DoesNotContain("rgba", c));

            // Focus on a frozen cell leaves the grid where it is.
            await page.Locator(".ib-datagrid table").Nth(2).Locator("tbody tr").First.Locator("td").First.FocusAsync();
            await page.Locator(".ib-datagrid table").Nth(2).EvaluateAsync("t => { t.closest('.ib-table-scroll').scrollLeft = 300; }");
            await page.Keyboard.PressAsync("ArrowDown");
            Assert.Equal(300, await page.Locator(".ib-datagrid table").Nth(2).EvaluateAsync<double>("t => t.closest('.ib-table-scroll').scrollLeft"), 1);
            await page.Locator(".ib-datagrid table").Nth(2).Locator("tbody tr").Nth(2).Locator("td").First.ClickAsync();
            Assert.Equal(300, await page.Locator(".ib-datagrid table").Nth(2).EvaluateAsync<double>("t => t.closest('.ib-table-scroll').scrollLeft"), 1);

            // A cell the arrows reach isn't left under them.
            await page.Locator(".ib-datagrid table").Nth(2).Locator("tbody tr").First.Locator("td").Nth(3).FocusAsync();
            await page.Keyboard.PressAsync("ArrowLeft");
            var clear = await page.EvaluateAsync<double[]>("() => { const r = document.activeElement.getBoundingClientRect(); const t = document.activeElement.closest('tr').cells[1].getBoundingClientRect(); return [r.left, t.right]; }");
            Assert.True(clear[0] >= clear[1] - 1, $"focused cell starts at {clear[0]}, frozen columns end at {clear[1]}");
        }
    }

    [Fact]
    public async Task Frozen_columns_too_wide_for_a_phone_scroll_like_the_rest()
    {
        var (playwright, browser, page) = await OpenAsync("static", 390);
        using (playwright)
        await using (browser)
        {
            var at = await page.EvaluateAsync<FrozenBoxes>(Frozen);
            Assert.True(at.Unfrozen);

            // Widening looks again, and so does narrowing.
            await page.SetViewportSizeAsync(1400, 900);
            await page.WaitForFunctionAsync("() => !document.querySelectorAll('.ib-datagrid table')[2].hasAttribute('data-ib-unfrozen')");
            await page.SetViewportSizeAsync(500, 900);
            await page.WaitForFunctionAsync("() => document.querySelectorAll('.ib-datagrid table')[2].hasAttribute('data-ib-unfrozen')");
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
    [InlineData("interactive")]
    public async Task Shift_click_adds_to_the_sort_and_a_plain_click_replaces_it(string mode)
    {
        var (playwright, browser, page) = await OpenAsync(mode);
        using (playwright)
        await using (browser)
        {
            var grid = page.Locator(".ib-datagrid").First;
            var customer = grid.Locator("th a.ib-table-sort", new() { HasText = "Customer" });
            var total = grid.Locator("th a.ib-table-sort", new() { HasText = "Total" });

            // The address changes before the page does: wait for the header that shows the new sort.
            var customerHeader = grid.Locator("th", new() { HasText = "Customer" });
            var totalHeader = grid.Locator("th", new() { HasText = "Total" });
            Task SortIs(string sort) => page.WaitForFunctionAsync(
                "sort => new URLSearchParams(location.search).get('sort') === sort", sort);

            await customer.ClickAsync();
            await SortIs("customer");
            await page.Locator(".ib-datagrid >> nth=0 >> th[aria-sort=ascending]", new() { HasText = "Customer" }).WaitForAsync();

            await total.ClickAsync(new() { Modifiers = [KeyboardModifier.Shift] });
            await SortIs("customer,total");
            await totalHeader.Locator(".ib-table-sort-order").WaitForAsync();
            Assert.Single(page.Context.Pages);
            Assert.Equal("ascending", await customerHeader.GetAttributeAsync("aria-sort"));
            Assert.Equal("none", await totalHeader.GetAttributeAsync("aria-sort"));
            Assert.Contains("Sort 2, ascending", await totalHeader.TextContentAsync());

            await total.ClickAsync(new() { Modifiers = [KeyboardModifier.Shift] });
            await SortIs("customer,-total");
            await page.Locator(".ib-datagrid >> nth=0 >> th", new() { HasText = "Sort 2, descending" }).WaitForAsync();

            await total.ClickAsync(new() { Modifiers = [KeyboardModifier.Shift] });
            await SortIs("customer");
            await totalHeader.Locator(".ib-table-sort-order").WaitForAsync(new() { State = WaitForSelectorState.Detached });

            await total.ClickAsync();
            await SortIs("total");
            await grid.Locator(".ib-table-sort-order").First.WaitForAsync(new() { State = WaitForSelectorState.Detached });
        }
    }

    [Fact]
    public async Task A_preview_is_left_out_of_the_index_and_the_sitemap()
    {
        var index = await site.Client.GetStringAsync("docs/components");
        Assert.DoesNotContain("/components/data-grid", index);
        var sitemap = await site.Client.GetStringAsync("sitemap.xml");
        Assert.DoesNotContain("data-grid", sitemap);
        var page = await site.Client.GetStringAsync("docs/components/data-grid");
        Assert.Contains("noindex", page);
        Assert.DoesNotContain("data-grid", await site.Client.GetStringAsync("docs/components/table/api"));
    }
}
