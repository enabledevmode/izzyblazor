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
