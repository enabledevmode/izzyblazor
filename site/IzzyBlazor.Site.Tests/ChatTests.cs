using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A chat on a static page: a message posts with the form, and the reply comes back with it.</summary>
public sealed class ChatTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Fact]
    public async Task A_message_sent_on_a_static_page_comes_back_with_its_reply()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/chat?mode=static").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });

        var chat = page.Locator("[data-ex='chat-usage'] ib-chat");
        var box = chat.GetByRole(AriaRole.Textbox, new() { Name = "Message" });
        await box.FillAsync("How long does shipping take?");
        await chat.GetByRole(AriaRole.Button, new() { Name = "Send" }).ClickAsync();

        var log = chat.GetByRole(AriaRole.Log, new() { Name = "Help desk assistant" });
        await Assertions.Expect(log.GetByRole(AriaRole.Heading, new() { Name = "You said" })).ToHaveCountAsync(1);
        await Assertions.Expect(log.GetByRole(AriaRole.Heading, new() { Name = "Assistant said" })).ToHaveCountAsync(1);
        await Assertions.Expect(log.GetByRole(AriaRole.Table)).ToBeVisibleAsync();
        await Assertions.Expect(box).ToHaveValueAsync("");
    }
}
