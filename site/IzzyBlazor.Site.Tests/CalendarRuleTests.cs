using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A Calendar posts and binds with its own stylesheet, and its Min and Max hold on the server.</summary>
public sealed class CalendarRuleTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task A_date_inside_the_limits_saves_and_one_outside_doesnt(string mode)
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 1000 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/calendar?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var form = page.Locator("[data-ex='calendar-limits'] form");

        // Its sheet has arrived: the month shows, with a cell for each day.
        await Assertions.Expect(form.Locator(".ib-calendar").First).ToBeVisibleAsync();
        await Assertions.Expect(form.Locator(".ib-calendar-day").First).ToHaveCSSAsync("width", "36px");

        // As a crafted post would, or a phone that ignored the limit.
        await form.EvaluateAsync("f => { f.noValidate = true; f.querySelectorAll('input[min], input[max]').forEach(i => { i.removeAttribute('min'); i.removeAttribute('max'); }); }");
        await form.GetByLabel("Pickup").FillAsync("2027-04-02");
        await form.GetByRole(AriaRole.Button, new() { Name = "Book" }).ClickAsync();
        await Assertions.Expect(form).ToContainTextAsync("or earlier");
        await Assertions.Expect(form).Not.ToContainTextAsync("Booked:");

        await form.EvaluateAsync("f => { f.noValidate = true; }");
        await form.GetByLabel("Pickup").FillAsync("2027-03-11");
        await form.GetByRole(AriaRole.Button, new() { Name = "Book" }).ClickAsync();
        await Assertions.Expect(form).ToContainTextAsync("Booked:");
        await Assertions.Expect(form.Locator("td[aria-selected=true] button").Nth(1)).ToHaveAttributeAsync("aria-label", "Thursday, March 11, 2027");
    }
}
