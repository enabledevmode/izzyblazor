using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>DataGrid in Chromium: widths the server set, and Shift+click adding to the sort.</summary>
public sealed class DataGridTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private async Task<(TestBrowser, IPage)> OpenAsync(string mode, int width = 1400)
    {
        var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = width, Height = 900 } });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/data-grid?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        if (mode == "interactive") await page.Locator("ib-table[data-ib-interactive]").First.WaitForAsync();
        return (browser, page);
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task Export_downloads_what_the_screen_shows_without_leaving_the_page(string mode)
    {
        var (browser, page) = await OpenAsync(mode);
        await using (browser)
        {
            var grid = page.Locator(".ib-datagrid", new() { HasText = "Orders to export" });
            await grid.ScrollIntoViewIfNeededAsync();
            var url = page.Url;
            var menu = grid.GetByRole(AriaRole.Button, new() { Name = "Export" });

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

    [Fact]
    public async Task The_grids_tools_go_where_theyre_written_with_no_script_too()
    {
        await using var browser = await TestBrowser.OpenAsync();
        foreach (var script in new[] { true, false })
        {
            var context = await browser.NewContextAsync(new() { JavaScriptEnabled = script, ViewportSize = new() { Width = 1400, Height = 900 } });
            var page = await context.NewPageAsync();
            await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/data-grid?mode=static").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
            var grid = page.Locator(".ib-datagrid", new() { HasText = "Orders with placed tools" });

            // In the order written, one of each.
            var start = grid.Locator(".ib-toolbar-start > .ib-toolbar-unit");
            Assert.Equal(2, await start.CountAsync());
            Assert.Contains("Add order", await start.Nth(0).TextContentAsync());
            Assert.Equal(1, await start.Nth(1).Locator("input[type=search]").CountAsync());
            var end = grid.Locator(".ib-toolbar-end > .ib-toolbar-unit");
            Assert.Equal(3, await end.CountAsync());
            Assert.Equal(1, await end.Nth(0).Locator(".ib-col-picker").CountAsync());
            Assert.Equal("Help", await end.Nth(1).Locator("button").GetAttributeAsync("aria-label"));
            Assert.Contains("Export Excel", await end.Nth(2).TextContentAsync());

            // Icons alone unless told otherwise, named all the same.
            var picker = end.Nth(0).Locator("button").First;
            Assert.Equal("Columns", await picker.GetAttributeAsync("aria-label"));
            Assert.Equal("Columns", await picker.GetAttributeAsync("title"));
            // Its checkboxes need script, so with none it isn't offered, like the resize handles.
            Assert.Equal(script, await picker.IsVisibleAsync());
            if (!script) Assert.Equal("none", await page.Locator(".ib-col-resize").First.EvaluateAsync<string>("e => getComputedStyle(e).display"));

            // One click for the default, and the other behind the arrow, with no script too.
            var main = await page.RunAndWaitForDownloadAsync(() => end.Nth(2).Locator(".ib-split-btn > a[download]").ClickAsync());
            Assert.Equal("orders.xlsx", main.SuggestedFilename);
            await end.Nth(2).Locator("button").ClickAsync();
            var csv = end.Nth(2).Locator("a[download]", new() { HasText = "Export CSV" });
            await csv.WaitForAsync();
            var download = await page.RunAndWaitForDownloadAsync(() => csv.ClickAsync());
            Assert.Equal("orders.csv", download.SuggestedFilename);

            // A toolbar with nothing in it takes no room.
            Assert.Equal("none", await page.EvaluateAsync<string>("""
                () => {
                  const empty = document.createElement('div');
                  empty.className = 'ib-grid-toolbar';
                  document.body.append(empty);
                  return getComputedStyle(empty).display;
                }
                """));
            await context.CloseAsync();
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
    public async Task Virtual_rows_draw_only_whats_in_view_and_the_keys_reach_them_all()
    {
        var (browser, page) = await OpenAsync("interactive");
        await using (browser)
        {
            var grid = page.Locator(".ib-datagrid", new() { HasText = "Every order" });
            var table = grid.Locator("table");
            var box = grid.Locator(".ib-table-scroll");
            await grid.ScrollIntoViewIfNeededAsync();
            await grid.Locator("tbody tr[aria-rowindex='2']").WaitForAsync();
            await page.WaitForFunctionAsync("() => [...document.querySelectorAll('table')].some(t => t.getAttribute('aria-rowcount') === '100001')");

            // A real table, a window of its rows, and no footer of pages.
            Assert.Equal("grid", await table.GetAttributeAsync("role"));
            Assert.True(await grid.Locator("tbody tr[aria-rowindex]").CountAsync() < 40);
            Assert.Equal(0, await grid.Locator(".ib-data-footer").CountAsync());

            // One height, so the scroll bar doesn't wander.
            var heights = await grid.Locator("tbody tr[aria-rowindex]").EvaluateAllAsync<double[]>("rows => rows.map(r => r.getBoundingClientRect().height)");
            Assert.All(heights, h => Assert.Equal(40, h, 0.5));

            // Keys, past what's drawn.
            await grid.Locator("tbody tr[aria-rowindex='2'] td").First.ClickAsync();
            await page.Keyboard.PressAsync("ArrowDown");
            await WaitForFocusAsync(page, "3");
            await page.Keyboard.PressAsync("Control+End");
            await WaitForFocusAsync(page, "100001");
            const string cellAt = "() => { const a = document.activeElement; const c = a.closest('td, th'); return [...c.parentElement.cells].indexOf(c) + '/' + c.parentElement.cells.length; }";
            Assert.Equal("3/4", await page.EvaluateAsync<string>(cellAt));
            await page.Keyboard.PressAsync("Control+Home");
            await WaitForFocusAsync(page, "2");
            Assert.Equal("0/4", await page.EvaluateAsync<string>(cellAt));
            await page.Keyboard.PressAsync("Control+End");
            await WaitForFocusAsync(page, "100001");
            await page.Keyboard.PressAsync("PageUp");
            await page.WaitForFunctionAsync($"() => +({Focused})() < 100001");
            await page.Keyboard.PressAsync("Control+Home");
            await WaitForFocusAsync(page, "2");
            await page.Keyboard.PressAsync("ArrowUp");
            await WaitForFocusAsync(page, "1");
            await page.Keyboard.PressAsync("ArrowDown");
            await WaitForFocusAsync(page, "2");

            // Held down, past the rows first drawn: focus is never lost, and the keys keep moving it on.
            for (var i = 0; i < 60; i++)
            {
                await page.Keyboard.PressAsync("ArrowDown");
                await page.WaitForTimeoutAsync(30);
                Assert.NotEqual("none", await page.EvaluateAsync<string>(Focused));
            }

            await page.WaitForFunctionAsync($"() => +({Focused})() > 30");

            // A row staying in view is kept as a scroll shifts the window, with whatever's in it.
            var kept = await page.EvaluateHandleAsync("() => document.activeElement.closest('tr')");
            await box.EvaluateAsync("b => b.scrollTop += 40");
            await page.WaitForTimeoutAsync(400);
            Assert.True(await kept.AsElement()!.EvaluateAsync<bool>("r => r.isConnected"));
            await page.Keyboard.PressAsync("Control+Home");
            await WaitForFocusAsync(page, "2");

            // A row on its way: focus waits on the box, not on the page, and goes to it when it comes.
            await page.Keyboard.PressAsync("Control+End");
            Assert.NotEqual("none", await page.EvaluateAsync<string>(Focused));
            await WaitForFocusAsync(page, "100001");
            await page.Keyboard.PressAsync("Control+Home");
            await WaitForFocusAsync(page, "2");

            // Keys on the way back to a row scrolled away stay with the grid, not the page behind it.
            var scrollY = await page.EvaluateAsync<double>("() => scrollY");
            await box.EvaluateAsync("b => b.scrollTop = 200000");
            await WaitForFocusAsync(page, "box");
            for (var i = 0; i < 12; i++)
            {
                await page.Keyboard.PressAsync("PageUp");
            }

            await WaitForFocusAsync(page, "2");
            Assert.Equal(scrollY, await page.EvaluateAsync<double>("() => scrollY"), 1);

            // Scrolled or left while it's on its way, it doesn't take focus when it comes.
            await page.Keyboard.PressAsync("Control+End");
            var search = page.GetByRole(AriaRole.Searchbox).First;
            await search.FocusAsync();
            await page.WaitForTimeoutAsync(500);
            Assert.Equal("INPUT", await page.EvaluateAsync<string>("() => document.activeElement.tagName"));
            await box.EvaluateAsync("b => b.scrollTop = 0");
            await grid.Locator("tbody tr[aria-rowindex='2']").WaitForAsync();
            await grid.Locator("tbody tr[aria-rowindex='2'] td").Nth(1).ClickAsync();

            // A focused row scrolled away: focus waits, and comes back with the next key.
            await box.EvaluateAsync("b => b.scrollTop = 200000");
            await WaitForFocusAsync(page, "box");
            await grid.Locator("tbody tr[aria-rowindex='5001']").WaitForAsync(new() { State = WaitForSelectorState.Attached });
            await page.Keyboard.PressAsync("ArrowDown");
            await WaitForFocusAsync(page, "2");
            Assert.True(await box.EvaluateAsync<double>("b => b.scrollTop") < 200);

            // Tab from there goes to the row it remembers, not the first one drawn.
            await box.EvaluateAsync("b => b.scrollTop = 200000");
            await WaitForFocusAsync(page, "box");
            await grid.Locator("tbody tr[aria-rowindex='5001']").WaitForAsync(new() { State = WaitForSelectorState.Attached });
            await page.Keyboard.PressAsync("Tab");
            await WaitForFocusAsync(page, "2");

            // Or when the row is back, with what's being scrolled left where it is.
            await box.EvaluateAsync("b => b.scrollTop = 200000");
            await WaitForFocusAsync(page, "box");
            await box.EvaluateAsync("b => b.scrollTop = 0");
            await WaitForFocusAsync(page, "2");

            // Selection is by key, so it outlasts the row's being drawn.
            var check = grid.Locator("tbody tr[aria-rowindex='3'] .ib-table-select");
            await check.CheckAsync();
            await box.EvaluateAsync("b => b.scrollTop = 200000");
            await grid.Locator("tbody tr[aria-rowindex='5001']").WaitForAsync(new() { State = WaitForSelectorState.Attached });
            Assert.Equal(0, await check.CountAsync());
            await box.EvaluateAsync("b => b.scrollTop = 0");
            await check.WaitForAsync();
            Assert.True(await check.IsCheckedAsync());

            // A new sort starts from the top.
            await box.EvaluateAsync("b => b.scrollTop = 200000");
            await grid.Locator("tbody tr[aria-rowindex='5001']").WaitForAsync(new() { State = WaitForSelectorState.Attached });
            await page.EvaluateAsync("() => document.querySelector('.ib-datagrid:has(table[aria-rowcount]) thead a, .ib-datagrid:has(table[aria-rowcount]) thead button[data-ib-primary]').click()");
            await page.WaitForFunctionAsync("() => document.querySelector('table[aria-rowcount]').closest('.ib-table-scroll').scrollTop === 0");
            await grid.Locator("tbody tr[aria-rowindex='2']").WaitForAsync();
        }
    }

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

    [Fact]
    public async Task What_doesnt_fit_stays_in_its_cell()
    {
        var (browser, page) = await OpenAsync("static");
        await using (browser)
        {
            // Date and Total, both sortable, far narrower than their text; Total also sorted second.
            await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/data-grid?mode=static&sort=customer,total").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
            var overflow = await page.EvaluateAsync<string[]>("""
                () => {
                  const table = document.querySelector(`.ib-datagrid:has(.ib-table-scroll[aria-label='Orders']) table`);
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
    [InlineData("interactive")]
    public async Task Frozen_columns_stay_while_the_rest_scroll_and_focus_stops_clear_of_them(string mode)
    {
        var (browser, page) = await OpenAsync(mode);
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

    [Fact]
    public async Task An_end_aligned_title_with_no_sort_lines_up_with_its_values()
    {
        var (browser, page) = await OpenAsync("static");
        await using (browser)
        {
            var grid = page.Locator(".ib-datagrid", new() { HasText = "Orders with menus" });
            var title = await grid.Locator("thead th", new() { HasText = "Total" }).Locator(".ib-table-title").EvaluateAsync<double>("t => t.getBoundingClientRect().right");
            var value = await grid.Locator("tbody tr").First.Locator("td").Nth(2).EvaluateAsync<double>("c => { const r = document.createRange(); r.selectNodeContents(c); return r.getBoundingClientRect().right; }");
            Assert.Equal(value, title, 1.5);
        }
    }

    [Fact]
    public async Task Frozen_columns_too_wide_for_a_phone_scroll_like_the_rest()
    {
        var (browser, page) = await OpenAsync("static", 390);
        await using (browser)
        {
            var at = await page.EvaluateAsync<FrozenBoxes>(Frozen);
            Assert.True(at.Unfrozen);

            // Widening looks again, and so does narrowing.
            await page.SetViewportSizeAsync(1400, 900);
            await page.WaitForFunctionAsync("() => !document.querySelector(`.ib-datagrid:has(.ib-table-scroll[aria-label='Orders by column']) table`).hasAttribute('data-ib-unfrozen')");
            await page.SetViewportSizeAsync(500, 900);
            await page.WaitForFunctionAsync("() => document.querySelector(`.ib-datagrid:has(.ib-table-scroll[aria-label='Orders by column']) table`).hasAttribute('data-ib-unfrozen')");
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
