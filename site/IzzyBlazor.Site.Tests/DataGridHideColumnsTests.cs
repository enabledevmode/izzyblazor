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

        // Order and Customer stay; the rest are there at no width, and nothing scrolls sideways.
        var widths = await grid.Locator("thead th").EvaluateAllAsync<double[]>("ths => ths.map(th => th.getBoundingClientRect().width)");
        Assert.Equal(6, widths.Length);
        Assert.Equal([true, true, true, false, false, false], widths.Select(w => w > 0));
        var box = grid.Locator(".ib-table-scroll");
        Assert.True(await box.EvaluateAsync<bool>("b => b.scrollWidth <= b.clientWidth"));

        await grid.GetByRole(AriaRole.Link, new() { Name = "Show details for Order 1001" }).ClickAsync();
        var values = grid.Locator(".ib-grid-hidden");
        await Assertions.Expect(values).ToBeVisibleAsync();
        // Only the hidden ones: Customer is in the row.
        Assert.Equal("Date Sep 8 Status Failed Total $66.25", await values.EvaluateAsync<string>(@"dl => dl.innerText.split(/\s*\n\s*/).join(' ')"));
    }
}
