using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>Filtering down to fewer rows leaves what's below a paged grid where it was.</summary>
public sealed class TableHoldsHeightTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task An_operator_that_leaves_no_rows_moves_nothing_below_the_grid(string mode)
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/data-grid?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var example = page.Locator("[data-ex='data-grid-filter-row']");
        var grid = example.Locator("ib-table");
        await Assertions.Expect(grid).ToHaveAttributeAsync("style", new System.Text.RegularExpressions.Regex("min-height"));
        var before = await grid.EvaluateAsync<double>("e => e.offsetHeight");

        await example.Locator(".ib-filter-cell-op[aria-label^='Customer']").ClickAsync();
        await page.Locator("[popover]:popover-open").GetByText("Is empty", new() { Exact = true }).ClickAsync();
        await Assertions.Expect(example).ToContainTextAsync("No items");

        Assert.Equal(before, await grid.EvaluateAsync<double>("e => e.offsetHeight"));
    }
}
