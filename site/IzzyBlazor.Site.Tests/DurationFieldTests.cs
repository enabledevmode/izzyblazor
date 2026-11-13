using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A DurationField's boxes write the input that posts, Blazor's form binding reads it, and Max holds on the server.</summary>
public sealed class DurationFieldTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private async Task<(IPage Page, ILocator Form, IAsyncDisposable Browser)> OpenAsync(string mode, bool script = true)
    {
        var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 }, Locale = "en-US", JavaScriptEnabled = script });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/duration-field?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        return (page, page.Locator("[data-ex='duration-field-usage'] form"), browser);
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task The_boxes_post_and_a_length_past_max_doesnt_save(string mode)
    {
        var (_, form, browser) = await OpenAsync(mode);
        await using (browser)
        {
            var retention = form.GetByRole(AriaRole.Group, new() { Name = "Retention" });
            await retention.GetByLabel("Days").FillAsync("91");
            await form.GetByRole(AriaRole.Button, new() { Name = "Save settings" }).ClickAsync();
            await Assertions.Expect(form).ToContainTextAsync("must be at most");
            await Assertions.Expect(form).Not.ToContainTextAsync("Saved:");

            await retention.GetByLabel("Days").FillAsync("2");
            await retention.GetByLabel("Hours").FillAsync("30");
            await form.GetByRole(AriaRole.Button, new() { Name = "Save settings" }).ClickAsync();
            await Assertions.Expect(form).ToContainTextAsync("keep for 3.06:00:00");
            await Assertions.Expect(retention.GetByLabel("Hours")).ToHaveValueAsync("6");
        }
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task The_sign_button_flips_what_posts(string mode)
    {
        var (page, _, browser) = await OpenAsync(mode);
        await using (browser)
        {
            var states = page.Locator("[data-ex='duration-field-states']");
            var negative = states.GetByRole(AriaRole.Group, new() { Name = "Clock offset" }).GetByRole(AriaRole.Button, new() { Name = "Negative" });
            var text = states.Locator(".ib-duration-text input").Nth(1);
            await Assertions.Expect(text).ToHaveValueAsync("-05:00:00");

            await negative.ClickAsync();
            await Assertions.Expect(text).ToHaveValueAsync("05:00:00");
            await Assertions.Expect(negative).ToHaveAttributeAsync("aria-pressed", "false");

            await negative.ClickAsync();
            await Assertions.Expect(text).ToHaveValueAsync("-05:00:00");
            await Assertions.Expect(negative).ToHaveAttributeAsync("aria-pressed", "true");
        }
    }

    [Fact]
    public async Task With_no_script_the_text_input_takes_the_format_and_posts()
    {
        var (_, form, browser) = await OpenAsync("off", script: false);
        await using (browser)
        {
            var retention = form.GetByRole(AriaRole.Textbox, new() { Name = "Retention" });
            await Assertions.Expect(form.GetByLabel("Days")).ToBeHiddenAsync();

            await retention.FillAsync("soon");
            Assert.False(await retention.EvaluateAsync<bool>("i => i.checkValidity()"));

            await retention.FillAsync("2.06:00:00");
            await form.GetByRole(AriaRole.Button, new() { Name = "Save settings" }).ClickAsync();
            await Assertions.Expect(form).ToContainTextAsync("keep for 2.06:00:00");
        }
    }
}
