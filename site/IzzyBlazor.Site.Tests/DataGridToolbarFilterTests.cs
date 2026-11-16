using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>DataGridFilter, in Chromium on a static page: the toolbar's Status filter sets the column's filter.</summary>
public sealed class DataGridToolbarFilterTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Fact]
    public async Task The_toolbar_filter_sets_the_columns_filter()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync();
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "frame/data-grid-toolbar-filter?mode=static").ToString());
        var grid = page.GetByRole(AriaRole.Grid, new() { Name = "Orders with a status filter" });

        // The column's header leaves its funnel to the toolbar; the others keep theirs.
        await Assertions.Expect(page.GetByRole(AriaRole.Columnheader, new() { Name = "Status" }).Locator(".ib-table-filter-button:not([data-ib-menu])")).ToHaveCountAsync(0);

        await page.GetByRole(AriaRole.Button, new() { Name = "Status", Exact = true }).ClickAsync();
        // Every status is ticked while there's no filter: untick them all, then tick Paid.
        await page.GetByRole(AriaRole.Checkbox, new() { Name = "(Select all)" }).UncheckAsync();
        await page.GetByRole(AriaRole.Checkbox, new() { Name = "Paid", Exact = true }).CheckAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Apply" }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Status: Paid" })).ToBeVisibleAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("status=Paid", RegexOptions.IgnoreCase));
        foreach (var status in new[] { "Pending", "Refunded", "Failed" })
            await Assertions.Expect(grid.GetByRole(AriaRole.Cell, new() { Name = status, Exact = true })).ToHaveCountAsync(0);
    }
}
