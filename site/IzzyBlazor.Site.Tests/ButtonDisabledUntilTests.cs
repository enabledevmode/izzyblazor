using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>Button's DisabledUntil, in Chromium: the button waits for its box on a static page.</summary>
public sealed class ButtonDisabledUntilTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Fact]
    public async Task The_button_waits_for_its_box()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync();
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "frame/button-disabled-until?mode=static").ToString());
        var button = page.GetByRole(AriaRole.Button, new() { Name = "Delete workspace" });
        // Clicked on its label, as people do: the label covers the box and the text.
        var label = page.Locator("label.ib-check", new() { HasText = "I understand this can't be undone" });

        await Assertions.Expect(button).ToBeDisabledAsync();
        await label.ClickAsync();
        await Assertions.Expect(button).ToBeEnabledAsync();
        await label.ClickAsync();
        await Assertions.Expect(button).ToBeDisabledAsync();
    }
}
