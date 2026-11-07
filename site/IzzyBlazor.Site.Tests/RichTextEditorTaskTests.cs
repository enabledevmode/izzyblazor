using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>Task lists: a box ticks with a click, and Enter in a task starts the next one.</summary>
public sealed class RichTextEditorTaskTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Fact]
    public async Task A_click_ticks_a_task_and_Enter_adds_the_next()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/rich-text-editor/task-lists?mode=static").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });

        var example = page.Locator("[data-ex='rich-text-editor-tasks']");
        var value = example.Locator("textarea");
        var box = example.Locator(".ib-rich-text-surface").GetByRole(AriaRole.Checkbox, new() { Name = "Brief the support team" });
        await Assertions.Expect(box).ToBeEnabledAsync();
        await box.ClickAsync();
        await Assertions.Expect(box).ToBeCheckedAsync();
        await Assertions.Expect(value).ToHaveValueAsync(new Regex("<input type=\"checkbox\" disabled checked>Brief the support team"));

        // A press on the box keeps focus where it was: the caret goes in the task's text.
        await example.Locator(".ib-rich-text-surface li", new() { HasText = "Brief the support team" }).ClickAsync(new() { Position = new() { X = 200, Y = 5 } });
        await page.Keyboard.PressAsync("End");
        await page.Keyboard.PressAsync("Enter");
        await page.Keyboard.TypeAsync("Tag the release");
        await Assertions.Expect(value).ToHaveValueAsync(new Regex("<li><input type=\"checkbox\" disabled>Tag the release</li></ul>$"));
    }
}
