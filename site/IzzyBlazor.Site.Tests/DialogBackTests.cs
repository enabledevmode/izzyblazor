using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>
/// A library Dialog in Chromium through Back: enhanced navigation back to the same page patches
/// the dialog's open attribute away, which must not leave it modal and the page inert.
/// </summary>
public sealed class DialogBackTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Theory]
    [InlineData("rename")]
    [InlineData("filters")]
    public async Task Back_with_a_dialog_open_leaves_the_page_usable(string id)
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 } });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/dialog").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });

        // A link that opens a dialog is an enhanced navigation to this page, so Back is one too;
        // Escape closes it and takes ?dialog= out of the address.
        await page.ClickAsync("main a[href*='dialog=whats-new']");
        await page.WaitForURLAsync("**/docs/components/dialog?dialog=whats-new");
        await page.WaitForFunctionAsync("() => document.getElementById('whats-new').matches(':modal')");
        await page.Keyboard.PressAsync("Escape");
        try
        {
            await page.WaitForFunctionAsync("() => !document.getElementById('whats-new').open && !location.search", null, new() { Timeout = 5000 });
        }
        catch (TimeoutException)
        {
            var state = await page.EvaluateAsync<string>("() => `open=${document.getElementById('whats-new').open} modal=${document.getElementById('whats-new').matches(':modal')} search=${location.search} active=${document.activeElement?.tagName}`");
            Assert.Fail("Escape didn't close the dialog and clear the address: " + state);
        }

        await page.ClickAsync($"main button[commandfor='{id}']:not(dialog button)");
        await page.WaitForFunctionAsync($"() => document.getElementById('{id}').matches(':modal')");
        await page.GoBackAsync();

        // Nothing left modal, and the page takes clicks again: the same button opens it.
        await page.WaitForFunctionAsync("() => !document.querySelector(':modal')", null, new() { Timeout = 5000 });
        await page.ClickAsync($"main button[commandfor='{id}']:not(dialog button)", new() { Timeout = 5000 });
        await page.WaitForFunctionAsync($"() => document.getElementById('{id}').matches(':modal')", null, new() { Timeout = 5000 });
    }
}
