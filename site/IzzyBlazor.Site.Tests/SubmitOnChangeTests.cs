using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>SubmitOnChange posts a field's form as it changes, with no button, on each kind of page.</summary>
public sealed class SubmitOnChangeTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private async Task<(ILocator Example, TestBrowser Browser)> OpenAsync(string component, string example, string mode)
    {
        var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 1000 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/{component}?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await TestBrowser.WaitForLiveAsync(page);
        return (page.Locator($"[data-ex='{example}']"), browser);
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task A_select_saves_the_choice_as_its_made(string mode)
    {
        var (example, browser) = await OpenAsync("select", "select-submit-on-change", mode);
        await using var _ = browser;

        await example.GetByLabel("Stage").SelectOptionAsync("Proposal");

        await Assertions.Expect(example).ToContainTextAsync("Last saved: Proposal.");
        await Assertions.Expect(example.GetByLabel("Stage")).ToHaveValueAsync("Proposal");
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task A_switch_saves_as_its_switched(string mode)
    {
        var (example, browser) = await OpenAsync("switch", "switch-save-on-change", mode);
        await using var _ = browser;

        // The label covers the box, as it does for a person.
        await example.Locator("label.ib-check").ClickAsync();

        await Assertions.Expect(example).ToContainTextAsync("Last saved: in maintenance mode.");
    }
}
