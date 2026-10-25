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
    public void A_toolbar_label_is_optional()
    {
        SetRendererInfo(new RendererInfo("Static", false));
        var cut = Render<Toolbar>(p => p.AddChildContent("x"));
        Assert.Null(cut.Find("ib-toolbar").GetAttribute("aria-label"));
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

        // A link stays a link; an icon button's label is its text. Publish (Never) isn't in the
        // menu, and Print (Always) is.
        Assert.Equal("/help", panel.QuerySelectorAll("a").Single(a => a.TextContent.Trim() == "Help").GetAttribute("href"));
        var texts = panel.QuerySelectorAll(".ib-menu-label").Select(l => l.TextContent.Trim()).ToList();
        Assert.Equal(["Undo", "Redo", "Archive", "Help", "Export", "CSV", "Print"], texts);

        // A ToolbarButtonGroup is a labeled group; a ToolbarMenu is a submenu with its items.
        Assert.Equal("History", panel.QuerySelector("[role=group] .ib-menu-heading")!.TextContent.Trim());
        Assert.NotNull(panel.QuerySelector("ib-popover [popover] a[href='/export.csv']"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_toggle_is_a_checkbox_item_in_the_menu(bool interactive)
    {
        var cut = RenderToolbar("toggles", interactive);
        var items = MenuPanel(cut).QuerySelectorAll(".ib-menu-item").ToDictionary(i => i.TextContent.Trim());

        // Roles and states wait for the script on static pages, as for any menu item.
        string? Attr(AngleSharp.Dom.IElement item, string name) => item.GetAttribute(interactive ? name : "data-ib-" + name.Replace("aria-", ""));
        Assert.Equal("menuitemcheckbox", Attr(items["Bold"], "role"));
        Assert.Equal("true", Attr(items["Bold"], "aria-checked"));
        Assert.Equal("false", Attr(items["Italic"], "aria-checked"));
        Assert.Equal("false", Attr(items["Preview"], "aria-checked"));
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
    public void Items_sit_at_their_position()
    {
        var cut = RenderToolbar("controls");
        // Each unit by its first control's text, or its name when that has none (a group of icon buttons); a separator is empty.
        string Texts(string region) => string.Join(",", cut.FindAll($"ib-toolbar > {region} > .ib-toolbar-unit")
            .Select(u => u.QuerySelector("button, a, .ib-toolbar-text")?.TextContent.Trim() is { Length: > 0 } text ? text : u.QuerySelector("[aria-label]")?.GetAttribute("aria-label")));

        Assert.Equal("History,,Archive,Help", Texts(".ib-toolbar-start"));
        Assert.Equal("Untitled", Texts(".ib-toolbar-center"));
        // Print (Always) has a bar copy too, which the CSS and the script keep hidden.
        Assert.Equal("Export,Publish,Print", Texts(".ib-toolbar-end"));
    }

    [Fact]
    public void A_control_inside_a_component_of_the_apps_still_joins()
    {
        var texts = MenuPanel(RenderToolbar("wrapped")).QuerySelectorAll(".ib-menu-label").Select(l => l.TextContent.Trim());

        Assert.Contains("Inside", texts);
    }

    [Theory]
    [InlineData("button", "use ToolbarButton")]
    [InlineData("icon-button", "use ToolbarIconButton")]
    [InlineData("button-group", "use ToolbarButtonGroup")]
    [InlineData("menu", "use ToolbarMenu")]
    [InlineData("tooltip", "put it in a ToolbarItem")]
    [InlineData("in-group", "use ToolbarButton or ToolbarIconButton")]
    [InlineData("placed-in-group", "set them on the group")]
    [InlineData("toolbar-button-in-item", "directly inside a Toolbar")]
    [InlineData("outside", "directly inside a Toolbar")]
    public void Only_toolbar_items_go_directly_in_a_toolbar(string kind, string message)
    {
        var error = Assert.Throws<InvalidOperationException>(() => RenderToolbar(kind));

        Assert.Contains(message, error.Message, StringComparison.Ordinal);
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

        // Only Plain can move: a reset button, items without MenuContent and text stay, and the
        // buttons inside them are plain buttons.
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
        // A group with no settings of its own, a menu's trigger and a button in an item take the toolbar's.
        Assert.Equal("ib-btn ib-btn-neutral ib-btn-ghost ib-btn-sm", Classes("In a group"));
        Assert.StartsWith("ib-btn ib-btn-neutral ib-btn-ghost ib-btn-sm", Classes("Menu"));
        Assert.Equal("ib-btn ib-btn-neutral ib-btn-ghost ib-btn-sm", Classes("In an item"));
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
        var cut = Render<Toolbar>(p => p.Add(t => t.Label, "Live").AddChildContent<ToolbarButton>(b => b.AddChildContent("Before")));
        Assert.Equal("Before", cut.Find(".ib-toolbar-more .ib-menu-label").TextContent.Trim());
        Assert.Equal("Before", cut.Find(".ib-toolbar-unit").TextContent.Trim());

        cut.Render(p => p.Add(t => t.Label, "Live").AddChildContent<ToolbarButton>(b => b.AddChildContent("After")));
        Assert.Equal("After", cut.Find(".ib-toolbar-unit").TextContent.Trim());
        Assert.Equal("After", cut.Find(".ib-toolbar-more .ib-menu-label").TextContent.Trim());
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

    [Fact]
    public void With_a_center_the_wider_side_goes_first()
    {
        // Start 100, end 208: dropping the start would leave the width as it is, so the end goes first.
        var units = Units("s", "c::never", "e", "e");
        Assert.Equal("1234", Bar(units, 532));
        Assert.Equal("124…", Bar(units, 400));
    }
}
