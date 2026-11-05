using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A MultiSelect with AllowCustom takes typed values, which post and bind with the options.</summary>
public sealed class MultiSelectCustomTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task Typed_addresses_become_chips_and_post_after_a_bad_one_is_removed(string mode)
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/multi-select?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var example = page.Locator("[data-ex='multi-select-recipients']");
        await example.Locator("ib-combobox[data-ready]").WaitForAsync();
        var input = example.GetByRole(AriaRole.Combobox, new() { Name = "Share with" });
        var chips = example.Locator(".ib-chip-text");

        // Enter adds the text; a comma ends a value; a teammate's address in other case picks the teammate.
        await input.PressSequentiallyAsync("bob@example.com");
        await input.PressAsync("Enter");
        await input.PressSequentiallyAsync("not-an-email,GRACE@contoso.com,");
        await Assertions.Expect(chips).ToHaveTextAsync(["grace@contoso.com", "bob@example.com", "not-an-email"]);

        // Leaving the field (for Share) adds what's still typed.
        await input.PressSequentiallyAsync("last@example.com");
        await example.GetByRole(AriaRole.Button, new() { Name = "Share" }).ClickAsync();
        await Assertions.Expect(example).ToContainTextAsync("\"not-an-email\" isn't an email address.");

        // Removing the bad one leaves the rest chosen, the typed values after it too.
        await example.GetByRole(AriaRole.Button, new() { Name = "Remove not-an-email" }).ClickAsync();
        await example.GetByRole(AriaRole.Button, new() { Name = "Share" }).ClickAsync();
        await Assertions.Expect(example).ToContainTextAsync("Shared with grace@contoso.com, bob@example.com, last@example.com.");
        await Assertions.Expect(chips).ToHaveTextAsync(["grace@contoso.com", "bob@example.com", "last@example.com"]);
    }
}
