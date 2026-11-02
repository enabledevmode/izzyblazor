using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A field's clear button empties it, on each kind of page and each kind of field.</summary>
public sealed class FieldClearTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private async Task<(ILocator Example, TestBrowser Browser)> OpenAsync(string component, string example, string mode)
    {
        var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 1000 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/{component}?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var found = page.Locator($"[data-ex='{example}']");
        await found.Locator("ib-field-clear[data-ready]").First.WaitForAsync(new() { State = WaitForSelectorState.Attached });
        return (found, browser);
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task It_empties_a_field_takes_itself_away_and_comes_back_with_a_value(string mode)
    {
        var (example, browser) = await OpenAsync("text-field", "text-field-clearable", mode);
        await using var _ = browser;
        var name = example.GetByLabel("Name");
        var clear = example.Locator("ib-field-clear button").First;

        await clear.ClickAsync();

        await Assertions.Expect(name).ToHaveValueAsync("");
        await Assertions.Expect(name).ToBeFocusedAsync();
        await Assertions.Expect(clear).ToBeHiddenAsync();

        await name.PressSequentiallyAsync("Grace");
        await Assertions.Expect(clear).ToBeVisibleAsync();
        await Assertions.Expect(name).ToHaveValueAsync("Grace");
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task It_empties_both_ends_of_a_range(string mode)
    {
        var (example, browser) = await OpenAsync("date-range-field", "date-range-field-states", mode);
        await using var _ = browser;
        var (start, end) = (example.GetByLabel("From"), example.GetByLabel("Until"));
        await start.FillAsync("2027-03-08");
        await end.FillAsync("2027-03-12");

        await example.Locator("ib-field-clear button").First.ClickAsync();

        await Assertions.Expect(start).ToHaveValueAsync("");
        await Assertions.Expect(end).ToHaveValueAsync("");
    }

    [Theory]
    [InlineData("date-field", "date-field-days", "Delivery")]
    [InlineData("time-field", "time-field-slots", "Visit")]
    public async Task A_date_or_time_has_one_beside_its_button(string component, string exampleId, string label)
    {
        var (example, browser) = await OpenAsync(component, exampleId, "static");
        await using var _ = browser;
        var input = example.GetByLabel(label, new() { Exact = true });
        var clear = example.Locator("ib-field-clear button").First;
        var (button, toggle) = ((await clear.BoundingBoxAsync())!, (await example.Locator(".ib-date-toggle").First.BoundingBoxAsync())!);
        Assert.True(button.X + button.Width <= toggle.X + 1, $"{button.X}+{button.Width} against {toggle.X}");

        await clear.ClickAsync();

        await Assertions.Expect(input).ToHaveValueAsync("");
    }

    [Fact]
    public async Task With_no_script_it_stays_out_of_the_way()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { JavaScriptEnabled = false, ViewportSize = new() { Width = 1400, Height = 1000 } });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/text-field?mode=noscript").ToString());

        await Assertions.Expect(page.Locator("[data-ex='text-field-clearable'] ib-field-clear").First).ToBeHiddenAsync();
    }
}
