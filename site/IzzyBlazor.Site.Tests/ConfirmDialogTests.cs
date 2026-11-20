using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>ConfirmDialog, in Chromium: one dialog asks for every row's button, and confirming does what the one pressed does.</summary>
public sealed class ConfirmDialogTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task A_rows_button_acts_only_once_confirmed(string mode)
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync();
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"frame/confirm-dialog-usage?mode={mode}").ToString());
        // The dialog's script has loaded, and on the interactive page its circuit has taken over.
        await page.WaitForFunctionAsync("() => !!customElements.get('ib-confirm')");
        if (mode == "interactive") await page.Locator("ib-confirm ib-dialog[data-ib-interactive]").WaitForAsync(new() { State = WaitForSelectorState.Attached });
        var revoke = page.GetByRole(AriaRole.Button, new() { Name = "Revoke CI pipeline" });
        var dialog = page.GetByRole(AriaRole.Alertdialog, new() { Name = "Revoke CI pipeline?" });

        // Cancelled: nothing happens, and the button is still there.
        await revoke.ClickAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
        await Assertions.Expect(dialog).ToBeHiddenAsync();
        await Assertions.Expect(page.GetByText("Revoked")).ToHaveCountAsync(0);

        await revoke.ClickAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Revoke key" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Revoked")).ToBeVisibleAsync();
        await Assertions.Expect(revoke).ToHaveCountAsync(0);
    }
}
