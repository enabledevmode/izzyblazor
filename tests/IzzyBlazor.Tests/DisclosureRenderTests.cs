using Microsoft.AspNetCore.Components;

namespace IzzyBlazor.Tests;

public class DisclosureRenderTests : BunitContext
{
    public DisclosureRenderTests() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    [Fact]
    public void Static_pages_render_open_with_nothing_for_the_script()
    {
        var details = Render<AccordionItem>(p => p.Add(i => i.Title, "Notes").Add(i => i.Open, true)).Find("details");

        Assert.True(details.HasAttribute("open"));
        Assert.False(details.HasAttribute("data-open"));
        Assert.False(details.HasAttribute("data-ib-seq"));
        Assert.False(details.HasAttribute("data-ib-interactive"));
    }

    private static RenderFragment Items(params (string Id, string Title, bool Open, bool Disabled)[] items) => builder =>
    {
        foreach (var (id, title, open, disabled) in items)
        {
            builder.OpenComponent<AccordionItem>(0);
            builder.AddComponentParameter(1, nameof(AccordionItem.Id), id);
            builder.AddComponentParameter(2, nameof(AccordionItem.Title), title);
            builder.AddComponentParameter(3, nameof(AccordionItem.Open), open);
            builder.AddComponentParameter(4, nameof(AccordionItem.Disabled), disabled);
            builder.CloseComponent();
        }
    };

    [Fact]
    public void Exclusive_accordion_shares_one_name()
    {
        var cut = Render<Accordion>(p => p
            .Add(a => a.Exclusive, true)
            .Add(a => a.Name, "faq")
            .Add(a => a.ChildContent, Items(("a", "One", true, false), ("b", "Two", false, false))));

        var details = cut.FindAll("details");
        Assert.All(details, d => Assert.Equal("faq", d.GetAttribute("name")));
        Assert.True(details[0].HasAttribute("open"));
        Assert.False(details[1].HasAttribute("open"));
        Assert.Equal("ib-accordion ib-accordion-separated", cut.Find("div").ClassName);
    }

    [Fact]
    public void Non_exclusive_accordion_has_no_name()
    {
        var cut = Render<Accordion>(p => p.Add(a => a.ChildContent, Items(("a", "One", false, false))));

        Assert.False(cut.Find("details").HasAttribute("name"));
    }

    [Fact]
    public void Disabled_item_is_out_of_the_tab_order()
    {
        var cut = Render<Accordion>(p => p.Add(a => a.ChildContent, Items(("a", "One", false, true))));

        var summary = cut.Find("summary");
        Assert.Equal("true", summary.GetAttribute("aria-disabled"));
        Assert.Equal("-1", summary.GetAttribute("tabindex"));
        Assert.Contains("ib-disclosure-disabled", cut.Find("details").ClassName);
    }

    [Fact]
    public void Toggle_event_updates_bound_state()
    {
        bool? reported = null;
        var cut = Render<AccordionItem>(p => p
            .Add(i => i.Id, "notes")
            .Add(i => i.Title, "Notes")
            .Add(i => i.OpenChanged, (bool open) => reported = open));

        cut.Find("details").TriggerEvent("onibtoggle", new IzzyToggleEventArgs { Id = "notes", Open = true });

        Assert.True(reported);
        Assert.True(cut.Find("details").HasAttribute("open"));
    }

    [Fact]
    public void Toggle_event_from_a_nested_element_is_ignored()
    {
        bool? reported = null;
        var cut = Render<AccordionItem>(p => p
            .Add(i => i.Id, "outer")
            .Add(i => i.Title, "Outer")
            .Add(i => i.OpenChanged, (bool open) => reported = open));

        cut.Find("details").TriggerEvent("onibtoggle", new IzzyToggleEventArgs { Id = "inner", Open = true });

        Assert.Null(reported);
        Assert.False(cut.Find("details").HasAttribute("open"));
    }

    [Fact]
    public void Collapsible_renders_label_and_chevron()
    {
        var cut = Render<Collapsible>(p => p
            .Add(c => c.Id, "more")
            .Add(c => c.Label, "Show more")
            .AddChildContent("Hidden"));

        Assert.Equal("ib-collapsible ib-disclosure", cut.Find("details").ClassName);
        Assert.Equal("Show more", cut.Find("summary span").TextContent);
        Assert.NotNull(cut.Find("summary .ib-collapsible-chevron"));
    }

