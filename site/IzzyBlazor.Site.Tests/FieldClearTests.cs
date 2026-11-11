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
    [InlineData("text-area", "textarea", "static")]
    [InlineData("text-area", "textarea", "interactive")]
    [InlineData("select", "select", "static")]
    [InlineData("select", "select", "interactive")]
    [InlineData("combobox", ".ib-combobox-input", "static")]
    [InlineData("duration-field", ".ib-duration-text input", "static")]
    [InlineData("duration-field", ".ib-duration-text input", "interactive")]
    public async Task It_empties_every_other_kind_of_field(string component, string holder, string mode)
    {
        var (example, browser) = await OpenAsync(component, $"{component}-states", mode);
        await using var _ = browser;
        var clear = example.Locator("ib-field-clear").First;
        var value = clear.Locator("xpath=ancestor::*[contains(@class,'ib-field')][1]/ancestor-or-self::*[contains(concat(' ',@class,' '),' ib-field ')][1]").Locator(holder).First;
        Assert.NotEqual("", await value.InputValueAsync());

        await clear.Locator("button").ClickAsync();

        await Assertions.Expect(value).ToHaveValueAsync("");
        await Assertions.Expect(clear).ToBeHiddenAsync();
    }
}
