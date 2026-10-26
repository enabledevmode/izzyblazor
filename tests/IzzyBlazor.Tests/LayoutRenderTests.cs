namespace IzzyBlazor.Tests;

public class LayoutRenderTests : BunitContext
{
    [Fact]
    public void Box_renders_a_div_with_no_attributes_by_default()
    {
        var cut = Render<Box>(p => p.AddChildContent("Hello"));

        cut.MarkupMatches("<div class=\"ib-box\">Hello</div>");
    }

    [Fact]
    public void Box_renders_the_As_element()
    {
        var cut = Render<Box>(p => p.Add(b => b.As, "section"));

        Assert.Equal("SECTION", cut.Find(".ib-box").TagName);
    }

    [Fact]
    public void Invalid_As_throws()
    {
        Assert.Throws<ArgumentException>(() => Render<Box>(p => p.Add(b => b.As, "div onclick=x")));
    }

    [Fact]
    public void Single_value_adds_one_class_and_variable()
    {
        var cut = Render<Box>(p => p.Add(b => b.Padding, 4));
        var box = cut.Find("div");

        Assert.Equal("ib-box ib-px ib-py", box.ClassName);
        Assert.Equal("--ib-px:var(--ib-space-4);--ib-py:var(--ib-space-4);", box.GetAttribute("style"));
    }

    [Fact]
    public void Map_adds_one_class_and_variable_per_breakpoint()
    {
        var cut = Render<Box>(p => p.Add(b => b.PaddingX, new Responsive<int> { Base = 4, Md = 10 }));
        var box = cut.Find("div");

        Assert.Equal("ib-box ib-px ib-px-md", box.ClassName);
        Assert.Equal("--ib-px:var(--ib-space-4);--ib-px-md:var(--ib-space-10);", box.GetAttribute("style"));
    }

    [Fact]
    public void Value_only_at_a_breakpoint_adds_no_base_class()
    {
        var cut = Render<Stack>(p => p.Add(s => s.Direction, new Responsive<Direction> { Md = Direction.Row }));

        Assert.Equal("ib-stack ib-dir-md", cut.Find("div").ClassName);
    }

    [Fact]
    public void Axis_props_replace_the_shorthand()
    {
        var cut = Render<Box>(p => p
            .Add(b => b.Padding, 2)
            .Add(b => b.PaddingX, 8));

        Assert.Equal("--ib-px:var(--ib-space-8);--ib-py:var(--ib-space-2);", cut.Find("div").GetAttribute("style"));
    }

    [Fact]
    public void Side_props_split_their_axis_and_fall_back_to_it()
    {
        var cut = Render<Box>(p => p
            .Add(b => b.Padding, 4)
            .Add(b => b.PaddingTop, 0)
            .Add(b => b.MarginEnd, new Responsive<int> { Md = -2 }));
        var box = cut.Find("div");

        // Inline padding stays one class; block padding splits, the bottom keeping the shorthand.
        Assert.Equal("ib-box ib-px ib-pt ib-pb ib-me-md", box.ClassName);
        Assert.Equal(
            "--ib-px:var(--ib-space-4);--ib-pt:var(--ib-space-0);--ib-pb:var(--ib-space-4);--ib-me-md:calc(var(--ib-space-2) * -1);",
            box.GetAttribute("style"));
    }

    [Fact]
    public void Theme_background_gets_its_contrast_text_automatically()
    {
        var cut = Render<Box>(p => p.Add(b => b.Bg, Color.Primary));

        Assert.Equal("--ib-bg:var(--ib-color-primary);--ib-fg:var(--ib-color-on-primary);", cut.Find("div").GetAttribute("style"));
    }

    [Fact]
    public void Surfaces_pair_with_on_surface()
    {
        var cut = Render<Box>(p => p.Add(b => b.Bg, Color.Fill));

        Assert.Contains("--ib-fg:var(--ib-color-on-surface);", cut.Find("div").GetAttribute("style"));
    }

    [Fact]
    public void Explicit_color_wins_over_automatic_contrast()
    {
        var cut = Render<Box>(p => p
            .Add(b => b.Bg, Color.Primary)
            .Add(b => b.Color, Color.Surface));

        Assert.Equal("--ib-bg:var(--ib-color-primary);--ib-fg:var(--ib-color-surface);", cut.Find("div").GetAttribute("style"));
    }

    [Fact]
    public void Raw_background_gets_no_automatic_text_color()
    {
        var cut = Render<Box>(p => p.Add(b => b.Bg, Color.Of("#ff6600")));

        Assert.Equal("ib-box ib-bg", cut.Find("div").ClassName);
    }

