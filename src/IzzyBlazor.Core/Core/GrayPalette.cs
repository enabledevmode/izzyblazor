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
/// amounts as the built-in sets, in its hue with a little more color for the darker fills, but no
/// fill gets darker than the built-in sets' same fill: a darker page gets a shallower ladder (and a
/// page darker than their Fill is lifted), so brand, status and code colors keep their
/// contrast on every fill. Muted
/// and subtle text hold 4.5:1 on the page, the surface and Fill, body text 7:1 on the
/// strongest fill. Dark mode uses the built-in dark lightness in the same hue, with half the color.
/// <see cref="IzzyTheme"/> renders it as the twelve <c>--ib-color-*</c> gray tokens.
/// </remarks>
public sealed class GrayPalette
{
    // Tokens in the order they're written, with their light and dark colors.
    private readonly (string Token, string Light, string Dark)[] _colors;

    // The darkest of the built-in sets' (neutral, slate, stone) light fill and fill-strong
    // colors: the ones fixed colors are checked on.
    private static readonly double[] Floors =
    [
        Darkest("#e9e9ec", "#e7ebf1", "#eae8e2"),
        Darkest("#dfdfe3", "#dce2ea", "#e0ddd5"),
    ];

    private static double Darkest(params string[] hexes) => hexes.Min(h => Rgb.Parse(h).Luminance());

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

        // Each fill stays at least as light as the built-in sets' same fill, so the colors that
        // don't change with the grays (brand and status text, code) keep their contrast on them: a
        // darker page gets a shallower ladder, and a page darker than their fill is lifted.
        while (Tint(page, 1).Luminance() < Floors[0] && page < 0.99)
        {
            page += 0.005;
        }

        var depth = 1.0;
        while (depth > 0 && (Tint(page - 0.050 * depth, 1.1).Luminance() < Floors[0]
            || Tint(page - 0.080 * depth, 1.2).Luminance() < Floors[1]))
        {
            depth -= 0.05;
        }

        depth = Math.Max(depth, 0);

        var light = new Dictionary<string, Rgb>
        {
            ["background"] = page == l0 ? Snap(input) : Tint(page, 1),
            ["fill-subtle"] = Tint(page - 0.020 * depth, 1.0),
            ["fill"] = Tint(page - 0.050 * depth, 1.1),
            ["fill-strong"] = Tint(page - 0.080 * depth, 1.2),
            ["border"] = Tint(page - 0.089, 1.2),
        };

        // Surfaces sit just above the page; with almost no color they're white, as in the built-in sets.
        var surfaceL = Math.Min(1.0, page + 0.015);
        light["surface"] = surfaceL >= 0.999 || c < 0.003 ? Rgb.Parse("#ffffff") : Tint(surfaceL, 0.4);
        light["surface-overlay"] = light["surface"];
        // Sunken holds surfaces of its own: below the surface, as the subtle fill is in light mode.
        light["surface-sunken"] = light["fill-subtle"];

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

        light["on-surface"] = LightText(0.21, 0.6, 0.02, 7, "fill-strong");
        light["muted"] = LightText(0.488, 1.2, 0.035, 4.5, "background", "surface", "fill");
        light["subtle"] = LightText(0.513, 1.2, 0.035, 4.5, "background", "surface", "fill");
        light["disabled"] = LightText(0.642, 1.2, 0.035, 3, "surface");
        light["outline"] = LightText(page - 0.200, 1.3, 0.05, 1.9, "surface");

        Rgb Shade(double l, double factor, double cap = 0.03) => Snap(FromLch(l, Math.Min(c * factor, cap), hue));
        var dark = new Dictionary<string, Rgb>
        {
            ["background"] = Shade(0.135, 0.5),
            ["surface"] = Shade(0.160, 0.6),
            ["fill-subtle"] = Shade(0.192, 0.6),
            ["fill"] = Shade(0.223, 0.7),
            ["fill-strong"] = Shade(0.257, 0.8),
            ["surface-overlay"] = Shade(0.206, 0.6),
            ["border"] = Shade(0.303, 0.8),
        };

        // Below the surface in dark mode: the page, already the lowest step.
        dark["surface-sunken"] = dark["background"];

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

        dark["on-surface"] = DarkText(0.944, 0.4, 0.012, 7, "fill-strong");
        dark["muted"] = DarkText(0.711, 0.8, 0.025, 4.5, "background", "surface", "fill");
        dark["subtle"] = DarkText(0.662, 0.8, 0.025, 4.5, "background", "surface", "fill");
        dark["disabled"] = DarkText(0.534, 0.8, 0.025, 2.2, "surface");
        dark["outline"] = DarkText(0.441, 0.8, 0.03, 1.9, "surface");

        string[] order =
        [
            "background", "surface", "surface-sunken", "fill-subtle", "fill", "fill-strong", "surface-overlay",
            "on-surface", "muted", "subtle", "disabled", "border", "outline",
        ];
        return new GrayPalette([.. order.Select(t => (t, light[t].ToHex(), dark[t].ToHex()))]);
    }

    /// <summary>The gray tokens as CSS declarations, each a <c>light-dark()</c> pair.</summary>
    public string ToCss() =>
        string.Join(" ", _colors.Select(c => string.Create(CultureInfo.InvariantCulture, $"--ib-color-{c.Token}: light-dark({c.Light}, {c.Dark});")));
}
