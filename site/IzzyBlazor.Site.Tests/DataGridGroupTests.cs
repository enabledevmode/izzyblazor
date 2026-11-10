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

    // Dragging is the only other way to change which level comes first.
    [Fact]
    public async Task Alt_and_an_arrow_move_a_grouping_level()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 } });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/data-grid/grouping?mode=static&grouped-group=status,customer").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => customElements.get('ib-grid-columns')");
        var remove = page.Locator(Grid).GetByRole(AriaRole.Link, new() { Name = "Remove Status" });
        await remove.FocusAsync();

        await page.Keyboard.PressAsync("Alt+ArrowRight");
        await page.WaitForFunctionAsync("() => new URLSearchParams(location.search).get('grouped-group') === 'customer,status'");
        await Assertions.Expect(page.Locator($"{Grid} >> .ib-grid-group-chip")).ToHaveTextAsync(["Customer", "Status"]);
        await Assertions.Expect(page.Locator(Grid).GetByRole(AriaRole.Link, new() { Name = "Remove Status" })).ToBeFocusedAsync();
    }
}