    [Fact]
    public void Responsive_background_pairs_per_breakpoint()
    {
        var cut = Render<Box>(p => p.Add(b => b.Bg, new Responsive<Color> { Base = Color.Surface, Md = Color.Primary }));

        Assert.Equal(
            "--ib-bg:var(--ib-color-surface);--ib-bg-md:var(--ib-color-primary);--ib-fg:var(--ib-color-on-surface);--ib-fg-md:var(--ib-color-on-primary);",
            cut.Find("div").GetAttribute("style"));
    }

    [Fact]
    public void Surface_props_use_tokens()
    {
        var cut = Render<Box>(p => p
            .Add(b => b.Bg, Color.Primary)
            .Add(b => b.Color, Color.OnPrimary)
            .Add(b => b.Radius, Radius.Md)
            .Add(b => b.MaxWidth, Length.Prose));

        Assert.Equal(
            "--ib-bg:var(--ib-color-primary);--ib-fg:var(--ib-color-on-primary);--ib-rounded:var(--ib-radius-md);--ib-maxw:var(--ib-size-prose);",
            cut.Find("div").GetAttribute("style"));
    }

    [Fact]
    public void User_class_and_style_come_after_the_components_own()
    {
        var cut = Render<Box>(p => p
            .Add(b => b.Bg, Color.Primary)
            .Add(b => b.Class, "card")
            .Add(b => b.Style, "font-style: italic"));
        var box = cut.Find("div");

        Assert.Equal("ib-box ib-bg ib-fg card", box.ClassName);
        Assert.Equal("--ib-bg:var(--ib-color-primary);--ib-fg:var(--ib-color-on-primary);font-style: italic;", box.GetAttribute("style"));
    }

    [Fact]
    public void Unmatched_attributes_pass_through()
    {
        var cut = Render<Box>(p => p
            .AddUnmatched("id", "hero")
            .AddUnmatched("aria-label", "Hero"));
        var box = cut.Find("div");

        Assert.Equal("hero", box.Id);
        Assert.Equal("Hero", box.GetAttribute("aria-label"));
    }

    [Fact]
    public void Visibility_props_add_classes()
    {
        var cut = Render<Box>(p => p
            .Add(b => b.HiddenFrom, Breakpoint.Md)
            .Add(b => b.VisibleFrom, Breakpoint.Sm));

        Assert.Equal("ib-box ib-hidden-from-md ib-visible-from-sm", cut.Find("div").ClassName);
    }

    [Fact]
    public void Stack_maps_layout_values_to_css()
    {
        var cut = Render<Stack>(p => p
            .Add(s => s.Gap, 4)
            .Add(s => s.Align, Align.Center)
            .Add(s => s.Justify, Justify.Between)
            .Add(s => s.Wrap, true));

        Assert.Equal(
            "--ib-gap:var(--ib-space-4);--ib-align:center;--ib-justify:space-between;--ib-wrap:wrap;",
            cut.Find("div").GetAttribute("style"));
    }

    [Fact]
    public void Row_defaults_to_row_and_Column_to_column()
    {
        Assert.Equal("--ib-dir:row;", Render<Row>().Find("div").GetAttribute("style"));
        Assert.Equal("--ib-dir:column;", Render<Column>().Find("div").GetAttribute("style"));
    }

    [Fact]
    public void Stack_direction_can_change_per_breakpoint()
    {
        var cut = Render<Stack>(p => p.Add(s => s.Direction, new Responsive<Direction> { Base = Direction.Column, Md = Direction.Row }));

        Assert.Equal("--ib-dir:column;--ib-dir-md:row;", cut.Find("div").GetAttribute("style"));
    }

    [Fact]
    public void Row_and_Column_fix_their_direction()
    {
        // Only Stack takes Direction: on Row and Column it's a build error, not a silent no-op.
        foreach (var type in new[] { typeof(Row), typeof(Column) })
        {
            var obsolete = type.GetProperty("Direction")!.GetCustomAttributes(typeof(ObsoleteAttribute), false).Cast<ObsoleteAttribute>().Single();
            Assert.True(obsolete.IsError);
        }

        Assert.Empty(typeof(Stack).GetProperty("Direction")!.GetCustomAttributes(typeof(ObsoleteAttribute), false));
    }

    [Fact]
    public void Inline_stack_adds_the_inline_class()
    {
        var cut = Render<Stack>(p => p.Add(s => s.Inline, true));

        Assert.Contains("ib-stack-inline", cut.Find("div").ClassName);
    }
}
