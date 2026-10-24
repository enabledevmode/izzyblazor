using Microsoft.AspNetCore.Components;

namespace IzzyBlazor.Tests;

public class ToolbarTests : BunitContext
{
    private IRenderedComponent<ToolbarProbe> RenderToolbar(string kind, bool interactive = false)
    {
        SetRendererInfo(new RendererInfo(interactive ? "Server" : "Static", interactive));
        return Render<ToolbarProbe>(p => p.Add(t => t.Kind, kind));
    }

    // The "…" menu's copy of each unit, by the text screen readers get from it.
    private static AngleSharp.Dom.IElement MenuPanel(IRenderedComponent<ToolbarProbe> cut) => cut.Find(".ib-toolbar-more [popover]");

    [Theory]
    [InlineData(false, "group", "toolbar")]
    [InlineData(true, "toolbar", null)]
    public void Static_pages_promise_a_toolbar_only_once_the_script_adds_the_keys(bool interactive, string role, string? deferred)
    {
        var toolbar = RenderToolbar("controls", interactive).Find("ib-toolbar");

        Assert.Equal(role, toolbar.GetAttribute("role"));
        Assert.Equal(deferred, toolbar.GetAttribute("data-ib-role"));
        Assert.Equal("Editor", toolbar.GetAttribute("aria-label"));
    }

    [Fact]
    public void A_toolbar_needs_a_label()
    {
        SetRendererInfo(new RendererInfo("Static", false));
        Assert.Throws<InvalidOperationException>(() => Render<Toolbar>(p => p.AddChildContent("x")));
    }

    [Fact]
    public void Each_control_has_a_menu_copy_that_does_the_same()
    {
        var panel = MenuPanel(RenderToolbar("controls"));

        // A submit button posts the same name, value and address from the menu.
        var archive = panel.QuerySelectorAll("button").Single(b => b.TextContent.Trim() == "Archive");
        Assert.Equal("submit", archive.GetAttribute("type"));
        Assert.Equal("intent", archive.GetAttribute("name"));
        Assert.Equal("archive", archive.GetAttribute("value"));
        Assert.Equal("/mail/archive", archive.GetAttribute("formaction"));

        // A link stays a link; an icon button's label is its text; MenuText replaces it.
        Assert.Equal("/help", panel.QuerySelectorAll("a").Single(a => a.TextContent.Trim() == "Help").GetAttribute("href"));
        var texts = panel.QuerySelectorAll(".ib-menu-label").Select(l => l.TextContent.Trim()).ToList();
        Assert.Equal(["Undo", "Redo", "Archive", "Help", "Bold", "Copy link", "Export", "CSV", "Print"], texts);

        // A ButtonGroup is a labeled group; a Menu is a submenu with its items.
        Assert.Equal("History", panel.QuerySelector("[role=group] .ib-menu-heading")!.TextContent.Trim());
        Assert.NotNull(panel.QuerySelector("ib-popover [popover] a[href='/export.csv']"));
    }

    [Fact]
    public void Ids_are_on_the_bar_copy_only()
    {
        var cut = RenderToolbar("controls");

        Assert.Single(cut.FindAll("#archive"));
        Assert.Null(MenuPanel(cut).QuerySelector("#archive"));
    }

    [Fact]
    public void Content_the_menu_cant_show_stays_in_the_bar()
    {
        var cut = RenderToolbar("stays");
        var panel = MenuPanel(cut);
        var texts = panel.QuerySelectorAll(".ib-menu-label").Select(l => l.TextContent.Trim()).ToList();

        // Only Plain can move: a reset button, a dialog's and a popover's triggers and custom
        // content stay, and the button inside the popover isn't the toolbar's.
        Assert.Equal(["Plain"], texts);
        Assert.Empty(cut.FindAll("[popover] .ib-toolbar-unit"));
    }

    [Fact]
    public void Buttons_are_ghost_neutral_and_small_unless_they_say_otherwise()
    {
        var cut = RenderToolbar("defaults");
        string Classes(string text) => cut.FindAll(".ib-toolbar-start .ib-btn").Single(b => b.TextContent.Trim() == text).ClassName!;

        Assert.Equal("ib-btn ib-btn-neutral ib-btn-ghost ib-btn-sm", Classes("Ghost"));
        Assert.Equal("ib-btn ib-btn-primary ib-btn-solid ib-btn-sm", Classes("Solid"));
        // A group with no settings of its own, and a menu's trigger, take the toolbar's.
        Assert.Equal("ib-btn ib-btn-neutral ib-btn-ghost ib-btn-sm", Classes("In a group"));
        Assert.StartsWith("ib-btn ib-btn-neutral ib-btn-ghost ib-btn-sm", Classes("Menu"));
    }

