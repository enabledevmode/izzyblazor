using Microsoft.Playwright;

namespace IzzyBlazor.Site.Tests;

/// <summary>A chart's Download button on a static page saves the chart as a PNG named after its title.</summary>
public sealed class ChartDownloadTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    [Fact]
    public async Task Download_saves_the_chart_as_a_png_named_after_its_title()
    {
        await using var browser = await TestBrowser.OpenAsync();
        var page = await browser.NewPageAsync(new() { AcceptDownloads = true, ViewportSize = new() { Width = 1400, Height = 900 } });
        await page.GotoAsync(new Uri(site.Client.BaseAddress!, "docs/components/chart/download?mode=static").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => customElements.get('ib-chart')");

        var download = await page.RunAndWaitForDownloadAsync(() => page.GetByRole(AriaRole.Button, new() { Name = "Download as PNG" }).First.ClickAsync());

        Assert.Equal("Revenue by region, 2026.png", download.SuggestedFilename);
        var path = await download.PathAsync();
        var bytes = await File.ReadAllBytesAsync(path);
        // A PNG's signature, and an image at twice the chart's size on screen.
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], bytes[..4]);
        var width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
        var chartWidth = await page.Locator("figure.ib-chart").First.EvaluateAsync<double>("f => f.getBoundingClientRect().width");
        Assert.True(width >= chartWidth * 2, $"The image is {width}px wide; the chart is {chartWidth}px.");
    }
}
