using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A month and a year chosen from their pages reach the model.</summary>
public sealed class DatePeriodTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private async Task<(IAsyncDisposable Browser, IPage Page)> OpenAsync()
    {
        var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 }, Locale = "en-US" });
        return (browser, page);
    }

    private async Task<ILocator> GoToAsync(IPage page, string component, string example, string mode)
    {
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/{component}?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        return page.Locator($"[data-ex='{example}']");
    }

    [Theory]
    [InlineData("static")]
    public async Task A_month_and_a_year_chosen_from_their_pages_save(string mode)
    {
        var (browser, page) = await OpenAsync();
        await using var _ = browser;
        var open = page.Locator("[popover]:popover-open ib-period-grid");

        var month = await GoToAsync(page, "month-field", "month-field-usage", mode);
        await month.GetByRole(AriaRole.Button, new() { Name = "Change month, March 2027" }).ClickAsync();
        await open.GetByRole(AriaRole.Button, new() { Name = "July 2027" }).ClickAsync();
        await Assertions.Expect(open).ToHaveCountAsync(0);
        await month.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(month).ToContainTextAsync("Starts July 2027.");

        var year = await GoToAsync(page, "year-field", "year-field-usage", mode);
        await year.GetByRole(AriaRole.Button, new() { Name = "Choose year" }).ClickAsync();
        await open.GetByRole(AriaRole.Button, new() { Name = "2025", Exact = true }).ClickAsync();
        await year.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(year).ToContainTextAsync("Model year 2025.");
    }

    [Fact]
    public async Task With_no_script_the_browsers_inputs_post_them()
    {
        var (browser, page) = await OpenAsync();
        await using var _ = browser;

        var month = await GoToAsync(page, "month-field", "month-field-usage", "noscript");
        await month.Locator("input[type=month]").FillAsync("2027-05");
        await month.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(month).ToContainTextAsync("Starts May 2027.");

        var year = await GoToAsync(page, "year-field", "year-field-usage", "noscript");
        await year.GetByLabel("Model year").FillAsync("2029");
        await year.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(year).ToContainTextAsync("Model year 2029.");
    }
}
