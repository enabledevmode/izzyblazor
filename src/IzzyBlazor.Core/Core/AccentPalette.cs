using System.Globalization;

namespace IzzyBlazor;

/// <summary>
/// An accent derived from one brand color: the fill, text on the fill, the accent as text and its
/// soft tint, for light and dark mode. Every pair meets WCAG AA: text on the fill holds 4.5:1, the
/// accent as text holds 4.5:1 on the surface, the gray fills and its own soft tint, and the dark
/// fill holds 3:1 against the dark surface so controls stay visible.
/// </summary>
/// <remarks>
/// The light fill is the brand color itself, unless neither white nor near-black text can reach
/// 4.5:1 on it; then its lightness moves just enough. Lightness moves in OKLab, which keeps the hue.
/// <see cref="IzzyTheme"/> renders a palette as the four <c>--ib-color-primary*</c> tokens.
/// </remarks>
public sealed record AccentPalette(
    string Fill,
    string OnFill,
    string Text,
    string Soft,
    string DarkFill,
    string DarkOnFill,
    string DarkText,
    string DarkSoft)
{
    // The default gray set's surfaces the accent is checked against (neutral; slate and stone sit
    // within a few hundredths of these).
    private static readonly Rgb White = Rgb.Parse("#ffffff");
    private static readonly Rgb Ink = Rgb.Parse("#111111");
    private static readonly Rgb ContainerHigh = Rgb.Parse("#dfdfe3");
    private static readonly Rgb DarkSurface = Rgb.Parse("#1b1b1e");
    private static readonly Rgb DarkContainerHigh = Rgb.Parse("#313135");

    /// <summary>Derives the palette from a hex color (<c>#rgb</c> or <c>#rrggbb</c>).</summary>
    /// <exception cref="ArgumentException">The value isn't a hex color.</exception>
    public static AccentPalette From(string hex)
    {
        if (!Rgb.TryParse(hex, out var brand))
        {
            throw new ArgumentException($"'{hex}' is not a hex color such as #2563eb.", nameof(hex));
        }

        var (fill, on) = WithText(brand);
        var soft = Rgb.Mix(fill, White, 0.16);
        var text = MoveUntil(fill, -0.01, c => Rgb.Ratio(c, soft) >= 4.5 && Rgb.Ratio(c, ContainerHigh) >= 4.5 && Rgb.Ratio(c, White) >= 4.5);

        // Dark mode: lift a dark brand color until it shows on the dark surface, then the same steps.
        // The accent as text is lifted further (6.5:1 on the strongest fill) so it reads as a
        // light tint of the brand, not a saturated one.
        var darkBase = MoveUntil(brand, 0.01, c => Rgb.Ratio(c, DarkSurface) >= 3);
        var (darkFill, darkOn) = WithText(darkBase);
        var darkSoft = Rgb.Mix(darkFill, DarkSurface, 0.22);
        var darkText = MoveUntil(darkFill, 0.01, c => Rgb.Ratio(c, darkSoft) >= 4.5 && Rgb.Ratio(c, DarkContainerHigh) >= 6.5);

        return new AccentPalette(
            fill.ToHex(), on.ToHex(), text.ToHex(), soft.ToHex(),
            darkFill.ToHex(), darkOn.ToHex(), darkText.ToHex(), darkSoft.ToHex());
    }

    /// <summary>The four primary tokens as CSS declarations, each a <c>light-dark()</c> pair.</summary>
    public string ToCss() =>
        $"--ib-color-primary: light-dark({Fill}, {DarkFill}); " +
        $"--ib-color-on-primary: light-dark({OnFill}, {DarkOnFill}); " +
        $"--ib-color-primary-text: light-dark({Text}, {DarkText}); " +
        $"--ib-color-primary-soft: light-dark({Soft}, {DarkSoft});";

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
        var current = color;
        for (var i = 0; i < 200 && !done(current); i++)
        {
            lab = lab with { L = Math.Clamp(lab.L + step, 0, 1) };
            current = lab.ToRgb();
            if (lab.L <= 0 || lab.L >= 1)
            {
                break;
            }
        }

        return current;
    }

    private readonly record struct Rgb(double R, double G, double B)
    {
        public static Rgb Parse(string hex) => TryParse(hex, out var c) ? c : throw new ArgumentException(hex);

        public static bool TryParse(string? value, out Rgb color)
        {
            color = default;
            var hex = value?.Trim() ?? "";
            if (!hex.StartsWith('#'))
            {
                return false;
            }

            hex = hex[1..];
            if (hex.Length == 3)
            {
                hex = string.Concat(hex.Select(c => new string(c, 2)));
            }

            if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
            {
                return false;
            }

            color = new Rgb(((v >> 16) & 0xFF) / 255.0, ((v >> 8) & 0xFF) / 255.0, (v & 0xFF) / 255.0);
            return true;
        }

        public string ToHex() =>
            string.Create(CultureInfo.InvariantCulture, $"#{Byte(R):x2}{Byte(G):x2}{Byte(B):x2}");

        public static double Ratio(Rgb a, Rgb b)
        {
            var (la, lb) = (a.Luminance(), b.Luminance());
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }

        public static Rgb Mix(Rgb a, Rgb b, double amountOfA)
        {
            var (x, y) = (Oklab.From(a), Oklab.From(b));
            return new Oklab(
                x.L * amountOfA + y.L * (1 - amountOfA),
                x.A * amountOfA + y.A * (1 - amountOfA),
                x.B * amountOfA + y.B * (1 - amountOfA)).ToRgb();
        }

        public double Luminance() => 0.2126 * Linear(R) + 0.7152 * Linear(G) + 0.0722 * Linear(B);

        public static double Linear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

        public static double Gamma(double c) => c <= 0.0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055;

        private static int Byte(double c) => (int)Math.Round(Math.Clamp(c, 0, 1) * 255);
    }

    private readonly record struct Oklab(double L, double A, double B)
    {
        public static Oklab From(Rgb c)
        {
            var (r, g, b) = (Rgb.Linear(c.R), Rgb.Linear(c.G), Rgb.Linear(c.B));
            var l = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
            var m = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
            var s = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
            return new Oklab(
                0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
                1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
                0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s);
        }

        // Out-of-gamut results are clamped per channel, which keeps the lightness close enough for
        // the contrast checks (they run on the clamped color).
        public Rgb ToRgb()
        {
            var l = Math.Pow(L + 0.3963377774 * A + 0.2158037573 * B, 3);
            var m = Math.Pow(L - 0.1055613458 * A - 0.0638541728 * B, 3);
            var s = Math.Pow(L - 0.0894841775 * A - 1.2914855480 * B, 3);
            return new Rgb(
                Rgb.Gamma(Math.Clamp(4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s, 0, 1)),
                Rgb.Gamma(Math.Clamp(-1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s, 0, 1)),
                Rgb.Gamma(Math.Clamp(-0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s, 0, 1)));
        }
    }
}
