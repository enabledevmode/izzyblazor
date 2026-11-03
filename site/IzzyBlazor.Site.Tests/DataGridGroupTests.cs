using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>DataGrid's groups in Chromium: the keys on a group's row, focus through a reload, and its checkbox.</summary>
public sealed class DataGridGroupTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private const string Grid = ".ib-datagrid:has(.ib-table-scroll[aria-label='Orders in groups'])";

    [Fact]
    public async Task Keys_open_and_close_a_group_and_its_box_ticks_its_rows()
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

        // A row's box goes with the group's, whichever is changed.
        await group.Locator(".ib-table-group-select").CheckAsync();
        var rows = page.Locator($"{Grid} >> tbody .ib-table-select");
        Assert.True(await rows.CountAsync() > 0);
        Assert.Equal(await rows.CountAsync(), await page.Locator($"{Grid} >> tbody .ib-table-select:checked").CountAsync());
        await rows.First.UncheckAsync();
        Assert.False(await group.Locator(".ib-table-group-select").IsCheckedAsync());

        await group.Locator(".ib-grid-group-toggle").FocusAsync();
        await page.Keyboard.PressAsync("ArrowLeft");
        await page.Locator($"{Grid} >> tr.ib-grid-group[aria-expanded=true]").WaitForAsync(new() { State = WaitForSelectorState.Detached });
        Assert.Equal(0, await rows.CountAsync());
    }
}
