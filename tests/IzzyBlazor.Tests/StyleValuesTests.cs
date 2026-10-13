using IzzyBlazor.Units;

namespace IzzyBlazor.Tests;

public class StyleValuesTests
{
    [Theory]
    [InlineData(0, "var(--ib-space-0)")]
    [InlineData(4, "var(--ib-space-4)")]
    [InlineData(16, "var(--ib-space-16)")]
    public void Spacing(int step, string expected) => Assert.Equal(expected, StyleValues.Spacing(step));

    [Theory]
    [InlineData(-1)]
    [InlineData(17)]
    public void Spacing_outside_the_scale_throws(int step) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => StyleValues.Spacing(step));

    [Theory]
    [InlineData(-2, "calc(var(--ib-space-2) * -1)")]
    [InlineData(3, "var(--ib-space-3)")]
    public void Margin_allows_negative_steps(int step, string expected) =>
        Assert.Equal(expected, StyleValues.Margin(step));

    [Fact]
    public void Enums_map_to_css()
    {
        Assert.Equal("row-reverse", StyleValues.Direction(Direction.RowReverse));
        Assert.Equal("flex-start", StyleValues.Align(Align.Start));
        Assert.Equal("space-between", StyleValues.Justify(Justify.Between));
        Assert.Equal("var(--ib-radius-full)", StyleValues.Radius(Radius.Full));
        Assert.Equal("var(--ib-shadow-md)", StyleValues.Shadow(Shadow.Md));
        Assert.Equal("wrap", StyleValues.Wrap(true));
        Assert.Equal("0", StyleValues.Factor(false));
    }

    [Fact]
    public void Colors()
    {
        Assert.Equal("var(--ib-color-surface-container-high)", Color.SurfaceContainerHigh.Css);
        Assert.Equal("var(--ib-color-surface-container-low)", Color.SurfaceContainerLow.Css);
        Assert.Equal("var(--ib-color-brand-500)", Color.Token("brand-500").Css);
        Assert.Equal("#ff6600", Color.Of("#ff6600").Css);
        Assert.Throws<FormatException>(() => Color.Token("Brand 500"));
    }

    [Fact]
    public void Contrast_text_pairs()
    {
        Assert.Equal(Color.OnPrimary, Color.Primary.ContrastText);
        Assert.Equal(Color.OnSurface, Color.SurfaceContainerHigh.ContrastText);
        Assert.Equal(Color.OnSurface, Color.Background.ContrastText);
        Assert.Equal(Color.PrimaryText, Color.PrimarySoft.ContrastText);
        Assert.Equal(Color.Token("on-brand"), Color.Token("brand").ContrastText);
        Assert.Null(Color.OnPrimary.ContrastText);
        Assert.Null(Color.DangerText.ContrastText);
        Assert.Null(Color.Muted.ContrastText);
        Assert.Null(Color.Of("#ff6600").ContrastText);
    }

    [Fact]
    public void Lengths()
    {
        Assert.Equal("var(--ib-size-prose)", Length.Prose.Css);
        Assert.Equal("100%", Length.Full.Css);
        Assert.Equal("1.5rem", Length.Rem(1.5).Css);
        Assert.Equal("calc(100% - 2rem)", Length.Of("calc(100% - 2rem)").Css);
    }

    [Fact]
    public void Unit_extensions_match_the_factories()
    {
        Assert.Equal(Length.Rem(15), 15.Rem);
        Assert.Equal(Length.Rem(1.5), 1.5.Rem);
        Assert.Equal(Length.Px(240), 240.Px);
        Assert.Equal(Length.Dvh(100), 100.Dvh);
    }

    [Theory]
    [InlineData("red; background: blue")]
    [InlineData("1px}body{color:red")]
    [InlineData("url(javascript:alert(1))")]
    [InlineData("10px /* comment */")]
    public void Unsafe_raw_values_are_rejected(string css)
    {
        Assert.Throws<FormatException>(() => Length.Of(css));
        Assert.Throws<FormatException>(() => Color.Of(css));
    }
}
