using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>
/// ListView's arrow keys, in Chromium: a shortcut on top of the Tab order, on the docs page's examples.
/// </summary>
public sealed class ListViewKeysTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private async Task<(TestBrowser, IPage)> OpenAsync(string mode, string topic = "")
    {
        var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 } });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/list-view{topic}?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        // Interactive: the lists render this once the circuit is up and their boxes answer.
        if (mode == "interactive") await page.Locator("ib-listview[data-ib-interactive]").First.WaitForAsync();
        return (browser, page);
    }

    private static Task<string?> FocusedNameAsync(IPage page) =>
        page.EvaluateAsync<string?>("() => { const a = document.activeElement; return a.getAttribute('aria-label') ?? a.textContent?.trim(); }");

    [Theory]
    [InlineData("static")]
    public async Task Arrows_go_between_the_same_kind_of_control(string mode)
    {
        var (browser, page) = await OpenAsync(mode);
        await using (browser)
        {
            var links = page.Locator("ib-listview", new() { Has = page.GetByRole(AriaRole.Link, new() { Name = "Ada Lovelace" }) });
            await links.GetByRole(AriaRole.Link, new() { Name = "Ada Lovelace" }).FocusAsync();

            await page.Keyboard.PressAsync("ArrowDown");
            Assert.Equal("Grace Hopper", await FocusedNameAsync(page));
            await page.Keyboard.PressAsync("End");
            Assert.Equal("Katherine Johnson", await FocusedNameAsync(page));

            // At the end the key does what it did, and focus stays.
            await page.Keyboard.PressAsync("ArrowDown");
            Assert.Equal("Katherine Johnson", await FocusedNameAsync(page));

            await page.Keyboard.PressAsync("Home");
            Assert.Equal("Ada Lovelace", await FocusedNameAsync(page));

            // Tab order isn't touched: each link is still a stop.
            Assert.Equal(0, await links.GetByRole(AriaRole.Link, new() { Name = "Grace Hopper" }).EvaluateAsync<int>("e => e.tabIndex"));

            // ItemActions: Star to Star, past the ⋯ menu, which keeps its own arrows.
            var star = page.GetByRole(AriaRole.Button, new() { NameRegex = new("^Star ") });
            var firstName = await star.First.GetAttributeAsync("aria-label");
            var secondName = await star.Nth(1).GetAttributeAsync("aria-label");
            await star.First.FocusAsync();
            await page.Keyboard.PressAsync("ArrowDown");
            Assert.NotEqual(firstName, secondName);
            Assert.Equal(secondName, await FocusedNameAsync(page));

            var menu = page.GetByRole(AriaRole.Button, new() { NameRegex = new("^Actions for ") }).First;
            await menu.FocusAsync();
            await page.Keyboard.PressAsync("ArrowDown");
            await page.GetByRole(AriaRole.Menu).First.WaitForAsync();
            await page.Keyboard.PressAsync("Escape");
        }
    }

    [Theory]
    [InlineData("static")]
    public async Task Shift_arrows_and_ctrl_a_select_from_a_checkbox(string mode)
    {
        var (browser, page) = await OpenAsync(mode, "/selection");
        await using (browser)
        {
            var list = page.Locator("ib-listview", new() { Has = page.GetByRole(AriaRole.Checkbox, new() { Name = "Select all messages" }) });
            var boxes = list.Locator(".ib-listview-select");
            Assert.True(await boxes.CountAsync() >= 3);
            await boxes.First.FocusAsync();

            // Plain arrows move and never select.
            await page.Keyboard.PressAsync("ArrowDown");
            await Assertions.Expect(boxes.First).Not.ToBeCheckedAsync();
            Assert.Equal(await boxes.Nth(1).GetAttributeAsync("aria-label"), await FocusedNameAsync(page));
            await page.Keyboard.PressAsync("ArrowUp");

            // Shift+Down selects from where it started; going back undoes what the run checked.
            await page.Keyboard.PressAsync("Shift+ArrowDown");
            await Assertions.Expect(boxes.First).ToBeCheckedAsync();
            await Assertions.Expect(boxes.Nth(1)).ToBeCheckedAsync();
            await Assertions.Expect(boxes.Nth(2)).Not.ToBeCheckedAsync();
            // The next key goes from here, once the render has settled and focus has arrived.
            await Assertions.Expect(boxes.Nth(1)).ToBeFocusedAsync();
            await page.Keyboard.PressAsync("Shift+ArrowDown");
            await Assertions.Expect(boxes.Nth(2)).ToBeCheckedAsync();
            await Assertions.Expect(boxes.Nth(2)).ToBeFocusedAsync();
            await page.Keyboard.PressAsync("Shift+ArrowUp");
            await Assertions.Expect(boxes.Nth(2)).Not.ToBeCheckedAsync();
            await Assertions.Expect(boxes.Nth(1)).ToBeCheckedAsync();

            // Ctrl+A selects the page.
            await page.Keyboard.PressAsync("Control+a");
            var count = await boxes.CountAsync();
            for (var i = 0; i < count; i++) await Assertions.Expect(boxes.Nth(i)).ToBeCheckedAsync();
        }
    }
}
