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
        await page.Locator(".ib-datagrid >> nth=0 >> table[role=grid]").WaitForAsync();
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
    [InlineData("interactive")]
    public async Task Arrows_move_between_cells_and_the_grid_is_one_tab_stop(string mode)
    {
        var (browser, page) = await OpenAsync(mode);
        await using (browser)
        {
            var stops = await page.EvaluateAsync<int>("() => [...document.querySelector('.ib-datagrid .ib-table-scroll').querySelectorAll('*')].filter(e => e.tabIndex === 0 && !e.closest('[popover]') && e.checkVisibility()).length");
            Assert.Equal(1, stops);
            // Nor is the scroll box a stop of its own.
            Assert.Equal(-1, await page.Locator(".ib-datagrid >> nth=0 >> .ib-table-scroll").EvaluateAsync<int>("e => e.tabIndex"));

            // First row's checkbox, then text, and Up reaches the header's sort link.
            await page.Locator(".ib-datagrid >> nth=0 >> tbody tr td").First.Locator("input").FocusAsync();
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

    [Fact]
    public async Task A_header_with_a_sort_and_a_filter_is_entered_and_left()
    {
        var (browser, page) = await OpenAsync("static");
        await using (browser)
        {
            // Customer: a sort link and a filter button, so focus stays on the cell.
            await page.Locator(".ib-datagrid >> nth=0 >> tbody tr td").Nth(2).FocusAsync();
            await PressAsync(page, "ArrowUp");
            Assert.Equal("r0c2", await FocusAsync(page));

            await PressAsync(page, "F2");
            Assert.Equal("r0c2 a", await FocusAsync(page));
            await PressAsync(page, "Tab");
            Assert.Equal("r0c2 button", await FocusAsync(page));
            // Past the end comes back to the cell, so there's no trap.
            await PressAsync(page, "Tab");
            Assert.Equal("r0c2", await FocusAsync(page));
            await PressAsync(page, "F2", "Escape");
            Assert.Equal("r0c2", await FocusAsync(page));

            // Alt+Down opens its filter; Enter sorts.
            await PressAsync(page, "Alt+ArrowDown");
            await page.Locator(".ib-datagrid >> nth=0 >> th [popover]:popover-open").First.WaitForAsync();
            await PressAsync(page, "Escape");
            await page.Locator(".ib-datagrid >> nth=0 >> th[aria-sort] [popover]:popover-open").WaitForAsync(new() { State = WaitForSelectorState.Detached });
            await page.Locator(".ib-datagrid >> nth=0 >> tbody tr td").Nth(2).FocusAsync();
            await PressAsync(page, "ArrowUp", "Enter");
            await page.WaitForFunctionAsync("() => new URLSearchParams(location.search).get('sort') === 'customer'");
            await page.Locator(".ib-datagrid >> nth=0 >> th[aria-sort=ascending]", new() { HasText = "Customer" }).WaitForAsync();
            await PressAsync(page, "ArrowRight", "ArrowRight");
            // Status has a sort link only, so it's focused directly: Shift+Enter adds it.
            Assert.Equal("r0c4 a", await FocusAsync(page));
            await PressAsync(page, "Shift+Enter");
            await page.WaitForFunctionAsync("() => new URLSearchParams(location.search).get('sort') === 'customer,status'");
            Assert.Single(page.Context.Pages);
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task Selection_keys_use_the_rows_checkboxes(string mode)
    {
        var (browser, page) = await OpenAsync(mode);
        await using (browser)
        {
            var boxes = page.Locator(".ib-datagrid >> nth=0 >> tbody .ib-table-select");
            await page.Locator(".ib-datagrid >> nth=0 >> tbody tr").First.Locator("td").Nth(1).FocusAsync();

            // Arrows never select.
            await PressAsync(page, "ArrowDown", "ArrowUp");
            await Assertions.Expect(boxes.First).Not.ToBeCheckedAsync();

            await PressAsync(page, "Space");
            await Assertions.Expect(boxes.First).ToBeCheckedAsync();
            Assert.Equal("true", await page.Locator(".ib-datagrid >> nth=0 >> tbody tr").First.GetAttributeAsync("aria-selected"));
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
    public async Task PageDown_at_the_last_row_goes_to_the_next_page_in_the_same_column(string mode)
    {
        var (browser, page) = await OpenAsync(mode);
        await using (browser)
        {
            await page.Locator(".ib-datagrid >> nth=0 >> tbody tr").First.Locator("td").Nth(2).FocusAsync();
            await PressAsync(page, "PageDown");
            Assert.Equal("r5c2", await FocusAsync(page));
            var first = await page.Locator(".ib-datagrid >> nth=0 >> tbody tr").First.GetAttributeAsync("data-ib-key");

            await PressAsync(page, "PageDown");
            await page.WaitForFunctionAsync("() => new URLSearchParams(location.search).get('page') === '2'");
            await page.WaitForFunctionAsync("key => document.querySelector('.ib-datagrid tbody tr').dataset.ibKey !== key", first);
            await page.WaitForFunctionAsync("() => document.activeElement.closest('tr')?.rowIndex === 1");
            Assert.Equal("r1c2", await FocusAsync(page));

            await PressAsync(page, "PageUp");
            await page.WaitForFunctionAsync("() => document.activeElement.closest('tr')?.rowIndex === 5");
            Assert.Equal("r5c2", await FocusAsync(page));
        }
    }

    // In the fourth example: Note (a field), Actions (two buttons), More (a menu button).
    [Fact]
    public async Task Cells_with_controls_are_entered_and_left_with_one_tab_stop_after()
    {
        var (browser, page) = await OpenAsync("static");
        await using (browser)
        {
            var grid = page.Locator(".ib-datagrid").Nth(5);
            var stops = () => grid.EvaluateAsync<int>("g => [...g.querySelector('.ib-table-scroll').querySelectorAll('*')].filter(e => e.tabIndex === 0 && !e.closest('[popover]') && e.checkVisibility()).length");
            Task<bool> Focused(string selector) => grid.Locator(selector).First.EvaluateAsync<bool>("e => e === document.activeElement || e.contains(document.activeElement)");
            await grid.Locator("tbody tr").First.Locator("td").Nth(4).FocusAsync();
            Assert.Equal(1, await stops());

            // Several buttons: Enter goes in, Tab past the end comes back, and then Tab leaves.
            await PressAsync(page, "Enter");
            Assert.Equal("BUTTON", await page.EvaluateAsync<string>("() => document.activeElement.tagName"));
            await PressAsync(page, "Tab", "Tab");
            Assert.Equal("TD", await page.EvaluateAsync<string>("() => document.activeElement.tagName"));
            Assert.Equal(1, await stops());
            await PressAsync(page, "Enter", "Escape");
            Assert.Equal("TD", await page.EvaluateAsync<string>("() => document.activeElement.tagName"));
            Assert.Equal(1, await stops());
            await PressAsync(page, "Tab");
            Assert.False(await Focused("tbody"));
            await PressAsync(page, "Shift+Tab");

            // Arrows go on from a button inside a cell that doesn't use them.
            await PressAsync(page, "Enter", "ArrowDown");
            Assert.Equal("TD", await page.EvaluateAsync<string>("() => document.activeElement.tagName"));
            await PressAsync(page, "ArrowUp");

            // A select keeps its arrows while it's open, and Escape comes back.
            await PressAsync(page, "ArrowLeft", "Enter");
            Assert.Equal("SELECT", await page.EvaluateAsync<string>("() => document.activeElement.tagName"));
            // Open, the picker has the arrows, and Escape closes it with focus on the select.
            await PressAsync(page, "Alt+ArrowDown");
            await page.WaitForFunctionAsync("() => document.activeElement.closest('select')?.matches(':open')");
            var row = await page.EvaluateAsync<int>("() => document.activeElement.closest('tr').rowIndex");
            await PressAsync(page, "ArrowDown", "ArrowDown");
            Assert.Equal(row, await page.EvaluateAsync<int>("() => document.activeElement.closest('tr').rowIndex"));
            await PressAsync(page, "Escape");
            await page.WaitForFunctionAsync("() => document.activeElement.tagName === 'SELECT' && !document.activeElement.matches(':open')");
            await PressAsync(page, "Escape");
            Assert.Equal("TD", await page.EvaluateAsync<string>("() => document.activeElement.tagName"));

            // A field keeps its arrows, and Escape comes back.
            await PressAsync(page, "ArrowLeft", "Enter");
            Assert.Equal("INPUT", await page.EvaluateAsync<string>("() => document.activeElement.tagName"));
            await PressAsync(page, "ArrowDown", "End");
            Assert.Equal("INPUT", await page.EvaluateAsync<string>("() => document.activeElement.tagName"));
            await PressAsync(page, "Escape");
            Assert.Equal("TD", await page.EvaluateAsync<string>("() => document.activeElement.tagName"));

            // A menu button takes focus directly, Up and Down move on, and Enter opens it.
            await PressAsync(page, "End");
            Assert.Equal("BUTTON", await page.EvaluateAsync<string>("() => document.activeElement.tagName"));
            await PressAsync(page, "ArrowDown");
            Assert.Equal(2, await page.EvaluateAsync<int>("() => document.activeElement.closest('tr').rowIndex"));
            await PressAsync(page, "Enter");
            await grid.Locator("[popover]:popover-open").WaitForAsync();
            await PressAsync(page, "Escape");

            // Shown after it was hidden (a closed tab, a dialog): one stop.
            await grid.Locator("table").EvaluateAsync("t => { t.closest('ib-table').style.display = 'none'; }");
            await page.WaitForTimeoutAsync(100);
            await grid.Locator("table").EvaluateAsync("t => { t.closest('ib-table').style.display = ''; }");
            await page.WaitForTimeoutAsync(100);
            Assert.Equal(1, await stops());

            // The header stays above a focused row.
            Assert.NotEqual("auto", await grid.Locator(".ib-table-scroll").EvaluateAsync<string>("e => getComputedStyle(e).scrollPaddingBlockStart"));
        }
    }

    [Fact]
    public async Task Clear_selection_clears_aria_selected()
    {
        var (browser, page) = await OpenAsync("static");
        await using (browser)
        {
            var grid = page.Locator(".ib-datagrid").First;
            await grid.Locator("tbody tr").First.Locator("td").Nth(1).FocusAsync();
            await PressAsync(page, "Control+a");
            Assert.Equal("true", await grid.Locator("tbody tr").Nth(2).GetAttributeAsync("aria-selected"));
            await grid.Locator(".ib-selection-clear").ClickAsync();
            await page.WaitForFunctionAsync("() => [...document.querySelector('.ib-datagrid').querySelectorAll('tbody tr')].every(r => r.getAttribute('aria-selected') === 'false') || false");
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
            await grid.ScrollIntoViewIfNeededAsync();
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
    public async Task Alt_Down_on_a_blocked_menu_doesnt_run_whats_inside_it()
    {
        var (browser, page) = await OpenAsync("static");
        await using (browser)
        {
            // What a SplitButton draws with its arrow blocked, and a closed panel holding an item.
            var cell = page.Locator(".ib-datagrid >> nth=0 >> tbody tr >> nth=0 >> td >> nth=2");
            await cell.EvaluateAsync("""
                c => {
                  c.insertAdjacentHTML('beforeend', '<button type="button" popovertarget="blocked" disabled>More</button><div popover id="blocked"><button type="button" popovertarget="blocked" popovertargetaction="hide">Delete</button></div>');
                  window.ran = 0;
                  c.querySelector('[popovertargetaction]').addEventListener('click', () => window.ran++);
                }
                """);
            await cell.FocusAsync();
            await page.Keyboard.PressAsync("Alt+ArrowDown");
            await page.WaitForTimeoutAsync(100);
            Assert.Equal(0, await page.EvaluateAsync<int>("() => window.ran"));
        }
    }

    [Fact]
    public async Task The_page_lists_the_keys_by_where_focus_is()
    {
        var (browser, page) = await OpenAsync("static");
        await using (browser)
        {
            Assert.Equal(1, await page.GetByRole(AriaRole.Heading, new() { Name = "Keyboard", Exact = true }).CountAsync());
            var rows = page.GetByRole(AriaRole.Heading, new() { Name = "On a header" });
            Assert.Equal(1, await rows.CountAsync());
            Assert.Contains("Press this key", await page.Locator("table", new() { HasText = "Add the column to the sort" }).TextContentAsync());
            Assert.True(await page.Locator("table", new() { HasText = "Add the column to the sort" }).Locator(".ib-kbd").CountAsync() > 5);

            // Listed in "On this page" where they are on it.
            var outline = await page.EvaluateAsync<string[]>("() => [...document.querySelectorAll('ib-on-this-page a')].map(a => a.textContent.trim())");
            var at = Array.IndexOf(outline, "Keyboard");
            Assert.Equal("Accessibility", outline[at - 1]);
            Assert.Equal(["In the rows", "Inside a cell", "Selecting rows", "On a header", "In the footer", "Parameters"], outline[(at + 1)..(at + 7)]);
        }
    }

    [Fact]
    public async Task With_no_script_its_a_plain_table()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var context = await browser.NewContextAsync(new() { JavaScriptEnabled = false });
        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/data-grid?mode=static").ToString());
        Assert.Null(await page.Locator(".ib-datagrid >> nth=0 >> table").GetAttributeAsync("role"));
        Assert.Equal("0", await page.Locator(".ib-datagrid >> nth=0 >> .ib-table-scroll").GetAttributeAsync("tabindex"));
    }
}
