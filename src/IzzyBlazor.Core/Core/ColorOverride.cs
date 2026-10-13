using System.Globalization;

namespace IzzyBlazor;

/// <summary>
/// Turns a <see cref="Color"/> passed to a button or badge into the three custom properties
/// their styles read: the fill (<c>--ib-accent</c>), text on the fill (<c>--ib-on-accent</c>),
/// text for the quieter variants (<c>--ib-accent-text</c>) and their tint (<c>--ib-accent-soft</c>).
/// </summary>
internal static class ColorOverride
{
    // Theme tokens that have a "-text" partner tuned for text on surfaces.
    private static readonly HashSet<string> TextTokens = new(StringComparer.Ordinal)
    {
        "primary", "success", "warning", "danger", "info",
    };

    private const string DarkText = "#0f172a";
    private const string LightText = "#ffffff";

    public static void Apply(StyleBuilder styles, Color color)
    {
        styles.AddVar("accent", color.Css);
        styles.AddVar("on-accent", TextOn(color));
        styles.AddVar("accent-text", TextTint(color));

        // Set even when it's the default mix, so a soft tint inherited from around the element
        // (an alert's, a badge's) never shows through.
        styles.AddVar("accent-soft", $"color-mix(in oklab, {color.Css} 16%, var(--ib-color-surface))");
    }

    /// <summary>
    /// Readable text on the color used as a fill: the token's <c>on-</c> partner, else black
    /// or white by contrast for hex colors, else white.
    /// </summary>
    internal static string TextOn(Color color)
    {
        if (color.ContrastText is { } pair)
        {
            return pair.Css;
        }

        if (!TryParseHex(color.Css, out var r, out var g, out var b))
        {
            return LightText;
        }

        // Whichever of dark and white text has more contrast on this fill.
        var fill = Luminance(r, g, b);
        var dark = Luminance(15 / 255.0, 23 / 255.0, 42 / 255.0);
        return Ratio(fill, dark) >= Ratio(fill, 1.0) ? DarkText : LightText;
    }

    /// <summary>
    /// Text in the color on a plain surface (soft, outline, ghost and link variants): the
    /// token's <c>-text</c> partner where the theme has one, else the color mixed 55/45 toward
    /// the surface's text color so it stays readable.
    /// </summary>
    internal static string TextTint(Color color) =>
        color.TokenName is { } name && TextTokens.Contains(name)
            ? Color.Token(name + "-text").Css
            : $"color-mix(in oklab, {color.Css} 55%, var(--ib-color-on-surface))";

    private static bool TryParseHex(string css, out double r, out double g, out double b)
    {
        r = g = b = 0;
        var hex = css.Trim();
        if (!hex.StartsWith('#'))
        {
            return false;
        }

        hex = hex[1..];
        if (hex.Length is 3 or 4)
        {
            hex = string.Concat(hex[..3].Select(c => new string(c, 2)));
        }
        else if (hex.Length is 6 or 8)
        {
            hex = hex[..6];
        }
        else
        {
            return false;
        }

        if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        r = ((value >> 16) & 0xFF) / 255.0;
        g = ((value >> 8) & 0xFF) / 255.0;
        b = (value & 0xFF) / 255.0;
        return true;
    }

    /// <summary>WCAG relative luminance.</summary>
    private static double Luminance(double r, double g, double b)
    {
        static double Channel(double c) => c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        return 0.2126 * Channel(r) + 0.7152 * Channel(g) + 0.0722 * Channel(b);
    }

    private static double Ratio(double a, double b) => (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
}
