using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A DateRangeField posts both ends and binds, and its rules hold on the server.</summary>
public sealed class DateRangeFieldTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Theory]
    [InlineData("static", true)]
    [InlineData("interactive", true)]
    [InlineData("noscript", false)]
    public async Task A_good_range_saves_and_a_bad_one_doesnt(string mode, bool script)
    {
        await using var browser = await TestBrowser.OpenAsync();
        var context = await browser.NewContextAsync(new() { JavaScriptEnabled = script, ViewportSize = new() { Width = 1400, Height = 900 }, Locale = "en-US" });
        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/date-range-field?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var form = page.Locator("[data-ex='date-range-field-limits'] form");
        var save = form.GetByRole(AriaRole.Button, new() { Name = "Book the stay" });

        async Task NoBrowserChecksAsync()
        {
            if (script) await form.EvaluateAsync("f => { f.noValidate = true; }");
        }

        // A phone or a crafted post gets past the browser. With no script, one past the browser's own checks.
        await NoBrowserChecksAsync();
        await form.GetByLabel("Start").FillAsync("2027-03-08");
        await form.GetByLabel("End").FillAsync("2027-04-30");
        await save.ClickAsync();
        await Assertions.Expect(form).ToContainTextAsync("at most 14 days");
        await Assertions.Expect(form).Not.ToContainTextAsync("Booked:");

        await NoBrowserChecksAsync();
        await form.GetByLabel("End").FillAsync("2027-03-12");
        await save.ClickAsync();
        await Assertions.Expect(form).ToContainTextAsync("Booked: 3/8/2027 to 3/12/2027, 5 days.");
        await Assertions.Expect(form.GetByLabel("End")).ToHaveValueAsync("2027-03-12");
    }
}
