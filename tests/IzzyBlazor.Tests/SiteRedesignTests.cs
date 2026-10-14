namespace IzzyBlazor.Tests;

// Gaps the enabledevmode site redesign worked around (Oct 14).
public class SiteRedesignTests : BunitContext
{
    [Fact]
    public void AppBrand_links_home_with_an_icon_and_a_one_line_name()
    {
        var a = Render<AppBrand>(p => p
            .Add(b => b.Href, "/")
            .Add(b => b.Icon, Lucide.Zap)
            .Add(b => b.Label, "Enable Dev Mode")).Find("a");

        Assert.Equal("/", a.GetAttribute("href"));
        Assert.Equal("ib-shell-brand", a.ClassName);
        Assert.NotNull(a.QuerySelector("svg.ib-shell-brand-icon"));
        Assert.Equal("Enable Dev Mode", a.QuerySelector(".ib-shell-brand-label")!.TextContent);
    }

    [Fact]
    public void AppBrand_without_a_link_is_text_and_a_logo_can_replace_the_name()
    {
        var cut = Render<AppBrand>(p => p
            .Add(b => b.Label, "Acme")
            .Add(b => b.Logo, "<img src=\"/logo.svg\" alt=\"\">")
            .Add(b => b.HideLabel, true));

        var root = cut.Find(".ib-shell-brand");
        Assert.Equal("SPAN", root.TagName);
        Assert.Null(root.GetAttribute("href"));
        Assert.Equal("true", root.QuerySelector(".ib-shell-brand-logo")!.GetAttribute("aria-hidden"));
        Assert.Equal("Acme", root.QuerySelector(".ib-visually-hidden")!.TextContent);
    }
}
