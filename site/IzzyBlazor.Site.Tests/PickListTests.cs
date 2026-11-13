using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A pick list on a static page: an item moved across and one moved down post in their new order.</summary>
public sealed class PickListTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Fact]
    public async Task Moves_and_order_post_on_a_static_page()
    {
        var browser = await TestBrowser.OpenAsync();
        await using var _ = browser;
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/pick-list?mode=static").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => customElements.get('ib-pick-list')");
        var example = page.Locator("[data-ex='pick-list-usage']");
        var available = example.GetByRole(AriaRole.Listbox, new() { Name = "Columns Available" });
        var chosen = example.GetByRole(AriaRole.Listbox, new() { Name = "Columns Chosen" });

        await available.GetByRole(AriaRole.Option, new() { Name = "Date" }).ClickAsync();
        await example.GetByRole(AriaRole.Button, new() { Name = "Move to chosen" }).ClickAsync();
        await Assertions.Expect(chosen.GetByRole(AriaRole.Option, new() { Name = "Date" })).ToBeVisibleAsync();

        await chosen.GetByRole(AriaRole.Option, new() { Name = "Order" }).ClickAsync();
        await page.Keyboard.PressAsync("Alt+ArrowDown");
        await Assertions.Expect(chosen.GetByRole(AriaRole.Option).First).ToHaveAttributeAsync("data-value", "Customer");

        await example.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(example).ToContainTextAsync("Columns: Customer, Order, Total, Date");
    }

    // A finger has no Ctrl: each tap adds an item to the selection or takes it out.
    [Fact]
    public async Task A_tap_on_a_touch_screen_toggles_an_item()
    {
        var browser = await TestBrowser.OpenAsync();
        await using var _ = browser;
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 }, HasTouch = true });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/pick-list?mode=static").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => customElements.get('ib-pick-list')");
        var chosen = page.Locator("[data-ex='pick-list-usage']").GetByRole(AriaRole.Listbox, new() { Name = "Columns Chosen" });
        var order = chosen.GetByRole(AriaRole.Option, new() { Name = "Order" });
        var total = chosen.GetByRole(AriaRole.Option, new() { Name = "Total" });

        await order.TapAsync();
        await total.TapAsync();
        await Assertions.Expect(order).ToHaveAttributeAsync("aria-selected", "true");
        await Assertions.Expect(total).ToHaveAttributeAsync("aria-selected", "true");

        await order.TapAsync();
        await Assertions.Expect(order).ToHaveAttributeAsync("aria-selected", "false");
        await Assertions.Expect(total).ToHaveAttributeAsync("aria-selected", "true");
    }
}
