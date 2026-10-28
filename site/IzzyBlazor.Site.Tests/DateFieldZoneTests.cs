using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>
/// A DateField's time zone on a static post, in Chromium: Blazor's form binding reads a posted
/// time without an offset in the server's zone, so the script posts it in the field's zone.
/// </summary>
public sealed class DateFieldZoneTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Fact]
    public async Task A_static_post_keeps_the_time_in_the_fields_zone()
    {
        Assert.Equal(0, Microsoft.Playwright.Program.Main(["install", "chromium"]));
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 } });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/date-field?mode=static").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });

        // July in New York is four hours behind UTC, whatever zone the server runs in.
        await page.GetByLabel("Deploy at (UTC)").FillAsync("2027-07-01T09:30");
        await page.GetByLabel("Standup (New York)").FillAsync("2027-07-01T09:30");
        var post = await page.RunAndWaitForRequestAsync(
            () => page.GetByRole(AriaRole.Button, new() { Name = "Schedule" }).ClickAsync(),
            request => request.Method == "POST");

        // The post carries the offset itself, so the server's own zone (which may be New York's) doesn't decide it.
        var body = Uri.UnescapeDataString((post.PostData ?? "").Replace('+', ' '));
        Assert.Contains("2027-07-01T09:30-04:00", body);

        var saved = page.GetByText("Saved 2027-07-01");
        await saved.WaitForAsync(new() { Timeout = 10000 });
        Assert.Equal("Saved 2027-07-01 09:30:00Z and 2027-07-01 09:30 -04:00.", (await saved.TextContentAsync())?.Trim());

        // The page shows the posted values back as they were typed.
        Assert.Equal("2027-07-01T09:30", await page.GetByLabel("Standup (New York)").InputValueAsync());
    }
}
