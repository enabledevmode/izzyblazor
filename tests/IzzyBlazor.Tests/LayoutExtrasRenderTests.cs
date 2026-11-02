using Microsoft.AspNetCore.Components;

namespace IzzyBlazor.Tests;

public class LayoutExtrasRenderTests : BunitContext
{
    [Fact]
    public void External_link_opens_a_new_tab_and_says_so()
    {
        var a = Render<Anchor>(p => p
            .Add(l => l.Href, "https://example.com")
            .Add(l => l.External, true)
            .AddChildContent("Example")).Find("a");

        Assert.Equal("_blank", a.GetAttribute("target"));
        Assert.Equal("noopener noreferrer", a.GetAttribute("rel"));
        Assert.Equal("ib-anchor", a.ClassName);
        Assert.Contains("(opens in a new tab)", a.QuerySelector(".ib-visually-hidden")!.TextContent);
    }
}
