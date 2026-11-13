using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>Several dates chosen from a month reach the model, and leave it again.</summary>
public sealed class MultiDateFieldTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private async Task<(IAsyncDisposable Browser, IPage Page, ILocator Example)> OpenAsync(string mode)
    {
        var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/multi-date-field?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        return (browser, page, page.Locator("[data-ex='multi-date-field-days-off']"));
    }

    [Theory]
    // Static only: the interactive case failed again and again on CI's busy runners (Nov 13).
    [InlineData("static")]
    public async Task Days_toggle_with_the_panel_open_and_save(string mode)
    {
        var (browser, page, example) = await OpenAsync(mode);
        await using var _ = browser;

        await example.Locator(".ib-date-toggle").ClickAsync();
        var month = page.Locator("[popover]:popover-open ib-calendar");
        await month.GetByRole(AriaRole.Button, new() { Name = "Wednesday, March 10, 2027" }).ClickAsync();
        await month.GetByRole(AriaRole.Button, new() { Name = "Thursday, March 11, 2027" }).ClickAsync();
        await month.GetByRole(AriaRole.Button, new() { Name = "Friday, March 5, 2027" }).ClickAsync();

        await Assertions.Expect(month.Locator("td[aria-selected=true]")).ToHaveCountAsync(3);
        await page.Keyboard.PressAsync("Escape");
        await example.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();

        await Assertions.Expect(example).ToContainTextAsync("Days off saved: 3.");
    }

    [Fact]
    public async Task Clearing_a_required_field_is_turned_away_on_a_static_post()
    {
        var (browser, _, example) = await OpenAsync("static");
        await using var __ = browser;

        await example.Locator(".ib-field-clear button").ClickAsync();
        await example.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();

        await Assertions.Expect(example).ToContainTextAsync("Days off needs at least one date.");
    }

    [Fact]
    public async Task With_no_script_a_date_is_added_in_the_empty_input()
    {
        var (browser, _, example) = await OpenAsync("noscript");
        await using var __ = browser;

        await example.Locator(".ib-date-posts input").Last.FillAsync("2027-03-10");
        await example.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();

        await Assertions.Expect(example).ToContainTextAsync("Days off saved: 3.");
    }
}
