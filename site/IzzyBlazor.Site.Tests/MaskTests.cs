using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A masked field takes its form as it's typed and pasted, and posts that way.</summary>
public sealed class MaskTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Fact]
    public async Task Typed_and_pasted_text_takes_the_masks_form_and_posts_on_a_static_page()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/text-field?mode=static").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var example = page.Locator("[data-ex='text-field-masks']");
        var phone = example.GetByRole(AriaRole.Textbox, new() { Name = "Telephone" });
        var card = example.GetByRole(AriaRole.Textbox, new() { Name = "Credit card" });
        var ssn = example.GetByRole(AriaRole.Textbox, new() { Name = "SSN" });

        await phone.PressSequentiallyAsync("4165550199");
        await Assertions.Expect(phone).ToHaveValueAsync("(416) 555-0199");

        // Backspace beside the dash removes the digit before it, not the dash.
        for (var i = 0; i < 4; i++) await phone.PressAsync("ArrowLeft");
        await phone.PressAsync("Backspace");
        await Assertions.Expect(phone).ToHaveValueAsync("(416) 550-199");
        await phone.PressAsync("5");
        await Assertions.Expect(phone).ToHaveValueAsync("(416) 555-0199");

        // A paste is an input event with the whole text, as here.
        await card.FillAsync("4242-4242-4242-4242");
        await Assertions.Expect(card).ToHaveValueAsync("4242 4242 4242 4242");
        await ssn.PressSequentiallyAsync("123456789");
        await Assertions.Expect(ssn).ToHaveValueAsync("123-45-6789");

        await example.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(example).ToContainTextAsync("Saved.");
    }
}
