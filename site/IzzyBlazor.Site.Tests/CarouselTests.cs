using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A fading carousel in Chromium: with no scrollbar, a mouse drag moves it.</summary>
public sealed class CarouselTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Fact]
    public async Task A_mouse_drag_moves_a_fading_carousel()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 } });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/carousel?mode=static").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => customElements.get('ib-carousel')");
        var carousel = page.Locator("ib-carousel.ib-carousel-fade").First;
        var track = carousel.Locator(".ib-carousel-track");
        var box = (await track.BoundingBoxAsync())!;
        var (x, y) = (box.X + box.Width / 2, box.Y + box.Height / 2);

        // The pointer over it pauses the rotation, so only the drag moves it.
        await page.Mouse.MoveAsync(x, y);
        var shown = track.Locator(":scope > [data-ib-shown]");
        var before = await shown.GetAttributeAsync("aria-label");
        var after = await track.EvaluateAsync<string>(
            "t => { const s = [...t.children]; const i = s.findIndex(c => c.hasAttribute('data-ib-shown')); return s[(i + 1) % s.length].getAttribute('aria-label'); }");

        Assert.NotEqual(before, after);

        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x - 120, y, new() { Steps = 6 });
        await page.Mouse.UpAsync();

        await Assertions.Expect(shown).ToHaveAttributeAsync("aria-label", after);
    }
}
