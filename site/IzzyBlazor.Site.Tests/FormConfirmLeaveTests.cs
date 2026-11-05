using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A Form with ConfirmLeave asks before a link leaves its unsaved changes, and not after a save.</summary>
public sealed class FormConfirmLeaveTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task Leaving_with_unsaved_changes_asks_and_after_saving_does_not(string mode)
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"frame/form-confirm-leave?mode={mode}").ToString());
        // Ready: the guard's code has loaded, and (interactive) the circuit has rendered it.
        await page.Locator(mode == "interactive" ? "ib-leave-guard[data-ib-interactive]" : "ib-leave-guard").WaitForAsync(new() { State = WaitForSelectorState.Attached });
        await page.WaitForFunctionAsync("() => !!customElements.get('ib-leave-guard')");

        var name = page.GetByLabel("Project name");
        var projects = page.GetByRole(AriaRole.Link, new() { Name = "Projects" });
        var dialog = page.GetByRole(AriaRole.Alertdialog, new() { Name = "Leave this page?" });
        var heading = page.GetByRole(AriaRole.Heading, new() { Name = "Projects" });

        // Stay keeps the edit; Leave goes.
        await name.FillAsync("Website relaunch");
        if (mode == "interactive")
        {
            await name.BlurAsync();
        }

        await projects.ClickAsync();
        await Assertions.Expect(dialog).ToBeVisibleAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Stay" }).ClickAsync();
        await Assertions.Expect(dialog).ToBeHiddenAsync();
        await Assertions.Expect(name).ToHaveValueAsync("Website relaunch");

        await projects.ClickAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Leave" }).ClickAsync();
        await Assertions.Expect(heading).ToBeVisibleAsync();

        // After a save, leaving doesn't ask.
        await page.GetByRole(AriaRole.Link, new() { Name = "Website refresh" }).ClickAsync();
        await name.FillAsync("Website relaunch");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Saved.", new() { Exact = true })).ToBeVisibleAsync();
        await projects.ClickAsync();
        await Assertions.Expect(heading).ToBeVisibleAsync();
        await Assertions.Expect(dialog).ToBeHiddenAsync();
    }
}
