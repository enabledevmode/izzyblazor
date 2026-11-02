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
        var name = await customer.InputValueAsync();
        await page.Keyboard.TypeAsync("!");
        await Assertions.Expect(customer).ToHaveValueAsync(name + "!");
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

    private const string HoldTheCircuit = """
            (() => {
                const send = WebSocket.prototype.send;
                window.__held = [];
                let count = 0, open = false;
                WebSocket.prototype.send = function (data) {
                    if (!open && ++count > 1) { window.__held.push(() => send.call(this, data)); return; }
                    send.call(this, data);
                };
                window.__release = () => { open = true; window.__held.forEach(f => f()); };
            })()
            """;

    // On a slow machine or connection this moment lasts seconds, so it's held open here: the socket is
    // up, and what starts the circuit, with the address as it is then, waits to be sent.
    [Theory]
    [InlineData("Orders edited in the row", "tbody tr a[aria-label^='Edit']", "tr[data-ib-editing] input[name$='.Customer']")]
    [InlineData("Orders you can edit", "tbody tr a[aria-label^='Edit']", "dialog[open] input[name$='.Customer']")]
    [InlineData("Orders edited in the row", "thead a.ib-table-sort:has-text('Customer')", "th[aria-sort='ascending']")]
    public async Task A_link_clicked_as_the_circuit_starts_still_counts(string caption, string link, string result)
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 } });
        await page.AddInitScriptAsync(HoldTheCircuit);
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/data-grid?mode=interactive").ToString());
        await page.WaitForFunctionAsync("() => window.__held.length > 0");
        var grid = $".ib-datagrid:has(.ib-table-scroll[aria-label='{caption}'])";
        await page.Locator(grid).Locator(link).First.ClickAsync();
        await Assertions.Expect(page.Locator($"{grid} {result}")).ToHaveCountAsync(1);
        await page.EvaluateAsync("() => window.__release()");
        await page.Locator($"{grid}[data-ib-interactive]").WaitForAsync();
        // Long enough for the circuit's first draw, and for it to catch up.
        await page.WaitForTimeoutAsync(1500);
        await Assertions.Expect(page.Locator($"{grid} {result}")).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task Cancel_gives_focus_back_though_the_circuit_redrew_the_button()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 } });
        await page.AddInitScriptAsync(HoldTheCircuit);
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/data-grid?mode=interactive").ToString());
        await page.WaitForFunctionAsync("() => window.__held.length > 0");
        const string grid = ".ib-datagrid:has(.ib-table-scroll[aria-label='Orders you can edit'])";
        var firstRow = page.Locator(grid).Locator(".ib-table-scroll tbody tr").First;
        await firstRow.Locator("a[aria-label^='Edit']").ClickAsync();
        await page.Locator($"{grid} dialog[open]").WaitForAsync();
        await page.EvaluateAsync("() => window.__release()");
        await page.Locator($"{grid}[data-ib-interactive]").WaitForAsync();
        await page.WaitForTimeoutAsync(1500);
        await page.Locator($"{grid} dialog[open] [data-ib-edit-cancel]").ClickAsync();
        await Assertions.Expect(firstRow.Locator("a[aria-label^='Edit']")).ToBeFocusedAsync();
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

        // Every month's name and every year in full, in English, beside their chevrons.
        var head = row.Locator(".ib-calendar-head select");
        const string overflowing = """
            select => {
                const style = getComputedStyle(select);
                const room = select.getBoundingClientRect().width - parseFloat(style.paddingInlineStart) - parseFloat(style.paddingInlineEnd) - parseFloat(style.borderLeftWidth) - parseFloat(style.borderRightWidth);
                const text = document.createElement("span");
                text.style.cssText = "position:absolute;white-space:nowrap";
                text.style.fontFamily = style.fontFamily;
                text.style.fontSize = style.fontSize;
                text.style.fontWeight = style.fontWeight;
                document.body.append(text);
                const over = [...select.options].filter(o => { text.textContent = o.text; return text.getBoundingClientRect().width > room; }).map(o => o.text);
                text.remove();
                return over;
            }
            """;
        Assert.Empty(await head.Nth(0).EvaluateAsync<string[]>(overflowing));
        Assert.Empty(await head.Nth(1).EvaluateAsync<string[]>(overflowing));
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
