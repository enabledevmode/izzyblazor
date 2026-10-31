using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>
/// ListView's arrow keys, in Chromium: a shortcut on top of the Tab order, on the docs page's examples.
/// </summary>
public sealed class ListViewKeysTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private async Task<(TestBrowser, IPage)> OpenAsync(string mode)
    {
        var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 } });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/list-view?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        // Interactive: the lists render this once the circuit is up and their boxes answer.
        if (mode == "interactive") await page.Locator("ib-listview[data-ib-interactive]").First.WaitForAsync();
        return (browser, page);
    }

    private static Task<string?> FocusedNameAsync(IPage page) =>
        page.EvaluateAsync<string?>("() => { const a = document.activeElement; return a.getAttribute('aria-label') ?? a.textContent?.trim(); }");

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
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

    [Fact]
    public async Task Cards_take_all_four_arrows()
    {
        var (browser, page) = await OpenAsync("static");
        await using (browser)
        {
            var cards = page.Locator("ib-listview.ib-listview-cards", new() { Has = page.Locator(".ib-listview-action") }).First.Locator(".ib-listview-action");
            Assert.True(await cards.CountAsync() >= 4);
            static Task<bool> FocusedAsync(ILocator el) => el.EvaluateAsync<bool>("e => e === document.activeElement");

            await cards.First.FocusAsync();
            await page.Keyboard.PressAsync("ArrowRight");
            Assert.True(await FocusedAsync(cards.Nth(1)));
            await page.Keyboard.PressAsync("ArrowLeft");
            Assert.True(await FocusedAsync(cards.First));
            // Three across: Down is the card under it, not the next in order.
            await page.Keyboard.PressAsync("ArrowDown");
            Assert.True(await FocusedAsync(cards.Nth(3)));
        }
    }

    [Fact]
    public async Task A_focused_card_is_wholly_in_view()
    {
        var (browser, page) = await OpenAsync("static");
        await using (browser)
        {
            await page.SetViewportSizeAsync(390, 500);
            var cards = page.Locator("ib-listview.ib-listview-cards", new() { Has = page.Locator(".ib-listview-action") }).First.Locator(".ib-listview-action");
            await cards.First.FocusAsync();
            // Going down the phone's one column, then back up under the sticky header.
            var count = await cards.CountAsync();
            for (var i = 1; i < count; i++) await AssertInViewAsync("ArrowDown");
            for (var i = 1; i < count; i++) await AssertInViewAsync("ArrowUp");

            async Task AssertInViewAsync(string key)
            {
                await page.Keyboard.PressAsync(key);
                var box = await page.EvaluateAsync<double[]>("""
                    () => {
                      const r = document.activeElement.closest('.ib-listview-item').getBoundingClientRect();
                      const header = [...document.querySelectorAll('header')].map(h => h.getBoundingClientRect()).filter(h => h.top <= 0 && h.bottom > 0);
                      return [r.top, r.bottom, Math.max(0, ...header.map(h => h.bottom)), innerHeight];
                    }
                    """);
                Assert.True(box[0] >= box[2] - 1 && box[1] <= box[3] + 1, $"{key}: card at {box[0]}..{box[1]}, view {box[2]}..{box[3]}");
            }
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task Shift_arrows_and_ctrl_a_select_from_a_checkbox(string mode)
    {
        var (browser, page) = await OpenAsync(mode);
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
            await page.Keyboard.PressAsync("Shift+ArrowDown");
            await Assertions.Expect(boxes.Nth(2)).ToBeCheckedAsync();
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
