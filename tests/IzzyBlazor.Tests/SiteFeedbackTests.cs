using Microsoft.AspNetCore.Components.Forms;

namespace IzzyBlazor.Tests;

public class SiteFeedbackTests : BunitContext
{
    public class Model
    {
        public string? Email { get; set; }
    }

    [Fact]
    public void HideLabel_keeps_the_label_for_screen_readers_only()
    {
        var model = new Model();
        var cut = Render<TextField>(p => p
            .AddCascadingValue(new EditContext(model))
            .Add(f => f.ValueExpression, () => model.Email)
            .Add(f => f.Value, model.Email)
            .Add(f => f.Label, "Email")
            .Add(f => f.HideLabel, true));

        var label = cut.Find("label");
        Assert.Equal("Email", label.TextContent.Trim());
        Assert.Contains("ib-visually-hidden", label.ClassName);
        Assert.Equal(cut.Find("input").Id, label.GetAttribute("for"));
    }

    [Fact]
    public void IzzyTheme_sets_the_mono_font()
    {
        var style = Render<IzzyTheme>(p => p.Add(t => t.MonoFont, "'JetBrains Mono', ui-monospace, monospace")).Find("style");

        Assert.Contains("--ib-font-mono: 'JetBrains Mono', ui-monospace, monospace;", style.TextContent);
    }

    [Fact]
    public void IzzyTheme_rejects_a_mono_font_that_escapes_the_declaration()
    {
        Assert.ThrowsAny<ArgumentException>(() => Render<IzzyTheme>(p => p.Add(t => t.MonoFont, "x; } body { color: red")));
    }

    [Fact]
    public void Fluid_text_size_runs_from_min_at_360px_to_size_at_1280px()
    {
        Assert.Equal(
            "clamp(var(--ib-text-3xl), 1.4348rem + 1.9565vw, var(--ib-text-5xl))",
            StyleValues.FluidTextSize(TextSize.Xl3, TextSize.Xl5));

        // Given the wrong way round, the sizes are swapped.
        Assert.Equal(StyleValues.FluidTextSize(TextSize.Xl3, TextSize.Xl5), StyleValues.FluidTextSize(TextSize.Xl5, TextSize.Xl3));
    }

    [Fact]
    public void Heading_with_MinSize_is_fluid()
    {
        var h1 = Render<Heading>(p => p
            .Add(h => h.Level, 1)
            .Add(h => h.Size, TextSize.Xl5)
            .Add(h => h.MinSize, TextSize.Xl3)
            .AddChildContent("Hi")).Find("h1");

        Assert.Contains("ib-fs", h1.ClassList);
        Assert.Contains("--ib-fs:clamp(var(--ib-text-3xl), 1.4348rem + 1.9565vw, var(--ib-text-5xl));", h1.GetAttribute("style"));
    }
}
