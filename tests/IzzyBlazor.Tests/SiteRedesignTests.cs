namespace IzzyBlazor.Tests;

// Gaps the enabledevmode site redesign worked around (Oct 14).
public class SiteRedesignTests : BunitContext
{
    [Fact]
    public void External_anchor_can_hide_its_icon_and_still_says_so()
    {
        var a = Render<Anchor>(p => p
            .Add(l => l.Href, "https://example.com")
            .Add(l => l.External, true)
            .Add(l => l.HideExternalIcon, true)
            .AddChildContent("Lesson 1")).Find("a");

        Assert.Equal("_blank", a.GetAttribute("target"));
        Assert.Null(a.QuerySelector("svg"));
        Assert.Contains("(opens in a new tab)", a.QuerySelector(".ib-visually-hidden")!.TextContent);
    }

    [Fact]
    public void External_LinkButton_can_hide_its_icon_and_still_says_so()
    {
        var a = Render<LinkButton>(p => p
            .Add(b => b.Href, "https://example.com")
            .Add(b => b.External, true)
            .Add(b => b.HideExternalIcon, true)
            .AddChildContent("Watch")).Find("a");

        Assert.Equal("_blank", a.GetAttribute("target"));
        Assert.Null(a.QuerySelector("svg"));
        Assert.Contains("(opens in a new tab)", a.QuerySelector(".ib-visually-hidden")!.TextContent);
    }

    [Fact]
    public void External_LinkButton_with_an_end_icon_still_says_it_opens_a_new_tab()
    {
        var a = Render<LinkButton>(p => p
            .Add(b => b.Href, "https://example.com")
            .Add(b => b.External, true)
            .Add(b => b.EndIcon, Tabler.ArrowRight)
            .Add(b => b.NewTabLabel, "(new tab)")
            .AddChildContent("Docs")).Find("a");

        Assert.Contains("(new tab)", a.QuerySelector(".ib-visually-hidden")!.TextContent);
    }

    [Fact]
    public void AppBrand_links_home_with_an_icon_and_a_one_line_name()
    {
        var a = Render<AppBrand>(p => p
            .Add(b => b.Href, "/")
            .Add(b => b.Icon, Tabler.Bolt)
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

    [Fact]
    public void VideoEmbed_without_an_id_is_coming_soon()
    {
        var cut = Render<VideoEmbed>(p => p.Add(v => v.Label, "Building a form"));

        Assert.Empty(cut.FindAll("ib-video"));
        Assert.Empty(cut.FindAll("a"));
        var box = cut.Find(".ib-video");
        Assert.Contains("ib-video-upcoming", box.ClassName);
        Assert.Equal("group", box.GetAttribute("role"));
        Assert.Equal("Building a form", box.GetAttribute("aria-label"));
        Assert.Null(box.QuerySelector("img"));
        Assert.Contains("Coming soon", box.QuerySelector(".ib-video-empty")!.TextContent);
    }

    [Fact]
    public void VideoEmbed_coming_soon_takes_a_poster_and_its_own_content()
    {
        var box = Render<VideoEmbed>(p => p
            .Add(v => v.Label, "Building a form")
            .Add(v => v.Poster, "/posters/forms.jpg")
            .Add(v => v.EmptyContent, "<strong>Out Friday</strong>")).Find(".ib-video");

        Assert.Equal("/posters/forms.jpg", box.QuerySelector("img")!.GetAttribute("src"));
        Assert.Equal("Out Friday", box.QuerySelector(".ib-video-empty strong")!.TextContent);
        Assert.DoesNotContain("Coming soon", box.TextContent);
    }
}
