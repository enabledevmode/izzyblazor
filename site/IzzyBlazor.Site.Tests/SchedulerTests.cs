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
        await example.GetByRole(AriaRole.Button, new() { Name = "Tuesday, March 24, 2026" }).ClickAsync();

        await Assertions.Expect(page).ToHaveURLAsync(new Regex("date=2026-03-24"));
        await Assertions.Expect(example.Locator(".ib-sched-day-head .ib-sched-num").First).ToHaveTextAsync("22");
    }

    [Fact]
    public async Task A_read_only_appointment_opens_a_card_about_it()
    {
        var browser = await TestBrowser.OpenAsync();
        await using var _ = browser;
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/scheduler?mode=static").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var example = page.Locator("[data-ex='scheduler-usage']");

        await example.GetByRole(AriaRole.Button, new() { Name = "Sprint planning", Exact = false }).ClickAsync();
        var card = page.GetByRole(AriaRole.Dialog, new() { Name = "Sprint planning" });
        await Assertions.Expect(card).ToBeVisibleAsync();
        await Assertions.Expect(card).ToContainTextAsync("Room 4");

        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(card).ToBeHiddenAsync();
    }

    // Israel, Nov 9: the appointment stayed in the address, so a reload opened the form again.
    [Fact]
    public async Task Closing_a_booking_takes_it_out_of_the_address_on_a_static_page()
    {
        var browser = await TestBrowser.OpenAsync();
        await using var _ = browser;
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/scheduler?mode=static").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => customElements.get('ib-scheduler')");
        var example = page.Locator("[data-ex='scheduler-booking']");

        await example.Locator("a.ib-sched-event[href*='appointment=']").First.ClickAsync();
        var dialog = page.Locator("dialog[open]");
        await Assertions.Expect(dialog).ToBeVisibleAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("rooms-appointment="));

        await dialog.GetByRole(AriaRole.Button, new() { Name = "Close" }).ClickAsync();
        await Assertions.Expect(page.Locator("dialog[open]")).ToHaveCountAsync(0);
        await Assertions.Expect(page).Not.ToHaveURLAsync(new Regex("rooms-appointment="));
    }
}
