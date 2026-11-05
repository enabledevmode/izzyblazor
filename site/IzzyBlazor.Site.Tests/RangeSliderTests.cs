using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A range slider's thumbs stop at each other, a press on the track moves the nearer one, and both ends post.</summary>
public sealed class RangeSliderTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Fact]
    public async Task The_thumbs_stop_at_each_other_and_both_ends_post_on_a_static_page()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/range-slider?mode=static").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var example = page.Locator("[data-ex='range-slider-price']");
        var low = example.GetByRole(AriaRole.Slider, new() { Name = "Minimum" });
        var high = example.GetByRole(AriaRole.Slider, new() { Name = "Maximum" });

        // The shown values get shorter ("$0 – $300") without lengthening the track, which would
        // move the upper thumb.
        var length = (await high.BoundingBoxAsync())!.Width;
        await low.FocusAsync();
        await page.Keyboard.PressAsync("Home");
        await Assertions.Expect(low).ToHaveValueAsync("0");
        Assert.Equal(length, (await high.BoundingBoxAsync())!.Width);

        // End goes to the top of the scale, but the lower thumb stops at the upper one.
        await page.Keyboard.PressAsync("End");
        await Assertions.Expect(low).ToHaveValueAsync("300");
        await Assertions.Expect(low).ToHaveAttributeAsync("aria-valuetext", "$300");

        // A press on the track at 100 moves the lower thumb there. The thumb's center travels the
        // track less one thumb (the track is a thumb high).
        var box = (await example.Locator(".ib-range-slider-track").BoundingBoxAsync())!;
        var thumb = box.Height;
        await page.Mouse.ClickAsync(box.X + thumb / 2 + (box.Width - thumb) * 0.25f, box.Y + box.Height / 2);
        await Assertions.Expect(low).ToHaveValueAsync("100");
        await Assertions.Expect(high).ToHaveValueAsync("300");

        await high.FocusAsync();
        await page.Keyboard.PressAsync("ArrowDown");
        await example.GetByRole(AriaRole.Button, new() { Name = "Show headphones" }).ClickAsync();
        await Assertions.Expect(example.Locator("li")).ToHaveTextAsync(["Travel noise-cancelling · $140", "Open-back studio · $210"]);
        await Assertions.Expect(example).ToContainTextAsync("$100 – $290");
    }

    [Fact]
    public async Task Crossed_ends_posted_with_no_script_bind_in_order()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var context = await browser.NewContextAsync(new() { JavaScriptEnabled = false });
        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/range-slider?mode=static").ToString());
        var example = page.Locator("[data-ex='range-slider-price']");

        await example.GetByRole(AriaRole.Slider, new() { Name = "Minimum" }).FillAsync("350");
        await example.GetByRole(AriaRole.Slider, new() { Name = "Maximum" }).FillAsync("120");
        await example.GetByRole(AriaRole.Button, new() { Name = "Show headphones" }).ClickAsync();

        example = page.Locator("[data-ex='range-slider-price']");
        await Assertions.Expect(example.Locator("li")).ToHaveTextAsync(["Travel noise-cancelling · $140", "Open-back studio · $210", "Premium noise-cancelling · $330"]);
        await Assertions.Expect(example.GetByRole(AriaRole.Slider, new() { Name = "Minimum" })).ToHaveValueAsync("120");
    }
}
