using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A Calendar moves and writes its input, posts and binds, and its Min and Max hold on the server.</summary>
public sealed class CalendarRuleTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private async Task<(IPage Page, ILocator Form, IAsyncDisposable Browser)> OpenAsync(string mode, bool script = true)
    {
        var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 1000 }, Locale = "en-US", JavaScriptEnabled = script });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/calendar?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        return (page, page.Locator("[data-ex='calendar-limits'] form"), browser);
    }

    private static ILocator Pickup(ILocator form) => form.Locator("ib-calendar").Nth(1);

    private static Task Choose(ILocator calendar, string day) =>
        calendar.GetByRole(AriaRole.Button, new() { Name = day, Exact = true }).ClickAsync();

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task A_day_is_chosen_with_a_click_or_the_keys_and_saves_and_stays_after(string mode)
    {
        var (_, form, browser) = await OpenAsync(mode);
        await using (browser)
        {
            var pickup = Pickup(form);
            await Assertions.Expect(pickup).ToHaveAttributeAsync("data-ready", "");

            // Its sheet has arrived.
            await Assertions.Expect(pickup.Locator(".ib-calendar-view")).ToBeVisibleAsync();

            await Choose(pickup, "Monday, March 15, 2027");
            await Assertions.Expect(pickup.Locator("td[aria-selected=true] button")).ToHaveAttributeAsync("aria-label", "Monday, March 15, 2027");
            await Assertions.Expect(pickup.Locator("input[type=date]")).ToHaveValueAsync("2027-03-15");

            // One tab stop, which arrows and Home move and Enter chooses.
            await pickup.Page.Keyboard.PressAsync("ArrowDown");
            await pickup.Page.Keyboard.PressAsync("ArrowUp");
            await pickup.Page.Keyboard.PressAsync("ArrowLeft");
            await Assertions.Expect(pickup.GetByRole(AriaRole.Button, new() { Name = "Sunday, March 14, 2027" })).ToBeFocusedAsync();
            await pickup.Page.Keyboard.PressAsync("End");
            await Assertions.Expect(pickup.GetByRole(AriaRole.Button, new() { Name = "Saturday, March 20, 2027" })).ToBeFocusedAsync();
            await pickup.Page.Keyboard.PressAsync("ArrowLeft");
            await pickup.Page.Keyboard.PressAsync("Enter");
            await Assertions.Expect(pickup.Locator("input[type=date]")).ToHaveValueAsync("2027-03-19");
            await Assertions.Expect(pickup.Locator("button[tabindex='0']")).ToHaveCountAsync(1);

            // A day outside the limits isn't chosen.
            await pickup.GetByRole(AriaRole.Button, new() { Name = "Saturday, March 20, 2027" }).ClickAsync(new() { Force = true });
            await Assertions.Expect(pickup.Locator("input[type=date]")).ToHaveValueAsync("2027-03-19");

            await form.GetByRole(AriaRole.Button, new() { Name = "Book" }).ClickAsync();
            await Assertions.Expect(form).ToContainTextAsync("Booked:");
            await Assertions.Expect(form).ToContainTextAsync("3/19/2027");
            await Assertions.Expect(pickup.Locator("td[aria-selected=true] button")).ToHaveAttributeAsync("aria-label", "Friday, March 19, 2027");
            await Assertions.Expect(pickup.Locator("button[tabindex='0']")).ToHaveCountAsync(1);
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task The_month_moves_by_buttons_selects_and_keys_and_stays_moved_across_a_render(string mode)
    {
        var (page, form, browser) = await OpenAsync(mode);
        await using (browser)
        {
            var delivery = form.Locator("ib-calendar").First;
            await Assertions.Expect(delivery).ToHaveAttributeAsync("data-ready", "");
            var culture = page.Locator("[data-ex='calendar-culture'] ib-calendar").Nth(1);
            await Assertions.Expect(culture).ToHaveAttributeAsync("data-ready", "");

            await culture.GetByRole(AriaRole.Button, new() { Name = "Next month" }).ClickAsync();
            await Assertions.Expect(culture.Locator("[data-ib-live]")).ToHaveTextAsync("February 2027");
            await Assertions.Expect(culture.GetByRole(AriaRole.Grid)).ToBeVisibleAsync();
            await Assertions.Expect(culture.GetByRole(AriaRole.Button, new() { Name = "Monday, February 1, 2027" })).ToBeVisibleAsync();
            await Assertions.Expect(culture.Locator("tbody button:not([hidden])")).ToHaveCountAsync(28);

            await culture.GetByRole(AriaRole.Combobox, new() { Name = "Year" }).SelectOptionAsync("2030");
            await culture.GetByRole(AriaRole.Combobox, new() { Name = "Month" }).SelectOptionAsync(new SelectOptionValue { Label = "May" });
            await Assertions.Expect(culture.GetByRole(AriaRole.Button, new() { Name = "Wednesday, May 1, 2030" })).ToBeVisibleAsync();

            // A day's number that May doesn't share lands on the month's last day.
            await culture.GetByRole(AriaRole.Button, new() { Name = "Friday, May 31, 2030" }).ClickAsync();
            await page.Keyboard.PressAsync("PageDown");
            await Assertions.Expect(culture.GetByRole(AriaRole.Button, new() { Name = "Sunday, June 30, 2030" })).ToBeFocusedAsync();
            await page.Keyboard.PressAsync("Shift+PageUp");
            await Assertions.Expect(culture.GetByRole(AriaRole.Button, new() { Name = "Saturday, June 30, 2029" })).ToBeFocusedAsync();
            await Assertions.Expect(culture.Locator("input[type=date]")).ToHaveValueAsync("2030-05-31");

            // Min and Max stop the month: Delivery is in March only.
            await Assertions.Expect(delivery.GetByRole(AriaRole.Button, new() { Name = "Next month" })).ToBeDisabledAsync();
            await Assertions.Expect(delivery.GetByRole(AriaRole.Button, new() { Name = "Previous month" })).ToBeDisabledAsync();

            // The label leads to the day.
            await form.Locator("label", new() { HasText = "Delivery" }).ClickAsync();
            await Assertions.Expect(delivery.GetByRole(AriaRole.Button, new() { Name = "Wednesday, March 3, 2027" })).ToBeFocusedAsync();

            // A choice on an interactive page comes back as a render; the month stays where the script has it.
            await culture.GetByRole(AriaRole.Button, new() { Name = "Next month" }).ClickAsync();
            await Choose(culture, "Friday, July 13, 2029");
            await Assertions.Expect(culture).ToHaveAttributeAsync("data-ib-value", mode == "interactive" ? "2029-07-13" : "2027-01-14");
            await Assertions.Expect(culture.GetByRole(AriaRole.Button, new() { Name = "Friday, July 13, 2029" })).ToBeVisibleAsync();
            await Assertions.Expect(culture.Locator("td[aria-selected=true]")).ToHaveCountAsync(1);
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task The_server_holds_the_limits_whatever_reaches_it(string mode)
    {
        var (_, form, browser) = await OpenAsync(mode);
        await using (browser)
        {
            await Assertions.Expect(Pickup(form)).ToHaveAttributeAsync("data-ready", "");

            // As a crafted post would, or a phone that ignored the limit.
            await Pickup(form).Locator("input[type=date]").EvaluateAsync("i => { i.removeAttribute('min'); i.removeAttribute('max'); i.value = '2027-04-02'; i.dispatchEvent(new Event('change', { bubbles: true })); }");
            await form.GetByRole(AriaRole.Button, new() { Name = "Book" }).ClickAsync();
            await Assertions.Expect(form).ToContainTextAsync("or earlier");
            await Assertions.Expect(form).Not.ToContainTextAsync("Booked:");
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task A_disabled_day_cant_be_chosen_the_month_stops_at_the_window_and_the_server_refuses_it(string mode)
    {
        var (page, _, browser) = await OpenAsync(mode);
        await using (browser)
        {
            var form = page.Locator("[data-ex='calendar-days'] form");
            var visit = form.Locator("ib-calendar").First;
            await Assertions.Expect(visit).ToHaveAttributeAsync("data-ready", "");

            // A weekend day takes focus, so the keys move evenly, and isn't chosen.
            await visit.GetByRole(AriaRole.Button, new() { Name = "Saturday, March 13, 2027" }).ClickAsync(new() { Force = true });
            await Assertions.Expect(visit.GetByRole(AriaRole.Button, new() { Name = "Saturday, March 13, 2027" })).ToBeFocusedAsync();
            await Assertions.Expect(visit.Locator("input[type=date]")).ToHaveValueAsync("2027-03-10");

            // It's still off in a month the script drew, with the look of a day that's off.
            await visit.GetByRole(AriaRole.Button, new() { Name = "Next month" }).ClickAsync();
            var off = visit.GetByRole(AriaRole.Button, new() { Name = "Saturday, April 10, 2027" });
            await Assertions.Expect(off).ToHaveAttributeAsync("aria-disabled", "true");
            await Assertions.Expect(off).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("ib-calendar-off"));

            // 90 days from March 10 is June 8: nothing past it is known.
            await visit.GetByRole(AriaRole.Button, new() { Name = "Next month" }).ClickAsync();
            await visit.GetByRole(AriaRole.Button, new() { Name = "Next month" }).ClickAsync();
            await Assertions.Expect(visit.GetByRole(AriaRole.Button, new() { Name = "Next month" })).ToBeDisabledAsync();
            await Assertions.Expect(visit.GetByRole(AriaRole.Button, new() { Name = "Wednesday, June 9, 2027" })).ToHaveAttributeAsync("aria-disabled", "true");

            // A class is there in a month the script drew too.
            var transfer = form.Locator("ib-calendar").Nth(1);
            await transfer.GetByRole(AriaRole.Button, new() { Name = "Next month" }).ClickAsync();
            await Assertions.Expect(transfer.GetByRole(AriaRole.Button, new() { Name = "Thursday, April 15, 2027" })).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("pay-day"));

            // As a crafted post would.
            await visit.Locator("input[type=date]").EvaluateAsync("i => { i.value = '2027-03-13'; i.dispatchEvent(new Event('change', { bubbles: true })); }");
            await form.GetByRole(AriaRole.Button, new() { Name = "Save dates" }).ClickAsync();
            await Assertions.Expect(form).ToContainTextAsync("can't be");
            await Assertions.Expect(form).Not.ToContainTextAsync("Saved:");
        }
    }

    [Fact]
    public async Task Something_else_writing_attributes_as_the_calendar_changes_doesnt_set_it_off_again()
    {
        var (page, form, browser) = await OpenAsync("static");
        await using (browser)
        {
            var delivery = form.Locator("ib-calendar").First;
            await Assertions.Expect(delivery).ToHaveAttributeAsync("data-ready", "");

            // As a browser extension might, marking what changes. Capped: unchecked, this and the calendar's
            // own observer answer each other forever, with no chance to paint or take input.
            await delivery.EvaluateAsync("""
                c => {
                  window.__writes = 0;
                  new MutationObserver(records => {
                    if (window.__writes < 2000 && records.some(r => r.attributeName !== 'data-marked')) {
                      window.__writes++;
                      c.querySelector('.ib-calendar-day').setAttribute('data-marked', window.__writes);
                    }
                  }).observe(c, { attributes: true, subtree: true });
                }
                """);
            await Choose(delivery, "Thursday, March 11, 2027");
            await Assertions.Expect(delivery.Locator("input[type=date]")).ToHaveValueAsync("2027-03-11");

            Assert.True(await page.EvaluateAsync<int>("window.__writes") < 10);

            // Choosing the same day again changes nothing, so it writes nothing.
            var before = await page.EvaluateAsync<int>("window.__writes");
            await Choose(delivery, "Thursday, March 11, 2027");
            Assert.Equal(before, await page.EvaluateAsync<int>("window.__writes"));
        }
    }

    [Fact]
    public async Task With_no_script_the_input_takes_the_date_above_a_month_that_shows_it()
    {
        var (page, form, browser) = await OpenAsync("noscript", script: false);
        await using (browser)
        {
            var input = Pickup(form).Locator("input[type=date]");
            await Assertions.Expect(input).ToBeVisibleAsync();
            await Assertions.Expect(Pickup(form).Locator(".ib-calendar-month")).ToBeVisibleAsync();
            await Assertions.Expect(Pickup(form).Locator(".ib-calendar-nav").First).ToBeHiddenAsync();
            await Assertions.Expect(Pickup(form).Locator("select").First).ToBeHiddenAsync();

            await input.FillAsync("2027-03-11");
            await form.GetByRole(AriaRole.Button, new() { Name = "Book" }).ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            form = page.Locator("[data-ex='calendar-limits'] form");
            await Assertions.Expect(form).ToContainTextAsync("Booked:");
            await Assertions.Expect(Pickup(form).Locator("td[aria-selected=true] button")).ToHaveAttributeAsync("aria-label", "Thursday, March 11, 2027");
        }
    }
}
