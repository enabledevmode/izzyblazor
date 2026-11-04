using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A pasted code fills the boxes, a clicked digit is replaced, and the code posts without its spaces.</summary>
public sealed class PinFieldTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Fact]
    public async Task A_pasted_code_fills_the_boxes_a_clicked_digit_is_replaced_and_it_posts_on_a_static_page()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/pin-field?mode=static").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var example = page.Locator("[data-ex='pin-field-verify']");
        await example.Locator("ib-pin-field").WaitForAsync();
        var code = example.GetByRole(AriaRole.Textbox, new() { Name = "Verification code" });

        // A paste is an input event with the whole text, as here; one digit is wrong.
        await code.FillAsync("129 456");
        await Assertions.Expect(code).ToHaveValueAsync("129456");

        // A click on that digit's box selects it, and the next key replaces it.
        var box = await example.Locator(".ib-pin-box").Nth(2).BoundingBoxAsync();
        await page.Mouse.ClickAsync(box!.X + box.Width / 2, box.Y + box.Height / 2);
        await page.Keyboard.TypeAsync("3");
        await Assertions.Expect(code).ToHaveValueAsync("123456");
        Assert.Equal(["1", "2", "3", "4", "5", "6"], await example.Locator(".ib-pin-box").EvaluateAllAsync<string[]>("boxes => boxes.map(b => b.dataset.c)"));
        await example.GetByRole(AriaRole.Button, new() { Name = "Verify" }).ClickAsync();
        await Assertions.Expect(example).ToContainTextAsync("Verified.");
    }
}
