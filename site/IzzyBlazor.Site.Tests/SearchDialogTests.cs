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
        await using var browser = await TestBrowser.OpenAsync();
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

        // Escape closes at once, though the field has text (a search field would only clear it).
        await page.Keyboard.PressAsync("Escape");
        Assert.False(await page.EvaluateAsync<bool>(Open));

        // Opened from its button, focus comes back to it.
        await page.ClickAsync("button.site-search-button");
        await page.Keyboard.PressAsync("Escape");
        Assert.True(await page.EvaluateAsync<bool>("() => document.activeElement.matches('.site-search-button')"));
    }

    [Fact]
    public async Task Back_with_search_open_leaves_the_page_usable()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 } });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/button").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });

        // An enhanced navigation, so Back is one too.
        await page.ClickAsync("main a[href='docs/components/form']");
        await page.WaitForURLAsync("**/docs/components/form");
        await page.Keyboard.PressAsync("Control+k");
        await page.Keyboard.TypeAsync("spin");
        await page.GoBackAsync();
        await page.WaitForURLAsync("**/docs/components/button");
        await page.WaitForTimeoutAsync(500);

        Assert.False(await page.EvaluateAsync<bool>("() => !!document.querySelector(':modal')"));
    }

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
