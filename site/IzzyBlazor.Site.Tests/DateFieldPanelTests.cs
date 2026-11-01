using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A DateField's calendar writes its input, and closes and hands focus back as a popover should.</summary>
public sealed class DateFieldPanelTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private async Task<(IPage Page, ILocator Form, IAsyncDisposable Browser)> OpenAsync(string mode, bool script = true)
    {
        var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 1000 }, Locale = "en-US", JavaScriptEnabled = script });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/date-field?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        return (page, page.Locator("[data-ex='date-field-days'] form"), browser);
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task A_day_chosen_in_the_panel_fills_the_input_closes_and_saves(string mode)
    {
        var (page, form, browser) = await OpenAsync(mode);
        await using (browser)
        {
            var input = form.GetByLabel("Delivery", new() { Exact = true });
            var button = form.GetByRole(AriaRole.Button, new() { Name = "Change date, Monday, March 15, 2027" });
            await button.ClickAsync();
            await Assertions.Expect(form.GetByRole(AriaRole.Button, new() { Name = "Monday, March 15, 2027", Exact = true })).ToBeFocusedAsync();

            // A weekend day stays open and changes nothing.
            await form.GetByRole(AriaRole.Button, new() { Name = "Saturday, March 20, 2027" }).ClickAsync(new() { Force = true });
            await Assertions.Expect(input).ToHaveValueAsync("2027-03-15");

            await form.GetByRole(AriaRole.Button, new() { Name = "Tuesday, March 16, 2027" }).ClickAsync();
            await Assertions.Expect(input).ToHaveValueAsync("2027-03-16");
            var changed = form.GetByRole(AriaRole.Button, new() { Name = "Change date, Tuesday, March 16, 2027" });
            await Assertions.Expect(changed).ToBeFocusedAsync();
            await Assertions.Expect(form.Locator("[popover]").First).ToBeHiddenAsync();

            await form.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
            await Assertions.Expect(form).ToContainTextAsync("Delivery on Mar 16, 2027");
        }
    }

    [Fact]
    public async Task Escape_and_tab_close_it_and_it_opens_on_what_was_typed()
    {
        var (page, form, browser) = await OpenAsync("static");
        await using (browser)
        {
            var input = form.GetByLabel("Delivery", new() { Exact = true });
            await Assertions.Expect(form.Locator("ib-calendar").First).ToHaveAttributeAsync("data-ready", "");
            await input.FillAsync("2027-04-07");
            await input.FocusAsync();
            await page.Keyboard.PressAsync("Alt+ArrowDown");
            var day = form.GetByRole(AriaRole.Button, new() { Name = "Wednesday, April 7, 2027", Exact = true });
            await Assertions.Expect(day).ToBeFocusedAsync();

            await page.Keyboard.PressAsync("ArrowRight");
            await page.Keyboard.PressAsync("Escape");
            await Assertions.Expect(input).ToHaveValueAsync("2027-04-07");
            await Assertions.Expect(input).ToBeFocusedAsync();

            // Past the last stop, Clear, it closes, and the next stop after the button takes focus.
            var button = form.GetByRole(AriaRole.Button, new() { Name = "Change date, Wednesday, April 7, 2027" });
            await button.FocusAsync();
            await page.Keyboard.PressAsync("Enter");
            await Assertions.Expect(day).ToBeFocusedAsync();
            await page.Keyboard.PressAsync("Tab");
            await page.Keyboard.PressAsync("Tab");
            await Assertions.Expect(form.Locator("[popover]").First).ToBeHiddenAsync();
            await Assertions.Expect(form.GetByLabel("Reminder", new() { Exact = true })).ToBeFocusedAsync();
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task A_date_and_time_waits_for_ok_and_saves_in_the_fields_zone(string mode)
    {
        var (page, _, browser) = await OpenAsync(mode);
        await using (browser)
        {
            var form = page.Locator("[data-ex='date-field-time-zones'] form");
            var input = form.GetByLabel("Standup (New York)");
            var panel = form.Locator("[popover]").Nth(1);
            var day = panel.GetByRole(AriaRole.Button, new() { Name = "Monday, March 15, 2027", Exact = true });
            var open = form.GetByRole(AriaRole.Button, new() { Name = "Change date, Tuesday, March 2, 2027, 09:30" });

            // A day alone changes nothing, and Escape drops it.
            await open.ClickAsync();
            await day.ClickAsync();
            await Assertions.Expect(panel.Locator("td[aria-selected=true] button")).ToHaveAttributeAsync("aria-label", "Monday, March 15, 2027");
            await Assertions.Expect(input).ToHaveValueAsync("2027-03-02T09:30");
            await page.Keyboard.PressAsync("Escape");
            await Assertions.Expect(panel).ToBeHiddenAsync();
            await Assertions.Expect(input).ToHaveValueAsync("2027-03-02T09:30");

            // It opens on the input's day and time again, and Enter in the time is OK, not a post.
            await open.ClickAsync();
            await Assertions.Expect(panel.Locator("td[aria-selected=true] button")).ToHaveAttributeAsync("aria-label", "Tuesday, March 2, 2027");
            await day.ClickAsync();
            var time = panel.GetByLabel("Time");
            await Assertions.Expect(time).ToHaveValueAsync("09:30");
            await time.FillAsync("14:45");
            await time.PressAsync("Enter");
            await Assertions.Expect(panel).ToBeHiddenAsync();
            await Assertions.Expect(input).ToHaveValueAsync("2027-03-15T14:45");
            await Assertions.Expect(form.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);

            // March 15 is after New York's clocks change, so the offset is the panel's day's.
            await form.GetByRole(AriaRole.Button, new() { Name = "Schedule" }).ClickAsync();
            await Assertions.Expect(form).ToContainTextAsync("2027-03-15 14:45 -04:00");
        }
    }

    [Fact]
    public async Task With_no_script_the_browser_has_the_only_calendar()
    {
        var (_, form, browser) = await OpenAsync("noscript", script: false);
        await using (browser)
        {
            await Assertions.Expect(form.GetByLabel("Delivery", new() { Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(form.Locator(".ib-date-toggle")).ToBeHiddenAsync();
        }
    }
}
