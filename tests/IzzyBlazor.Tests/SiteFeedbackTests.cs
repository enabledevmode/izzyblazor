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
}
