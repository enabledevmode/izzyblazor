using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A rating chosen, cleared by clicking it again, and chosen again reaches the model on a static page.</summary>
public sealed class RatingTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Fact]
    public async Task A_rating_clears_and_saves_on_a_static_page()
    {
        var browser = await TestBrowser.OpenAsync();
        await using var _ = browser;
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/rating?mode=static").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => customElements.get('ib-rating')");
        var example = page.Locator("[data-ex='rating-review']");
        var two = example.GetByRole(AriaRole.Radio, new() { Name = "2 stars" });
        var save = example.GetByRole(AriaRole.Button, new() { Name = "Save" });

        await two.ClickAsync(new() { Force = true });
        await two.ClickAsync(new() { Force = true });
        await Assertions.Expect(example.GetByRole(AriaRole.Radio, new() { Name = "No rating" })).ToBeCheckedAsync();
        await save.ClickAsync();
        await Assertions.Expect(example).ToContainTextAsync("Saved: no rating.");

        await example.GetByRole(AriaRole.Radio, new() { Name = "4 stars" }).ClickAsync(new() { Force = true });
        await save.ClickAsync();
        await Assertions.Expect(example).ToContainTextAsync("Saved: 4 of 5 stars.");
    }
}
