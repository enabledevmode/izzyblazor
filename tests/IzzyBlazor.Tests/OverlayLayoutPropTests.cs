using Microsoft.AspNetCore.Components;

namespace IzzyBlazor.Tests;

// Item props on overlays and disclosure, PanelWidth, ContentPadding and Code MaxHeight.
public class OverlayLayoutPropTests : BunitContext
{
    public OverlayLayoutPropTests() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    [Fact]
    public void Menu_puts_item_props_on_the_outer_element_and_Class_on_the_panel()
    {
        var cut = Render<Menu>(p => p
            .Add(m => m.Id, "actions")
            .Add(m => m.Label, "Actions")
            .Add(m => m.MarginStart, 2)
            .Add(m => m.HiddenFrom, Breakpoint.Md)
            .Add(m => m.Class, "mine"));
        var root = cut.Find("ib-popover");
        var panel = cut.Find("[popover]");

        Assert.Equal("ib-pop-root ib-ms ib-hidden-from-md", root.ClassName);
        Assert.Equal("anchor-name:--actions;--ib-ms:var(--ib-space-2);", root.GetAttribute("style"));
        Assert.Contains("mine", panel.ClassName);
        Assert.DoesNotContain("ib-ms", panel.ClassName);
    }

    [Fact]
    public void Popover_PanelWidth_sizes_the_panel()
    {
        var cut = Render<Popover>(p => p
            .Add(x => x.Id, "filters")
            .Add(x => x.Label, "Filters")
            .Add(x => x.PanelWidth, Length.Rem(32))
            .Add(x => x.Grow, true));
        var panel = cut.Find("[popover]");

        Assert.Contains("ib-pop-sized", panel.ClassName);
        Assert.Contains("--ib-pop-w:32rem", panel.GetAttribute("style"));
        Assert.Contains("ib-grow", cut.Find("ib-popover").ClassName);
        Assert.DoesNotContain("ib-grow", panel.ClassName);
    }

    [Fact]
    public void Popover_without_PanelWidth_keeps_its_default_width()
    {
        var panel = Render<Popover>(p => p.Add(x => x.Id, "f").Add(x => x.Label, "Filters")).Find("[popover]");

        Assert.DoesNotContain("ib-pop-sized", panel.ClassName);
        Assert.DoesNotContain("--ib-pop-w", panel.GetAttribute("style"));
    }

    [Fact]
    public void Tooltip_puts_item_props_on_the_outer_element()
    {
        var cut = Render<Tooltip>(p => p
            .Add(t => t.Text, "Copy")
            .Add(t => t.MarginEnd, 1)
            .AddChildContent("<button>Copy</button>"));

        Assert.Contains("ib-me", cut.Find("ib-tooltip").ClassName);
        Assert.DoesNotContain("ib-me", cut.Find("[role=tooltip]").ClassName);
    }

    [Fact]
    public void Collapsible_takes_item_props_on_its_details()
    {
        var details = Render<Collapsible>(p => p
            .Add(c => c.Id, "more")
            .Add(c => c.Label, "Show more")
            .Add(c => c.MaxWidth, Length.Prose)
            .AddChildContent("Hidden")).Find("details");

        Assert.Equal("ib-collapsible ib-disclosure ib-maxw", details.ClassName);
    }

    [Fact]
    public void Dialog_Width_and_ContentPadding()
    {
        var cut = Render<Dialog>(p => p
            .Add(d => d.Id, "wide")
            .Add(d => d.Title, "Members")
            .Add(d => d.Width, Length.Rem(56))
            .Add(d => d.ContentPadding, 0)
            .AddChildContent("table"));
        var dialog = cut.Find("dialog");
        var body = cut.Find(".ib-dialog-body");

        Assert.Equal("ib-dialog ib-dialog-md ib-dialog-center ib-dialog-sized", dialog.ClassName);
        Assert.Equal("--ib-dlgw:56rem;", dialog.GetAttribute("style"));
        Assert.Equal("ib-dialog-body ib-px ib-py ib-dialog-pad", body.ClassName);
        Assert.Contains("--ib-px:var(--ib-space-0)", body.GetAttribute("style"));
    }

