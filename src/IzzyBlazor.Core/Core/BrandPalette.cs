using System.Globalization;

using static IzzyBlazor.ColorMath;

namespace IzzyBlazor;

/// <summary>
/// The accent derived from a brand color (<see cref="IzzyTheme.Brand"/>): the fill, text on the fill, the accent as text and its
/// soft tint, for light and dark mode. Every pair meets WCAG AA: text on the fill holds 4.5:1, the
/// accent as text holds 4.5:1 on the surface, the gray fills and its own soft tint, and the dark
/// fill holds 3:1 against the dark surface so controls stay visible.
/// </summary>
/// <remarks>
/// The light fill is the brand color itself, unless neither white nor near-black text can reach
/// 4.5:1 on it; then its lightness moves just enough. Lightness moves in OKLab, which keeps the hue.
/// <see cref="IzzyTheme"/> renders it as the four <c>--ib-color-primary*</c> tokens, which <see cref="Accent.Primary"/> controls are painted with.
/// </remarks>
public sealed record BrandPalette(
    string Fill,
    string OnFill,
    string Text,
    string Soft,
    string DarkFill,
    string DarkOnFill,
    string DarkText,
    string DarkSoft)
{
    // The built-in gray sets' surfaces the accent is checked against (neutral, slate, stone), so
    // the promises hold on each. A hex Gray keeps its fills at least this light (GrayPalette).
    private static readonly Rgb White = Rgb.Parse("#ffffff");
    private static readonly Rgb Ink = Rgb.Parse("#111111");
    private static readonly Rgb[] FillStrong = [Rgb.Parse("#dfdfe3"), Rgb.Parse("#dce2ea"), Rgb.Parse("#e0ddd5")];
    private static readonly Rgb[] DarkSurface = [Rgb.Parse("#111113"), Rgb.Parse("#13161d"), Rgb.Parse("#121314")];
    private static readonly Rgb[] DarkFillStrong = [Rgb.Parse("#28282b"), Rgb.Parse("#2b3039"), Rgb.Parse("#2a2c2f")];

    /// <summary>Derives the palette from a hex color (<c>#rgb</c> or <c>#rrggbb</c>).</summary>
    /// <exception cref="ArgumentException">The value isn't a hex color.</exception>
    public static BrandPalette From(string hex)
    {
        if (!Rgb.TryParse(hex, out var brand))
        {
            throw new ArgumentException($"'{hex}' is not a hex color such as #2563eb.", nameof(hex));
        }

        var (fill, on) = WithText(brand);
        var soft = Snap(Rgb.Mix(fill, White, 0.16));
        var text = MoveUntil(fill, -0.01, c => Rgb.Ratio(c, soft) >= 4.5 && FillStrong.All(g => Rgb.Ratio(c, g) >= 4.5) && Rgb.Ratio(c, White) >= 4.5);

        // Dark mode: lift a dark brand color until it shows on the dark surface, then the same steps.
        // The accent as text is lifted further (6.5:1 on the strongest fill) so it reads as a
        // light tint of the brand, not a saturated one.
        var darkBase = MoveUntil(brand, 0.01, c => DarkSurface.All(g => Rgb.Ratio(c, g) >= 3));
        var (darkFill, darkOn) = WithText(darkBase);
        var darkSoft = Snap(Rgb.Mix(darkFill, DarkSurface[0], 0.22));
        var darkText = MoveUntil(darkFill, 0.01, c => Rgb.Ratio(c, darkSoft) >= 4.5 && DarkFillStrong.All(g => Rgb.Ratio(c, g) >= 6.5));

        return new BrandPalette(
            fill.ToHex(), on.ToHex(), text.ToHex(), soft.ToHex(),
            darkFill.ToHex(), darkOn.ToHex(), darkText.ToHex(), darkSoft.ToHex());
    }

    /// <summary>The four primary tokens as CSS declarations, each a <c>light-dark()</c> pair.</summary>
    public string ToCss() =>
        $"--ib-color-primary: light-dark({Fill}, {DarkFill}); " +
        $"--ib-color-on-primary: light-dark({OnFill}, {DarkOnFill}); " +
        $"--ib-color-primary-text: light-dark({Text}, {DarkText}); " +
        $"--ib-color-primary-soft: light-dark({Soft}, {DarkSoft});";

    // Each check is on the color as written (rounded to hex), so rounding can't take it under.
    // White or near-black, whichever reads better; if neither reaches 4.5:1, move the fill's
    // lightness away from the text until it does.
    private static (Rgb Fill, Rgb On) WithText(Rgb fill)
    {
        var on = Rgb.Ratio(White, fill) >= Rgb.Ratio(Ink, fill) ? White : Ink;
        var step = on == White ? -0.005 : 0.005;
        return (MoveUntil(fill, step, c => Rgb.Ratio(on, c) >= 4.5), on);
    }

    private static Rgb MoveUntil(Rgb color, double step, Func<Rgb, bool> done)
    {
        var lab = Oklab.From(color);
        var current = Snap(color);
        for (var i = 0; i < 200 && !done(current); i++)
        {
            lab = lab with { L = Math.Clamp(lab.L + step, 0, 1) };
            current = Snap(lab.ToRgb());
            if (lab.L <= 0 || lab.L >= 1)
            {
                break;
            }
        }

        return current;
    }
}
