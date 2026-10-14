using System.Globalization;
using static IzzyBlazor.ColorMath;

namespace IzzyBlazor;

/// <summary>
/// A gray set derived from one color (<see cref="IzzyTheme.Gray"/> as hex): the page, surfaces,
/// fills, borders and text, in light and dark, each text color checked for contrast.
/// </summary>
/// <remarks>
/// The color is the light page background, as given (its lightness is kept between 0.90 and 0.99
/// in OKLab, so text on it stays readable). The rest of the ladder steps down from it by the same
/// amounts as the built-in sets, in its hue with a little more color for the darker fills. Muted
/// and subtle text hold 4.5:1 on the page, the surface and the container fill, body text 7:1 on the
/// strongest fill. Dark mode uses the built-in dark lightness in the same hue, with half the color.
/// <see cref="IzzyTheme"/> renders it as the twelve <c>--ib-color-*</c> gray tokens.
/// </remarks>
public sealed class GrayPalette
{
    // Tokens in the order they're written, with their light and dark colors.
    private readonly (string Token, string Light, string Dark)[] _colors;

    private GrayPalette((string, string, string)[] colors) => _colors = colors;

    /// <summary>The light and dark color of each gray token, such as <c>("background", "#f4efe6", "#161411")</c>.</summary>
    public IReadOnlyList<(string Token, string Light, string Dark)> Colors => _colors;

    /// <summary>Derives the set from a hex color (<c>#rgb</c> or <c>#rrggbb</c>): your page background.</summary>
    /// <exception cref="ArgumentException">The value isn't a hex color.</exception>
    public static GrayPalette From(string hex)
    {
        if (!Rgb.TryParse(hex, out var input))
        {
            throw new ArgumentException($"'{hex}' is not a hex color such as #f4efe6.", nameof(hex));
        }

        var (l0, c0, hue) = ToLch(input);
        var c = Math.Min(c0, 0.05);
        var page = Math.Clamp(l0, 0.90, 0.99);
        Rgb Tint(double l, double factor, double cap = 0.05) => Snap(FromLch(l, Math.Min(c * factor, cap), hue));

        var light = new Dictionary<string, Rgb>
        {
            ["background"] = page == l0 ? Snap(input) : Tint(page, 1),
            ["surface-container-low"] = Tint(page - 0.020, 1.0),
            ["surface-container"] = Tint(page - 0.050, 1.1),
            ["surface-container-high"] = Tint(page - 0.080, 1.2),
            ["border"] = Tint(page - 0.089, 1.2),
        };

        // Surfaces sit just above the page; with almost no color they're white, as in the built-in sets.
        var surfaceL = Math.Min(1.0, page + 0.015);
        light["surface"] = surfaceL >= 0.999 || c < 0.003 ? Rgb.Parse("#ffffff") : Tint(surfaceL, 0.4);
        light["surface-raised"] = light["surface"];

        Rgb LightText(double l, double factor, double cap, double target, params string[] on)
        {
            var color = Tint(l, factor, cap);
            for (var i = 0; i < 200 && !on.All(t => Rgb.Ratio(color, light[t]) >= target); i++)
            {
                l -= 0.005;
                color = Tint(l, factor, cap);
            }

            return color;
        }

        light["on-surface"] = LightText(0.21, 0.6, 0.02, 7, "surface-container-high");
        light["muted"] = LightText(0.488, 1.2, 0.035, 4.5, "background", "surface", "surface-container");
        light["subtle"] = LightText(0.513, 1.2, 0.035, 4.5, "background", "surface", "surface-container");
        light["disabled"] = LightText(0.642, 1.2, 0.035, 3, "surface");
        light["outline"] = LightText(page - 0.200, 1.3, 0.05, 1.9, "surface");

        Rgb Shade(double l, double factor, double cap = 0.03) => Snap(FromLch(l, Math.Min(c * factor, cap), hue));
        var dark = new Dictionary<string, Rgb>
        {
            ["background"] = Shade(0.192, 0.5),
            ["surface"] = Shade(0.223, 0.6),
            ["surface-container-low"] = Shade(0.266, 0.6),
            ["surface-container"] = Shade(0.286, 0.7),
            ["surface-container-high"] = Shade(0.315, 0.8),
            ["surface-raised"] = Shade(0.258, 0.6),
            ["border"] = Shade(0.343, 0.8),
        };

        Rgb DarkText(double l, double factor, double cap, double target, params string[] on)
        {
            var color = Shade(l, factor, cap);
            for (var i = 0; i < 200 && !on.All(t => Rgb.Ratio(color, dark[t]) >= target); i++)
            {
                l += 0.005;
                color = Shade(l, factor, cap);
            }

            return color;
        }

        dark["on-surface"] = DarkText(0.944, 0.4, 0.012, 7, "surface-container-high");
        dark["muted"] = DarkText(0.711, 0.8, 0.025, 4.5, "background", "surface", "surface-container");
        dark["subtle"] = DarkText(0.662, 0.8, 0.025, 4.5, "background", "surface", "surface-container");
        dark["disabled"] = DarkText(0.534, 0.8, 0.025, 2.2, "surface");
        dark["outline"] = DarkText(0.441, 0.8, 0.03, 1.9, "surface");

        string[] order =
        [
            "background", "surface", "surface-container-low", "surface-container", "surface-container-high", "surface-raised",
            "on-surface", "muted", "subtle", "disabled", "border", "outline",
        ];
        return new GrayPalette([.. order.Select(t => (t, light[t].ToHex(), dark[t].ToHex()))]);
    }

    /// <summary>The gray tokens as CSS declarations, each a <c>light-dark()</c> pair.</summary>
    public string ToCss() =>
        string.Join(" ", _colors.Select(c => string.Create(CultureInfo.InvariantCulture, $"--ib-color-{c.Token}: light-dark({c.Light}, {c.Dark});")));
}
