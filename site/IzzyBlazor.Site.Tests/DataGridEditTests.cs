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
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/data-grid?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        return (browser, page);
    }

    private static async Task<ILocator> EditFirstRowAsync(IPage page)
    {
        await page.Locator(Grid).Locator("tbody tr").First.Locator("a[aria-label^='Edit']").ClickAsync();
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
        await Assertions.Expect(page.Locator(Grid).Locator("tbody tr").First).ToContainTextAsync("Saved Customer");
        Assert.DoesNotContain("edits-edit", page.Url);
        await Assertions.Expect(page.Locator(Grid).Locator("tbody tr").First.Locator("a[aria-label^='Edit']")).ToBeFocusedAsync();
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
        await Assertions.Expect(page.Locator(Grid).Locator("tbody tr").First).Not.ToContainTextAsync("Not saved");
        await Assertions.Expect(page.Locator(Grid).Locator("tbody tr").First.Locator("a[aria-label^='Edit']")).ToBeFocusedAsync();
        Assert.DoesNotContain("edits-edit", page.Url);
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
        var number = await page.Locator(Grid).Locator("tbody tr").First.Locator("td").First.InnerTextAsync();

        var dialog = await EditFirstRowAsync(page);
        await dialog.Locator("input[name='ib-edit.Total']").EvaluateAsync("i => { i.type = 'text'; i.value = 'abc'; i.form.noValidate = true; }");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(page.Locator($"{Grid} dialog[open] .ib-field-error")).ToContainTextAsync("not valid");

        dialog = page.Locator($"{Grid} dialog[open]");
        await dialog.Locator("input[name='ib-edit.Total']").EvaluateAsync("i => { i.type = 'number'; i.value = '5'; i.insertAdjacentHTML('afterend', '<input type=\"hidden\" name=\"ib-edit.Number\" value=\"999999\">'); }");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(page.Locator($"{Grid} dialog[open]")).ToHaveCountAsync(0);
        Assert.Equal(number, await page.Locator(Grid).Locator("tbody tr").First.Locator("td").First.InnerTextAsync());
    }
}
