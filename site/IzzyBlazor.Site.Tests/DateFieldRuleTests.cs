using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A DateField's Min and Max hold on the server, whatever the browser let through.</summary>
public sealed class DateFieldRuleTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Theory]
    [InlineData("static")]
    public async Task A_date_past_the_limit_doesnt_save(string mode)
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/date-field?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var form = page.Locator("[data-ex='date-field-limits'] form");

        // As a crafted post would, or a phone that ignored the limit.
        await form.EvaluateAsync("f => { f.noValidate = true; f.querySelectorAll('input[min], input[max]').forEach(i => { i.removeAttribute('min'); i.removeAttribute('max'); }); }");
        await form.GetByLabel("Check-in").FillAsync("2027-06-10");
        await form.GetByLabel("Check-out").FillAsync("2027-06-01");
        await form.GetByRole(AriaRole.Button, new() { Name = "Book" }).ClickAsync();

        await Assertions.Expect(form).ToContainTextAsync("or later");
        await Assertions.Expect(form).Not.ToContainTextAsync("Booked");
    }
}
