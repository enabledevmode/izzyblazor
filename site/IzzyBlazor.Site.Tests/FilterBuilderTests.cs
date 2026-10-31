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
        await builder.Locator("[data-ib-fb-add-group]").First.ClickAsync();
        var inner = builder.Locator(".ib-fb-group .ib-fb-group");
        await inner.Locator("[data-ib-fb-logic]").SelectOptionAsync("any");
        await inner.Locator("[data-ib-fb-field-select]").SelectOptionAsync(new SelectOptionValue { Label = "Total" });
        await inner.Locator("[data-ib-fb-op]").SelectOptionAsync("between");
        await inner.Locator("[data-ib-fb-from]").FillAsync("10");

        // A rule with nothing in it is left out.
        await builder.Locator("[data-ib-fb-add-rule]").First.ClickAsync();
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
}
