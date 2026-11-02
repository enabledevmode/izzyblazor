using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>Editing a DataGrid's row where it stands, in Chromium, on the example.</summary>
public sealed class DataGridEditRowTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private const string Grid = ".ib-datagrid:has(.ib-table-scroll[aria-label='Orders edited in the row'])";

    private async Task<(TestBrowser, IPage)> OpenAsync(string mode)
    {
        var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 } });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/data-grid?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        return (browser, page);
    }

    private static ILocator FirstRow(IPage page) => page.Locator(Grid).Locator(".ib-table-scroll tbody tr").First;

    private static async Task<ILocator> EditFirstRowAsync(IPage page)
    {
        await FirstRow(page).Locator("a[aria-label^='Edit']").ClickAsync();
        var row = page.Locator($"{Grid} tr[data-ib-editing]");
        await row.WaitForAsync();
        return row;
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task A_save_ends_the_edit_and_shows_the_new_value(string mode)
    {
        var (browser, page) = await OpenAsync(mode);
        await using var _ = browser;
        var row = await EditFirstRowAsync(page);
        var customer = row.GetByLabel("Customer");
        await Assertions.Expect(customer).ToBeFocusedAsync();
        await customer.FillAsync("Saved In Row");
        await row.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(page.Locator($"{Grid} tr[data-ib-editing]")).ToHaveCountAsync(0);
        await Assertions.Expect(FirstRow(page)).ToContainTextAsync("Saved In Row");
        Assert.DoesNotContain("row-edit", page.Url);
        await Assertions.Expect(FirstRow(page).Locator("a[aria-label^='Edit']")).ToBeFocusedAsync();
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task Enter_saves_and_Escape_cancels(string mode)
    {
        var (browser, page) = await OpenAsync(mode);
        await using var _ = browser;
        var row = await EditFirstRowAsync(page);
        await row.GetByLabel("Customer").FillAsync("Not saved");
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(page.Locator($"{Grid} tr[data-ib-editing]")).ToHaveCountAsync(0);
        await Assertions.Expect(FirstRow(page)).Not.ToContainTextAsync("Not saved");
        Assert.DoesNotContain("row-edit", page.Url);
        await Assertions.Expect(FirstRow(page).Locator("a[aria-label^='Edit']")).ToBeFocusedAsync();

        row = await EditFirstRowAsync(page);
        await row.GetByLabel("Customer").FillAsync("Saved by Enter");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(FirstRow(page)).ToContainTextAsync("Saved by Enter");
        await Assertions.Expect(page.Locator($"{Grid} tr[data-ib-editing]")).ToHaveCountAsync(0);

        await FirstRow(page).Locator("td").First.ClickAsync();
        await page.Keyboard.PressAsync("F2");
        await Assertions.Expect(page.Locator($"{Grid} tr[data-ib-editing]").GetByLabel("Customer")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task It_saves_with_no_script()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var context = await browser.NewContextAsync(new() { JavaScriptEnabled = false, Locale = "en-US", ViewportSize = new() { Width = 1400, Height = 900 } });
        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/data-grid?mode=static").ToString());
        var row = await EditFirstRowAsync(page);
        await row.GetByLabel("Customer").FillAsync("Saved with no script");
        await row.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(FirstRow(page)).ToContainTextAsync("Saved with no script");
        Assert.DoesNotContain("row-edit", page.Url);

        row = await EditFirstRowAsync(page);
        await row.GetByRole(AriaRole.Link, new() { Name = "Cancel" }).ClickAsync();
        await Assertions.Expect(page.Locator($"{Grid} tr[data-ib-editing]")).ToHaveCountAsync(0);
        Assert.DoesNotContain("row-edit", page.Url);
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task A_failed_save_stays_open_with_the_error_on_its_field(string mode)
    {
        var (browser, page) = await OpenAsync(mode);
        await using var _ = browser;
        var row = await EditFirstRowAsync(page);
        await row.GetByLabel("Customer").FillAsync("Kept when it fails");
        await row.GetByLabel("Total").FillAsync("20000");
        await row.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(row).ToContainTextAsync("An order can't be over 10,000.");
        await Assertions.Expect(row.GetByLabel("Customer")).ToHaveValueAsync("Kept when it fails");
        await Assertions.Expect(row.GetByLabel("Total")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task A_new_row_starts_at_the_top_and_saves()
    {
        var (browser, page) = await OpenAsync("static");
        await using var _ = browser;
        await page.Locator(Grid).GetByRole(AriaRole.Link, new() { Name = "Add" }).ClickAsync();
        var row = page.Locator($"{Grid} tr[data-ib-editing]");
        await row.WaitForAsync();
        await Assertions.Expect(FirstRow(page).GetByLabel("Customer")).ToBeFocusedAsync();
        await row.GetByLabel("Customer").FillAsync("A new customer");
        await row.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(FirstRow(page)).ToContainTextAsync("A new customer");
        await Assertions.Expect(page.Locator($"{Grid} tr[data-ib-editing]")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task A_picker_in_a_row_keeps_its_own_look()
    {
        var (browser, page) = await OpenAsync("static");
        await using var _ = browser;
        var row = await EditFirstRowAsync(page);
        await row.Locator(".ib-date-toggle").ClickAsync();
        var day = row.Locator(".ib-calendar-grid tbody td").First;
        await day.WaitForAsync();
        Assert.Equal("0px", await day.EvaluateAsync<string>("e => getComputedStyle(e).borderBottomWidth"));
    }

    [Fact]
    public async Task A_sort_link_ends_the_edit()
    {
        var (browser, page) = await OpenAsync("static");
        await using var _ = browser;
        await EditFirstRowAsync(page);
        Assert.DoesNotContain("row-edit", await page.Locator(Grid).GetByRole(AriaRole.Link, new() { Name = "Customer", Exact = true }).GetAttributeAsync("href"));
        await Assertions.Expect(page.Locator($"{Grid} .ib-pagination a[href*='row-edit']")).ToHaveCountAsync(0);
    }

}
