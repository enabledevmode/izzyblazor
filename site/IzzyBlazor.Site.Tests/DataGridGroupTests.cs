using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>DataGrid's groups in Chromium: the keys on a group's row, and focus through a reload.</summary>
public sealed class DataGridGroupTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private const string Grid = ".ib-datagrid:has(.ib-table-scroll[aria-label='Orders in groups'])";

    [Fact]
    public async Task Keys_open_and_close_a_group()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 } });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/data-grid/grouping?mode=static").ToString());
        await page.Locator($"{Grid} >> table[role=treegrid]").WaitForAsync();

        var group = page.Locator($"{Grid} >> tr.ib-grid-group").First;
        await group.Locator(".ib-grid-group-toggle").FocusAsync();

        await page.Keyboard.PressAsync("ArrowRight");
        await page.Locator($"{Grid} >> tr.ib-grid-group[aria-expanded=true]").WaitForAsync();
        Assert.Contains("grouped-open=", page.Url);
        // Focus stays on its group through the reload.
        Assert.True(await group.Locator(".ib-grid-group-toggle").EvaluateAsync<bool>("e => e === document.activeElement"));

        var rows = page.Locator($"{Grid} >> tbody tr[aria-level='2']");
        Assert.True(await rows.CountAsync() > 0);

        await group.Locator(".ib-grid-group-toggle").FocusAsync();
        await page.Keyboard.PressAsync("ArrowLeft");
        await page.Locator($"{Grid} >> tr.ib-grid-group[aria-expanded=true]").WaitForAsync(new() { State = WaitForSelectorState.Detached });
        Assert.Equal(0, await rows.CountAsync());
    }
}
