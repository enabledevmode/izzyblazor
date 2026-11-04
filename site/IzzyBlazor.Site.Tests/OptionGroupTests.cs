using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>Typing in a grouped Combobox hides the groups with no match, and the choice posts.</summary>
public sealed class OptionGroupTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Fact]
    public async Task Typing_hides_groups_with_no_match_and_the_choice_posts_on_a_static_page()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/combobox?mode=static").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var example = page.Locator("[data-ex='combobox-groups']");
        await example.Locator("ib-combobox[data-ready]").WaitForAsync();
        var owner = example.GetByRole(AriaRole.Combobox);

        await owner.FillAsync("");
        await owner.PressSequentiallyAsync("hamil");
        await Assertions.Expect(example.GetByRole(AriaRole.Group)).ToHaveCountAsync(1);
        await Assertions.Expect(example.GetByRole(AriaRole.Group, new() { Name = "Flight software" })).ToBeVisibleAsync();
        await owner.PressAsync("Enter");
        await Assertions.Expect(owner).ToHaveValueAsync("Margaret Hamilton");
        await example.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(example).ToContainTextAsync("Saved: Margaret Hamilton.");
    }
}
