using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A suggestion or free text posts on a static page; a provider searches as you type on an interactive one.</summary>
public sealed class AutocompleteTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private async Task<(IAsyncDisposable Browser, IPage Page)> OpenAsync(string mode)
    {
        var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/autocomplete?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        return (browser, page);
    }

    [Fact]
    public async Task A_suggestion_or_any_text_saves_on_a_static_page()
    {
        var (browser, page) = await OpenAsync("static");
        await using var _ = browser;
        var example = page.Locator("[data-ex='autocomplete-cities']");
        await example.Locator("ib-combobox[data-ready]").WaitForAsync();
        var city = example.GetByRole(AriaRole.Combobox);
        var save = example.GetByRole(AriaRole.Button, new() { Name = "Save" });

        await city.FillAsync("krak");
        await city.PressAsync("ArrowDown");
        await city.PressAsync("Enter");
        await Assertions.Expect(city).ToHaveValueAsync("Kraków");
        await save.ClickAsync();
        await Assertions.Expect(example).ToContainTextAsync("Saved: Kraków.");

        await city.FillAsync("Ljubljana");
        await save.ClickAsync();
        await Assertions.Expect(example).ToContainTextAsync("Saved: Ljubljana.");
    }

    [Fact]
    public async Task A_provider_finds_suggestions_as_you_type_on_an_interactive_page()
    {
        var (browser, page) = await OpenAsync("interactive");
        await using var _ = browser;
        var example = page.Locator("[data-ex='autocomplete-provider']");
        await example.Locator("ib-combobox[data-server][data-ready]").WaitForAsync();
        var product = example.GetByRole(AriaRole.Combobox);

        await product.PressSequentiallyAsync("lamp, walnut");
        await Assertions.Expect(example.GetByRole(AriaRole.Option).First).ToHaveTextAsync("Alto Lamp, walnut");
        await example.GetByRole(AriaRole.Option, new() { Name = "Nova Lamp, walnut" }).ClickAsync();
        await Assertions.Expect(product).ToHaveValueAsync("Nova Lamp, walnut");
    }
}
