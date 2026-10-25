using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>
/// The header's search dialog in Chromium: Ctrl+K, results as you type, the arrow keys and
/// Escape with script; with JavaScript off, the button still opens it and Enter goes to /search.
/// </summary>
public sealed class SearchDialogTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private const string Open = "() => document.getElementById('site-search').open";

    private const string Focused = "() => document.activeElement?.getAttribute('href') ?? document.activeElement?.getAttribute('name') ?? ''";

    [Fact]
    public async Task Ctrl_K_opens_search_and_results_show_as_you_type()
    {
        Assert.Equal(0, Microsoft.Playwright.Program.Main(["install", "chromium"]));
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 } });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/button").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });

        await page.Keyboard.PressAsync("Control+k");
        Assert.True(await page.EvaluateAsync<bool>(Open));
        Assert.Equal("q", await page.EvaluateAsync<string>(Focused));

        await page.Keyboard.TypeAsync("toggle buttons");
        await page.WaitForFunctionAsync("() => /results? for “toggle buttons”/.test(document.querySelector('[data-site-search-count]').textContent)");
        Assert.Equal("docs/components/button#toggle-buttons",
            await page.Locator("#site-search .site-search-link").First.GetAttributeAsync("href"));

        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal("docs/components/button#toggle-buttons", await page.EvaluateAsync<string>(Focused));
        await page.Keyboard.PressAsync("ArrowUp");
        Assert.Equal("q", await page.EvaluateAsync<string>(Focused));

        await page.Keyboard.PressAsync("Escape");
        Assert.False(await page.EvaluateAsync<bool>(Open));
    }

    [Fact]
    public async Task Without_script_the_button_opens_search_and_Enter_goes_to_the_search_page()
    {
        Assert.Equal(0, Microsoft.Playwright.Program.Main(["install", "chromium"]));
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
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
