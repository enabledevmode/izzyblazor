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
    [InlineData("interactive")]
    public async Task Alt_Down_on_the_main_button_opens_the_menu_each_time(string mode)
    {
        var browser = await TestBrowser.OpenAsync();
        await using (browser)
        {
            var page = await OpenAsync(browser, "docs/components/split-button", mode);
            const string ex = "split-button-actions";
            await ReadyAsync(page, ex, mode);
            var main = page.Locator("#split-save");
            var toggle = page.Locator(Example(ex)).GetByRole(AriaRole.Button, new() { Name = "More save options" });

            for (var i = 0; i < 3; i++)
            {
                await main.FocusAsync();
                await page.Keyboard.PressAsync("Alt+ArrowDown");
                await AssertFocusAsync(page, "Save as draft");
                Assert.True(await OpenPanelAsync(toggle));

                // Alt+Down in the open menu does nothing more.
                await page.Keyboard.PressAsync("Alt+ArrowDown");
                await AssertFocusAsync(page, "Save as draft");

                await page.Keyboard.PressAsync("ArrowDown");
                await AssertFocusAsync(page, "Save and close");
                await page.Keyboard.PressAsync("ArrowDown");
                await AssertFocusAsync(page, "Open drafts");
                await page.Keyboard.PressAsync("ArrowDown");
                await AssertFocusAsync(page, "Save as draft");
                await page.Keyboard.PressAsync("End");
                await AssertFocusAsync(page, "Open drafts");
                await page.Keyboard.PressAsync("Home");
                await AssertFocusAsync(page, "Save as draft");

                // Escape comes back to the main button, not the arrow.
                await page.Keyboard.PressAsync("Escape");
                await AssertFocusAsync(page, "Save");
                Assert.False(await OpenPanelAsync(toggle));
            }

            // Plain Down and other modifiers on the main button don't open it.
            foreach (var key in new[] { "ArrowDown", "Control+ArrowDown", "Alt+Shift+ArrowDown", "ArrowUp" })
            {
                await main.FocusAsync();
                await page.Keyboard.PressAsync(key);
                Assert.False(await OpenPanelAsync(toggle), key);
                await AssertFocusAsync(page, "Save");
            }
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
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

    [Fact]
    public async Task Loading_blocks_both_parts_and_they_work_again_after_it()
    {
        var browser = await TestBrowser.OpenAsync();
        await using (browser)
        {
            var page = await OpenAsync(browser, "docs/components/split-button", "interactive");
            const string ex = "split-button-on-click";
            await ReadyAsync(page, ex, "interactive");
            var run = page.Locator("#split-run");
            var toggle = page.Locator(Example(ex)).GetByRole(AriaRole.Button, new() { Name = "More ways to run" });
            var loading = page.Locator(Example(ex)).GetByRole(AriaRole.Switch, new() { Name = "Loading" });

            // By script, so focus stays where it is.
            async Task SetLoading(bool on)
            {
                await loading.EvaluateAsync("s => s.click()");
                if (on)
                {
                    await Assertions.Expect(run).ToHaveAttributeAsync("aria-busy", "true");
                }
                else
                {
                    await Assertions.Expect(run).Not.ToHaveAttributeAsync("aria-busy", "true");
                }
            }

            for (var i = 0; i < 3; i++)
            {
                await toggle.FocusAsync();
                await SetLoading(true);

                // The arrow keeps focus, and nothing opens the menu from it or from the main button.
                await AssertFocusAsync(page, "More ways to run");
                Assert.Equal("true", await toggle.GetAttributeAsync("aria-disabled"));
                foreach (var key in new[] { "Enter", "Space", "ArrowDown", "ArrowUp" })
                {
                    await page.Keyboard.PressAsync(key);
                    Assert.False(await OpenPanelAsync(toggle), key);
                }

                await run.FocusAsync();
                await page.Keyboard.PressAsync("Alt+ArrowDown");
                Assert.False(await OpenPanelAsync(toggle));
                await page.Keyboard.PressAsync("Enter");
                await AssertTextAsync(page, ex, "Nothing run yet");
                Assert.Equal("split-run", await page.EvaluateAsync<string>("() => document.activeElement.id"));

                await SetLoading(false);
                Assert.Null(await toggle.GetAttributeAsync("aria-disabled"));

                // Everything's back, from both parts and with each key.
                await page.Keyboard.PressAsync("Alt+ArrowDown");
                await AssertFocusAsync(page, "Run tests");
                await page.Keyboard.PressAsync("Escape");
                await AssertFocusAsync(page, "Run");
                await toggle.FocusAsync();
                await page.Keyboard.PressAsync("ArrowDown");
                await AssertFocusAsync(page, "Run tests");
                await page.Keyboard.PressAsync("Escape");
                await AssertFocusAsync(page, "More ways to run");
                await page.Keyboard.PressAsync("Enter");
                await AssertFocusAsync(page, "Run tests");
                await page.Keyboard.PressAsync("Escape");
            }
        }
    }

    [Fact]
    public async Task A_link_gets_everything_back_when_it_is_enabled_again()
    {
        var browser = await TestBrowser.OpenAsync();
        await using (browser)
        {
            var page = await OpenAsync(browser, "docs/components/split-button", "interactive");
            const string ex = "split-button-on-click";
            await ReadyAsync(page, ex, "interactive");
            var report = page.Locator("#split-report");
            var disabled = page.Locator(Example(ex)).GetByRole(AriaRole.Switch, new() { Name = "Disabled", Exact = true });
            const string read = "a => [...a.attributes].filter(x => !x.name.startsWith('_bl')).map(x => x.name + '=' + x.value).sort()";

            var enabled = await report.EvaluateAsync<string[]>(read);
            Assert.Contains(enabled, a => a.StartsWith("href=", StringComparison.Ordinal));
            Assert.Contains("id=split-report", enabled);
            Assert.Contains(enabled, a => a.StartsWith("class=", StringComparison.Ordinal));

            for (var i = 0; i < 3; i++)
            {
                await disabled.EvaluateAsync("s => s.click()");
                await Assertions.Expect(report).ToHaveAttributeAsync("aria-disabled", "true");
                var off = await report.EvaluateAsync<string[]>(read);
                Assert.DoesNotContain(off, a => a.StartsWith("href=", StringComparison.Ordinal));
                Assert.Contains("id=split-report", off);
                Assert.Contains("role=link", off);
                Assert.Contains(off, a => a.StartsWith("class=", StringComparison.Ordinal));

                await disabled.EvaluateAsync("s => s.click()");
                await Assertions.Expect(report).Not.ToHaveAttributeAsync("aria-disabled", "true");
                Assert.Equal(enabled, await report.EvaluateAsync<string[]>(read));
            }

            // With OnClick and a link: the click runs, and it still navigates.
            await report.ClickAsync();
            await page.WaitForURLAsync("**/docs/components/menu**");
        }
    }

    [Fact]
    public async Task A_disabled_main_button_leaves_the_menu_working()
    {
        var browser = await TestBrowser.OpenAsync();
        await using (browser)
        {
            var page = await OpenAsync(browser, "docs/components/split-button", "static");
            const string ex = "split-button-states";
            await ReadyAsync(page, ex, "static");
            var section = page.Locator(Example(ex));

            var format = section.GetByRole(AriaRole.Button, new() { Name = "Choose a format" });
            await format.FocusAsync();
            await page.Keyboard.PressAsync("Enter");
            await AssertFocusAsync(page, "CSV");
            await page.Keyboard.PressAsync("Escape");
            await AssertFocusAsync(page, "Choose a format");

            await Assertions.Expect(section.GetByRole(AriaRole.Button, new() { Name = "No default" })).ToBeDisabledAsync();
            await Assertions.Expect(section.GetByRole(AriaRole.Button, new() { Name = "No other formats" })).ToBeDisabledAsync();
            await Assertions.Expect(section.GetByRole(AriaRole.Button, new() { Name = "No others", Exact = true })).ToBeEnabledAsync();
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task Arrow_keys_move_between_the_two_parts_in_a_toolbar(string mode)
    {
        var browser = await TestBrowser.OpenAsync();
        await using (browser)
        {
            var page = await OpenAsync(browser, "docs/components/toolbar", mode);
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
    [InlineData("interactive")]
    public async Task In_the_toolbars_menu_it_is_a_submenu_with_the_main_action_first(string mode)
    {
        var browser = await TestBrowser.OpenAsync();
        await using (browser)
        {
            // Narrow enough for Save to move.
            var page = await OpenAsync(browser, "docs/components/toolbar", mode, width: 380);
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
            var page = await OpenAsync(browser, "docs/components/toolbar", "static");
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

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task Alt_Down_never_scrolls_the_page(string mode)
    {
        var browser = await TestBrowser.OpenAsync();
        await using (browser)
        {
            var page = await OpenAsync(browser, "docs/components/split-button", mode);
            await ReadyAsync(page, "split-button-states", mode);
            var states = page.Locator(Example("split-button-states"));

            // On a main button whose menu can't open, and on one that opens: then again in the open menu.
            var noOthers = states.Locator(".ib-split-btn").Nth(3).Locator("> .ib-btn");
            await FocusScrolledAsync(page, noOthers);
            await AssertStaysPutAsync(page);
            Assert.Equal("No others", (await page.EvaluateAsync<string>(Focused)));
            Assert.False(await states.Locator(".ib-split-btn").Nth(3).Locator("[popover]").EvaluateAsync<bool>("p => p.matches(':popover-open')"));

            await FocusScrolledAsync(page, page.Locator("#split-save"));
            await AssertStaysPutAsync(page);
            await AssertFocusAsync(page, "Save as draft");
            await AssertStaysPutAsync(page);
            await AssertFocusAsync(page, "Save as draft");
            await page.Keyboard.PressAsync("Escape");

            // On the arrow it opens too, and comes back there.
            var arrow = page.Locator(Example("split-button-actions")).GetByRole(AriaRole.Button, new() { Name = "More save options" });
            await FocusScrolledAsync(page, arrow);
            await AssertStaysPutAsync(page);
            await AssertFocusAsync(page, "Save as draft");
            await page.Keyboard.PressAsync("Escape");
            await AssertFocusAsync(page, "More save options");

            // A plain Menu shares the keys.
            await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/menu?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
            await ReadyAsync(page, "menu-actions", mode);
            var project = page.Locator(Example("menu-actions")).Locator("ib-popover > .ib-btn");
            await FocusScrolledAsync(page, project);
            await AssertStaysPutAsync(page);
            await page.Keyboard.PressAsync("Enter");
            await page.WaitForFunctionAsync("() => document.activeElement.closest('[popover]')");
            await AssertStaysPutAsync(page);
            await AssertStaysPutAsync(page, "Alt+ArrowUp");
        }
    }

    [Fact]
    public async Task A_menu_closes_when_its_arrow_gets_blocked_so_nothing_runs_twice()
    {
        var browser = await TestBrowser.OpenAsync();
        await using (browser)
        {
            var page = await OpenAsync(browser, "docs/components/split-button", "interactive");
            const string ex = "split-button-on-click";
            await ReadyAsync(page, ex, "interactive");
            var toggle = page.Locator(Example(ex)).GetByRole(AriaRole.Button, new() { Name = "More ways to run" });
            var loading = page.Locator(Example(ex)).GetByRole(AriaRole.Switch, new() { Name = "Loading" });
            var disabled = page.Locator(Example(ex)).GetByRole(AriaRole.Switch, new() { Name = "Disabled", Exact = true });
            const string closed = "() => !document.querySelector('[data-ex=\"split-button-on-click\"] [popover]').matches(':popover-open')";

            for (var i = 0; i < 2; i++)
            {
                // From the arrow, Loading: closed, and focus back on the arrow, so Enter there runs nothing.
                await toggle.FocusAsync();
                await page.Keyboard.PressAsync("Enter");
                await AssertFocusAsync(page, "Run tests");
                await loading.EvaluateAsync("s => s.click()");
                await page.WaitForFunctionAsync(closed);
                await AssertFocusAsync(page, "More ways to run");
                await page.Keyboard.PressAsync("Enter");
                await AssertTextAsync(page, ex, "Nothing run yet");

                // From the main button: back there.
                await loading.EvaluateAsync("s => s.click()");
                await Assertions.Expect(toggle).Not.ToHaveAttributeAsync("aria-disabled", "true");
                await page.Locator("#split-run").FocusAsync();
                await page.Keyboard.PressAsync("Alt+ArrowDown");
                await AssertFocusAsync(page, "Run tests");
                await loading.EvaluateAsync("s => s.click()");
                await page.WaitForFunctionAsync(closed);
                Assert.Equal("split-run", await page.EvaluateAsync<string>("() => document.activeElement.id"));
                await page.Keyboard.PressAsync("Enter");
                await AssertTextAsync(page, ex, "Nothing run yet");
                await loading.EvaluateAsync("s => s.click()");
                await Assertions.Expect(toggle).Not.ToHaveAttributeAsync("aria-disabled", "true");

                // Disabled closes it too.
                await toggle.FocusAsync();
                await page.Keyboard.PressAsync("Enter");
                await AssertFocusAsync(page, "Run tests");
                await disabled.EvaluateAsync("s => s.click()");
                await page.WaitForFunctionAsync(closed);
                await disabled.EvaluateAsync("s => s.click()");
                await Assertions.Expect(toggle).ToBeEnabledAsync();
            }
        }
    }

    [Fact]
    public async Task Escape_finds_somewhere_to_go_when_the_opener_has_been_disabled()
    {
        var browser = await TestBrowser.OpenAsync();
        await using (browser)
        {
            var page = await OpenAsync(browser, "docs/components/split-button", "interactive");
            const string ex = "split-button-on-click";
            await ReadyAsync(page, ex, "interactive");
            var report = page.Locator("#split-report");
            var mainOff = page.Locator(Example(ex)).GetByRole(AriaRole.Switch, new() { Name = "Report's main button disabled" });

            for (var i = 0; i < 2; i++)
            {
                await report.FocusAsync();
                await page.Keyboard.PressAsync("Alt+ArrowDown");
                await AssertFocusAsync(page, "Weekly");
                await mainOff.EvaluateAsync("s => s.click()");
                await Assertions.Expect(report).ToHaveAttributeAsync("aria-disabled", "true");
                await AssertFocusAsync(page, "Weekly");

                // The main button can't take it: the arrow does.
                await page.Keyboard.PressAsync("Escape");
                await AssertFocusAsync(page, "More reports");

                await mainOff.EvaluateAsync("s => s.click()");
                await Assertions.Expect(report).Not.ToHaveAttributeAsync("aria-disabled", "true");
            }

            // In a toolbar with both parts disabled: the toolbar's next control.
            await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/toolbar?mode=interactive").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
            const string tb = "toolbar-split-button";
            await ReadyAsync(page, tb, "interactive");
            await page.WaitForFunctionAsync("() => document.querySelector('[data-ex=\"toolbar-split-button\"] ib-toolbar').hasAttribute('data-ib-measured')");
            var off = page.Locator(Example(tb)).GetByRole(AriaRole.Switch, new() { Name = "Save disabled" });
            for (var i = 0; i < 2; i++)
            {
                await page.Locator("#toolbar-save").FocusAsync();
                await page.Keyboard.PressAsync("Alt+ArrowDown");
                await AssertFocusAsync(page, "Save as draft");
                await off.EvaluateAsync("s => s.click()");
                await AssertFocusAsync(page, "Link");
                await off.EvaluateAsync("s => s.click()");
                await Assertions.Expect(page.Locator("#toolbar-save")).ToBeEnabledAsync();
            }
        }
    }

    [Fact]
    public async Task A_loading_link_keeps_focus_and_its_tab_stop()
    {
        var browser = await TestBrowser.OpenAsync();
        await using (browser)
        {
            var page = await OpenAsync(browser, "docs/components/split-button", "interactive");
            const string ex = "split-button-on-click";
            await ReadyAsync(page, ex, "interactive");
            var report = page.Locator("#split-report");
            var loading = page.Locator(Example(ex)).GetByRole(AriaRole.Switch, new() { Name = "Loading" });
            const string active = "() => document.activeElement.id";

            for (var i = 0; i < 3; i++)
            {
                await report.FocusAsync();
                await loading.EvaluateAsync("s => s.click()");
                await Assertions.Expect(report).ToHaveAttributeAsync("aria-busy", "true");
                Assert.Null(await report.GetAttributeAsync("href"));
                await page.WaitForTimeoutAsync(100);
                Assert.Equal("split-report", await page.EvaluateAsync<string>(active));

                // Off to the arrow and back: it's still a stop. Enter does nothing.
                await page.Keyboard.PressAsync("Tab");
                await AssertFocusAsync(page, "More reports");
                await page.Keyboard.PressAsync("Shift+Tab");
                Assert.Equal("split-report", await page.EvaluateAsync<string>(active));
                await page.Keyboard.PressAsync("Enter");
                Assert.Contains("split-button", page.Url);

                await loading.EvaluateAsync("s => s.click()");
                await Assertions.Expect(report).Not.ToHaveAttributeAsync("aria-busy", "true");
                Assert.NotNull(await report.GetAttributeAsync("href"));
                Assert.Equal("split-report", await page.EvaluateAsync<string>(active));
            }
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task An_open_menu_closes_when_its_button_moves_into_the_more_menu(string mode)
    {
        var browser = await TestBrowser.OpenAsync();
        await using (browser)
        {
            var page = await OpenAsync(browser, "docs/components/toolbar", mode);
            await ReadyAsync(page, "toolbar-split-button", mode);
            await page.WaitForFunctionAsync("() => [...document.querySelectorAll('ib-toolbar')].every(t => t.hasAttribute('data-ib-measured') || !t.querySelector('.ib-toolbar-more'))");

            // A ToolbarSplitButton, then a ToolbarMenu: both leave the bar, closed, and focus goes to "…".
            foreach (var (example, name) in new[] { ("toolbar-split-button", "More save options"), ("toolbar-filters", "Export") })
            {
                await page.SetViewportSizeAsync(1400, 900);
                var unit = page.Locator(Example(example)).Locator("ib-toolbar").First.Locator(".ib-toolbar-unit").Filter(new() { Has = page.GetByRole(AriaRole.Button, new() { Name = name }) });
                var button = unit.GetByRole(AriaRole.Button, new() { Name = name });
                await Assertions.Expect(button).ToBeVisibleAsync();
                await button.FocusAsync();
                await page.Keyboard.PressAsync("Enter");
                await page.WaitForFunctionAsync("() => document.activeElement.closest('[popover]')");
                var panel = await unit.Locator("[popover]").First.ElementHandleAsync();
                Assert.True(await panel.EvaluateAsync<bool>("p => p.matches(':popover-open')"));

                await page.SetViewportSizeAsync(380, 900);
                await page.WaitForFunctionAsync("p => !p.matches(':popover-open')", panel);
                await AssertFocusAsync(page, "More actions");
            }
        }
    }

    // The toggle event comes a moment after the panel opens: a key pressed before it stays where it got to.
    [Fact]
    public async Task A_key_pressed_as_the_menu_opens_is_not_undone()
    {
        var browser = await TestBrowser.OpenAsync();
        await using (browser)
        {
            var page = await OpenAsync(browser, "docs/components/split-button", "static");
            const string ex = "split-button-actions";
            await ReadyAsync(page, ex, "static");
            var toggle = page.Locator(Example(ex)).GetByRole(AriaRole.Button, new() { Name = "More save options" });

            for (var i = 0; i < 3; i++)
            {
                await toggle.EvaluateAsync("""
                    t => {
                        const key = () => document.activeElement.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true }));
                        t.focus();
                        t.click();
                        key();
                        key();
                    }
                    """);
                await page.WaitForTimeoutAsync(100);
                Assert.Equal("Save and close", await page.EvaluateAsync<string>(Focused));
                await page.Keyboard.PressAsync("Escape");
                await AssertFocusAsync(page, "More save options");
            }
        }
    }

    [Fact]
    public async Task In_a_joined_row_only_the_rows_ends_are_round()
    {
        var browser = await TestBrowser.OpenAsync();
        await using (browser)
        {
            var page = await OpenAsync(browser, "docs/components/split-button", "static");
            var section = page.Locator(Example("split-button-in-group"));
            const string read = "b => { const s = getComputedStyle(b); return [s.borderStartStartRadius, s.borderEndStartRadius, s.borderStartEndRadius, s.borderEndEndRadius].map(r => r === '0px' ? 0 : 1).join(''); }";
            async Task<string> Radii(string name, string part) =>
                await section.Locator($"[role=group][aria-label='{name}'] {part}").First.EvaluateAsync<string>(read);

            // Order: start-start, end-start, start-end, end-end.
            Assert.Equal("0000", await Radii("Middle", "> .ib-btn"));
            Assert.Equal("0000", await Radii("Middle", "ib-popover > .ib-btn"));
            Assert.Equal("1100", await Radii("First", "> .ib-btn"));
            Assert.Equal("0000", await Radii("First", "ib-popover > .ib-btn"));
            Assert.Equal("0000", await Radii("Last", "> .ib-btn"));
            Assert.Equal("0011", await Radii("Last", "ib-popover > .ib-btn"));
            Assert.Equal("1100", await Radii("Alone", "> .ib-btn"));
            Assert.Equal("0011", await Radii("Alone", "ib-popover > .ib-btn"));

            // Mid-row, the solid main button is divided from the button before it.
            Assert.Contains("inset", await section.Locator("[role=group][aria-label='Solid middle'] > .ib-btn").EvaluateAsync<string>("b => getComputedStyle(b).boxShadow"));
        }
    }

    [Fact]
    public async Task The_divider_is_on_the_inner_edge_in_either_direction()
    {
        var browser = await TestBrowser.OpenAsync();
        await using (browser)
        {
            var page = await OpenAsync(browser, "docs/components/split-button", "static");
            var looks = page.Locator(Example("split-button-looks"));
            const string shadow = "b => getComputedStyle(b).boxShadow";
            foreach (var name in new[] { "Solid", "Soft", "Ghost" })
            {
                var arrow = looks.Locator($"[role=group][aria-label='{name}'] ib-popover > .ib-btn");
                Assert.Contains(" 1px 0px 0px 0px inset", await arrow.EvaluateAsync<string>(shadow));

                await page.EvaluateAsync("() => document.documentElement.dir = 'rtl'");
                Assert.Contains(" -1px 0px 0px 0px inset", await arrow.EvaluateAsync<string>(shadow));
                await page.EvaluateAsync("() => document.documentElement.dir = 'ltr'");
            }
        }
    }

    [Fact]
    public async Task FullWidth_stretches_the_main_button_and_not_the_arrow()
    {
        var browser = await TestBrowser.OpenAsync();
        await using (browser)
        {
            var page = await OpenAsync(browser, "docs/components/split-button", "static");
            var group = page.Locator(Example("split-button-in-group")).Locator("[role=group][aria-label='Full width']");
            var parent = await group.Locator("xpath=..").BoundingBoxAsync();
            var box = await group.BoundingBoxAsync();
            var arrow = await group.Locator("ib-popover > .ib-btn").BoundingBoxAsync();
            Assert.Equal(parent!.Width, box!.Width, 1);
            Assert.Equal(arrow!.Height, arrow.Width, 1);
        }
    }

    [Fact]
    public async Task The_main_link_downloads_with_script()
    {
        var browser = await TestBrowser.OpenAsync();
        await using (browser)
        {
            var page = await OpenAsync(browser, "docs/components/split-button", "static");
            var download = page.WaitForDownloadAsync();
            await page.Locator("#split-download").ClickAsync();
            Assert.Equal("izzy.svg", (await download).SuggestedFilename);
        }
    }

    [Fact]
    public async Task With_JavaScript_off_links_posts_and_the_menu_still_work()
    {
        var browser = await TestBrowser.OpenAsync();
        await using (browser)
        {
            var page = await OpenAsync(browser, "docs/components/split-button", "noscript", scripts: false);
            const string ex = "split-button-actions";
            var toggle = page.Locator(Example(ex)).GetByRole(AriaRole.Button, new() { Name = "More save options" });
            var draft = page.Locator(Example(ex)).Locator("button.ib-menu-item", new() { HasText = "Save as draft" });

            // No menu roles, as they promise keys only the script adds.
            Assert.Null(await toggle.GetAttributeAsync("aria-haspopup"));
            Assert.Null(await draft.GetAttributeAsync("role"));

            // The main button posts, and an item does, over and over.
            for (var i = 0; i < 2; i++)
            {
                await page.Locator("#split-save").ClickAsync();
                await AssertTextAsync(page, ex, "Posted: save");

                await toggle.ClickAsync();
                await Assertions.Expect(draft).ToBeVisibleAsync();
                await draft.ClickAsync();
                await AssertTextAsync(page, ex, "Posted: draft");
            }

            // Tab to the arrow, Enter opens it, Escape closes it, natively. Alt+Down does nothing.
            await page.Locator("#split-save").FocusAsync();
            await page.Keyboard.PressAsync("Alt+ArrowDown");
            await Assertions.Expect(draft).ToBeHiddenAsync();
            await page.Keyboard.PressAsync("Tab");
            await page.Keyboard.PressAsync("Enter");
            await Assertions.Expect(draft).ToBeVisibleAsync();
            await page.Keyboard.PressAsync("Escape");
            await Assertions.Expect(draft).ToBeHiddenAsync();

            // A link item is a link.
            await toggle.ClickAsync();
            await page.Locator(Example(ex)).GetByRole(AriaRole.Link, new() { Name = "Open drafts" }).ClickAsync();
            await AssertTextAsync(page, ex, "Opened: drafts");

            // The main link downloads.
            var download = page.WaitForDownloadAsync();
            await page.Locator("#split-download").ClickAsync();
            Assert.Equal("izzy.svg", (await download).SuggestedFilename);
        }
    }

    [Fact]
    public async Task With_JavaScript_off_the_toolbars_menu_still_works()
    {
        var browser = await TestBrowser.OpenAsync();
        await using (browser)
        {
            var page = await OpenAsync(browser, "docs/components/toolbar", "noscript", width: 380, scripts: false);
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
