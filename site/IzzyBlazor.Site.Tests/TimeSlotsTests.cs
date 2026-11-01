using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A TimeField's list writes its input, closes and hands focus back, and its rule holds on the server.</summary>
public sealed class TimeSlotsTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private async Task<(IPage Page, ILocator Form, ILocator Input, TestBrowser Browser)> OpenAsync(string mode)
    {
        var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 1000 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/time-field?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var form = page.Locator("[data-ex='time-field-slots'] form");
        await form.Locator("ib-time-slots[data-ready]").WaitForAsync(new() { State = WaitForSelectorState.Attached });
        return (page, form, form.GetByLabel("Visit"), browser);
    }

    private static ILocator Slot(ILocator form, string value) => form.Locator($"[role=option][data-value='{value}']");

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task A_slot_fills_the_input_closes_and_saves(string mode)
    {
        var (_, form, input, browser) = await OpenAsync(mode);
        await using var _ = browser;
        var toggle = form.Locator(".ib-date-toggle");
        await Assertions.Expect(toggle).ToHaveAttributeAsync("aria-label", "Change time, 10:30 AM");
        await toggle.ClickAsync();
        await Assertions.Expect(Slot(form, "10:30")).ToBeFocusedAsync();
        await Assertions.Expect(Slot(form, "10:30")).ToHaveAttributeAsync("aria-selected", "true");

        await Slot(form, "14:15").ClickAsync();

        await Assertions.Expect(input).ToHaveValueAsync("14:15");
        await Assertions.Expect(form.Locator("[popover]").First).ToBeHiddenAsync();
        await Assertions.Expect(toggle).ToBeFocusedAsync();
        await Assertions.Expect(toggle).ToHaveAttributeAsync("aria-label", "Change time, 2:15 PM");

        await form.GetByRole(AriaRole.Button, new() { Name = "Book the visit" }).ClickAsync();
        await Assertions.Expect(form).ToContainTextAsync("Booked for 14:15.");
    }

    [Fact]
    public async Task The_keys_move_a_slot_at_a_time_and_a_disabled_one_cant_be_chosen()
    {
        var (page, form, input, browser) = await OpenAsync("static");
        await using var _ = browser;
        await input.FocusAsync();
        await page.Keyboard.PressAsync("Alt+ArrowDown");
        await Assertions.Expect(Slot(form, "10:30")).ToBeFocusedAsync();

        await page.Keyboard.PressAsync("ArrowDown");
        await Assertions.Expect(Slot(form, "10:45")).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Home");
        await Assertions.Expect(Slot(form, "09:00")).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("End");
        await Assertions.Expect(Slot(form, "17:00")).ToBeFocusedAsync();

        await Slot(form, "12:15").FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(input).ToHaveValueAsync("10:30");
        await Assertions.Expect(form.Locator("[popover]").First).ToBeVisibleAsync();

        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(form.Locator("[popover]").First).ToBeHiddenAsync();
        await Assertions.Expect(input).ToHaveValueAsync("10:30");
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task A_typed_time_the_list_turns_off_is_turned_away_on_the_server(string mode)
    {
        var (_, form, input, browser) = await OpenAsync(mode);
        await using var _ = browser;
        await input.FillAsync("12:15");
        await form.GetByRole(AriaRole.Button, new() { Name = "Book the visit" }).ClickAsync();
        await Assertions.Expect(form).ToContainTextAsync("can't be");
        await Assertions.Expect(form).Not.ToContainTextAsync("Booked for");

        await input.FillAsync("10:15");
        await form.GetByRole(AriaRole.Button, new() { Name = "Book the visit" }).ClickAsync();
        await Assertions.Expect(form).ToContainTextAsync("Booked for 10:15.");
    }
}
