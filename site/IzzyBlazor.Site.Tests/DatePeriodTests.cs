using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A month and a year chosen from their pages reach the model.</summary>
public sealed class DatePeriodTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private async Task<(IAsyncDisposable Browser, IPage Page, ILocator Example)> OpenAsync(string mode)
    {
        var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/date-field?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        return (browser, page, page.Locator("[data-ex='date-field-periods']"));
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task A_month_and_a_year_chosen_from_their_pages_save(string mode)
    {
        var (browser, page, example) = await OpenAsync(mode);
        await using var _ = browser;
        var open = page.Locator("[popover]:popover-open ib-period-grid");

        await example.GetByRole(AriaRole.Button, new() { Name = "Change month, March 2027" }).ClickAsync();
        await open.GetByRole(AriaRole.Button, new() { Name = "July 2027" }).ClickAsync();
        await Assertions.Expect(open).ToHaveCountAsync(0);

        await example.GetByRole(AriaRole.Button, new() { Name = "Choose year" }).ClickAsync();
        await open.GetByRole(AriaRole.Button, new() { Name = "2025", Exact = true }).ClickAsync();

        await example.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(example).ToContainTextAsync("Starts July 2027, model year 2025.");
    }

    [Fact]
    public async Task With_no_script_the_browsers_inputs_post_them()
    {
        var (browser, page, example) = await OpenAsync("noscript");
        await using var _ = browser;

        await example.Locator("input[type=month]").FillAsync("2027-05");
        await example.GetByLabel("Model year").FillAsync("2029");
        await example.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();

        await Assertions.Expect(example).ToContainTextAsync("Starts May 2027, model year 2029.");
    }
}
