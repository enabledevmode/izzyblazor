using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>Editing a DataGrid's cells one at a time, in Chromium, on the example.</summary>
public sealed class DataGridEditCellTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private const string Grid = ".ib-datagrid:has(.ib-table-scroll[aria-label='Orders edited by the cell'])";

    private async Task<(TestBrowser, IPage)> OpenAsync()
    {
        var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 } });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/data-grid/editing?mode=interactive").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await page.Locator($"{Grid}[data-ib-interactive]").WaitForAsync();
        return (browser, page);
    }

    private static ILocator Row(IPage page, int number) => page.Locator($"{Grid} tr[data-ib-key='{number}']");

    private static ILocator Cell(IPage page, int number, string column) => Row(page, number).Locator($"td[data-ib-edit-col='{column}']");

    private static ILocator Open(IPage page) => page.Locator($"{Grid} td[data-ib-editing]");

    [Fact]
    public async Task A_click_starts_a_cell_and_Tab_saves_it_and_starts_the_next()
    {
        var (browser, page) = await OpenAsync();
        await using var _ = browser;
        await Cell(page, 1001, "customer").ClickAsync();
        var field = Open(page).GetByLabel("Customer");
        await Assertions.Expect(field).ToBeFocusedAsync();
        await page.Keyboard.TypeAsync("Saved by Tab");
        await page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(Cell(page, 1001, "customer")).ToContainTextAsync("Saved by Tab");
        await Assertions.Expect(Open(page).GetByLabel("Status")).ToBeFocusedAsync();
        await Assertions.Expect(Open(page)).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task Typing_starts_a_cell_adding_to_its_text_and_Enter_saves_it()
    {
        var (browser, page) = await OpenAsync();
        await using var _ = browser;
        var before = (await Cell(page, 1002, "customer").TextContentAsync())!.Trim();
        await Cell(page, 1002, "customer").FocusAsync();
        await page.Keyboard.PressAsync("X");
        await Assertions.Expect(Open(page).GetByLabel("Customer")).ToHaveValueAsync(before + "X");
        await page.Keyboard.TypeAsync("yz");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(Open(page)).ToHaveCountAsync(0);
        await Assertions.Expect(Cell(page, 1002, "customer")).ToHaveTextAsync(before + "Xyz");
        await Assertions.Expect(Cell(page, 1002, "customer")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task Escape_cancels_and_goes_back_to_the_cell()
    {
        var (browser, page) = await OpenAsync();
        await using var _ = browser;
        var before = await Cell(page, 1003, "customer").TextContentAsync();
        await Cell(page, 1003, "customer").FocusAsync();
        await page.Keyboard.PressAsync("F2");
        await Assertions.Expect(Open(page).GetByLabel("Customer")).ToBeFocusedAsync();
        await page.Keyboard.TypeAsync("Not saved");
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(Open(page)).ToHaveCountAsync(0);
        await Assertions.Expect(Cell(page, 1003, "customer")).ToHaveTextAsync(before!);
        await Assertions.Expect(Cell(page, 1003, "customer")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task An_invalid_cell_stays_open_with_its_error_and_a_click_elsewhere_saves_a_valid_one()
    {
        var (browser, page) = await OpenAsync();
        await using var _ = browser;
        await Cell(page, 1004, "total").ClickAsync();
        await Open(page).GetByLabel("Total").FillAsync("20000");
        await page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(Open(page)).ToContainTextAsync("An order can't be over 10,000.");
        await Assertions.Expect(Open(page).GetByLabel("Total")).ToBeFocusedAsync();

        await Open(page).GetByLabel("Total").FillAsync("123");
        await Cell(page, 1005, "customer").ClickAsync();
        await Assertions.Expect(Row(page, 1004)).ToContainTextAsync("$123.00");
        await Assertions.Expect(Open(page).GetByLabel("Customer")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task A_key_typed_to_start_a_cell_saves_with_nothing_more_typed()
    {
        var (browser, page) = await OpenAsync();
        await using var _ = browser;
        var before = (await Cell(page, 1003, "customer").TextContentAsync())!.Trim();
        await Cell(page, 1003, "customer").FocusAsync();
        await page.Keyboard.PressAsync("X");
        await Assertions.Expect(Open(page).GetByLabel("Customer")).ToHaveValueAsync(before + "X");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(Cell(page, 1003, "customer")).ToHaveTextAsync(before + "X");
    }

    [Fact]
    public async Task An_open_list_keeps_its_keys_and_a_dates_button_is_a_stop_on_the_way_out()
    {
        var (browser, page) = await OpenAsync();
        await using var _ = browser;
        await Cell(page, 1001, "status").ClickAsync();
        await Assertions.Expect(Open(page).GetByLabel("Status")).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Alt+ArrowDown");
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(Open(page)).ToHaveCountAsync(1);
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(Open(page)).ToHaveCountAsync(0);

        await Cell(page, 1001, "date").ClickAsync();
        await Assertions.Expect(Open(page).GetByLabel("Date", new() { Exact = true })).ToBeFocusedAsync();
        // Through the date's own parts, as many as the browser gives it, and then its button.
        var toggle = Open(page).Locator(".ib-date-toggle");
        for (var i = 0; i < 4 && !await toggle.EvaluateAsync<bool>("e => document.activeElement === e"); i++)
        {
            await page.Keyboard.PressAsync("Tab");
        }

        await Assertions.Expect(toggle).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(Open(page).GetByLabel("Total")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task A_cell_starts_with_the_cursor_after_its_text()
    {
        var (browser, page) = await OpenAsync();
        await using var _ = browser;
        await Cell(page, 1001, "customer").ClickAsync();
        var customer = Open(page).GetByLabel("Customer");
        await Assertions.Expect(customer).ToBeFocusedAsync();
        var before = await customer.InputValueAsync();
        await page.Keyboard.TypeAsync("!");
        await Assertions.Expect(customer).ToHaveValueAsync(before + "!");
        await page.Keyboard.PressAsync("Escape");

        await Cell(page, 1001, "total").ClickAsync();
        var total = Open(page).GetByLabel("Total");
        await Assertions.Expect(total).ToBeFocusedAsync();
        before = await total.InputValueAsync();
        await page.Keyboard.TypeAsync("1");
        await Assertions.Expect(total).ToHaveValueAsync(before + "1");
    }

    [Fact]
    public async Task A_row_highlights_only_when_asked_and_not_while_a_cell_in_it_is_edited()
    {
        var (browser, page) = await OpenAsync();
        await using var _ = browser;
        static Task<string> Layer(ILocator cell) => cell.EvaluateAsync<string>("e => getComputedStyle(e).backgroundImage");

        var overview = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 } });
        await overview.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/data-grid?mode=static").ToString());
        var asked = overview.Locator(".ib-datagrid:has(.ib-table-scroll[aria-label='Orders with stripes and lines']) .ib-table-scroll tbody tr").First;
        await asked.HoverAsync();
        Assert.NotEqual("none", await Layer(asked.Locator("td").First));
        await overview.CloseAsync();

        // This grid doesn't ask for it, so it's turned on here for the moment a cell is edited.
        await page.Locator(Grid).EvaluateAsync("e => e.classList.add('ib-table-row-hover')");
        await Row(page, 1002).HoverAsync();
        Assert.NotEqual("none", await Layer(Cell(page, 1002, "customer")));

        await Cell(page, 1002, "customer").ClickAsync();
        await Assertions.Expect(Open(page)).ToHaveCountAsync(1);
        await Row(page, 1002).Locator("td").First.HoverAsync();
        Assert.Equal("none", await Layer(Row(page, 1002).Locator("td").First));
        await page.Keyboard.PressAsync("Escape");

        // Not asked for.
        var other = page.Locator(".ib-datagrid:has(.ib-table-scroll[aria-label='Orders edited in the row']) .ib-table-scroll tbody tr").First;
        await other.HoverAsync();
        Assert.Equal("none", await Layer(other.Locator("td").First));
    }

    [Theory]
    [InlineData("customer")]
    [InlineData("status")]
    [InlineData("date")]
    [InlineData("total")]
    public async Task A_cell_being_edited_keeps_its_rows_height(string column)
    {
        var (browser, page) = await OpenAsync();
        await using var _ = browser;
        const string height = "e => e.getBoundingClientRect().height";
        var before = await Row(page, 1001).EvaluateAsync<double>(height);
        await Cell(page, 1001, column).ClickAsync();
        await Open(page).WaitForAsync();
        Assert.Equal(before, await Row(page, 1001).EvaluateAsync<double>(height));
    }

    [Fact]
    public async Task A_new_row_is_edited_as_a_row_and_saves()
    {
        var (browser, page) = await OpenAsync();
        await using var _ = browser;
        await page.Locator(Grid).GetByRole(AriaRole.Link, new() { Name = "Add" }).ClickAsync();
        var row = page.Locator($"{Grid} tr[data-ib-editing]");
        await row.GetByLabel("Customer").FillAsync("A new customer");
        await row.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(page.Locator($"{Grid} tr[data-ib-editing]")).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator(Grid).Locator(".ib-table-scroll tbody tr").First).ToContainTextAsync("A new customer");

        // And cells work again after it.
        await Cell(page, 1001, "customer").ClickAsync();
        await Assertions.Expect(Open(page).GetByLabel("Customer")).ToBeFocusedAsync();
    }

}
