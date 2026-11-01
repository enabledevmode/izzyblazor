using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A DataGrid's FilterRow in Chromium: a field that filters, and with no script.</summary>
public sealed class FilterRowTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private async Task<(TestBrowser, IPage, ILocator)> OpenAsync(string mode, bool script = true)
    {
        var browser = await TestBrowser.OpenAsync();
        var context = await browser.NewContextAsync(new() { JavaScriptEnabled = script, Locale = "en-US", ViewportSize = new() { Width = 1400, Height = 900 } });
        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/data-grid?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        if (mode == "interactive") await page.Locator("ib-table[data-ib-interactive]", new() { HasText = "Orders by row filter" }).WaitForAsync();
        var grid = page.Locator("ib-table", new() { HasText = "Orders by row filter" });
        await grid.ScrollIntoViewSettledAsync();
        if (script) await page.WaitForFunctionAsync("() => !!customElements.get('ib-table')", null, new() { PollingInterval = 100 });
        return (browser, page, grid);
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task A_list_opened_from_the_row_stays_under_it_as_ticks_apply(string mode)
    {
        var (browser, page, grid) = await OpenAsync(mode);
        await using var _ = browser;
        var summary = grid.Locator(".ib-filter-cell-summary").First;
        var panel = page.Locator("[popover]:popover-open");
        await summary.ClickAsync();

        async Task UnderSummaryAsync()
        {
            var (under, list) = ((await summary.BoundingBoxAsync())!, (await panel.BoundingBoxAsync())!);
            Assert.Equal(under.X, list.X, 0);
            Assert.True(list.Y >= under.Y + under.Height);
        }

        await UnderSummaryAsync();
        await panel.GetByLabel("Paid", new() { Exact = true }).ClickAsync();
        await Assertions.Expect(summary).ToHaveTextAsync("3 selected");
        await UnderSummaryAsync();

        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(summary).ToBeFocusedAsync();
    }

    [Fact]
    public async Task A_static_operator_shows_before_the_response_arrives()
    {
        var (browser, page, grid) = await OpenAsync("static");
        await using var _ = browser;
        var release = new TaskCompletionSource();
        await page.RouteAsync("**/*row-f-customer-op=startswith*", async route =>
        {
            await release.Task;
            await route.ContinueAsync();
        });
        var button = grid.Locator(".ib-filter-cell-op[aria-label^='Customer']");
        var field = button.Locator("xpath=ancestor::*[contains(@class,'ib-filter-cell')][1]").Locator("input");

        await button.ClickAsync();
        var item = page.Locator("[popover]:popover-open a", new() { HasText = "Starts with" });
        var icon = await item.Locator("svg").InnerHTMLAsync();
        await item.ClickAsync();

        await Assertions.Expect(field).ToHaveAttributeAsync("placeholder", "Starts with");
        Assert.Equal(icon, await button.Locator("svg").InnerHTMLAsync());
        release.SetResult();
        await page.WaitForURLAsync("**/*row-f-customer-op=startswith*");
        await Assertions.Expect(field).ToHaveAttributeAsync("placeholder", "Starts with");
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task Typing_in_a_field_filters_and_the_panel_says_the_same(string mode)
    {
        var (browser, page, grid) = await OpenAsync(mode);
        await using var _ = browser;
        var field = grid.GetByRole(AriaRole.Searchbox, new() { Name = "Filter Customer" }).Locator("visible=true");

        await field.FillAsync("ada");

        await page.WaitForURLAsync("**/*row-f-customer=ada*");
        await Assertions.Expect(grid.Locator("tbody tr").First).ToContainTextAsync("Ada");
        await Assertions.Expect(field).ToHaveValueAsync("ada");
        await Assertions.Expect(grid.Locator("th .ib-table-filter-button[aria-label^='Filter']:visible")).ToHaveCountAsync(0);
        Assert.Equal("ada", await grid.Locator("[popover] input[name='row-f-customer']").InputValueAsync());
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task Choosing_between_opens_the_panel_on_its_first_value(string mode)
    {
        var (browser, page, grid) = await OpenAsync(mode);
        await using var _ = browser;

        await grid.Locator(".ib-table-filter-row [data-ib-menu][aria-label^='Total']").ClickAsync();
        await page.Locator("[popover]:popover-open").GetByRole(AriaRole.Menuitemradio, new() { Name = "Between" })
            .Or(page.Locator("[popover]:popover-open").GetByRole(AriaRole.Menuitemcheckbox, new() { Name = "Between" })).ClickAsync();

        var from = grid.Locator("[popover]:popover-open input[name='row-f-total']");
        await from.WaitForAsync();
        await Assertions.Expect(grid.Locator(".ib-table-filter-row .ib-filter-cell-summary[aria-label^='Filter Total']")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task With_no_script_enter_in_a_field_posts_it_with_the_rest_of_the_state()
    {
        var (browser, page, grid) = await OpenAsync("static", script: false);
        await using var _ = browser;
        await page.GotoAsync(page.Url + "&row-f-total-op=between&row-f-total=1&row-sort=customer");
        grid = page.Locator("ib-table", new() { HasText = "Orders by row filter" });

        await grid.Locator("input[data-ib-row][name='row-f-customer']").FillAsync("a");
        await page.Keyboard.PressAsync("Enter");

        await page.WaitForURLAsync("**/*row-f-customer=a*");
        Assert.Contains("row-f-total-op=between", page.Url, StringComparison.Ordinal);
        Assert.Contains("row-sort=customer", page.Url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_keys_reach_the_row_and_a_field_keeps_its_arrows()
    {
        var (browser, page, grid) = await OpenAsync("static");
        await using var _ = browser;
        var cell = grid.Locator(".ib-table-filter-row td").Nth(1);
        var field = cell.Locator("input[data-ib-row]");

        // From the Customer header, down onto the row's cell, and into its field past the operator.
        await grid.Locator("thead th").Nth(1).Locator("[data-ib-primary]").FocusAsync();
        await page.Keyboard.PressAsync("ArrowDown");
        await Assertions.Expect(cell).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(field).ToBeFocusedAsync();

        await page.Keyboard.TypeAsync("ab");
        await page.Keyboard.PressAsync("ArrowLeft");
        Assert.Equal(1, await field.EvaluateAsync<int>("f => f.selectionStart"));
        await Assertions.Expect(field).ToBeFocusedAsync();

        // Escape leaves the field and what's in it.
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(cell).ToBeFocusedAsync();
        Assert.Equal("ab", await field.InputValueAsync());

        await page.Keyboard.PressAsync("ArrowUp");
        // The header, or its title's link where that's all it holds.
        await Assertions.Expect(grid.Locator("thead th:nth-child(2):focus-within, thead th:nth-child(2):focus")).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task Between_takes_a_new_value_over_the_one_typed_in_the_row()
    {
        var (browser, page, grid) = await OpenAsync("static");
        await using var _ = browser;

        await grid.Locator(".ib-table-filter-row input[name='row-f-total']").FillAsync("50");
        await page.WaitForURLAsync("**/*row-f-total=50*");
        await grid.Locator(".ib-table-filter-row [data-ib-menu][aria-label^='Total']").ClickAsync();
        await page.Locator("[popover]:popover-open a", new() { HasText = "Between" }).ClickAsync();
        await page.WaitForURLAsync("**/*row-f-total-op=between*");

        await grid.Locator("[popover]:popover-open input[name='row-f-total']").FillAsync("100");

        await page.WaitForURLAsync("**/*row-f-total=100*");
    }
}


