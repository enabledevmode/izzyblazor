using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A static Combobox with Search asks the server as you type, and the chosen value posts.</summary>
public sealed class ComboboxTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Fact]
    public async Task Search_finds_options_as_you_type_on_a_static_page()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/combobox?mode=static").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var example = page.Locator("[data-ex='combobox-server-search']");
        await example.Locator("ib-combobox[data-ib-suggest-key][data-ready]").WaitForAsync();
        var owner = example.GetByRole(AriaRole.Combobox);

        // The page came with the first 8 people; Ken Thompson is the 14th. Enter waits for the answer.
        await owner.PressSequentiallyAsync("ken");
        await owner.PressAsync("Enter");
        await Assertions.Expect(owner).ToHaveValueAsync("Ken Thompson");
        Assert.Equal("14", await example.Locator("select").EvaluateAsync<string>("s => s.value"));
    }
}
