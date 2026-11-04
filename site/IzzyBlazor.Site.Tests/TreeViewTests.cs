using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A tree of checkboxes on a static page: a parent checks its branch, the keys move and uncheck, and Save posts the boxes.</summary>
public sealed class TreeViewTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Fact]
    public async Task A_checkbox_tree_cascades_works_by_keyboard_and_posts_on_a_static_page()
    {
        var browser = await TestBrowser.OpenAsync();
        await using var _ = browser;
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 }, Locale = "en-US" });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/tree-view/selection?mode=static").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => customElements.get('ib-tree')");
        var example = page.Locator("[data-ex='tree-view-permissions']");
        var tree = example.GetByRole(AriaRole.Tree, new() { Name = "Permissions" });
        var products = tree.GetByRole(AriaRole.Treeitem, new() { Name = "Products", Exact = true });

        await products.Locator(":scope > label").ClickAsync();
        await Assertions.Expect(products).ToHaveAttributeAsync("aria-checked", "true");

        await page.Keyboard.PressAsync("ArrowRight");
        await page.Keyboard.PressAsync("ArrowDown");
        await Assertions.Expect(tree.GetByRole(AriaRole.Treeitem, new() { Name = "View products" })).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Space");
        await Assertions.Expect(products).ToHaveAttributeAsync("aria-checked", "mixed");

        await example.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(example).ToContainTextAsync("Saved 4 permissions: orders.view, products.edit, products.price, products.stock.");
    }
}