    [Fact]
    public void Dialog_Width_can_change_per_breakpoint()
    {
        var dialog = Render<Dialog>(p => p
            .Add(d => d.Id, "wide")
            .Add(d => d.Title, "Members")
            .Add(d => d.Width, new Responsive<Length> { Md = Length.Rem(48) })).Find("dialog");

        Assert.Contains("ib-dialog-sized-md", dialog.ClassName);
        Assert.DoesNotContain("ib-dialog-sized ", dialog.ClassName + " ");
        Assert.Equal("--ib-dlgw-md:48rem;", dialog.GetAttribute("style"));
    }

    [Fact]
    public void Dialog_rejects_layout_props_that_dont_apply()
    {
        Assert.Throws<InvalidOperationException>(() => Render<Dialog>(p => p.Add(d => d.Id, "a").Add(d => d.Title, "A").Add(d => d.MarginX, 4)));
        Assert.Throws<InvalidOperationException>(() => Render<Drawer>(p => p.Add(d => d.Id, "b").Add(d => d.Title, "B").Add(d => d.MaxWidth, Length.Rem(40))));
        Assert.Throws<InvalidOperationException>(() => Render<Dialog>(p => p.Add(d => d.Id, "c").Add(d => d.Title, "C").Add(d => d.Grow, true)));
    }

    [Fact]
    public void Dialog_body_has_no_style_by_default()
    {
        var body = Render<Dialog>(p => p.Add(d => d.Id, "d").Add(d => d.Title, "Hi")).Find(".ib-dialog-body");

        Assert.Equal("ib-dialog-body", body.ClassName);
        Assert.False(body.HasAttribute("style"));
    }

    private static RenderFragment TwoTabs(int? secondPadding = null) => b =>
    {
        b.OpenComponent<Tab>(0);
        b.AddComponentParameter(1, nameof(Tab.Label), "One");
        b.CloseComponent();
        b.OpenComponent<Tab>(2);
        b.AddComponentParameter(3, nameof(Tab.Label), "Two");
        if (secondPadding is { } padding)
        {
            b.AddComponentParameter(4, nameof(Tab.ContentPadding), (Responsive<int>)padding);
        }

        b.CloseComponent();
    };

    [Fact]
    public void Tabs_ContentPadding_reaches_every_panel_and_a_tab_can_override_it()
    {
        var panels = Render<Tabs>(p => p
            .Add(t => t.Id, "t")
            .Add(t => t.ContentPadding, 6)
            .Add(t => t.ChildContent, TwoTabs(secondPadding: 0))).FindAll(".ib-tab-panel");

        Assert.Equal(2, panels.Count);
        Assert.Contains("--ib-px:var(--ib-space-6)", panels[0].GetAttribute("style"));
        Assert.Contains("--ib-px:var(--ib-space-0)", panels[1].GetAttribute("style"));
    }

    [Fact]
    public void Changing_Tabs_ContentPadding_updates_every_panel()
    {
        var cut = Render<Tabs>(p => p
            .Add(t => t.Id, "t")
            .Add(t => t.ContentPadding, 6)
            .Add(t => t.ChildContent, TwoTabs()));

        cut.Render(p => p.Add(t => t.ContentPadding, 2));

        Assert.All(cut.FindAll(".ib-tab-panel"), panel => Assert.Contains("--ib-px:var(--ib-space-2)", panel.GetAttribute("style")));
    }

    [Fact]
    public void Tab_panels_have_no_padding_props_by_default()
    {
        var panel = Render<Tabs>(p => p.Add(t => t.Id, "t").Add(t => t.ChildContent, TwoTabs())).FindAll(".ib-tab-panel")[0];

        Assert.Equal("ib-tab-panel", panel.ClassName);
    }

    [Fact]
    public void Code_MaxHeight_scrolls_a_block()
    {
        var block = Render<Code>(p => p
            .Add(c => c.Block, true)
            .Add(c => c.MaxHeight, Length.Rem(20))
            .AddChildContent("var x = 1;")).Find("ib-code");

        Assert.Contains("ib-code-sized", block.ClassName);
        Assert.Contains("--ib-code-maxh:20rem", block.GetAttribute("style"));
    }

    [Fact]
    public void Code_MaxHeight_is_ignored_inline()
    {
        var code = Render<Code>(p => p.Add(c => c.MaxHeight, Length.Rem(20)).AddChildContent("x")).Find("code");

        Assert.Equal("ib-code", code.ClassName);
    }
}
