using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>FilterBuilder in Chromium: what's built reaches the address, and draws again from it.</summary>
public sealed class FilterBuilderTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task A_filter_with_a_group_goes_in_the_address_and_comes_back(string mode)
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await (await browser.NewContextAsync(new() { Locale = "en-US", ViewportSize = new() { Width = 1200, Height = 900 } })).NewPageAsync();
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/filter-builder?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        var builder = page.Locator("[data-ib-fb-root]").First;
        await builder.Locator("[data-ib-fb-field-select]").First.WaitForAsync();

        var first = builder.Locator(".ib-fb-rule").First;
        await first.Locator("[data-ib-fb-field-select]").SelectOptionAsync(new SelectOptionValue { Label = "Customer" });
        await first.Locator("[data-ib-fb-from]").FillAsync("a");
        var add = builder.Locator("[data-ib-fb-items] > [data-ib-fb-add]").Last;
        await add.GetByRole(AriaRole.Button, new() { Name = "Add group", Exact = true }).ClickAsync();
        var inner = builder.Locator(".ib-fb-group .ib-fb-group");
        await inner.Locator("[data-ib-logic='any']").ClickAsync();
        await inner.Locator("[data-ib-fb-field-select]").SelectOptionAsync(new SelectOptionValue { Label = "Total" });
        await inner.Locator("[data-ib-fb-op]").SelectOptionAsync("between");
        await inner.Locator("[data-ib-fb-from]").FillAsync("10");

        // A rule with nothing in it is left out.
        await add.GetByRole(AriaRole.Button, new() { Name = "Add rule", Exact = true }).ClickAsync();
        await page.Locator("[data-ib-fb-apply]").First.ClickAsync();

        await page.WaitForURLAsync("**/*orders-filter=*");
        var text = System.Web.HttpUtility.ParseQueryString(new Uri(page.Url).Query)["orders-filter"];
        Assert.Equal("contains(customer,'a') and between(total,10,null)", text);

        await page.ReloadAsync(new() { WaitUntil = WaitUntilState.NetworkIdle });
        await builder.Locator(".ib-fb-rule").First.WaitForAsync();
        Assert.Equal(2, await builder.Locator(".ib-fb-rule").CountAsync());
        Assert.Equal("a", await builder.Locator("[data-ib-fb-from]").First.InputValueAsync());
    }

    [Fact]
    public async Task With_no_script_it_is_hidden()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await (await browser.NewContextAsync(new() { JavaScriptEnabled = false })).NewPageAsync();
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/filter-builder?mode=static").ToString());

        await Assertions.Expect(page.Locator("ib-filter-builder").First).ToBeHiddenAsync();
    }

    [Theory]
    [InlineData("static")]
    [InlineData("interactive")]
    public async Task A_grids_builder_filters_it_and_closes(string mode)
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await (await browser.NewContextAsync(new() { Locale = "en-US", ViewportSize = new() { Width = 1400, Height = 900 } })).NewPageAsync();
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, $"docs/components/data-grid?mode={mode}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        if (mode == "interactive") await page.Locator("ib-table[data-ib-interactive]", new() { HasText = "Orders by builder" }).WaitForAsync();
        var grid = page.Locator("ib-table", new() { HasText = "Orders by builder" });
        await grid.ScrollIntoViewSettledAsync();

        await grid.GetByRole(AriaRole.Button, new() { Name = "Filter builder" }).ClickAsync();
        var panel = page.Locator("[popover]:popover-open");
        await panel.Locator("[data-ib-fb-root] [data-ib-fb-field-select]").First.SelectOptionAsync(new SelectOptionValue { Label = "Customer" });
        await panel.Locator("[data-ib-fb-root] [data-ib-fb-from]").First.FillAsync("Ada");
        await panel.Locator("[data-ib-fb-apply]").ClickAsync();

        await page.WaitForURLAsync("**/*built-filter=*");
        await Assertions.Expect(panel).ToHaveCountAsync(0);
        await Assertions.Expect(grid.Locator("tbody tr").First).ToContainTextAsync("Ada");
        await Assertions.Expect(grid.Locator(".ib-table-filter-tags")).ToContainTextAsync("contains(customer,'Ada')");
    }
}

