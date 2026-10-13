using System.Globalization;

namespace IzzyBlazor.Tests;

public class AccentPaletteTests
{
    // Brand colors chosen to stress the derivation: mid blue, dark green, light yellow, navy, a
    // light orange, mid gray, red, violet, black, white, light blue, lime.
    public static TheoryData<string> Brands => new()
    {
        "#2563eb", "#0a7d5a", "#facc15", "#1e3a8a", "#ff6a00", "#888888",
        "#e11d48", "#7c3aed", "#000000", "#ffffff", "#0ea5e9", "#84cc16", "#0af",
    };

    [Theory]
    [MemberData(nameof(Brands))]
    public void Every_pair_meets_AA(string brand)
    {
        var p = AccentPalette.From(brand);

        Assert.True(Ratio(p.OnFill, p.Fill) >= 4.5, $"text on fill {Ratio(p.OnFill, p.Fill):0.00}");
        Assert.True(Ratio(p.Text, "#ffffff") >= 4.5, "accent text on the surface");
        Assert.True(Ratio(p.Text, "#dfdfe3") >= 4.5, "accent text on the strongest gray fill");
        Assert.True(Ratio(p.Text, p.Soft) >= 4.5, "accent text on its soft tint");

        Assert.True(Ratio(p.DarkOnFill, p.DarkFill) >= 4.5, "dark: text on fill");
        Assert.True(Ratio(p.DarkFill, "#1b1b1e") >= 3, "dark: fill visible on the surface");
        Assert.True(Ratio(p.DarkText, "#313135") >= 4.5, "dark: accent text on the strongest gray fill");
        Assert.True(Ratio(p.DarkText, p.DarkSoft) >= 4.5, "dark: accent text on its soft tint");
    }

    [Fact]
    public void A_fill_that_carries_text_is_kept_as_given()
    {
        var p = AccentPalette.From("#2563eb");

        Assert.Equal("#2563eb", p.Fill);
        Assert.Equal("#ffffff", p.OnFill);
        Assert.Equal("#2563eb", p.DarkFill);
    }

    [Fact]
    public void A_light_fill_gets_dark_text_and_a_darker_accent_text()
    {
        var p = AccentPalette.From("#facc15");

        Assert.Equal("#facc15", p.Fill);
        Assert.Equal("#111111", p.OnFill);
        Assert.True(Ratio(p.Text, "#ffffff") >= 4.5);
    }

    [Fact]
    public void A_dark_brand_is_lifted_in_dark_mode()
    {
        var p = AccentPalette.From("#1e3a8a");

        Assert.Equal("#1e3a8a", p.Fill);
        Assert.NotEqual("#1e3a8a", p.DarkFill);
        Assert.True(Ratio(p.DarkFill, "#1b1b1e") >= 3);
    }

    [Fact]
    public void Css_is_the_four_primary_tokens_as_light_dark_pairs()
    {
        var css = AccentPalette.From("#2563eb").ToCss();

        Assert.StartsWith("--ib-color-primary: light-dark(#2563eb, #2563eb);", css);
        Assert.Contains("--ib-color-on-primary: light-dark(#ffffff, #ffffff);", css);
        Assert.Contains("--ib-color-primary-text: light-dark(", css);
        Assert.Contains("--ib-color-primary-soft: light-dark(", css);
    }

    [Theory]
    [InlineData("blue")]
    [InlineData("#12345")]
    [InlineData("2563eb")]
    [InlineData("")]
    public void Rejects_values_that_are_not_hex(string value) =>
        Assert.Throws<ArgumentException>(() => AccentPalette.From(value));

    private static double Ratio(string a, string b)
    {
        var (la, lb) = (Luminance(a), Luminance(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminance(string hex)
    {
        var v = int.Parse(hex[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        static double Lin(int c) => c / 255.0 <= 0.04045 ? c / 255.0 / 12.92 : Math.Pow((c / 255.0 + 0.055) / 1.055, 2.4);
        return 0.2126 * Lin((v >> 16) & 0xFF) + 0.7152 * Lin((v >> 8) & 0xFF) + 0.0722 * Lin(v & 0xFF);
    }
}

public class IzzyThemeRenderTests : BunitContext
{
    [Fact]
    public void Renders_the_primary_tokens_in_the_tokens_layer()
    {
        var style = Render<IzzyTheme>(p => p.Add(t => t.Primary, "#0a7d5a")).Find("style");

        Assert.True(style.HasAttribute("data-ib-theme-style"));
        Assert.StartsWith("@layer izzy.tokens { :root:root:root { --ib-color-primary: light-dark(#0a7d5a,", style.TextContent);
        Assert.Null(style.GetAttribute("nonce"));
    }

    [Fact]
    public void Look_accent_gray_and_radius_render_as_meta_tags_the_theme_css_matches()
    {
        var cut = Render<IzzyTheme>(p => p
            .Add(t => t.Look, ThemeLook.Compact)
            .Add(t => t.Accent, ThemeAccent.Teal)
            .Add(t => t.Gray, ThemeGray.Stone)
            .Add(t => t.Radius, ThemeRadius.Lg)
            .Add(t => t.Density, ThemeDensity.Comfortable));

        var metas = cut.FindAll("meta").Select(m => $"{m.GetAttribute("name")}={m.GetAttribute("content")}");
        Assert.Equal(["ib-theme=compact", "ib-accent=teal", "ib-gray=stone", "ib-radius=lg", "ib-density=comfortable"], metas);
        Assert.Empty(cut.FindAll("style"));
    }

    [Fact]
    public void Fonts_and_heading_weight_go_in_the_style_block()
    {
        var css = Render<IzzyTheme>(p => p
            .Add(t => t.Font, " Inter, system-ui, sans-serif ")
            .Add(t => t.HeadingFont, "\"Source Serif 4\", Georgia, serif")
            .Add(t => t.HeadingWeight, 700)).Find("style").TextContent;

        Assert.Equal(
            "@layer izzy.tokens { :root:root:root { --ib-font-sans: Inter, system-ui, sans-serif; --ib-font-heading: \"Source Serif 4\", Georgia, serif; --ib-weight-heading: 700; } }",
            css);
    }

    [Theory]
    [InlineData("Inter; color: red")]
    [InlineData("Inter } body { color: red")]
    [InlineData("</style><script>")]
    [InlineData("Inter /* x */")]
    public void Rejects_a_font_that_is_not_a_font_family_list(string font) =>
        Assert.Throws<ArgumentException>(() => Render<IzzyTheme>(p => p.Add(t => t.Font, font)));

    [Fact]
    public void Rejects_a_heading_weight_outside_100_to_900() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Render<IzzyTheme>(p => p.Add(t => t.HeadingWeight, 950)));

    [Fact]
    public void Renders_nothing_when_nothing_is_set() => Assert.Empty(Render<IzzyTheme>().Markup.Trim());

    [Fact]
    public void Passes_a_nonce_and_renders_nothing_without_a_color()
    {
        Assert.Equal("abc", Render<IzzyTheme>(p => p.Add(t => t.Primary, "#0af").Add(t => t.Nonce, "abc")).Find("style").GetAttribute("nonce"));
        Assert.Empty(Render<IzzyTheme>(p => p.Add(t => t.Primary, "")).FindAll("style"));
    }
}