    [Fact]
    public void Size_and_content_spacing()
    {
        var cut = Render<Accordion>(p => p
            .Add(a => a.Size, ControlSize.Sm)
            .Add(a => a.ContentGap, (Responsive<int>)2)
            .Add(a => a.ChildContent, Items(("a", "One", true, false))));

        Assert.Contains("ib-accordion-sm", cut.Find("div").ClassName);
        var panel = cut.Find(".ib-accordion-panel");
        Assert.Equal("ib-accordion-panel ib-gap", panel.ClassName);
        Assert.Equal("--ib-gap:var(--ib-space-2);", panel.GetAttribute("style"));
    }

    [Fact]
    public void Item_content_padding_overrides_the_accordion()
    {
        var item = Render<AccordionItem>(p => p
            .Add(i => i.Title, "Usage")
            .Add(i => i.ContentPadding, (Responsive<int>)0));

        var panel = item.Find(".ib-accordion-panel");
        Assert.Equal("ib-accordion-panel ib-px ib-py", panel.ClassName);
        Assert.Equal("--ib-px:var(--ib-space-0);--ib-py:var(--ib-space-0);", panel.GetAttribute("style"));
    }
}

/// <summary>
/// On interactive pages the script opens and closes a Disclosure to match data-open, and
/// data-ib-seq says which of the browser's toggles C# has handled (toggle.ts).
/// </summary>
public class InteractiveDisclosureRenderTests : BunitContext
{
    public InteractiveDisclosureRenderTests() => SetRendererInfo(new RendererInfo("Server", isInteractive: true));

    [Fact]
    public void Open_is_rendered_as_data_open_for_the_script()
    {
        var cut = Render<AccordionItem>(p => p.Add(i => i.Id, "notes").Add(i => i.Title, "Notes").Add(i => i.Open, true));

        var details = cut.Find("details");
        Assert.False(details.HasAttribute("open"));
        Assert.True(details.HasAttribute("data-open"));
        Assert.True(details.HasAttribute("data-ib-interactive"));
        Assert.Equal("0", details.GetAttribute("data-ib-seq"));
    }

    [Fact]
    public void Collapsible_open_is_rendered_as_data_open_too()
    {
        var details = Render<Collapsible>(p => p.Add(c => c.Label, "More").Add(c => c.Open, true)).Find("details");

        Assert.False(details.HasAttribute("open"));
        Assert.True(details.HasAttribute("data-open"));
        Assert.Equal("0", details.GetAttribute("data-ib-seq"));
    }

    [Fact]
    public void Popover_and_Menu_render_the_count_for_their_script()
    {
        var popover = Render<Popover>(p => p.Add(x => x.Label, "Details").Add(x => x.ChildContent, "Body")).Find("ib-popover");
        var menu = Render<Menu>(p => p.Add(x => x.Label, "Actions")).Find("ib-popover");

        foreach (var root in new[] { popover, menu })
        {
            Assert.True(root.HasAttribute("data-ib-interactive"));
            Assert.Equal("0", root.GetAttribute("data-ib-seq"));
        }
    }

    [Fact]
    public void Each_handled_toggle_is_counted_even_when_it_changes_nothing()
    {
        bool? reported = null;
        var cut = Render<Collapsible>(p => p.Add(c => c.Id, "more").Add(c => c.Label, "More").Add(c => c.OpenChanged, (bool open) => reported = open));

        cut.Find("details").TriggerEvent("onibtoggle", new IzzyToggleEventArgs { Id = "more", Open = true, Seq = 1 });
        Assert.True(reported);
        Assert.True(cut.Find("details").HasAttribute("data-open"));
        Assert.Equal("1", cut.Find("details").GetAttribute("data-ib-seq"));

        // Already open: nothing to report, but the count still moves on.
        reported = null;
        cut.Find("details").TriggerEvent("onibtoggle", new IzzyToggleEventArgs { Id = "more", Open = true, Seq = 2 });
        Assert.Null(reported);
        Assert.Equal("2", cut.Find("details").GetAttribute("data-ib-seq"));
    }
}
