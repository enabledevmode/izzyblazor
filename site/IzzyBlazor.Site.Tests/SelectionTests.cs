using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A view's selection in Chromium: the count in the footer, the bar of actions, and what select-all reaches.</summary>
public sealed class SelectionTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private async Task<(TestBrowser, IPage, ILocator)> OpenAsync(string mode, string caption, bool script = true)
    {
        var browser = await TestBrowser.OpenAsync();
        var context = await browser.NewContextAsync(new() { JavaScriptEnabled = script, Locale = "en-US", ViewportSize = new() { Width = 1400, Height = 900 } });
        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/table?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var table = page.Locator("ib-table", new() { HasText = caption });
        await table.ScrollIntoViewSettledAsync();
        if (mode == "interactive") await page.Locator("ib-table[data-ib-interactive]", new() { HasText = caption }).WaitForAsync();
        if (script) await page.Locator("ib-table[data-ready]", new() { HasText = caption }).WaitForAsync();
        return (browser, page, table);
    }

    private static Task<double> TopAsync(ILocator table) => table.Locator("thead").EvaluateAsync<double>("h => h.getBoundingClientRect().top");

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task Selecting_moves_nothing_and_the_footer_counts(string mode)
    {
        var (browser, page, table) = await OpenAsync(mode, "Orders to archive");
        await using (browser)
        {
            var top = await TopAsync(table);
            var archive = table.GetByRole(AriaRole.Button, new() { Name = "Archive" });
            await Assertions.Expect(archive).ToBeDisabledAsync();
            await Assertions.Expect(table.Locator(".ib-selection-summary")).ToBeHiddenAsync();

            await table.Locator("tbody .ib-table-select").First.CheckAsync();

            await Assertions.Expect(table.Locator(".ib-data-footer .ib-selection-count")).ToHaveTextAsync("1 selected");
            await Assertions.Expect(archive).ToBeEnabledAsync();
            Assert.Equal(top, await TopAsync(table));

            await table.Locator(".ib-selection-clear").ClickAsync();
            await Assertions.Expect(table.Locator(".ib-selection-summary")).ToBeHiddenAsync();
            await Assertions.Expect(archive).ToBeDisabledAsync();
            await Assertions.Expect(table.Locator("tbody .ib-table-select").First).ToBeFocusedAsync();
            Assert.Equal(top, await TopAsync(table));
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task SelectAllMode_All_reaches_every_match_on_any_page(string mode)
    {
        var (browser, page, table) = await OpenAsync(mode, "Orders to archive");
        await using (browser)
        {
            var header = table.Locator(".ib-table-select-all");
            await header.CheckAsync();
            await Assertions.Expect(table.Locator(".ib-selection-count")).ToHaveTextAsync(new System.Text.RegularExpressions.Regex(@"^All \d+ selected$"));
            await Assertions.Expect(table.Locator("tbody .ib-table-select").First).ToBeCheckedAsync();

            // The next page's rows come selected.
            await table.Locator(".ib-pagination a, .ib-pagination button").Filter(new() { HasText = "2" }).First.ClickAsync();
            await Assertions.Expect(table.Locator("tbody .ib-table-select").First).ToBeCheckedAsync();

            // A row left out shows on the header, and clicking it there selects them all again.
            await table.Locator("tbody .ib-table-select").First.UncheckAsync();
            await Assertions.Expect(table.Locator(".ib-selection-count")).ToHaveTextAsync(new System.Text.RegularExpressions.Regex(@"^\d+ selected$"));
            Assert.True(await header.EvaluateAsync<bool>("b => b.indeterminate"));

            await header.ClickAsync();
            await header.ClickAsync();
            await Assertions.Expect(table.Locator(".ib-selection-summary")).ToBeHiddenAsync();
            await Assertions.Expect(table.Locator("tbody .ib-table-select").First).Not.ToBeCheckedAsync();
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task In_All_mode_a_page_ticked_by_hand_isnt_all_of_them(string mode)
    {
        var (browser, page, table) = await OpenAsync(mode, "Orders to archive");
        await using (browser)
        {
            var boxes = table.Locator("tbody .ib-table-select");
            for (var i = 0; i < await boxes.CountAsync(); i++)
            {
                await boxes.Nth(i).CheckAsync();
            }

            var header = table.Locator(".ib-table-select-all");
            await Assertions.Expect(table.Locator(".ib-selection-count")).ToHaveTextAsync("5 selected");
            await Assertions.Expect(header).Not.ToBeCheckedAsync();
            Assert.True(await header.EvaluateAsync<bool>("b => b.indeterminate"));

            // One click takes them all, where it would have dropped what was ticked.
            await header.ClickAsync();
            await Assertions.Expect(table.Locator(".ib-selection-count")).ToHaveTextAsync(new System.Text.RegularExpressions.Regex(@"^All \d+ selected$"));
            await Assertions.Expect(header).ToBeCheckedAsync();
        }
    }

    [Fact]
    public async Task An_action_that_empties_the_selection_leaves_focus_on_a_box()
    {
        var (browser, page, table) = await OpenAsync("interactive", "Orders to archive");
        await using (browser)
        {
            await table.Locator("tbody .ib-table-select").First.CheckAsync();
            var archive = table.GetByRole(AriaRole.Button, new() { Name = "Archive" });
            await Assertions.Expect(archive).ToBeEnabledAsync();
            await archive.FocusAsync();
            await page.Keyboard.PressAsync("Enter");

            await Assertions.Expect(archive).ToBeDisabledAsync();
            await Assertions.Expect(table.Locator("tbody .ib-table-select").First).ToBeFocusedAsync();
        }
    }

    [Fact]
    public async Task A_tick_made_as_the_next_page_arrives_stays_with_its_own_row()
    {
        var (browser, page, table) = await OpenAsync("interactive", "Orders to archive");
        await using (browser)
        {
            // Every message to the server takes its time, so the tick goes before the rows come.
            await page.Context.RouteWebSocketAsync("**/_blazor**", ws =>
            {
                var server = ws.ConnectToServer();
                ws.OnMessage(async message =>
                {
                    await Task.Delay(400);
                    if (message.Binary is { } binary) server.Send(binary);
                    else server.Send(message.Text!);
                });
            });
            await page.ReloadAsync(new() { WaitUntil = WaitUntilState.NetworkIdle });
            await page.Locator("ib-table[data-ib-interactive]", new() { HasText = "Orders to archive" }).WaitForAsync();
            await page.WaitForTimeoutAsync(1500);
            await table.ScrollIntoViewSettledAsync();

            await table.Locator(".ib-pagination a, .ib-pagination button").Filter(new() { HasText = "2" }).First.ClickAsync();
            await table.Locator("tbody .ib-table-select").Nth(1).CheckAsync();
            await page.WaitForTimeoutAsync(1500);

            // One selected, on the page it was ticked on: nothing here is ticked but what the count says.
            await Assertions.Expect(table.Locator(".ib-selection-count")).ToHaveTextAsync("1 selected");
            Assert.Equal(0, await table.Locator("tbody .ib-table-select:checked").CountAsync());
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task Clearing_with_no_rows_keeps_focus_nearby(string mode)
    {
        var (browser, page, table) = await OpenAsync(mode, "Orders to archive");
        await using (browser)
        {
            await table.Locator("tbody .ib-table-select").First.CheckAsync();
            await table.Locator("th .ib-table-filter-button[aria-label^='Filter Customer']").ClickAsync();
            await page.Locator("[popover]:popover-open input[type=search]").FillAsync("zzzzz");
            await page.Keyboard.PressAsync("Enter");
            await Assertions.Expect(table.Locator("tbody .ib-table-select")).ToHaveCountAsync(0);

            await table.Locator(".ib-selection-clear").FocusAsync();
            await page.Keyboard.PressAsync("Enter");

            await Assertions.Expect(table.Locator(".ib-selection-count")).ToBeHiddenAsync();
            Assert.NotEqual("BODY", await page.EvaluateAsync<string>("() => document.activeElement.tagName"));
            Assert.True(await page.EvaluateAsync<bool>("() => !!document.activeElement.closest('ib-table')"));
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task A_first_tick_doesnt_change_the_footers_height_on_a_phone(string mode)
    {
        var (browser, page, table) = await OpenAsync(mode, "Orders to archive");
        await using (browser)
        {
            await page.SetViewportSizeAsync(390, 800);
            await table.ScrollIntoViewSettledAsync();
            var footer = table.Locator(":scope > .ib-data-footer");
            var before = await footer.EvaluateAsync<double>("f => f.getBoundingClientRect().height");
            await table.Locator("tbody .ib-table-select").First.CheckAsync();
            await Assertions.Expect(table.Locator(".ib-selection-count")).ToHaveTextAsync("1 selected");

            Assert.Equal(before, await footer.EvaluateAsync<double>("f => f.getBoundingClientRect().height"));
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task On_a_grid_with_a_toolbar_the_bar_takes_its_place_and_ctrl_a_takes_every_match(string mode)
    {
        var browser = await TestBrowser.OpenAsync();
        await using (browser)
        {
            var context = await browser.NewContextAsync(new() { Locale = "en-US", ViewportSize = new() { Width = 1400, Height = 900 } });
            var page = await context.NewPageAsync();
            await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/data-grid?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
            var grid = page.Locator(".ib-datagrid[data-ready]", new() { HasText = "New order" });
            await grid.ScrollIntoViewSettledAsync();
            if (mode == "interactive") await page.Locator("ib-table[data-ib-interactive]", new() { HasText = "New order" }).WaitForAsync();
            var head = grid.Locator("thead");
            var top = await head.EvaluateAsync<double>("h => h.getBoundingClientRect().top");
            await Assertions.Expect(grid.Locator(".ib-grid-toolbar")).ToBeVisibleAsync();

            await grid.Locator("tbody td").First.ClickAsync();
            await page.Keyboard.PressAsync("Control+a");

            await Assertions.Expect(grid.Locator(".ib-selection-count")).ToHaveTextAsync(new System.Text.RegularExpressions.Regex(@"^All \d+ selected$"));
            await Assertions.Expect(grid.GetByRole(AriaRole.Button, new() { Name = "Archive" })).ToBeEnabledAsync();
            await Assertions.Expect(grid.Locator(".ib-grid-toolbar")).ToBeHiddenAsync();
            Assert.Equal(top, await head.EvaluateAsync<double>("h => h.getBoundingClientRect().top"), 1);
        }
    }

    [Fact]
    public async Task SelectAllMode_All_posts_everything_for_Apply_to_find()
    {
        var (browser, page, table) = await OpenAsync("static", "Orders to archive");
        await using (browser)
        {
            await table.Locator(".ib-table-select-all").CheckAsync();
            await table.GetByRole(AriaRole.Button, new() { Name = "Archive" }).ClickAsync();

            var status = page.Locator("[role=status]", new() { HasText = "Archived" });
            await Assertions.Expect(status).ToBeVisibleAsync();
            Assert.DoesNotContain("Archived 0", await status.TextContentAsync(), StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task With_no_actions_there_is_no_bar_and_the_header_selects_the_page(string mode)
    {
        var (browser, page, table) = await OpenAsync(mode, "Recent orders");
        await using (browser)
        {
            var top = await TopAsync(table);
            await table.Locator(".ib-table-select-all").CheckAsync();

            await Assertions.Expect(table.Locator(".ib-selection-count")).ToHaveTextAsync(new System.Text.RegularExpressions.Regex(@"^\d+ selected$"));
            await Assertions.Expect(table.Locator(".ib-selection-bar")).ToBeHiddenAsync();
            Assert.Equal(top, await TopAsync(table));
        }
    }

    [Fact]
    public async Task With_no_script_the_actions_are_there_and_post_what_is_ticked()
    {
        var (browser, page, table) = await OpenAsync("static", "Orders to archive", script: false);
        await using (browser)
        {
            var archive = table.GetByRole(AriaRole.Button, new() { Name = "Archive" });
            await Assertions.Expect(archive).ToBeEnabledAsync();
            Assert.False(await table.Locator(".ib-table-select-all").IsVisibleAsync());

            await table.Locator("tbody .ib-table-select").First.CheckAsync();
            await archive.ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Assertions.Expect(page.Locator("[role=status]", new() { HasText = "Archived" })).ToBeVisibleAsync();
        }
    }
}
