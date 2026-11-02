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
        await Assertions.Expect(toggle).ToHaveAttributeAsync("aria-label", new System.Text.RegularExpressions.Regex(@"^Change time, 10:30\sAM$"));
        await toggle.ClickAsync();
        await Assertions.Expect(Slot(form, "10:30")).ToBeFocusedAsync();
        await Assertions.Expect(Slot(form, "10:30")).ToHaveAttributeAsync("aria-selected", "true");

        await Slot(form, "14:15").ClickAsync();

        await Assertions.Expect(input).ToHaveValueAsync("14:15");
        await Assertions.Expect(form.Locator("[popover]").First).ToBeHiddenAsync();
        await Assertions.Expect(toggle).ToBeFocusedAsync();
        await Assertions.Expect(toggle).ToHaveAttributeAsync("aria-label", new System.Text.RegularExpressions.Regex(@"^Change time, 2:15\sPM$"));

        await form.GetByRole(AriaRole.Button, new() { Name = "Book the visit" }).ClickAsync();
        await Assertions.Expect(form).ToContainTextAsync("Booked for 14:15.");
    }

    [Theory]
    [InlineData("static")]
    public async Task Where_a_list_makes_no_sense_it_has_boxes_and_writes_on_ok(string mode)
    {
        var browser = await TestBrowser.OpenAsync();
        await using var _ = browser;
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 1000 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/time-field?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var field = page.Locator("[data-ex='time-field-states'] .ib-field", new() { HasText = "With seconds" });
        await field.Locator("ib-time-slots[data-ready]").WaitForAsync(new() { State = WaitForSelectorState.Attached });
        var input = field.GetByRole(AriaRole.Textbox, new() { Name = "With seconds" });
        var before = await input.InputValueAsync();
        var (hour, minute, second) = (field.GetByRole(AriaRole.Spinbutton, new() { Name = "Hour" }), field.GetByRole(AriaRole.Spinbutton, new() { Name = "Minute" }), field.GetByRole(AriaRole.Spinbutton, new() { Name = "Second" }));

        await field.Locator(".ib-date-toggle").ClickAsync();
        await Assertions.Expect(hour).ToBeFocusedAsync();
        await second.FillAsync("45");
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(input).ToHaveValueAsync(before);

        await field.Locator(".ib-date-toggle").ClickAsync();
        await second.FillAsync("45");
        await minute.FillAsync("20");
        await field.Locator("[data-ib-ok]").ClickAsync();

        await Assertions.Expect(input).ToHaveValueAsync(new System.Text.RegularExpressions.Regex(":20:45$"));
        await Assertions.Expect(field.Locator(".ib-date-toggle")).ToBeFocusedAsync();
    }
}
