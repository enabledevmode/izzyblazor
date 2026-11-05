using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A Menu with ContextFor opens at the pointer on a right-click in its element, and its items work.</summary>
public sealed class MenuContextTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task Right_clicking_a_card_opens_its_menu_there_and_an_item_acts_on_that_card(string mode)
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/menu?mode={mode}").ToString());
        var example = page.Locator("[data-ex='menu-context']");
        // Ready: the element that opens it has loaded, and (interactive) the circuit has rendered the menus.
        await example.Locator(mode == "interactive" ? "ib-popover[data-ib-interactive] ib-context-menu" : "ib-context-menu").First.WaitForAsync(new() { State = WaitForSelectorState.Attached });
        await page.WaitForFunctionAsync("() => customElements.get('ib-context-menu') !== undefined");

        var card = example.Locator("#file-2");
        await card.ScrollIntoViewIfNeededAsync();
        var box = (await card.BoundingBoxAsync())!;
        await page.Mouse.ClickAsync(box.X + 20, box.Y + box.Height / 2, new() { Button = MouseButton.Right });
        var menu = example.Locator("[popover]:popover-open");
        await Assertions.Expect(menu).ToBeVisibleAsync();
        var at = (await menu.BoundingBoxAsync())!;
        Assert.InRange(at.X, box.X + 18, box.X + 22);

        await menu.GetByText("Delete").ClickAsync();
        await Assertions.Expect(example).ToContainTextAsync("Deleted Budget 2027.xlsx.");
    }
}
