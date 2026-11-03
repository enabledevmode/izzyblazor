using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>
/// SplitButton in Chromium, on its docs page and the Toolbar's: the keys, repeatedly and across
/// renders and posts, on a static page and an interactive one; state changes on a real page; the
/// Toolbar's "…" menu; and with JavaScript off, what still works.
/// </summary>
public sealed class SplitButtonTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private const string Focused = "() => (document.activeElement?.textContent ?? '').trim() || (document.activeElement?.getAttribute('aria-label') ?? '')";

    private static string Example(string id) => $"[data-ex=\"{id}\"]";

    private async Task<IPage> OpenAsync(TestBrowser browser, string path, string mode, int width = 1400, bool scripts = true)
    {
        var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = width, Height = 900 }, JavaScriptEnabled = scripts, AcceptDownloads = true });
        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"{path}?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        return page;
    }

    // The script has set up a static example, or the circuit an interactive one.
    private static Task ReadyAsync(IPage page, string example, string mode) =>
        page.WaitForSelectorAsync($"{Example(example)} ib-popover{(mode == "interactive" ? "[data-ib-interactive]" : "")} > [popover][role=menu]", new() { State = WaitForSelectorState.Attached });

    private static async Task<bool> OpenPanelAsync(ILocator trigger) =>
        await trigger.EvaluateAsync<bool>("t => document.getElementById(t.getAttribute('aria-controls')).matches(':popover-open')");

    private static async Task AssertFocusAsync(IPage page, string name) =>
        await page.WaitForFunctionAsync($"() => (({Focused})()) === {System.Text.Json.JsonSerializer.Serialize(name)}", null, new() { Timeout = 5000 });

    // Moves through an open menu to the item with this name.
    private static async Task MoveToAsync(IPage page, string name)
    {
        var seen = new List<string>();
        for (var i = 0; i < 10; i++)
        {
            seen.Add(await page.EvaluateAsync<string>(Focused));
            if (seen[^1] == name)
            {
                return;
            }

            await page.Keyboard.PressAsync("ArrowDown");
        }

        Assert.Fail($"{name} not reached: {string.Join(" > ", seen)}");
    }

    private static async Task AssertTextAsync(IPage page, string example, string text) =>
        await Assertions.Expect(page.Locator(Example(example)).GetByText(text, new() { Exact = true })).ToBeVisibleAsync();

    [Theory]
    [InlineData("static")]
    public async Task The_two_parts_are_two_tab_stops_and_the_arrow_opens_with_its_keys(string mode)
    {
        var browser = await TestBrowser.OpenAsync();
        await using (browser)
        {
            var page = await OpenAsync(browser, "docs/components/split-button", mode);
            const string ex = "split-button-actions";
            await ReadyAsync(page, ex, mode);
            var toggle = page.Locator(Example(ex)).GetByRole(AriaRole.Button, new() { Name = "More save options" });

            await page.Locator("#split-save").FocusAsync();
            await page.Keyboard.PressAsync("Tab");
            await AssertFocusAsync(page, "More save options");
            await page.Keyboard.PressAsync("Shift+Tab");
            await AssertFocusAsync(page, "Save");

            for (var i = 0; i < 3; i++)
            {
                await toggle.FocusAsync();
                await page.Keyboard.PressAsync("Enter");
                await AssertFocusAsync(page, "Save as draft");
                await page.Keyboard.PressAsync("Escape");
                await AssertFocusAsync(page, "More save options");

                await page.Keyboard.PressAsync("Space");
                await AssertFocusAsync(page, "Save as draft");
                await page.Keyboard.PressAsync("Escape");
                await AssertFocusAsync(page, "More save options");

                await page.Keyboard.PressAsync("ArrowDown");
                await AssertFocusAsync(page, "Save as draft");
                await page.Keyboard.PressAsync("Escape");

                await page.Keyboard.PressAsync("ArrowUp");
                await AssertFocusAsync(page, "Open drafts");
                await page.Keyboard.PressAsync("Tab");
                Assert.False(await OpenPanelAsync(toggle));
            }

            // A click on the main button runs it and doesn't open the menu.
            await page.Locator("#split-save").ClickAsync();
            await AssertTextAsync(page, ex, "Posted: save");
            await ReadyAsync(page, ex, mode);
            Assert.False(await OpenPanelAsync(toggle));

            // On the arrow it opens, and again closes it.
            await toggle.ClickAsync();
            Assert.True(await OpenPanelAsync(toggle));
            await toggle.ClickAsync();
            Assert.False(await OpenPanelAsync(toggle));
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task Each_post_says_what_ran_and_the_keys_still_work_after_it(string mode)
    {
        var browser = await TestBrowser.OpenAsync();
        await using (browser)
        {
            var page = await OpenAsync(browser, "docs/components/split-button", mode);
            const string ex = "split-button-actions";
            await ReadyAsync(page, ex, mode);

            for (var i = 0; i < 3; i++)
            {
                await page.Locator("#split-save").ClickAsync();
                await AssertTextAsync(page, ex, "Posted: save");
                await ReadyAsync(page, ex, mode);

                // Choosing an item runs it; the main action stays as it was.
                await page.Locator("#split-save").FocusAsync();
                await page.Keyboard.PressAsync("Alt+ArrowDown");
                await AssertFocusAsync(page, "Save as draft");
                await page.Keyboard.PressAsync("Enter");
                await AssertTextAsync(page, ex, "Posted: draft");
                await ReadyAsync(page, ex, mode);
                Assert.Equal("Save", (await page.Locator("#split-save").TextContentAsync())?.Trim());

                await page.Locator("#split-save").FocusAsync();
                await page.Keyboard.PressAsync("Enter");
                await AssertTextAsync(page, ex, "Posted: save");
                await ReadyAsync(page, ex, mode);

                await page.Locator("#split-save").FocusAsync();
                await page.Keyboard.PressAsync("Alt+ArrowDown");
                await AssertFocusAsync(page, "Save as draft");
                await page.Keyboard.PressAsync("ArrowDown");
                await page.Keyboard.PressAsync("Enter");
                await AssertTextAsync(page, ex, "Posted: close");
                await ReadyAsync(page, ex, mode);
            }
        }
    }

    [Fact]
    public async Task OnClick_runs_again_and_again_and_focus_comes_back_through_renders()
    {
        var browser = await TestBrowser.OpenAsync();
        await using (browser)
        {
            var page = await OpenAsync(browser, "docs/components/split-button", "interactive");
            const string ex = "split-button-on-click";
            await ReadyAsync(page, ex, "interactive");
            var run = page.Locator("#split-run");
            var count = 0;

            for (var i = 0; i < 3; i++)
            {
                await run.FocusAsync();
                await page.Keyboard.PressAsync("Enter");
                await AssertTextAsync(page, ex, $"Run ({++count})");

                await page.Keyboard.PressAsync("Alt+ArrowDown");
                await AssertFocusAsync(page, "Run tests");
                await page.Keyboard.PressAsync("Enter");
                await AssertTextAsync(page, ex, $"Run tests ({++count})");
                await AssertFocusAsync(page, "Run");

                await page.Keyboard.PressAsync("Tab");
                await AssertFocusAsync(page, "More ways to run");
                await page.Keyboard.PressAsync("ArrowUp");
                await AssertFocusAsync(page, "Run all");
                await page.Keyboard.PressAsync("Enter");
                await AssertTextAsync(page, ex, $"Run all ({++count})");
                await AssertFocusAsync(page, "More ways to run");
            }
        }
    }

    [Theory]
    [InlineData("static")]
    public async Task Arrow_keys_move_between_the_two_parts_in_a_toolbar(string mode)
    {
        var browser = await TestBrowser.OpenAsync();
        await using (browser)
        {
            var page = await OpenAsync(browser, "docs/components/toolbar/actions", mode);
            const string ex = "toolbar-split-button";
            await ReadyAsync(page, ex, mode);
            var bar = page.Locator(Example(ex)).Locator("ib-toolbar").First;
            await page.WaitForFunctionAsync("() => [...document.querySelectorAll('[data-ex=\"toolbar-split-button\"] ib-toolbar')].every(t => t.hasAttribute('data-ib-measured'))");

            for (var i = 0; i < 3; i++)
            {
                await page.Locator("#toolbar-save").FocusAsync();
                await page.Keyboard.PressAsync("ArrowRight");
                await AssertFocusAsync(page, "More save options");
                await page.Keyboard.PressAsync("ArrowRight");
                await AssertFocusAsync(page, "Publish");
                await page.Keyboard.PressAsync("ArrowLeft");
                await page.Keyboard.PressAsync("ArrowLeft");
                await AssertFocusAsync(page, "Save");

                // Alt+Down is the split button's, and Escape comes back to it.
                await page.Keyboard.PressAsync("Alt+ArrowDown");
                await AssertFocusAsync(page, "Save as draft");
                await page.Keyboard.PressAsync("Escape");
                await AssertFocusAsync(page, "Save");
            }

            // One tab stop: Tab leaves the toolbar, from either part.
            const string inBar = "() => document.activeElement.closest('ib-toolbar') === document.querySelector('[data-ex=\"toolbar-split-button\"] ib-toolbar')";
            await page.Locator("#toolbar-save").FocusAsync();
            await page.Keyboard.PressAsync("Tab");
            Assert.False(await page.EvaluateAsync<bool>(inBar));

            await page.Locator("#toolbar-save").FocusAsync();
            await page.Keyboard.PressAsync("ArrowRight");
            await AssertFocusAsync(page, "More save options");
            await page.Keyboard.PressAsync("Tab");
            Assert.False(await page.EvaluateAsync<bool>(inBar));
        }
    }

    [Theory]
    [InlineData("static")]
    public async Task In_the_toolbars_menu_it_is_a_submenu_with_the_main_action_first(string mode)
    {
        var browser = await TestBrowser.OpenAsync();
        await using (browser)
        {
            // Narrow enough for Save to move.
            var page = await OpenAsync(browser, "docs/components/toolbar/actions", mode, width: 380);
            const string ex = "toolbar-split-button";
            await ReadyAsync(page, ex, mode);
            await page.WaitForFunctionAsync("() => document.querySelector('[data-ex=\"toolbar-split-button\"] ib-toolbar').hasAttribute('data-ib-measured')");
            // A font arriving late measures again, which can bring an item back to the bar under its menu.
            await page.EvaluateAsync("() => document.fonts.ready");
            var bar = page.Locator(Example(ex)).Locator("ib-toolbar").First;
            await Assertions.Expect(page.Locator("#toolbar-save")).ToBeHiddenAsync();
            var more = bar.Locator(".ib-toolbar-more [popovertarget]").First;

            async Task ToSubmenu()
            {
                await more.FocusAsync();
                await page.Keyboard.PressAsync("Enter");
                await page.WaitForFunctionAsync("() => document.activeElement.closest('[popover]')");
                await MoveToAsync(page, "More save options");
                await page.Keyboard.PressAsync("ArrowRight");
                await AssertFocusAsync(page, "Save");
            }

            for (var i = 0; i < 3; i++)
            {
                await ToSubmenu();

                // The main action, then the items, wrapping; Left comes back to the submenu's item.
                await page.Keyboard.PressAsync("ArrowDown");
                await AssertFocusAsync(page, "Save as draft");
                await page.Keyboard.PressAsync("ArrowDown");
                await AssertFocusAsync(page, "Open drafts");
                await page.Keyboard.PressAsync("ArrowDown");
                await AssertFocusAsync(page, "Save");
                await page.Keyboard.PressAsync("ArrowLeft");
                await AssertFocusAsync(page, "More save options");

                await page.Keyboard.PressAsync("Escape");
                await page.WaitForFunctionAsync("() => !document.querySelector('[data-ex=\"toolbar-split-button\"] ib-toolbar .ib-toolbar-more [popover]').matches(':popover-open')");

                // Choosing the main action and an item each runs it, and closes the whole menu.
                await ToSubmenu();
                await page.Keyboard.PressAsync("Enter");
                await AssertTextAsync(page, ex, "Posted: save");
                await page.WaitForFunctionAsync("() => !document.querySelector('[data-ex=\"toolbar-split-button\"] ib-toolbar .ib-toolbar-more [popover]').matches(':popover-open')");
                await page.WaitForFunctionAsync("() => document.querySelector('[data-ex=\"toolbar-split-button\"] ib-toolbar').hasAttribute('data-ib-measured')");
                await ReadyAsync(page, ex, mode);

                await ToSubmenu();
                await page.Keyboard.PressAsync("ArrowDown");
                await page.Keyboard.PressAsync("Enter");
                await AssertTextAsync(page, ex, "Posted: draft");
                await page.WaitForFunctionAsync("() => document.querySelector('[data-ex=\"toolbar-split-button\"] ib-toolbar').hasAttribute('data-ib-measured')");
                await ReadyAsync(page, ex, mode);
            }

            // Export has Overflow="Always": a submenu whose first item is the same link, with download.
            await more.ClickAsync();
            var export = bar.Locator(".ib-toolbar-menu-unit").Filter(new() { HasText = "Export formats" });
            await export.Locator(".ib-menu-sub-trigger").ClickAsync();
            var first = export.Locator(".ib-menu-sub > a").First;
            await Assertions.Expect(first).ToBeVisibleAsync();
            Assert.Equal("Export", (await first.TextContentAsync())?.Trim());
            Assert.Equal("izzy.svg", await first.GetAttributeAsync("download"));
            var download = page.WaitForDownloadAsync();
            await first.ClickAsync();
            Assert.Equal("izzy.svg", (await download).SuggestedFilename);
        }
    }

    [Fact]
    public async Task Stays_in_the_bar_when_there_is_room_and_collapses_before_it_moves()
    {
        var browser = await TestBrowser.OpenAsync();
        await using (browser)
        {
            var page = await OpenAsync(browser, "docs/components/toolbar/actions", "static");
            const string ex = "toolbar-split-button";
            await ReadyAsync(page, ex, "static");
            await page.WaitForFunctionAsync("() => [...document.querySelectorAll('[data-ex=\"toolbar-split-button\"] ib-toolbar')].every(t => t.hasAttribute('data-ib-measured'))");
            var section = page.Locator(Example(ex));
            var bars = section.Locator("ib-toolbar");

            await Assertions.Expect(page.Locator("#toolbar-save")).ToBeVisibleAsync();
            await Assertions.Expect(bars.First.Locator(".ib-toolbar-unit").Filter(new() { HasText = "Export" })).ToBeHiddenAsync();

            // Narrower: the label goes and the name stays, as a tooltip too, with the arrow.
            var share = bars.Nth(1).Locator(".ib-split-btn").First;
            await share.WaitForAsync();
            await page.SetViewportSizeAsync(420, 900);
            await page.WaitForFunctionAsync("() => document.querySelectorAll('[data-ex=\"toolbar-split-button\"] ib-toolbar')[1].hasAttribute('data-ib-collapsed')");
            Assert.Equal("absolute", await share.Locator(".ib-btn-label").First.EvaluateAsync<string>("l => getComputedStyle(l).position"));
            Assert.Equal("Share", await share.Locator("> .ib-btn").GetAttributeAsync("title"));
            await Assertions.Expect(share.GetByRole(AriaRole.Button, new() { Name = "Share options" })).ToBeVisibleAsync();
        }
    }

    // The page mustn't move: Chromium scrolls on Alt+Down unless something stops it.
    private static async Task AssertStaysPutAsync(IPage page, string keys = "Alt+ArrowDown")
    {
        var before = await page.EvaluateAsync<double>("() => scrollY");
        Assert.True(before > 0, "the page should be scrolled for this to mean anything");
        await page.Keyboard.PressAsync(keys);
        await page.WaitForTimeoutAsync(150);
        Assert.Equal(before, await page.EvaluateAsync<double>("() => scrollY"));
    }

    private static async Task FocusScrolledAsync(IPage page, ILocator target)
    {
        await target.EvaluateAsync("t => { t.focus(); t.scrollIntoView({ block: 'center' }); }");
        await page.EvaluateAsync("() => scrollBy(0, 40)");
    }

    [Fact]
    public async Task With_JavaScript_off_the_toolbars_menu_still_works()
    {
        var browser = await TestBrowser.OpenAsync();
        await using (browser)
        {
            var page = await OpenAsync(browser, "docs/components/toolbar/actions", "noscript", width: 380, scripts: false);
            const string ex = "toolbar-split-button";
            var bar = page.Locator(Example(ex)).Locator("ib-toolbar").First;
            await Assertions.Expect(page.Locator("#toolbar-save")).ToBeHiddenAsync();

            foreach (var (name, posted) in new[] { ("Save", "Posted: save"), ("Save as draft", "Posted: draft") })
            {
                await bar.Locator(".ib-toolbar-more [popovertarget]").First.ClickAsync();
                var unit = bar.Locator(".ib-toolbar-menu-unit").Filter(new() { HasText = "More save options" });
                await unit.Locator(".ib-menu-sub-trigger").ClickAsync();
                await unit.GetByRole(AriaRole.Button, new() { Name = name, Exact = true }).ClickAsync();
                await AssertTextAsync(page, ex, posted);
            }
        }
    }
}
