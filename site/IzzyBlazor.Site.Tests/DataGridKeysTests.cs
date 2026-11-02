using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>DataGrid's keyboard in Chromium: one Tab stop, arrows between cells, the way in and out of a cell, selection keys.</summary>
public sealed class DataGridKeysTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private async Task<(TestBrowser, IPage)> OpenAsync(string mode, string query = "")
    {
        var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 } });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/data-grid?mode={mode}{query}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        if (mode == "interactive") await page.Locator("ib-table[data-ib-interactive]").First.WaitForAsync();
        await page.Locator(".ib-datagrid:has(.ib-table-scroll[aria-label='Orders']) >> table[role=grid]").WaitForAsync();
        return (browser, page);
    }

    // What has focus: "r2c3" for a cell's row and column (the header row is 0), and what in it.
    private static Task<string> FocusAsync(IPage page) => page.EvaluateAsync<string>("""
        () => {
          const a = document.activeElement;
          const cell = a.closest('td, th');
          if (!cell || !cell.closest('.ib-datagrid')) return 'outside: ' + a.tagName;
          const row = cell.parentElement;
          const where = 'r' + [...cell.closest('table').rows].indexOf(row) + 'c' + cell.cellIndex;
          return where + (a === cell ? '' : ' ' + a.tagName.toLowerCase());
        }
        """);

    private static async Task PressAsync(IPage page, params string[] keys)
    {
        foreach (var key in keys) await page.Keyboard.PressAsync(key);
    }

    [Theory]
    [InlineData("static")]
    public async Task Arrows_move_between_cells_and_the_grid_is_one_tab_stop(string mode)
    {
        var (browser, page) = await OpenAsync(mode);
        await using (browser)
        {
            var stops = await page.EvaluateAsync<int>("() => [...document.querySelector(`.ib-datagrid:has(.ib-table-scroll[aria-label='Orders']) .ib-table-scroll`).querySelectorAll('*')].filter(e => e.tabIndex === 0 && !e.closest('[popover]') && e.checkVisibility()).length");
            Assert.Equal(1, stops);
            // Nor is the scroll box a stop of its own.
            Assert.Equal(-1, await page.Locator(".ib-datagrid:has(.ib-table-scroll[aria-label='Orders']) >> .ib-table-scroll").EvaluateAsync<int>("e => e.tabIndex"));

            // First row's checkbox, then text, and Up reaches the header's sort link.
            await page.Locator(".ib-datagrid:has(.ib-table-scroll[aria-label='Orders']) >> tbody tr td").First.Locator("input").FocusAsync();
            Assert.Equal("r1c0 input", await FocusAsync(page));
            await PressAsync(page, "ArrowRight");
            Assert.Equal("r1c1", await FocusAsync(page));
            await PressAsync(page, "ArrowDown");
            Assert.Equal("r2c1", await FocusAsync(page));
            await PressAsync(page, "End");
            Assert.Equal("r2c5", await FocusAsync(page));
            // Stops at the edge.
            await PressAsync(page, "ArrowRight");
            Assert.Equal("r2c5", await FocusAsync(page));
            await PressAsync(page, "Home");
            Assert.Equal("r2c0 input", await FocusAsync(page));
            await PressAsync(page, "Control+End");
            Assert.Equal("r5c5", await FocusAsync(page));
            await PressAsync(page, "Control+Home");
            Assert.Equal("r1c0 input", await FocusAsync(page));

            // Tab leaves, Shift+Tab comes back to the same cell.
            await PressAsync(page, "ArrowRight", "ArrowDown", "Tab");
            Assert.StartsWith("outside", await FocusAsync(page));
            await PressAsync(page, "Shift+Tab");
            Assert.Equal("r2c1", await FocusAsync(page));
        }
    }

    [Theory]
    [InlineData("static")]
    public async Task Selection_keys_use_the_rows_checkboxes(string mode)
    {
        var (browser, page) = await OpenAsync(mode);
        await using (browser)
        {
            var boxes = page.Locator(".ib-datagrid:has(.ib-table-scroll[aria-label='Orders']) >> tbody .ib-table-select");
            await page.Locator(".ib-datagrid:has(.ib-table-scroll[aria-label='Orders']) >> tbody tr").First.Locator("td").Nth(1).FocusAsync();

            // Arrows never select.
            await PressAsync(page, "ArrowDown", "ArrowUp");
            await Assertions.Expect(boxes.First).Not.ToBeCheckedAsync();

            await PressAsync(page, "Space");
            await Assertions.Expect(boxes.First).ToBeCheckedAsync();
            Assert.Equal("true", await page.Locator(".ib-datagrid:has(.ib-table-scroll[aria-label='Orders']) >> tbody tr").First.GetAttributeAsync("aria-selected"));
            await PressAsync(page, "Space");
            await Assertions.Expect(boxes.First).Not.ToBeCheckedAsync();

            await PressAsync(page, "Shift+ArrowDown", "Shift+ArrowDown");
            await Assertions.Expect(boxes.Nth(2)).ToBeCheckedAsync();
            Assert.Equal("r3c1", await FocusAsync(page));
            await PressAsync(page, "Shift+ArrowUp");
            await Assertions.Expect(boxes.Nth(2)).Not.ToBeCheckedAsync();
            await Assertions.Expect(boxes.Nth(1)).ToBeCheckedAsync();

            await PressAsync(page, "Control+a");
            for (var i = 0; i < await boxes.CountAsync(); i++) await Assertions.Expect(boxes.Nth(i)).ToBeCheckedAsync();
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task A_rows_menu_opens_by_button_right_click_and_keys_and_links_keep_the_browsers(string mode)
    {
        var (browser, page) = await OpenAsync(mode);
        await using (browser)
        {
            var grid = page.Locator(".ib-datagrid", new() { HasText = "Orders with menus" });
            await grid.ScrollIntoViewSettledAsync();
            var row = grid.Locator("tbody tr").Nth(1);
            var open = grid.Locator("[popover]:popover-open");

            // Right-click at the pointer: the row's own menu, there.
            var cell = await row.Locator("td").Nth(1).BoundingBoxAsync();
            await page.Mouse.ClickAsync(cell!.X + 10, cell.Y + 10, new() { Button = MouseButton.Right });
            await open.WaitForAsync();
            Assert.Contains(await row.Locator("td").First.TextContentAsync() ?? "x", await open.TextContentAsync());
            var box = await open.BoundingBoxAsync();
            Assert.Equal(cell.X + 10, box!.X, 2);
            Assert.Equal(cell.Y + 10, box.Y, 2);
            await page.Keyboard.PressAsync("Escape");
            await open.WaitForAsync(new() { State = WaitForSelectorState.Detached });

            // From the keys, on a cell of text.
            await row.Locator("td").Nth(1).FocusAsync();
            await page.Keyboard.PressAsync("Shift+F10");
            await open.WaitForAsync();
            await page.Keyboard.PressAsync("Escape");
            await open.WaitForAsync(new() { State = WaitForSelectorState.Detached });

            // A long press opens it as the finger lifts, and only that click is swallowed.
            await row.Locator("td").Nth(1).EvaluateAsync("""
                c => new Promise(done => {
                  const r = c.getBoundingClientRect();
                  const init = { bubbles: true, pointerType: 'touch', isPrimary: true, clientX: r.left + 5, clientY: r.top + 5 };
                  c.dispatchEvent(new PointerEvent('pointerdown', init));
                  setTimeout(() => { c.dispatchEvent(new PointerEvent('pointerup', init)); done(); }, 600);
                })
                """);
            await open.WaitForAsync();
            await page.WaitForTimeoutAsync(400);
            Assert.True(await open.IsVisibleAsync());
            await page.Keyboard.PressAsync("Escape");
            await open.WaitForAsync(new() { State = WaitForSelectorState.Detached });

            // A short press doesn't.
            await row.Locator("td").Nth(1).EvaluateAsync("""
                c => new Promise(done => {
                  const init = { bubbles: true, pointerType: 'touch', isPrimary: true };
                  c.dispatchEvent(new PointerEvent('pointerdown', init));
                  setTimeout(() => { c.dispatchEvent(new PointerEvent('pointerup', init)); done(); }, 100);
                })
                """);
            await page.WaitForTimeoutAsync(100);
            Assert.False(await open.IsVisibleAsync());

            // Scrolled away, a menu at the pointer closes rather than sit over another row.
            await page.Mouse.ClickAsync(cell.X + 10, cell.Y + 10, new() { Button = MouseButton.Right });
            await open.WaitForAsync();
            // Something else on the page scrolling isn't the row moving.
            await page.EvaluateAsync("""
                () => new Promise(done => {
                  const other = document.createElement('div');
                  other.style.cssText = 'position:fixed;top:0;left:0;width:50px;height:50px;overflow:auto';
                  other.innerHTML = '<div style="height:500px"></div>';
                  document.body.append(other);
                  other.addEventListener('scroll', () => { other.remove(); done(); }, { once: true });
                  other.scrollTop = 100;
                })
                """);
            await page.WaitForTimeoutAsync(100);
            Assert.True(await open.IsVisibleAsync());
            await page.EvaluateAsync("() => window.scrollBy(0, 40)");
            await open.WaitForAsync(new() { State = WaitForSelectorState.Detached });

            // The button, which sits where it was after a menu at the pointer has closed.
            await row.Locator("[data-ib-row-menu]").ClickAsync();
            await open.WaitForAsync();
            var button = await row.Locator("[data-ib-row-menu]").BoundingBoxAsync();
            box = await open.BoundingBoxAsync();
            Assert.True(box!.Y >= button!.Y + button.Height - 2, "under its button");
            await page.Keyboard.PressAsync("Escape");
        }
    }

    [Fact]
    public async Task With_no_script_its_a_plain_table()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var context = await browser.NewContextAsync(new() { JavaScriptEnabled = false });
        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/data-grid?mode=static").ToString());
        Assert.Null(await page.Locator(".ib-datagrid:has(.ib-table-scroll[aria-label='Orders']) >> table").GetAttributeAsync("role"));
        Assert.Equal("0", await page.Locator(".ib-datagrid:has(.ib-table-scroll[aria-label='Orders']) >> .ib-table-scroll").GetAttributeAsync("tabindex"));
    }
}
