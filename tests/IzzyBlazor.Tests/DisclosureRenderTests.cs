using Microsoft.AspNetCore.Components;

namespace IzzyBlazor.Tests;

public class DisclosureRenderTests : BunitContext
{
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
