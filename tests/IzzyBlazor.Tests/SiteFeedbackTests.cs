using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace IzzyBlazor.Tests;

public class SiteFeedbackTests : BunitContext
{
    public class Model
    {
        public string? Email { get; set; }
    }

    [Fact]
    public void HideLabel_keeps_the_label_for_screen_readers_only()
    {
        var model = new Model();
        var cut = Render<TextField>(p => p
            .AddCascadingValue(new EditContext(model))
            .Add(f => f.ValueExpression, () => model.Email)
            .Add(f => f.Value, model.Email)
            .Add(f => f.Label, "Email")
            .Add(f => f.HideLabel, true));

        var label = cut.Find("label");
        Assert.Equal("Email", label.TextContent.Trim());
        Assert.Contains("ib-visually-hidden", label.ClassName);
        Assert.Equal(cut.Find("input").Id, label.GetAttribute("for"));
    }

    [Fact]
    public void IzzyTheme_rejects_a_mono_font_that_escapes_the_declaration()
    {
        Assert.ThrowsAny<ArgumentException>(() => Render<IzzyTheme>(p => p.Add(t => t.MonoFont, "x; } body { color: red")));
    }

    [Theory]
    [InlineData("'Inter")]
    [InlineData("Inter,\nsans-serif")]
    public void IzzyTheme_rejects_a_font_list_with_an_open_string(string font)
    {
        Assert.ThrowsAny<ArgumentException>(() => Render<IzzyTheme>(p => p.Add(t => t.BodyFont, font)));
    }

    [Fact]
    public void VideoEmbed_rejects_anything_but_a_video_id()
    {
        Assert.ThrowsAny<ArgumentException>(() => Render<VideoEmbed>(p => p.Add(v => v.YouTubeId, "x\" onload=\"alert(1)").Add(v => v.Label, "X")));
    }

    [Fact]
    public void SiteNav_renders_a_row_and_a_panel_toggled_by_a_native_popover_button()
    {
        var cut = Render<SiteNav>(p => p
            .Add(n => n.Label, "Site")
            .Add(n => n.Id, "site-nav")
            .Add(n => n.Breakpoint, Breakpoint.Sm)
            .Add(n => n.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<NavItem>(0);
                b.AddAttribute(1, nameof(NavItem.Href), "/articles");
                b.AddAttribute(2, nameof(NavItem.ChildContent), (RenderFragment)(t => t.AddContent(0, "Articles")));
                b.CloseComponent();
                b.OpenComponent<SiteNavFooter>(3);
                b.AddAttribute(4, nameof(SiteNavFooter.ChildContent), (RenderFragment)(f => f.AddContent(0, "Footer")));
                b.CloseComponent();
            })));

        var root = cut.Find("ib-site-nav");
        Assert.Contains("ib-site-nav-bp-sm", root.ClassName);
        Assert.Contains("ib-site-nav-end", root.ClassName);
        Assert.DoesNotContain("ib-site-nav-start", root.ClassName);

        // The same links twice: the row and the panel, each its own labeled nav.
        var navs = cut.FindAll("nav");
        Assert.Equal(2, navs.Count);
        Assert.All(navs, n => Assert.Equal("Site", n.GetAttribute("aria-label")));
        Assert.Equal(2, cut.FindAll("a[href='/articles']").Count);

        // The button opens and closes the panel with no script; the footer is drawn once, in the panel.
        var panel = cut.Find(".ib-site-nav-panel");
        Assert.Equal("site-nav", panel.Id);
        Assert.True(panel.HasAttribute("popover"));
        Assert.Equal("site-nav", cut.Find(".ib-site-nav-toggle").GetAttribute("popovertarget"));
        Assert.Equal("Menu", cut.Find(".ib-site-nav-toggle").GetAttribute("aria-label"));
        Assert.Null(cut.Find(".ib-site-nav-toggle").GetAttribute("popovertargetaction"));
        Assert.Equal("Footer", cut.Find(".ib-site-nav-panel .ib-site-nav-foot").TextContent);
        Assert.Single(cut.FindAll(".ib-site-nav-foot"));
    }
}
