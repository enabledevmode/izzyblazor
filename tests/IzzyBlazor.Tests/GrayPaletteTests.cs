namespace IzzyBlazor.Tests;

// Brand and Gray take a built-in name or a hex color (styling API D2).
public class GrayPaletteTests : BunitContext
{
    [Theory]
    [InlineData("#f4efe6")]
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

    [Theory]
    [InlineData("tael")]
    [InlineData("0a7d5a")]
    [InlineData("#0a7d5a; } body {")]
    public void Brand_and_gray_take_a_name_or_hex_and_say_which(string value)
    {
        var brand = Assert.ThrowsAny<ArgumentException>(() => Render<IzzyTheme>(p => p.Add(t => t.Brand, value)));
        Assert.Contains("blue, ink, indigo, violet, teal, green, rose", brand.Message);
        var gray = Assert.ThrowsAny<ArgumentException>(() => Render<IzzyTheme>(p => p.Add(t => t.Gray, value)));
        Assert.Contains("neutral, slate, stone", gray.Message);
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
