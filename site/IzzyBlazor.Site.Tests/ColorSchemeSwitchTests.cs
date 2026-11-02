using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>ColorSchemeSwitch in Chromium: each look changes the page and the cookie at once, and with no script it's hidden.</summary>
public sealed class ColorSchemeSwitchTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private const string Example = "[data-ex=\"color-scheme-switch-variants\"]";

    private async Task<(TestBrowser, IPage, ILocator)> OpenAsync(string mode, bool script = true)
    {
        var browser = await TestBrowser.OpenAsync();
        var context = await browser.NewContextAsync(new() { JavaScriptEnabled = script, Locale = "en-US", ViewportSize = new() { Width = 1400, Height = 900 } });
        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/color-scheme-switch?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var example = page.Locator(Example);
        await example.ScrollIntoViewSettledAsync();
        if (script) await page.WaitForFunctionAsync("() => !!customElements.get('ib-scheme-switch')", null, new() { PollingInterval = 100 });
        if (mode == "interactive") await example.Locator("ib-scheme-switch").First.Page.WaitForTimeoutAsync(500);
        return (browser, page, example);
    }

    private static Task SchemeIsAsync(IPage page, string scheme) =>
        Assertions.Expect(page.Locator("html")).ToHaveAttributeAsync("data-ib-color-scheme", scheme);

    private static async Task<string?> CookieAsync(IPage page) =>
        (await page.Context.CookiesAsync()).FirstOrDefault(c => c.Name == "ib-scheme")?.Value;

    [Theory]
    [InlineData("static")]
    public async Task The_menu_picks_a_scheme_by_mouse_and_by_keys_without_leaving_the_page(string mode)
    {
        var (browser, page, example) = await OpenAsync(mode);
        await using var _ = browser;
        var url = page.Url;
        var trigger = example.GetByRole(AriaRole.Button, new() { Name = "Color scheme" });
        var dark = page.GetByRole(AriaRole.Menuitemradio, new() { Name = "Dark", Exact = true }).First;

        await trigger.ClickAsync();
        var menu = page.Locator("[popover]:popover-open");
        await menu.WaitForAsync();
        await menu.GetByRole(AriaRole.Menuitemradio, new() { Name = "Dark", Exact = true }).ClickAsync();

        await SchemeIsAsync(page, "dark");
        Assert.Equal("dark", await CookieAsync(page));
        Assert.Equal(url, page.Url);
        await menu.WaitForAsync(new() { State = WaitForSelectorState.Hidden });

        // By the keys: the chosen one is marked, and Enter on another moves it.
        await trigger.FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        await menu.WaitForAsync();
        Assert.Equal("true", await menu.GetByRole(AriaRole.Menuitemradio, new() { Name = "Dark", Exact = true }).GetAttributeAsync("aria-checked"));
        var light = menu.GetByRole(AriaRole.Menuitemradio, new() { Name = "Light", Exact = true });
        for (var i = 0; i < 3 && !await light.EvaluateAsync<bool>("i => document.activeElement === i"); i++)
        {
            await page.Keyboard.PressAsync("ArrowDown");
        }

        await Assertions.Expect(light).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Enter");

        await SchemeIsAsync(page, "light");
        Assert.Equal("light", await CookieAsync(page));
        await Assertions.Expect(trigger).ToBeFocusedAsync();
    }

    [Theory]
    [InlineData("static")]
    public async Task The_toggle_goes_each_way_and_keeps_focus(string mode)
    {
        var (browser, page, example) = await OpenAsync(mode);
        await using var _ = browser;

        var toDark = example.GetByRole(AriaRole.Button, new() { Name = "Switch to dark mode" });
        await toDark.FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        await SchemeIsAsync(page, "dark");

        var toLight = example.GetByRole(AriaRole.Button, new() { Name = "Switch to light mode" });
        await Assertions.Expect(toLight).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Space");
        await SchemeIsAsync(page, "light");
        await Assertions.Expect(toDark).ToBeFocusedAsync();
    }

    [Fact]
    public async Task With_no_script_it_is_hidden_and_a_saved_choice_still_shows()
    {
        var (browser, page, example) = await OpenAsync("static", script: false);
        await using var _ = browser;

        await Assertions.Expect(example.Locator("ib-scheme-switch").First).ToBeHiddenAsync();

        await page.Context.AddCookiesAsync([new() { Name = "ib-scheme", Value = "dark", Url = page.Url }]);
        await page.ReloadAsync();

        // The server says it in a meta tag, which the stylesheet reads.
        Assert.Equal("dark", await page.Locator("meta[name='ib-color-scheme']").GetAttributeAsync("content"));
    }
}
