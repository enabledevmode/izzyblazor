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
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/data-grid?mode=interactive").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
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
    public async Task Typing_starts_a_cell_with_what_was_typed_and_Enter_saves_it()
    {
        var (browser, page) = await OpenAsync();
        await using var _ = browser;
        await Cell(page, 1002, "customer").FocusAsync();
        await page.Keyboard.PressAsync("X");
        await Assertions.Expect(Open(page).GetByLabel("Customer")).ToHaveValueAsync("X");
        await page.Keyboard.TypeAsync("yz");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(Open(page)).ToHaveCountAsync(0);
        await Assertions.Expect(Cell(page, 1002, "customer")).ToHaveTextAsync("Xyz");
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

    [Fact]
    public async Task On_a_static_page_it_edits_the_row()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 } });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/data-grid?mode=static").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await page.Locator(Grid).Locator(".ib-table-scroll tbody tr").First.Locator("a[aria-label^='Edit']").ClickAsync();
        var row = page.Locator($"{Grid} tr[data-ib-editing]");
        await row.GetByLabel("Customer").FillAsync("Saved in the row");
        await row.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(page.Locator($"{Grid} tr[data-ib-editing]")).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator(Grid).Locator(".ib-table-scroll tbody tr").First).ToContainTextAsync("Saved in the row");
    }
}
