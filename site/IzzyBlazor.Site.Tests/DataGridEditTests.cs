using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>Editing a DataGrid's row in a dialog, in Chromium, on the example.</summary>
public sealed class DataGridEditTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private const string Grid = ".ib-datagrid:has(.ib-table-scroll[aria-label='Orders you can edit'])";

    private async Task<(TestBrowser, IPage)> OpenAsync(string mode)
    {
        var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 } });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/data-grid/editing?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await TestBrowser.WaitForLiveAsync(page);
        return (browser, page);
    }

    private static async Task<ILocator> EditFirstRowAsync(IPage page)
    {
        await page.Locator(Grid).Locator(".ib-table-scroll tbody tr").First.Locator("a[aria-label^='Edit']").ClickAsync();
        var dialog = page.Locator($"{Grid} dialog[open]");
        await dialog.WaitForAsync();
        return dialog;
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task A_save_closes_the_dialog_and_shows_the_new_value(string mode)
    {
        var (browser, page) = await OpenAsync(mode);
        await using var _ = browser;
        var dialog = await EditFirstRowAsync(page);
        await dialog.GetByLabel("Customer").FillAsync("Saved Customer");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(page.Locator($"{Grid} dialog[open]")).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator(Grid).Locator(".ib-table-scroll tbody tr").First).ToContainTextAsync("Saved Customer");
        Assert.DoesNotContain("edits-edit", page.Url);
        await Assertions.Expect(page.Locator(Grid).Locator(".ib-table-scroll tbody tr").First.Locator("a[aria-label^='Edit']")).ToBeFocusedAsync();
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task Cancel_leaves_the_row_and_the_address_as_they_were(string mode)
    {
        var (browser, page) = await OpenAsync(mode);
        await using var _ = browser;
        var dialog = await EditFirstRowAsync(page);
        await dialog.GetByLabel("Customer").FillAsync("Not saved");
        await dialog.Locator("[data-ib-edit-cancel]").ClickAsync();
        await Assertions.Expect(page.Locator($"{Grid} dialog[open]")).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator(Grid).Locator(".ib-table-scroll tbody tr").First).Not.ToContainTextAsync("Not saved");
        await Assertions.Expect(page.Locator(Grid).Locator(".ib-table-scroll tbody tr").First.Locator("a[aria-label^='Edit']")).ToBeFocusedAsync();
        Assert.DoesNotContain("edits-edit", page.Url);
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task Add_makes_a_row_and_delete_asks_then_removes_it(string mode)
    {
        var (browser, page) = await OpenAsync(mode);
        await using var _ = browser;
        var rows = page.Locator(Grid).Locator(".ib-table-scroll tbody tr");
        await page.Locator(Grid).Locator(".ib-grid-toolbar [data-ib-edit]").ClickAsync();
        var dialog = page.Locator($"{Grid} dialog[open]");
        await dialog.GetByLabel("Customer").FillAsync("Brand New");
        await dialog.GetByLabel("Status").SelectOptionAsync("Pending");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(dialog).ToHaveCountAsync(0);
        await Assertions.Expect(rows.First).ToContainTextAsync("Brand New");

        await rows.First.Locator("[aria-label^='Delete']").ClickAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
        await Assertions.Expect(dialog).ToHaveCountAsync(0);
        await Assertions.Expect(rows.First).Not.ToContainTextAsync("Brand New");
    }

    // The button isn't drawn for a paid order, but anyone can type the address.
    [Fact]
    public async Task A_row_that_cant_be_deleted_cant_be_by_its_address()
    {
        var (browser, page) = await OpenAsync("static");
        await using var _ = browser;
        var paid = page.Locator(Grid).Locator(".ib-table-scroll tbody tr", new() { HasText = "Paid" }).First;
        Assert.Equal(0, await paid.Locator("[aria-label^='Delete']").CountAsync());
        var number = await paid.Locator("td").First.InnerTextAsync();

        await page.GotoAsync($"{page.Url}&edits-delete={number}", new() { WaitUntil = WaitUntilState.NetworkIdle });
        var dialog = page.Locator($"{Grid} dialog[open]");
        await Assertions.Expect(dialog).ToContainTextAsync("isn't available");
        Assert.Equal(0, await dialog.GetByRole(AriaRole.Button, new() { Name = "Delete" }).CountAsync());
    }

    [Fact]
    public async Task A_field_of_your_own_saves_with_the_generated_ones()
    {
        var (browser, page) = await OpenAsync("static");
        await using var _ = browser;
        var grid = page.Locator(".ib-datagrid:has(.ib-table-scroll[aria-label='Orders in two columns'])");
        var rows = grid.Locator(".ib-table-scroll tbody tr");
        await rows.First.Locator("[aria-label^='Edit']").ClickAsync();
        var dialog = grid.Locator("dialog[open]");
        await dialog.Locator("input[name='ib-edit.Customer']").FillAsync("Sectioned");
        await dialog.GetByRole(AriaRole.Radio, new() { Name = "Refunded" }).CheckAsync(new() { Force = true });
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(dialog).ToHaveCountAsync(0);
        await Assertions.Expect(rows.First).ToContainTextAsync("Sectioned");
        await Assertions.Expect(rows.First).ToContainTextAsync("Refunded");
    }

    // 99999999999 passes a number input's check, and doesn't fit an int: Blazor would save 0, with no error.
    [Fact]
    public async Task A_form_of_your_own_refuses_a_number_that_wont_fit_and_saves_one_that_will()
    {
        var (browser, page) = await OpenAsync("static");
        await using var _ = browser;
        var grid = page.Locator(".ib-datagrid:has(.ib-table-scroll[aria-label='Orders with your form'])");
        var rows = grid.Locator(".ib-table-scroll tbody tr");
        await rows.First.Locator("[aria-label^='Edit']").ClickAsync();
        var dialog = grid.Locator("dialog[open]");
        await dialog.GetByLabel("Quantity").FillAsync("99999999999");
        await dialog.Locator("input[name$='Quantity']").EvaluateAsync("i => i.form.noValidate = true");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(dialog.Locator(".ib-form-errors")).ToContainTextAsync("not valid");

        await dialog.GetByLabel("Quantity").FillAsync("7");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(dialog).ToHaveCountAsync(0);
        await Assertions.Expect(rows.First).ToContainTextAsync("7");
    }

    [Fact]
    public async Task A_form_of_your_own_closes_after_saving_on_an_interactive_page()
    {
        var (browser, page) = await OpenAsync("interactive");
        await using var _ = browser;
        var grid = page.Locator(".ib-datagrid:has(.ib-table-scroll[aria-label='Orders with your form'])");
        var rows = grid.Locator(".ib-table-scroll tbody tr");
        await rows.First.Locator("[aria-label^='Edit']").ClickAsync();
        var dialog = grid.Locator("dialog[open]");
        await dialog.GetByLabel("Quantity").FillAsync("7");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(dialog).ToHaveCountAsync(0);
        await Assertions.Expect(rows.First).ToContainTextAsync("7");
    }

    [Fact]
    public async Task An_error_from_the_save_stays_on_its_field()
    {
        var (browser, page) = await OpenAsync("static");
        await using var _ = browser;
        var dialog = await EditFirstRowAsync(page);
        await dialog.GetByLabel("Total").FillAsync("20000");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(page.Locator($"{Grid} dialog[open] .ib-field-error")).ToContainTextAsync("can't be over 10,000");
        await Assertions.Expect(page.Locator($"{Grid} dialog[open]").GetByLabel("Total")).ToHaveValueAsync("20000");
    }

    // A browser wouldn't send these on its own, so they're forced in: what's posted can't be trusted.
    [Fact]
    public async Task A_post_changes_only_valid_values_of_editable_columns()
    {
        var (browser, page) = await OpenAsync("static");
        await using var _ = browser;
        var number = await page.Locator(Grid).Locator(".ib-table-scroll tbody tr").First.Locator("td").First.InnerTextAsync();

        var dialog = await EditFirstRowAsync(page);
        await dialog.Locator("input[name='ib-edit.Total']").EvaluateAsync("i => { i.type = 'text'; i.value = 'abc'; i.form.noValidate = true; }");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(page.Locator($"{Grid} dialog[open] .ib-field-error")).ToContainTextAsync("not valid");

        dialog = page.Locator($"{Grid} dialog[open]");
        await dialog.Locator("input[name='ib-edit.Total']").EvaluateAsync("i => { i.type = 'number'; i.value = '5'; i.insertAdjacentHTML('afterend', '<input type=\"hidden\" name=\"ib-edit.Number\" value=\"999999\">'); }");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(page.Locator($"{Grid} dialog[open]")).ToHaveCountAsync(0);
        Assert.Equal(number, await page.Locator(Grid).Locator(".ib-table-scroll tbody tr").First.Locator("td").First.InnerTextAsync());
    }
}
