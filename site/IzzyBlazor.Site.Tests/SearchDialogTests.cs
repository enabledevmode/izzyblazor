using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>
/// The header's search dialog in Chromium with JavaScript off: the button still opens it and Enter goes to /search.
/// </summary>
public sealed class SearchDialogTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Fact]
    public async Task Without_script_the_button_opens_search_and_Enter_goes_to_the_search_page()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { JavaScriptEnabled = false, ViewportSize = new() { Width = 1400, Height = 900 } });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/button").ToString());

        await page.ClickAsync("button.site-search-button");
        Assert.NotNull(await page.Locator("#site-search").GetAttributeAsync("open"));

        await page.FillAsync("#site-search input[name=q]", "form name");
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForURLAsync("**/search?q=form+name");
        Assert.Contains("results for", await page.Locator("main p[role=status]").TextContentAsync());
    }
}
