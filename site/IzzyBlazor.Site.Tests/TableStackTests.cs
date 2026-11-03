using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A Table that stacks its rows on a phone, in Chromium.</summary>
public sealed class TableStackTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Fact]
    public async Task On_a_phone_each_row_is_a_card_of_titles_and_values()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 390, Height = 844 } });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/table").ToString());
        var box = page.Locator(".ib-table-scroll[aria-label='Orders that stack']");

        Assert.True(await box.EvaluateAsync<bool>("b => b.scrollWidth <= b.clientWidth"));
        var cell = box.Locator("tbody tr").First.Locator("td").Nth(2);
        Assert.Equal("\"Customer\"", await cell.EvaluateAsync<string>("td => getComputedStyle(td, '::before').content.split(' / ')[0]"));
        await Assertions.Expect(page.GetByRole(AriaRole.Table, new() { Name = "Orders that stack" })).ToBeVisibleAsync();
    }
}