    [Fact]
    public void A_wrapping_toolbar_has_no_menu()
    {
        Assert.Empty(RenderToolbar("wrap").FindAll(".ib-toolbar-more"));
    }

    [Fact]
    public void Its_style_element_takes_the_nonce()
    {
        Assert.Equal("abc123", RenderToolbar("nonce").Find("ib-toolbar style").GetAttribute("nonce"));
    }

    [Fact]
    public void The_menu_follows_the_controls_on_interactive_pages()
    {
        SetRendererInfo(new RendererInfo("Server", true));
        var cut = Render<Toolbar>(p => p.Add(t => t.Label, "Live").AddChildContent<Button>(b => b.AddChildContent("Before")));
        Assert.Equal("Before", cut.Find(".ib-toolbar-more .ib-menu-label").TextContent.Trim());

        cut.Render(p => p.Add(t => t.Label, "Live").AddChildContent<Button>(b => b.AddChildContent("After")));
        Assert.Equal("After", cut.Find(".ib-toolbar-more .ib-menu-label").TextContent.Trim());
    }

    // Units for the plan: id, region, width, priority, overflow; "|" is a separator.
    private static List<ToolbarPlan.Unit> Units(params string[] specs) => specs.Select((spec, i) =>
    {
        if (spec == "|")
        {
            return new ToolbarPlan.Unit(i + 1, ToolbarRegion.Start, 1, 0, ToolbarOverflow.Never, true);
        }

        var parts = spec.Split(':');
        var region = parts[0][0] switch { 'c' => ToolbarRegion.Center, 'e' => ToolbarRegion.End, _ => ToolbarRegion.Start };
        var overflow = parts.Length > 2 ? Enum.Parse<ToolbarOverflow>(parts[2], true) : ToolbarOverflow.Auto;
        var priority = parts.Length > 1 && parts[1].Length > 0 ? int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) : 0;
        return new ToolbarPlan.Unit(i + 1, region, 100, priority, overflow, false);
    }).ToList();

    // The units shown in the bar at this width, "…" when the menu button shows.
    private static string Bar(List<ToolbarPlan.Unit> units, double width)
    {
        var state = ToolbarPlan.States(units, 8, 32).Single(s => width >= s.Min && width < s.Max);
        return string.Concat(units.Select(u => state.BarHidden.Contains(u.Id) ? "" : u.Separator ? "|" : u.Id.ToString(System.Globalization.CultureInfo.InvariantCulture))) + (state.More ? "…" : "");
    }

    [Theory]
    // Four 100px units: all fit at 424px (3 gaps between them, 1 between the regions).
    [InlineData(new[] { "s", "s", "e", "e" }, 424, "1234")]
    // Ties go from the middle outward: the start from its end, then the end from its start.
    [InlineData(new[] { "s", "s", "e", "e" }, 423, "134…")]
    [InlineData(new[] { "s", "s", "e", "e" }, 300, "34…")]
    [InlineData(new[] { "s", "s", "e", "e" }, 200, "4…")]
    [InlineData(new[] { "s", "s", "e", "e" }, 100, "…")]
    // Higher priority stays longer; Never stays; Always is only in the menu.
    [InlineData(new[] { "s:1", "s", "e" }, 250, "13…")]
    [InlineData(new[] { "s:1", "s", "e" }, 150, "1…")]
    [InlineData(new[] { "s", "s", "e::never" }, 150, "3…")]
    [InlineData(new[] { "s", "s::always", "e" }, 1000, "13…")]
    // A separator goes when either side has nothing left.
    [InlineData(new[] { "s", "|", "s" }, 300, "1|3")]
    [InlineData(new[] { "s", "|", "s" }, 200, "1…")]
    public void Units_leave_the_bar_in_order(string[] specs, double width, string expected)
    {
        Assert.Equal(expected, Bar(Units(specs), width));
    }

    [Fact]
    public void A_center_stays_centered()
    {
        // Start 100, center 100, end 100: the center needs equal room on both sides.
        var units = Units("s", "c::never", "e");
        Assert.Equal("123", Bar(units, 316));
        Assert.Equal("2…", Bar(units, 315));
    }
}
