using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A pasted code fills the boxes and posts without its spaces.</summary>
public sealed class PinFieldTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Fact]
    public async Task A_pasted_code_fills_the_boxes_and_posts_on_a_static_page()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/pin-field?mode=static").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var example = page.Locator("[data-ex='pin-field-usage']");
        await example.Locator("ib-pin-field").WaitForAsync();
        var code = example.GetByRole(AriaRole.Textbox, new() { Name = "Verification code" });

        // A paste is an input event with the whole text, as here.
        await code.FillAsync("123 456");
        await Assertions.Expect(code).ToHaveValueAsync("123456");
        Assert.Equal(["1", "2", "3", "4", "5", "6"], await example.Locator(".ib-pin-box").EvaluateAllAsync<string[]>("boxes => boxes.map(b => b.dataset.c)"));
        await example.GetByRole(AriaRole.Button, new() { Name = "Verify" }).ClickAsync();
        await Assertions.Expect(example).ToContainTextAsync("Verified.");
    }
}
