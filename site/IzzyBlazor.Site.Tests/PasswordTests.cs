using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A new password's requirements tick as it's typed, and it posts once they're met.</summary>
public sealed class PasswordTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Fact]
    public async Task Requirements_tick_as_you_type_and_the_password_posts_on_a_static_page()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/text-field?mode=static").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var example = page.Locator("[data-ex='text-field-new-password']");
        var password = example.Locator("input[type=password]");
        var rules = example.Locator(".ib-password-rules > li");

        await password.FillAsync("aaaaaaaaaaaaaaaa");
        await Assertions.Expect(rules.Nth(0)).ToHaveAttributeAsync("data-met", "");
        await Assertions.Expect(rules.Nth(1)).Not.ToHaveAttributeAsync("data-met", "");
        await Assertions.Expect(rules.Nth(1)).ToContainTextAsync("not yet");

        await password.FillAsync("correct horse battery staple");
        await Assertions.Expect(rules.Nth(1)).ToContainTextAsync("met");
        await Assertions.Expect(example.Locator(".ib-password-status")).ToHaveTextAsync("All requirements met.");
        await example.GetByRole(AriaRole.Button, new() { Name = "Create account" }).ClickAsync();
        await Assertions.Expect(example).ToContainTextAsync("Account created.");
    }
}
