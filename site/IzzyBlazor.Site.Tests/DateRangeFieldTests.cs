using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A DateRangeField posts both ends and binds, and its rules hold on the server.</summary>
public sealed class DateRangeFieldTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Theory]
    [InlineData("static", true)]
    [InlineData("interactive", true)]
    [InlineData("noscript", false)]
    public async Task A_good_range_saves_and_a_bad_one_doesnt(string mode, bool script)
    {
        await using var browser = await TestBrowser.OpenAsync();
        var context = await browser.NewContextAsync(new() { JavaScriptEnabled = script, ViewportSize = new() { Width = 1400, Height = 900 }, Locale = "en-US" });
        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/date-range-field?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var form = page.Locator("[data-ex='date-range-field-limits'] form");
        var save = form.GetByRole(AriaRole.Button, new() { Name = "Book the stay" });

        async Task NoBrowserChecksAsync()
        {
            if (script) await form.EvaluateAsync("f => { f.noValidate = true; }");
        }

        // A phone or a crafted post gets past the browser. With no script, one past the browser's own checks.
        await NoBrowserChecksAsync();
        await form.GetByLabel("Start").FillAsync("2027-03-08");
        await form.GetByLabel("End").FillAsync("2027-04-30");
        await save.ClickAsync();
        await Assertions.Expect(form).ToContainTextAsync("at most 14 days");
        await Assertions.Expect(form).Not.ToContainTextAsync("Booked:");

        await NoBrowserChecksAsync();
        await form.GetByLabel("End").FillAsync("2027-03-12");
        await save.ClickAsync();
        await Assertions.Expect(form).ToContainTextAsync("Booked: 3/8/2027 to 3/12/2027, 5 days.");
        await Assertions.Expect(form.GetByLabel("End")).ToHaveValueAsync("2027-03-12");
    }

    private async Task<(IPage Page, ILocator Form, ILocator Start, ILocator End, TestBrowser Browser)> OpenAsync(string mode, int width = 1400)
    {
        var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = width, Height = 1000 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/date-range-field?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var form = page.Locator("[data-ex='date-range-field-limits'] form");
        if (mode == "interactive") await form.Locator("[data-ib-range-value]").WaitForAsync(new() { State = WaitForSelectorState.Attached });
        await form.Locator("ib-calendar[data-ready]").WaitForAsync(new() { State = WaitForSelectorState.Attached });
        return (page, form, form.GetByLabel("Start"), form.GetByLabel("End"), browser);
    }

    private static ILocator Toggle(ILocator form) => form.Locator(".ib-date-toggle");

    private static ILocator Day(ILocator form, string name) => form.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task The_second_day_writes_both_closes_and_saves(string mode)
    {
        var (_, form, start, end, browser) = await OpenAsync(mode);
        await using var _ = browser;
        await Toggle(form).ClickAsync();
        await Assertions.Expect(Day(form, "Monday, March 8, 2027")).ToBeFocusedAsync();
        await Assertions.Expect(form.Locator(".ib-calendar-summary")).ToHaveTextAsync("5 days");

        await Day(form, "Monday, March 15, 2027").ClickAsync();
        await Assertions.Expect(form.Locator(".ib-calendar-summary")).ToHaveTextAsync("Choose the last day");
        await Assertions.Expect(start).ToHaveValueAsync("2027-03-08");

        await Day(form, "Thursday, March 18, 2027").ClickAsync();
        await Assertions.Expect(start).ToHaveValueAsync("2027-03-15");
        await Assertions.Expect(end).ToHaveValueAsync("2027-03-18");
        await Assertions.Expect(form.Locator("[popover]").First).ToBeHiddenAsync();
        await Assertions.Expect(Toggle(form)).ToBeFocusedAsync();
        await Assertions.Expect(Toggle(form)).ToHaveAttributeAsync("aria-label", "Change dates, Monday, March 15, 2027 – Thursday, March 18, 2027");

        await form.GetByRole(AriaRole.Button, new() { Name = "Book the stay" }).ClickAsync();
        await Assertions.Expect(form).ToContainTextAsync("Booked: 3/15/2027 to 3/18/2027, 4 days.");
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task Leaving_with_a_first_day_waiting_changes_nothing(string mode)
    {
        var (page, form, start, end, browser) = await OpenAsync(mode);
        await using var _ = browser;

        await Toggle(form).ClickAsync();
        await Day(form, "Monday, March 15, 2027").ClickAsync();
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(form.Locator("[popover]").First).ToBeHiddenAsync();
        await Assertions.Expect(start).ToHaveValueAsync("2027-03-08");
        await Assertions.Expect(end).ToHaveValueAsync("2027-03-12");

        await Toggle(form).ClickAsync();
        await Day(form, "Monday, March 15, 2027").ClickAsync();
        await page.Mouse.ClickAsync(5, 5);
        await Assertions.Expect(form.Locator("[popover]").First).ToBeHiddenAsync();
        await Assertions.Expect(start).ToHaveValueAsync("2027-03-08");

        // Opened again, nothing's waiting.
        await Toggle(form).ClickAsync();
        await Assertions.Expect(form.Locator(".ib-calendar-summary")).ToHaveTextAsync("5 days");
    }

    [Fact]
    public async Task A_first_day_limits_the_last_and_one_before_it_starts_again()
    {
        var (_, form, start, end, browser) = await OpenAsync("static");
        await using var _ = browser;
        await Toggle(form).ClickAsync();
        await Day(form, "Monday, March 15, 2027").ClickAsync();

        // MinDays 2 and MaxDays 14; it may span a Sunday, but not end on one.
        await Assertions.Expect(Day(form, "Monday, March 15, 2027")).ToHaveAttributeAsync("aria-disabled", "true");
        await Assertions.Expect(Day(form, "Tuesday, March 16, 2027")).Not.ToHaveAttributeAsync("aria-disabled", "true");
        await Assertions.Expect(Day(form, "Saturday, March 27, 2027")).Not.ToHaveAttributeAsync("aria-disabled", "true");
        await Assertions.Expect(Day(form, "Monday, March 29, 2027")).ToHaveAttributeAsync("aria-disabled", "true");
        await Assertions.Expect(Day(form, "Sunday, March 21, 2027")).ToHaveAttributeAsync("aria-disabled", "true");

        await Day(form, "Wednesday, March 10, 2027").ClickAsync();
        await Assertions.Expect(form.Locator(".ib-calendar-span [aria-label='Wednesday, March 10, 2027']")).ToHaveCountAsync(1);
        await Day(form, "Friday, March 12, 2027").ClickAsync();
        await Assertions.Expect(start).ToHaveValueAsync("2027-03-10");
        await Assertions.Expect(end).ToHaveValueAsync("2027-03-12");
    }

    [Fact]
    public async Task The_months_move_together_and_the_keys_cross_them()
    {
        var (page, form, _, _, browser) = await OpenAsync("static");
        await using var __ = browser;
        var titles = form.Locator(".ib-calendar-sheet .ib-calendar-month");
        await Toggle(form).ClickAsync();
        await Assertions.Expect(titles).ToHaveTextAsync(new[] { "March 2027", "April 2027" });

        await form.GetByRole(AriaRole.Button, new() { Name = "Next month" }).ClickAsync();
        await Assertions.Expect(titles).ToHaveTextAsync(new[] { "April 2027", "May 2027" });
        await form.GetByRole(AriaRole.Button, new() { Name = "Previous month" }).ClickAsync();
        await Assertions.Expect(titles).ToHaveTextAsync(new[] { "March 2027", "April 2027" });

        // Into the second month, the months stay; past it, they move.
        await Day(form, "Monday, March 8, 2027").FocusAsync();
        await page.Keyboard.PressAsync("PageDown");
        await Assertions.Expect(Day(form, "Thursday, April 8, 2027")).ToBeFocusedAsync();
        await Assertions.Expect(titles).ToHaveTextAsync(new[] { "March 2027", "April 2027" });
        await page.Keyboard.PressAsync("PageDown");
        await Assertions.Expect(Day(form, "Saturday, May 8, 2027")).ToBeFocusedAsync();
        await Assertions.Expect(titles).ToHaveTextAsync(new[] { "April 2027", "May 2027" });
    }

    [Fact]
    public async Task A_phone_width_shows_one_month()
    {
        var (_, form, _, _, browser) = await OpenAsync("static", width: 390);
        await using var __ = browser;
        await Toggle(form).ClickAsync();

        await Assertions.Expect(form.Locator(".ib-calendar-sheet").Nth(0)).ToBeVisibleAsync();
        await Assertions.Expect(form.Locator(".ib-calendar-sheet").Nth(1)).ToBeHiddenAsync();
        var box = (await form.Locator("[popover]").First.BoundingBoxAsync())!;
        Assert.True(box.X >= 0 && box.X + box.Width <= 390, $"{box.X} {box.Width}");

        await form.GetByRole(AriaRole.Button, new() { Name = "Next month" }).ClickAsync();
        await Assertions.Expect(form.Locator(".ib-calendar-sheet .ib-calendar-month").Nth(0)).ToHaveTextAsync("April 2027");
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task Clear_empties_both_ends(string mode)
    {
        var (page, _, _, _, browser) = await OpenAsync(mode);
        await using var _ = browser;
        var form = page.Locator("[data-ex='date-range-field-states']");
        var start = form.GetByLabel("From");
        var end = form.GetByLabel("Until");
        await start.FillAsync("2027-03-08");
        await end.FillAsync("2027-03-12");

        await Toggle(form).First.ClickAsync();
        await form.GetByRole(AriaRole.Button, new() { Name = "Clear" }).First.ClickAsync();

        await Assertions.Expect(start).ToHaveValueAsync("");
        await Assertions.Expect(end).ToHaveValueAsync("");
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task A_preset_writes_both_and_shows_as_pressed(string mode)
    {
        var (page, _, _, _, browser) = await OpenAsync(mode);
        await using var _ = browser;
        var field = page.Locator("[data-ex='date-range-field-presets']");
        await field.Locator(".ib-date-toggle").ClickAsync();

        await field.GetByRole(AriaRole.Button, new() { Name = "Last 7 days" }).ClickAsync();

        await Assertions.Expect(field.GetByLabel("Start")).ToHaveValueAsync("2027-03-09");
        await Assertions.Expect(field.GetByLabel("End")).ToHaveValueAsync("2027-03-15");
        await Assertions.Expect(field.Locator("[popover]").First).ToBeHiddenAsync();

        await field.Locator(".ib-date-toggle").ClickAsync();
        await Assertions.Expect(field.GetByRole(AriaRole.Button, new() { Name = "Last 7 days" })).ToHaveAttributeAsync("aria-pressed", "true");
        await Assertions.Expect(field.GetByRole(AriaRole.Button, new() { Name = "This month" })).ToHaveAttributeAsync("aria-pressed", "false");
    }

    [Fact]
    public async Task With_only_an_end_the_months_open_on_it()
    {
        var (page, _, _, _, browser) = await OpenAsync("static");
        await using var _ = browser;
        var form = page.Locator("[data-ex='date-range-field-states']");
        await form.GetByLabel("Until").FillAsync("2027-06-20");

        await form.Locator(".ib-date-toggle").First.ClickAsync();

        await Assertions.Expect(form.Locator(".ib-calendar-sheet .ib-calendar-month").First).ToHaveTextAsync("June 2027");
        await Assertions.Expect(form.Locator("td[aria-selected='true'] [aria-label='Sunday, June 20, 2027']")).ToHaveCountAsync(1);
    }
}
