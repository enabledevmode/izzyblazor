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

        // A link stays a link; an icon button's label is its text. Publish (Never) isn't in the
        // menu, and Print (Always) is.
        Assert.Equal("/help", panel.QuerySelectorAll("a").Single(a => a.TextContent.Trim() == "Help").GetAttribute("href"));
        var texts = panel.QuerySelectorAll(".ib-menu-label").Select(l => l.TextContent.Trim()).ToList();
        Assert.Equal(["Undo", "Redo", "Archive", "Help", "Export", "CSV", "Print"], texts);

        // A ToolbarButtonGroup is a labeled group; a ToolbarMenu is a submenu with its items.
        Assert.Equal("History", panel.QuerySelector("[role=group] .ib-menu-heading")!.TextContent.Trim());
        Assert.NotNull(panel.QuerySelector("ib-popover [popover] a[href='/export.csv']"));
    }

    [Fact]
    public void A_toggle_turns_on_from_the_bar_and_from_the_menu()
    {
        var cut = RenderToolbar("toggles", interactive: true);

        cut.Find(".ib-toolbar-unit button.ib-btn:not([aria-label])").Click();
        Assert.True(cut.Instance.Preview);
        Assert.Equal("true", cut.Find(".ib-toolbar-unit button.ib-btn:not([aria-label])").GetAttribute("aria-pressed"));

        MenuPanel(cut).QuerySelectorAll(".ib-menu-item").Single(i => i.TextContent.Trim() == "Bold").Click();
        Assert.False(cut.Instance.Bold);
        Assert.Equal("false", cut.Find(".ib-toolbar-unit button[aria-label='Bold']").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void A_control_inside_a_component_of_the_apps_still_joins()
    {
        var texts = MenuPanel(RenderToolbar("wrapped")).QuerySelectorAll(".ib-menu-label").Select(l => l.TextContent.Trim());

        Assert.Contains("Inside", texts);
    }

    // Units for the plan: id, region, width, priority, overflow; "|" is a separator.
    private static List<ToolbarPlan.Unit> Units(params string[] specs) => specs.Select((spec, i) =>
    {
        if (spec == "|")
        {
            return new ToolbarPlan.Unit(i + 1, ToolbarPosition.Start, 1, 0, ToolbarOverflow.Never, true);
        }

        var parts = spec.Split(':');
        var region = parts[0][0] switch { 'c' => ToolbarPosition.Center, 'e' => ToolbarPosition.End, _ => ToolbarPosition.Start };
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
    [InlineData(new[] { "s", "s", "e", "e" }, 100, "…")]
    // Higher priority stays longer; Never stays; Always is only in the menu.
    [InlineData(new[] { "s:1", "s", "e" }, 250, "13…")]
    [InlineData(new[] { "s", "s", "e::never" }, 150, "3…")]
    [InlineData(new[] { "s", "s::always", "e" }, 1000, "13…")]
    // A separator goes when either side has nothing left.
    [InlineData(new[] { "s", "|", "s" }, 300, "1|3")]
    public void Units_leave_the_bar_in_order(string[] specs, double width, string expected)
    {
        Assert.Equal(expected, Bar(Units(specs), width));
    }

    // The bar at this width with labels collapsing first: units 1 and 2 collapse to 32px, and "c"
    // marks a collapsed state.
    private static string Collapsing(bool collapse, double width)
    {
        var units = Units("s", "s", "e").Select(u => u.Id < 3 ? u with { CollapsedWidth = 32 } : u).ToList();
        var state = ToolbarPlan.States(units, 8, 32, collapse).Single(s => width >= s.Min && width < s.Max);
        return string.Concat(units.Where(u => !state.BarHidden.Contains(u.Id)).Select(u => u.Id)) + (state.More ? "…" : "") + (state.Collapsed ? "c" : "");
    }

    [Theory]
    // All three at full width need 316px; below that the labels collapse (72px of start) before
    // anything moves, and then units move at their collapsed widths.
    [InlineData(true, 316, "123")]
    [InlineData(true, 315, "123c")]
    [InlineData(true, 179, "3…c")]
    // Without CollapseLabels, a unit moves instead.
    [InlineData(false, 315, "13…")]
    public void Labels_collapse_before_units_move(bool collapse, double width, string expected)
    {
        Assert.Equal(expected, Collapsing(collapse, width));
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
