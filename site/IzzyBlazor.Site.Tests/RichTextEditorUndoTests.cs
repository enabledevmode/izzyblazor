using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>
/// The editor keeps its own undo history: the browser's knows only its own steps, not the ones the
/// editor makes, such as a color's span.
/// </summary>
public sealed class RichTextEditorUndoTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Fact]
    public async Task Typing_and_a_color_undo_and_redo_and_the_shortcuts_still_format()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/rich-text-editor?mode=static").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Typing is one step until a pause; Ctrl+B still formats what's typed next.
        var comment = page.Locator("[data-ex='rich-text-editor-comment']");
        var text = comment.Locator(".ib-rich-text-surface");
        await text.ClickAsync();
        await page.Keyboard.TypeAsync("Hello ");
        await page.Keyboard.PressAsync("Control+b");
        await page.Keyboard.TypeAsync("bold");
        await Assertions.Expect(text.Locator("b, strong")).ToHaveTextAsync("bold");
        await page.WaitForTimeoutAsync(1200);
        await page.Keyboard.TypeAsync(" more");
        await page.Keyboard.PressAsync("Control+z");
        await Assertions.Expect(text).ToHaveTextAsync("Hello bold");

        // A color undone and redone, the value following.
        var flyer = page.Locator("[data-ex='rich-text-editor-custom-colors']");
        var line = flyer.Locator(".ib-rich-text-surface p").Nth(1);
        await line.ClickAsync(new() { Position = new() { X = 2, Y = 8 } });
        await page.Keyboard.PressAsync("Home");
        for (var i = 0; i < 5; i++) await page.Keyboard.PressAsync("Shift+ArrowRight");
        await flyer.GetByRole(AriaRole.Button, new() { Name = "Text color" }).ClickAsync();
        await flyer.GetByRole(AriaRole.Menuitemradio, new() { Name = "Red" }).ClickAsync();
        await Assertions.Expect(line.Locator("span.ib-color-red")).ToHaveTextAsync("Talks");

        await line.PressAsync("Control+z");
        await Assertions.Expect(line.Locator("span.ib-color-red")).ToHaveCountAsync(0);
        await Assertions.Expect(flyer.Locator("textarea")).Not.ToHaveValueAsync(new System.Text.RegularExpressions.Regex("ib-color-red"));

        await flyer.GetByRole(AriaRole.Button, new() { Name = "Redo", Exact = true }).ClickAsync();
        await Assertions.Expect(line.Locator("span.ib-color-red")).ToHaveTextAsync("Talks");
        await Assertions.Expect(flyer.Locator("textarea")).ToHaveValueAsync(new System.Text.RegularExpressions.Regex("<span class=\"ib-color-red\">Talks</span>"));
    }
}
