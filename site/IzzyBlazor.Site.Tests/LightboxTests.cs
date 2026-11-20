using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A lightbox in Chromium: a picture opens in the viewer, the keys move it, and Escape hands focus back.</summary>
public sealed class LightboxTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Fact]
    public async Task A_picture_opens_in_the_viewer_which_moves_with_the_keys_and_closes_back_to_its_link()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 } });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/lightbox?mode=static").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => customElements.get('ib-lightbox') && customElements.get('ib-carousel')");
        var lightbox = page.Locator("ib-lightbox").First;
        var viewer = lightbox.Locator("dialog");
        var counter = viewer.Locator(".ib-lightbox-counter");
        var second = lightbox.Locator("a.ib-lightbox-link").Nth(1);

        // The link would open the image; with script it opens the viewer on that image instead.
        await second.ClickAsync();
        await Assertions.Expect(viewer).ToBeVisibleAsync();
        await Assertions.Expect(counter).ToHaveAttributeAsync("data-ib-text", "2 of 4");
        await Assertions.Expect(viewer.Locator(".ib-carousel-item").Nth(1).Locator("img:visible")).ToBeInViewportAsync();
        Assert.EndsWith("/docs/components/lightbox", new Uri(page.Url).AbsolutePath);

        await page.Keyboard.PressAsync("ArrowRight");
        await Assertions.Expect(counter).ToHaveAttributeAsync("data-ib-text", "3 of 4");
        await Assertions.Expect(viewer.Locator(".ib-carousel-item").Nth(2).Locator("img:visible")).ToBeInViewportAsync();

        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(viewer).ToBeHiddenAsync();
        await Assertions.Expect(second).ToBeFocusedAsync();
    }
}
