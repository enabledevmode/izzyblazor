using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>Closable tabs close with their button or Delete, the next tab opening; the add button adds one.</summary>
public sealed class TabsCloseTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Theory]
    [InlineData("interactive", "tabs-closable")]
    [InlineData("static", "tabs-closable-links")]
    public async Task Closing_the_open_tab_opens_the_next_and_adding_opens_the_new_one(string mode, string id)
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/tabs?mode={mode}").ToString());
        var example = page.Locator($"[data-ex='{id}']");
        // Ready: the circuit has rendered the tabs, or (static) the script has made them tabs.
        await example.Locator(mode == "interactive" ? "ib-tabs[data-ib-interactive]:has(> .ib-tab-add)" : "ib-tabs:has(> .ib-tab-add) > [role=tablist]").WaitForAsync();
        var tabs = example.Locator("ib-tabs:has(> .ib-tab-add) > .ib-tabs-list > .ib-tab .ib-tab-text");
        ILocator Tab(string name) => example.GetByRole(AriaRole.Tab, new() { Name = name });

        // The close button (for the pointer) on the open tab: the one after it opens and takes focus.
        await Tab("INV-1043").ClickAsync();
        await example.Locator(".ib-tab-close[aria-label='Close INV-1043']").ClickAsync();
        await Assertions.Expect(tabs).ToHaveTextAsync(["All invoices", "INV-1042", "INV-1045"]);
        await Assertions.Expect(Tab("INV-1045")).ToHaveAttributeAsync("aria-selected", "true");
        await Assertions.Expect(Tab("INV-1045")).ToBeFocusedAsync();

        // Delete on the last tab: the one before it opens.
        await page.Keyboard.PressAsync("Delete");
        await Assertions.Expect(tabs).ToHaveTextAsync(["All invoices", "INV-1042"]);
        await Assertions.Expect(Tab("INV-1042")).ToHaveAttributeAsync("aria-selected", "true");

        await example.GetByRole(AriaRole.Button, new() { Name = "New invoice" }).Or(example.GetByRole(AriaRole.Link, new() { Name = "New invoice" })).ClickAsync();
        await Assertions.Expect(tabs).ToHaveTextAsync(["All invoices", "INV-1042", "INV-1046"]);
        await Assertions.Expect(Tab("INV-1046")).ToHaveAttributeAsync("aria-selected", "true");

        // Interactive: a new draft asks before it closes, and Cancel leaves it open and selected.
        if (mode == "interactive")
        {
            await example.Locator(".ib-tab-close[aria-label='Close INV-1046']").ClickAsync();
            await page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
            await Assertions.Expect(tabs).ToHaveTextAsync(["All invoices", "INV-1042", "INV-1046"]);
            await Assertions.Expect(Tab("INV-1046")).ToHaveAttributeAsync("aria-selected", "true");

            await example.Locator(".ib-tab-close[aria-label='Close INV-1046']").ClickAsync();
            await page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Discard" }).ClickAsync();
            await Assertions.Expect(tabs).ToHaveTextAsync(["All invoices", "INV-1042"]);
            await Assertions.Expect(Tab("INV-1042")).ToHaveAttributeAsync("aria-selected", "true");
        }
    }
}
