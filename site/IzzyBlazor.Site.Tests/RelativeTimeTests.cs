using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>RelativeTime, in Chromium: the library's script keeps the text current as time passes on a static page.</summary>
public sealed class RelativeTimeTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Fact]
    public async Task The_time_stays_current_while_the_page_is_open()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync();
        await page.Clock.InstallAsync();
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "frame/relative-time-usage?mode=static").ToString());
        var newest = page.Locator("time.ib-relative-time").First;

        await Assertions.Expect(newest).ToHaveTextAsync("now");
        await page.Clock.FastForwardAsync("02:00:00");
        await Assertions.Expect(newest).ToHaveTextAsync("2 hours ago");
    }
}
