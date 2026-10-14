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
    public void IzzyTheme_sets_the_mono_font()
    {
        var style = Render<IzzyTheme>(p => p.Add(t => t.MonoFont, "'JetBrains Mono', ui-monospace, monospace")).Find("style");

        Assert.Contains("--ib-font-mono: 'JetBrains Mono', ui-monospace, monospace;", style.TextContent);
    }

    [Fact]
    public void IzzyTheme_rejects_a_mono_font_that_escapes_the_declaration()
    {
        Assert.ThrowsAny<ArgumentException>(() => Render<IzzyTheme>(p => p.Add(t => t.MonoFont, "x; } body { color: red")));
    }

    [Fact]
    public void Fluid_text_size_runs_from_min_at_360px_to_size_at_1280px()
    {
        Assert.Equal(
            "clamp(var(--ib-text-3xl), 1.4348rem + 1.9565vw, var(--ib-text-5xl))",
            StyleValues.FluidTextSize(TextSize.Xl3, TextSize.Xl5));

        // Given the wrong way round, the sizes are swapped.
        Assert.Equal(StyleValues.FluidTextSize(TextSize.Xl3, TextSize.Xl5), StyleValues.FluidTextSize(TextSize.Xl5, TextSize.Xl3));
    }

    [Fact]
    public void Section_is_a_full_bleed_band_by_default()
    {
        var section = Render<Section>(p => p.Add(s => s.Bg, Color.SurfaceContainerLow).AddChildContent("Hi")).Find("section");

        Assert.Equal("ib-section ib-bg ib-fg ib-section-md ib-section-bleed", section.ClassName);
        Assert.Equal("Hi", section.TextContent);
    }

    [Fact]
    public void Section_spacing_element_and_bleed_can_change()
    {
        var div = Render<Section>(p => p
            .Add(s => s.As, "div")
            .Add(s => s.Spacing, SectionSpacing.Lg)
            .Add(s => s.FullBleed, false)).Find("div");

        Assert.Equal("ib-section ib-section-lg", div.ClassName);

        var none = Render<Section>(p => p.Add(s => s.Spacing, SectionSpacing.None)).Find("section");
        Assert.Equal("ib-section ib-section-bleed", none.ClassName);
    }

    [Fact]
    public void List_renders_bullets_numbers_and_icons()
    {
        var bullets = Render<List>(p => p.AddChildContent<ListItem>(i => i.AddChildContent("One"))).Find("ul");
        Assert.Equal("ib-list ib-gap", bullets.ClassName);
        Assert.Equal("--ib-gap:var(--ib-space-2);", bullets.GetAttribute("style"));
        Assert.Null(bullets.GetAttribute("role"));
        Assert.Equal("ib-list-item", bullets.QuerySelector("li")!.ClassName);

        var numbers = Render<List>(p => p.Add(l => l.Ordered, true)).Find("ol");
        Assert.Contains("ib-list-ordered", numbers.ClassList);

        var checks = Render<List>(p => p
            .Add(l => l.Icon, Lucide.Check)
            .Add(l => l.IconColor, Color.SuccessText)
            .AddChildContent<ListItem>(i => i.AddChildContent("Works with JavaScript off")));
        var ul = checks.Find("ul");
        Assert.Equal("list", ul.GetAttribute("role"));
        Assert.Contains("ib-list-icons", ul.ClassList);
        var li = checks.Find("li");
        Assert.Contains("ib-list-item-icon", li.ClassList);
        Assert.NotNull(li.QuerySelector("svg.ib-list-icon"));
        Assert.Equal("Works with JavaScript off", li.QuerySelector(".ib-list-content")!.TextContent);
    }

    [Fact]
    public void An_item_icon_overrides_the_list_icon()
    {
        var cut = Render<List>(p => p
            .Add(l => l.Plain, true)
            .AddChildContent<ListItem>(i => i.Add(x => x.Icon, Lucide.X).AddChildContent("No")));

        Assert.Equal("list", cut.Find("ul").GetAttribute("role"));
        Assert.NotNull(cut.Find("li svg.ib-list-icon"));
    }

    [Fact]
    public void Prose_wraps_html_at_a_size()
    {
        var md = Render<Prose>(p => p.AddChildContent("<h2>Title</h2><p>Text</p>")).Find("div");
        Assert.Equal("ib-prose", md.ClassName);
        Assert.Equal("Title", md.QuerySelector("h2")!.TextContent);

        var lg = Render<Prose>(p => p.Add(x => x.Size, ProseSize.Lg).Add(x => x.As, "article")).Find("article");
        Assert.Equal("ib-prose ib-prose-lg", lg.ClassName);
    }

    [Fact]
    public void Timeline_items_have_a_marker_title_time_and_content()
    {
        var cut = Render<Timeline>(p => p.AddChildContent<TimelineItem>(i => i
            .Add(x => x.Title, "Add Section")
            .Add(x => x.Href, "/commits/93c85d4")
            .Add(x => x.Time, "Oct 13")
            .Add(x => x.DateTime, "2026-10-13")
            .Add(x => x.Current, true)
            .AddChildContent("93c85d4")));

        Assert.Equal("ib-timeline", cut.Find("ol").ClassName);
        var li = cut.Find("li");
        Assert.Equal("ib-timeline-item ib-timeline-current", li.ClassName);
        Assert.Equal("true", li.GetAttribute("aria-current"));
        Assert.Equal("true", li.QuerySelector(".ib-timeline-marker")!.GetAttribute("aria-hidden"));
        Assert.Equal("/commits/93c85d4", li.QuerySelector("a.ib-timeline-title")!.GetAttribute("href"));
        Assert.Equal("2026-10-13", li.QuerySelector("time.ib-timeline-time")!.GetAttribute("datetime"));
        Assert.Equal("93c85d4", li.QuerySelector(".ib-timeline-content")!.TextContent);
    }

    [Fact]
    public void Timeline_icon_and_status_change_the_marker()
    {
        var li = Render<TimelineItem>(p => p
            .Add(x => x.Title, "Delivered")
            .Add(x => x.Time, "Today")
            .Add(x => x.Icon, Lucide.PackageCheck)
            .Add(x => x.Status, Status.Success)).Find("li");

        Assert.Equal("ib-timeline-item ib-timeline-has-icon ib-timeline-success", li.ClassName);
        Assert.NotNull(li.QuerySelector(".ib-timeline-marker svg"));
        Assert.Equal("span", li.QuerySelector(".ib-timeline-title")!.LocalName);
        Assert.Equal("span", li.QuerySelector(".ib-timeline-time")!.LocalName);
        Assert.Null(li.GetAttribute("aria-current"));
    }

    [Fact]
    public void Brand_icons_are_filled_shapes_in_the_text_color()
    {
        var svg = Render<Icon>(p => p.Add(i => i.Name, Brands.GitHub).Add(i => i.Label, "GitHub")).Find("svg");

        Assert.Equal("currentColor", svg.GetAttribute("fill"));
        Assert.Null(svg.GetAttribute("stroke"));
        Assert.Equal("GitHub", svg.GetAttribute("aria-label"));
        Assert.Single(svg.QuerySelectorAll("path"));
    }

    [Fact]
    public void Image_loads_lazily_and_keeps_its_space()
    {
        var img = Render<Image>(p => p
            .Add(i => i.Src, "/a.png")
            .Add(i => i.Alt, "The rules list")
            .Add(i => i.IntrinsicWidth, 1200)
            .Add(i => i.IntrinsicHeight, 675)).Find("img");

        Assert.Equal("lazy", img.GetAttribute("loading"));
        Assert.Equal("async", img.GetAttribute("decoding"));
        Assert.Equal("1200", img.GetAttribute("width"));
        Assert.Equal("675", img.GetAttribute("height"));
        Assert.Equal("ib-image", img.ClassName);
    }

    [Fact]
    public void Image_ratio_fit_and_caption()
    {
        var cut = Render<Image>(p => p
            .Add(i => i.Src, "/a.png")
            .Add(i => i.Alt, "")
            .Add(i => i.Ratio, 16 / 9.0)
            .Add(i => i.Fit, ImageFit.Contain)
            .Add(i => i.Lazy, false)
            .Add(i => i.Caption, "The console"));

        var img = cut.Find("figure.ib-figure > img");
        Assert.Equal("ib-image ib-image-ratio ib-image-contain", img.ClassName);
        Assert.Equal("--ib-ratio:1.7778;", img.GetAttribute("style"));
        Assert.Null(img.GetAttribute("loading"));
        Assert.Equal("", img.GetAttribute("alt"));
        Assert.Equal("The console", cut.Find("figcaption").TextContent);
    }

    [Fact]
    public void VideoEmbed_is_a_link_to_the_video_until_played()
    {
        var host = Render<VideoEmbed>(p => p.Add(v => v.YouTubeId, "dQw4w9WgXcQ").Add(v => v.Title, "Forms")).Find("ib-video");

        var link = host.QuerySelector("a.ib-video-poster")!;
        Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", link.GetAttribute("href"));
        Assert.Equal("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ?autoplay=1", link.GetAttribute("data-ib-video-src"));
        Assert.Equal("Play video: Forms", link.GetAttribute("aria-label"));
        Assert.Equal("https://i.ytimg.com/vi/dQw4w9WgXcQ/hqdefault.jpg", link.QuerySelector("img")!.GetAttribute("src"));
        Assert.Empty(host.QuerySelector(".ib-video-frame")!.Children);
        Assert.Empty(host.QuerySelectorAll("iframe"));
    }

    [Fact]
    public void VideoEmbed_rejects_anything_but_a_video_id()
    {
        Assert.ThrowsAny<ArgumentException>(() => Render<VideoEmbed>(p => p.Add(v => v.YouTubeId, "x\" onload=\"alert(1)").Add(v => v.Title, "X")));
    }

    [Fact]
    public void Heading_with_MinSize_is_fluid()
    {
        var h1 = Render<Heading>(p => p
            .Add(h => h.Level, 1)
            .Add(h => h.Size, TextSize.Xl5)
            .Add(h => h.MinSize, TextSize.Xl3)
            .AddChildContent("Hi")).Find("h1");

        Assert.Contains("ib-fs", h1.ClassList);
        Assert.Contains("--ib-fs:clamp(var(--ib-text-3xl), 1.4348rem + 1.9565vw, var(--ib-text-5xl));", h1.GetAttribute("style"));
    }

    [Fact]
    public void HeaderNav_renders_a_row_and_a_sheet_opened_by_a_native_popover_button()
    {
        var cut = Render<HeaderNav>(p => p
            .Add(n => n.Label, "Site")
            .Add(n => n.Id, "site-nav")
            .Add(n => n.Breakpoint, Breakpoint.Sm)
            .Add(n => n.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<NavItem>(0);
                b.AddAttribute(1, nameof(NavItem.Href), "/articles");
                b.AddAttribute(2, nameof(NavItem.ChildContent), (RenderFragment)(t => t.AddContent(0, "Articles")));
                b.CloseComponent();
            }))
            .Add(n => n.PanelFooter, (RenderFragment)(b => b.AddContent(0, "Footer"))));

        var root = cut.Find("ib-header-nav");
        Assert.Contains("ib-header-nav-bp-sm", root.ClassName);
        Assert.Contains("ib-header-nav-end", root.ClassName);

        // The same links twice: the row and the sheet, each its own labelled nav.
        var navs = cut.FindAll("nav");
        Assert.Equal(2, navs.Count);
        Assert.All(navs, n => Assert.Equal("Site", n.GetAttribute("aria-label")));
        Assert.Equal(2, cut.FindAll("a[href='/articles']").Count);

        // The button opens the sheet with no script; the close button hides it.
        var panel = cut.Find(".ib-header-nav-panel");
        Assert.Equal("site-nav", panel.Id);
        Assert.True(panel.HasAttribute("popover"));
        Assert.Equal("site-nav", cut.Find(".ib-header-nav-toggle").GetAttribute("popovertarget"));
        Assert.Equal("Open menu", cut.Find(".ib-header-nav-toggle").GetAttribute("aria-label"));
        var close = cut.Find(".ib-header-nav-close");
        Assert.Equal("site-nav", close.GetAttribute("popovertarget"));
        Assert.Equal("hide", close.GetAttribute("popovertargetaction"));
        Assert.Equal("Footer", cut.Find(".ib-header-nav-foot").TextContent);
    }
}
