using System.Globalization;

namespace IzzyBlazor;

/// <summary>
/// The color arithmetic ColorPanel draws with: hex with or without alpha, to and from RGB and HSV,
/// and a plain name for a color ("dark blue"), which screen readers hear. color-panel.ts does the
/// same in the browser.
/// </summary>
internal static class ColorPanelMath
{
    /// <summary>A color: red, green and blue 0-255, alpha 0-1.</summary>
    public readonly record struct Rgba(int R, int G, int B, double A = 1);

    /// <summary>Hue 0-360, saturation and value 0-1.</summary>
    public readonly record struct Hsv(double H, double S, double V);

    /// <summary>A hex color, <c>#rgb</c>, <c>#rrggbb</c> or <c>#rrggbbaa</c>, any case; null when it isn't one.</summary>
    public static Rgba? Parse(string? value)
    {
        var text = value?.Trim().TrimStart('#');
        if (text is null || !text.All(char.IsAsciiHexDigit)) return null;
        if (text.Length == 3) text = string.Concat(text.Select(c => $"{c}{c}"));
        if (text.Length is not (6 or 8)) return null;
        int Byte(int at) => int.Parse(text.AsSpan(at, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new Rgba(Byte(0), Byte(2), Byte(4), text.Length == 8 ? Math.Round(Byte(6) / 255.0, 2, MidpointRounding.AwayFromZero) : 1);
    }

    /// <summary><c>#rrggbb</c>, or <c>#rrggbbaa</c> when the color isn't opaque, lowercase.</summary>
    public static string ToHex(Rgba color)
    {
        var hex = $"#{color.R:x2}{color.G:x2}{color.B:x2}";
        return color.A < 1 ? hex + ((int)Math.Round(color.A * 255, MidpointRounding.AwayFromZero)).ToString("x2", CultureInfo.InvariantCulture) : hex;
    }

    public static Hsv ToHsv(Rgba color)
    {
        double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var delta = max - Math.Min(r, Math.Min(g, b));
        var h = delta == 0 ? 0
            : max == r ? 60 * (((g - b) / delta % 6 + 6) % 6)
            : max == g ? 60 * ((b - r) / delta + 2)
            : 60 * ((r - g) / delta + 4);
        return new Hsv(h, max == 0 ? 0 : delta / max, max);
    }

    // Halves round up, as the script's Math.round does, so both sides write the same hex.
    private static int Byte255(double channel) => (int)Math.Round(channel * 255, MidpointRounding.AwayFromZero);

    public static Rgba ToRgba(Hsv color, double alpha = 1)
    {
        double C(double n)
        {
            var k = (n + color.H / 60) % 6;
            return color.V - color.V * color.S * Math.Max(0, Math.Min(k, Math.Min(4 - k, 1)));
        }

        return new Rgba(Byte255(C(5)), Byte255(C(3)), Byte255(C(1)), alpha);
    }

    /// <summary>A hue's name, with no light or dark: "blue".</summary>
    public static string HueName(double hue, IzzyText text) => hue switch
    {
        < 15 or >= 345 => text.ColorRed,
        < 40 => text.ColorOrange,
        < 65 => text.ColorYellow,
        < 160 => text.ColorGreen,
        < 195 => text.ColorCyan,
        < 255 => text.ColorBlue,
        < 290 => text.ColorPurple,
        _ => text.ColorPink,
    };

    /// <summary>
    /// A plain name for a color, from its hue and lightness: "red", "light green", "dark blue",
    /// "gray", "black", "white". Close enough to tell swatches and positions apart by ear.
    /// </summary>
    public static string Name(Rgba color, IzzyText text)
    {
        double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
        var chroma = Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b));
        // Lightness as people see it (CIE L*, 0-100), not HSL's: a strong blue looks dark.
        static double Linear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        var luminance = 0.2126 * Linear(r) + 0.7152 * Linear(g) + 0.0722 * Linear(b);
        var light = luminance > 0.008856 ? 116 * Math.Cbrt(luminance) - 16 : 903.3 * luminance;
        if (light < 12) return text.ColorBlack;
        if (light > 96 && chroma < 0.2) return text.ColorWhite;
        string name;
        if (chroma < 0.1)
        {
            name = text.ColorGray;
        }
        else
        {
            name = HueName(ToHsv(color).H, text);
        }

        return light > 80 ? string.Format(CultureInfo.CurrentCulture, text.ColorLight, name)
            : light < 45 ? string.Format(CultureInfo.CurrentCulture, text.ColorDark, name)
            : name;
    }
}
