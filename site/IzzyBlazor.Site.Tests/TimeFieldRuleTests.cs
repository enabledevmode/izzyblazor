using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A TimeField posts and binds, and its Min and Max hold on the server, across midnight too.</summary>
public sealed class TimeFieldRuleTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Theory]
    [InlineData("static")]
    public async Task A_time_inside_the_window_saves_and_one_outside_doesnt(string mode)
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/time-field?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var form = page.Locator("[data-ex='time-field-limits'] form");

        // As a crafted post would, or a phone that ignored the limit.
        await form.EvaluateAsync("f => { f.noValidate = true; f.querySelectorAll('input[min], input[max]').forEach(i => { i.removeAttribute('min'); i.removeAttribute('max'); }); }");
        await form.GetByLabel("Night shift starts").FillAsync("12:00");
        await form.GetByRole(AriaRole.Button, new() { Name = "Save hours" }).ClickAsync();
        await Assertions.Expect(form).ToContainTextAsync("must be between");
        await Assertions.Expect(form).Not.ToContainTextAsync("Saved:");

        await form.EvaluateAsync("f => { f.noValidate = true; }");
        await form.GetByLabel("Night shift starts").FillAsync("23:30");
        await form.GetByRole(AriaRole.Button, new() { Name = "Save hours" }).ClickAsync();
        await Assertions.Expect(form).ToContainTextAsync("night shift from 23:30");
    }
}
