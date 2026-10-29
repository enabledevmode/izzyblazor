namespace IzzyBlazor.Tests;

// Brand and Gray take a built-in name or a hex color (styling API D2).
public class GrayPaletteTests : BunitContext
{
    [Theory]
    [InlineData("#f4efe6")]
    [InlineData("#eef3fb")]
    [InlineData("#e8f5ee")]
    [InlineData("#fafafa")]
    [InlineData("#ffffff")]
    [InlineData("#d9d2c0")]
    public void A_custom_gray_keeps_text_readable_in_light_and_dark(string hex)
    {
        var p = Colors(GrayPalette.From(hex));
        foreach (var scheme in new[] { 0, 1 })
        {
            string C(string token) => scheme == 0 ? p[token].Light : p[token].Dark;
            foreach (var text in new[] { "muted", "subtle" })
            {
                foreach (var on in new[] { "background", "surface", "fill" })
                {
                    Assert.True(Ratio(C(text), C(on)) >= 4.5, $"{hex} {text} on {on} ({(scheme == 0 ? "light" : "dark")}): {Ratio(C(text), C(on)):F2}");
                }
            }

            Assert.True(Ratio(C("on-surface"), C("fill-strong")) >= 7, $"{hex} body text ({scheme})");
        }
    }

    [Fact]
    public void The_hex_is_the_light_page_exactly_and_the_ladder_steps_down_from_it()
    {
        var p = Colors(GrayPalette.From("#F4EFE6"));

        Assert.Equal("#f4efe6", p["background"].Light);
        Assert.True(Lum(p["surface"].Light) > Lum(p["background"].Light));
        Assert.True(Lum(p["fill-subtle"].Light) > Lum(p["fill"].Light));
        Assert.True(Lum(p["fill"].Light) > Lum(p["fill-strong"].Light));
        Assert.True(Lum(p["background"].Dark) < Lum(p["surface"].Dark));
        // Sunken sits below the surface in both modes, so cards on it lift.
        Assert.True(Lum(p["surface-sunken"].Light) < Lum(p["surface"].Light));
        Assert.True(Lum(p["surface-sunken"].Dark) < Lum(p["surface"].Dark));
        // Warm stays warm in dark mode: red above blue.
        Assert.True(Convert.ToInt32(p["background"].Dark[1..3], 16) > Convert.ToInt32(p["background"].Dark[5..7], 16));
    }

    [Fact]
    public void A_near_neutral_page_gives_the_neutral_set()
    {
        var p = Colors(GrayPalette.From("#fafafa"));
        Assert.Equal("#ffffff", p["surface"].Light);
        Assert.Equal("#ffffff", p["surface-overlay"].Light);
        Assert.Equal(13, p.Count);
    }

    [Fact]
    public void A_dark_page_color_is_lifted_to_stay_light()
    {
        var p = Colors(GrayPalette.From("#333333"));
        Assert.True(Lum(p["background"].Light) > 0.7);
    }

    [Fact]
    public void Gray_takes_a_built_in_name_as_a_meta_tag_and_a_hex_as_tokens()
    {
        Assert.Equal("stone", Render<IzzyTheme>(p => p.Add(t => t.Gray, "Stone")).Find("meta[name='ib-gray']").GetAttribute("content"));

        var style = Render<IzzyTheme>(p => p.Add(t => t.Gray, "#f4efe6")).Find("style").TextContent;
        Assert.Contains("--ib-color-background: light-dark(#f4efe6, ", style);
        Assert.Contains("--ib-color-muted: light-dark(", style);
    }

    [Fact]
    public void Brand_takes_a_built_in_name_as_a_meta_tag_and_a_hex_as_tokens()
    {
        Assert.Equal("teal", Render<IzzyTheme>(p => p.Add(t => t.Brand, "teal")).Find("meta[name='ib-brand']").GetAttribute("content"));

        var cut = Render<IzzyTheme>(p => p.Add(t => t.Brand, "#0A7D5A"));
        Assert.Empty(cut.FindAll("meta[name='ib-brand']"));
        Assert.Contains("--ib-color-primary: light-dark(#0a7d5a, ", cut.Find("style").TextContent);
    }

    [Theory]
    [InlineData("tael")]
    [InlineData("0a7d5a")]
    [InlineData("#0a7d5a; } body {")]
    [InlineData("red")]
    public void Brand_and_gray_take_a_name_or_hex_and_say_which(string value)
    {
        var brand = Assert.ThrowsAny<ArgumentException>(() => Render<IzzyTheme>(p => p.Add(t => t.Brand, value)));
        Assert.Contains("blue, ink, indigo, violet, teal, green, rose", brand.Message);
        var gray = Assert.ThrowsAny<ArgumentException>(() => Render<IzzyTheme>(p => p.Add(t => t.Gray, value)));
        Assert.Contains("neutral, slate, stone", gray.Message);
    }

    [Fact]
    public void Part_of_a_page_takes_a_brand_and_a_built_in_gray_but_not_a_hex_gray()
    {
        var part = Render<IzzyTheme>(p => p.Add(t => t.Brand, "rose").Add(t => t.Gray, "slate").AddChildContent("x")).Find("div");
        Assert.Equal("rose", part.GetAttribute("data-ib-brand"));
        Assert.Equal("slate", part.GetAttribute("data-ib-gray"));

        var e = Assert.ThrowsAny<ArgumentException>(() => Render<IzzyTheme>(p => p.Add(t => t.Gray, "#f4efe6").AddChildContent("x")));
        Assert.Contains("app-wide", e.Message);
    }

    private static Dictionary<string, (string Light, string Dark)> Colors(GrayPalette palette) =>
        palette.Colors.ToDictionary(c => c.Token, c => (c.Light, c.Dark));

    private static double Lum(string hex)
    {
        static double Lin(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        var v = Convert.ToInt32(hex[1..], 16);
        return 0.2126 * Lin(((v >> 16) & 0xFF) / 255.0) + 0.7152 * Lin(((v >> 8) & 0xFF) / 255.0) + 0.0722 * Lin((v & 0xFF) / 255.0);
    }

    private static double Ratio(string a, string b)
    {
        var (x, y) = (Lum(a), Lum(b));
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }
}
