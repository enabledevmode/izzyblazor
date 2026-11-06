using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace IzzyBlazor.Tests;

public class ColorPanelTests : BunitContext
{
    public class Model
    {
        public string? Color { get; set; }
    }

    public ColorPanelTests() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    // color-panel.ts does the same arithmetic in the browser: a case that changes here changes there.
    [Theory]
    [InlineData("#2563eb")]
    [InlineData("#ffffff")]
    [InlineData("#000000")]
    [InlineData("#7f7f7f")]
    [InlineData("#ef4444cc")]
    public void Hex_comes_back_from_hsv_unchanged(string hex)
    {
        var color = ColorPanelMath.Parse(hex)!.Value;
        Assert.Equal(hex, ColorPanelMath.ToHex(ColorPanelMath.ToRgba(ColorPanelMath.ToHsv(color), color.A)));
    }

    [Theory]
    [InlineData("#1d4ed8", "dark blue")]
    [InlineData("#86efac", "light green")]
    [InlineData("#ef4444", "red")]
    [InlineData("#71717a", "gray")]
    [InlineData("#000000", "black")]
    [InlineData("#ffffff", "white")]
    public void Colors_have_plain_names(string hex, string name) =>
        Assert.Equal(name, ColorPanelMath.Name(ColorPanelMath.Parse(hex)!.Value, new IzzyText()));

    [Fact]
    public void Renders_named_swatches_a_2d_slider_and_posts_the_chosen_swatch_with_no_script()
    {
        var model = new Model { Color = "#3B82F6" };
        var cut = Render<ColorPanel>(p => p
            .AddCascadingValue(new EditContext(model))
            .Add(f => f.ValueExpression, () => model.Color)
            .Add(f => f.Value, model.Color)
            .Add(f => f.ValueChanged, v => model.Color = v)
            .Add(f => f.Label, "Accent"));

        var swatches = cut.FindAll(".ib-cp-swatch-input");
        Assert.Equal(21, swatches.Count);
        Assert.All(swatches, s => Assert.Equal("model.Color", s.GetAttribute("name")));
        var chosen = Assert.Single(swatches, s => s.HasAttribute("checked"));
        Assert.Equal("#3b82f6", chosen.GetAttribute("value"));
        Assert.Equal("blue", chosen.GetAttribute("aria-label"));

        var area = cut.Find(".ib-cp-area");
        Assert.Equal("slider", area.GetAttribute("role"));
        Assert.Equal("2D slider", area.GetAttribute("aria-roledescription"));
        Assert.Equal("Saturation 76%, brightness 96%, blue", area.GetAttribute("aria-valuetext"));

        // With swatches to post, the hex field doesn't, and the hidden field only does once the script names it.
        Assert.Null(cut.Find("[data-ib-field=hex]").GetAttribute("name"));
        Assert.Null(cut.Find(".ib-cp-value").GetAttribute("name"));
    }
}
