namespace IzzyBlazor.Tests;

// Borders, sizes, overflow and the new color tokens (styling API D10, D11).
public class StylePropTests : BunitContext
{
    [Fact]
    public void BorderY_draws_lines_above_and_below_in_the_border_color()
    {
        var box = Render<Box>(p => p.Add(b => b.BorderY, 1)).Find("div");

        Assert.Contains("ib-by", box.ClassName);
        Assert.DoesNotContain("ib-bx", box.ClassName);
        Assert.Contains("--ib-by:1px", box.GetAttribute("style"));
    }

    [Fact]
    public void Border_works_like_padding_sides_beat_the_axis_and_the_axis_beats_all()
    {
        var box = Render<Box>(p => p.Add(b => b.Border, 1).Add(b => b.BorderTop, 2).Add(b => b.BorderColor, Color.Outline)).Find("div");
        var style = box.GetAttribute("style")!;

        Assert.Contains("ib-bx", box.ClassName);
        Assert.Contains("--ib-bx:1px", style);
        Assert.Contains("--ib-bt:2px", style);
        Assert.Contains("--ib-bb:1px", style);
        Assert.Contains("--ib-bc:var(--ib-color-outline)", style);
    }

    [Fact]
    public void BorderColor_alone_recolors_a_components_own_border()
    {
        var card = Render<Card>(p => p.Add(c => c.BorderColor, Color.Primary)).Find(".ib-card");
        Assert.Contains("ib-bc", card.ClassName);
        Assert.DoesNotContain("ib-bx", card.ClassName);
    }

    [Fact]
    public void BorderStyle_sets_the_line_for_borders_set_here()
    {
        var box = Render<Box>(p => p
            .Add(b => b.Border, 1)
            .Add(b => b.BorderStyle, new Responsive<BorderStyle> { Base = BorderStyle.Dashed, Md = BorderStyle.Solid })).Find("div");
        var style = box.GetAttribute("style")!;

        Assert.Contains("ib-bst", box.ClassName);
        Assert.Contains("ib-bst-md", box.ClassName);
        Assert.Contains("--ib-bst:dashed", style);
        Assert.Contains("--ib-bst-md:solid", style);
    }

    [Fact]
    public void BorderStyle_alone_restyles_a_components_own_border()
    {
        var card = Render<Card>(p => p.Add(c => c.BorderStyle, BorderStyle.Dotted)).Find(".ib-card");
        Assert.Contains("ib-bst", card.ClassName);
        Assert.Contains("--ib-bst:dotted", card.GetAttribute("style"));
        Assert.DoesNotContain("ib-bx", card.ClassName);
    }

    [Fact]
    public void BorderStyle_at_a_breakpoint_survives_a_width_set_at_a_larger_one()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "izzyblazor.sln"))) dir = dir.Parent;
        var css = File.ReadAllText(Path.Combine(dir!.FullName, "src", "IzzyBlazor.Core", "Styles", "_generated", "style-props.css")).Replace("\r", "");

        Assert.Contains(".ib-bst-md { --ib-bst-now: var(--ib-bst-md); border-style: var(--ib-bst-md); }", css);
        Assert.Contains(".ib-bx { border-inline: var(--ib-bx) var(--ib-bst-now, solid) var(--ib-bc-now, var(--ib-color-border)); }", css);
    }

    [Theory]
    [InlineData(BorderStyle.Solid, "solid")]
    [InlineData(BorderStyle.Dashed, "dashed")]
    [InlineData(BorderStyle.Dotted, "dotted")]
    [InlineData(BorderStyle.None, "none")]
    public void BorderStyle_values_are_css_keywords(BorderStyle value, string css)
    {
        Assert.Equal(css, StyleValues.BorderStyle(value));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(17)]
    public void Border_widths_are_0_to_16_pixels(int width)
    {
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => Render<Box>(p => p.Add(b => b.Border, width)));
    }

    [Fact]
    public void Sizes_and_overflow_are_style_props()
    {
        var box = Render<Box>(p => p
            .Add(b => b.Height, Length.Rem(10))
            .Add(b => b.MaxHeight, Length.Rem(20))
            .Add(b => b.MinWidth, Length.Px(240))
            .Add(b => b.Overflow, new Responsive<Overflow> { Base = Overflow.Hidden, Md = Overflow.Auto })).Find("div");
        var style = box.GetAttribute("style")!;

        Assert.Contains("--ib-h:10rem", style);
        Assert.Contains("--ib-maxh:20rem", style);
        Assert.Contains("--ib-minw:240px", style);
        Assert.Contains("--ib-ov:hidden", style);
        Assert.Contains("--ib-ov-md:auto", style);
        Assert.Contains("ib-ov-md", box.ClassName);
    }

    [Fact]
    public void Soft_status_colors_pair_with_their_text_colors()
    {
        var box = Render<Box>(p => p.Add(b => b.Bg, Color.DangerSoft)).Find("div");
        Assert.Contains("--ib-bg:var(--ib-color-danger-soft)", box.GetAttribute("style"));
        Assert.Contains("--ib-fg:var(--ib-color-danger-text)", box.GetAttribute("style"));

        Assert.Equal(Color.SuccessText, Color.SuccessSoft.ContrastText);
        Assert.Equal(Color.WarningText, Color.WarningSoft.ContrastText);
        Assert.Equal(Color.InfoText, Color.InfoSoft.ContrastText);
        Assert.Equal(Color.PrimaryText, Color.PrimarySoft.ContrastText);
    }

    [Fact]
    public void Focus_carries_its_fallback()
    {
        Assert.Equal("var(--ib-color-focus, var(--ib-color-primary))", Color.Focus.Css);
        Assert.Equal(Color.OnPrimary, Color.Focus.ContrastText);
    }
}
