using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A Scheduler on a static page: the title's calendar, and the form's dialog in the address.</summary>
public sealed class SchedulerTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Fact]
    public async Task A_day_chosen_from_the_title_goes_to_its_week()
    {
        var browser = await TestBrowser.OpenAsync();
        await using var _ = browser;
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/scheduler?mode=static&date=2026-03-11").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => customElements.get('ib-scheduler') && document.querySelector('[data-ex=scheduler-usage] ib-calendar[data-ready]')");
        var example = page.Locator("[data-ex='scheduler-usage']");

        await example.GetByRole(AriaRole.Button, new() { Name = "March 2026" }).ClickAsync();
        // Picking a month from the calendar's select keeps it open; a day goes there.
        await example.Locator(".ib-sched-jump-form select").First.SelectOptionAsync(new SelectOptionValue { Label = "April" });
        var april24 = example.GetByRole(AriaRole.Button, new() { Name = "Friday, April 24, 2026" });
        await Assertions.Expect(april24).ToBeVisibleAsync();
        await april24.ClickAsync();

        await Assertions.Expect(page).ToHaveURLAsync(new Regex("date=2026-04-24"));
        await Assertions.Expect(example.Locator(".ib-sched-day-head .ib-sched-num").First).ToHaveTextAsync("19");
    }

    [Fact]
    public async Task A_read_only_appointment_opens_a_card_about_it()
    {
        var browser = await TestBrowser.OpenAsync();
        await using var _ = browser;
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/scheduler?mode=static").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var example = page.Locator("[data-ex='scheduler-usage']");

        await page.WaitForFunctionAsync("() => customElements.get('ib-scheduler')");
        var appointment = example.GetByRole(AriaRole.Button, new() { Name = "Sprint planning", Exact = false });
        await appointment.ClickAsync();
        var card = page.GetByRole(AriaRole.Dialog, new() { Name = "Sprint planning" });
        await Assertions.Expect(card).ToBeVisibleAsync();
        await Assertions.Expect(card).ToContainTextAsync("Room 4");
        await Assertions.Expect(card).ToBeFocusedAsync();

        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(card).ToBeHiddenAsync();
        await Assertions.Expect(appointment).ToBeFocusedAsync();
    }

    // Israel, Nov 9: the new appointment stayed in the address, so a reload or another view opened
    // the form again.
    [Fact]
    public async Task A_closed_form_stays_closed_on_a_static_page()
    {
        var browser = await TestBrowser.OpenAsync();
        await using var _ = browser;
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/scheduler?mode=static").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => customElements.get('ib-scheduler')");
        var example = page.Locator("[data-ex='scheduler-booking']");

        await example.GetByRole(AriaRole.Link, new() { Name = "New appointment" }).ClickAsync();
        var dialog = page.Locator("dialog[open]");
        await Assertions.Expect(dialog).ToBeVisibleAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("rooms-appointment="));

        await dialog.GetByRole(AriaRole.Button, new() { Name = "Close" }).ClickAsync();
        await Assertions.Expect(page.Locator("dialog[open]")).ToHaveCountAsync(0);
        await Assertions.Expect(page).Not.ToHaveURLAsync(new Regex("rooms-appointment="));

        await example.GetByRole(AriaRole.Button, new() { Name = "Work week" }).ClickAsync();
        await page.GetByRole(AriaRole.Menuitemradio, new() { Name = "Week", Exact = true }).ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("rooms-view=week"));
        await Assertions.Expect(page).Not.ToHaveURLAsync(new Regex("rooms-appointment="));
        await Assertions.Expect(example.GetByRole(AriaRole.Button, new() { Name = "Week", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("dialog[open]")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Hovering_a_booking_shows_its_card_and_a_click_opens_its_form()
    {
        var browser = await TestBrowser.OpenAsync();
        await using var _ = browser;
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/scheduler?mode=static").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => customElements.get('ib-scheduler')");
        var booking = page.Locator("[data-ex='scheduler-booking'] .ib-sched-col a.ib-sched-event").First;

        await booking.HoverAsync();
        var card = page.Locator(".ib-sched-card:popover-open");
        await Assertions.Expect(card).ToHaveCountAsync(1);

        // A press in the card (selecting its text) isn't a press on the time under it.
        await card.ClickAsync();
        await Assertions.Expect(page.Locator("dialog[open]")).ToHaveCountAsync(0);

        await booking.ClickAsync();
        await Assertions.Expect(page.Locator("dialog[open]")).ToBeVisibleAsync();
        await Assertions.Expect(card).ToHaveCountAsync(0);
    }
}
