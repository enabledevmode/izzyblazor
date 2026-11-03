using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A DataGrid that hides columns on a phone, and shows them in a row's detail, in Chromium.</summary>
public sealed class DataGridHideColumnsTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Fact]
    public async Task On_a_phone_columns_hide_and_a_rows_button_shows_their_values()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 390, Height = 844 } });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/data-grid/small-screens").ToString());
        var grid = page.Locator(".ib-datagrid:has(.ib-table-scroll[aria-label='Orders that fit a phone'])");

        // Order, Customer and the actions stay; the rest are there at no width, and nothing scrolls sideways.
        var widths = await grid.Locator("thead th").EvaluateAllAsync<double[]>("ths => ths.map(th => th.getBoundingClientRect().width)");
        Assert.Equal([true, true, true, false, false, false, true], widths.Select(w => w > 0));
        var box = grid.Locator(".ib-table-scroll");
        Assert.True(await box.EvaluateAsync<bool>("b => b.scrollWidth <= b.clientWidth"));

        // The row's actions are in its ⋮ now.
        var actions = await grid.Locator("tbody tr").First.Locator(".ib-grid-actions :is(a, button)")
            .EvaluateAllAsync<string[]>("els => els.filter(e => e.checkVisibility()).map(e => e.getAttribute('aria-label'))");
        Assert.Equal(["Actions for Order 1001"], actions);

        await grid.GetByRole(AriaRole.Link, new() { Name = "Show details for Order 1001" }).ClickAsync();
        var values = grid.Locator(".ib-grid-hidden");
        await Assertions.Expect(values).ToBeVisibleAsync();
        // Only the hidden ones: Customer is in the row.
        Assert.Equal("Date Sep 8 Status Failed Total $66.25", await values.EvaluateAsync<string>(@"dl => dl.innerText.split(/\s*\n\s*/).join(' ')"));

        // Closed again, the grid isn't held at the height it had open.
        await grid.GetByRole(AriaRole.Link, new() { Name = "Hide details for Order 1001" }).ClickAsync();
        await Assertions.Expect(values).ToHaveCountAsync(0);
        Assert.True(await grid.EvaluateAsync<bool>("g => g.querySelector('.ib-table-scroll').clientHeight - g.querySelector('.ib-table-scroll > table').offsetHeight < 4"));
    }

    [Fact]
    public async Task On_a_phone_a_row_edits_its_hidden_columns_in_its_detail_and_saves_them()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 390, Height = 844 } });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/data-grid/small-screens?mode=static").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var grid = page.Locator(".ib-datagrid:has(.ib-table-scroll[aria-label='Orders edited on a phone'])");
        var first = grid.Locator(".ib-table-scroll tbody tr").First;
        await first.Locator("[data-ib-row-menu]").ClickAsync();
        await grid.Locator("ib-popover a[data-ib-edit-row]").First.ClickAsync();

        // Its fields are under it, the first focused; the cells' copies, hidden, don't post.
        var customer = grid.Locator(".ib-grid-detail").GetByLabel("Customer");
        await Assertions.Expect(customer).ToBeFocusedAsync();
        await grid.Locator(".ib-grid-detail").GetByLabel("Total").FillAsync("123.45");
        await grid.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(grid.Locator("tr[data-ib-editing]")).ToHaveCountAsync(0);

        await first.Locator(".ib-grid-expand").ClickAsync();
        await Assertions.Expect(grid.Locator(".ib-grid-hidden")).ToContainTextAsync("$123.45");
    }
}
