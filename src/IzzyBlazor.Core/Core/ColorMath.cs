using System.Globalization;

namespace IzzyBlazor;

/// <summary>sRGB, OKLab and WCAG contrast, for the palettes derived from one color.</summary>
internal static class ColorMath
{
    internal readonly record struct Rgb(double R, double G, double B)
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

            if (hex.Length != 6 || !hex.All(char.IsAsciiHexDigit) || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
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

    internal readonly record struct Oklab(double L, double A, double B)
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

    /// <summary>A color from OKLCH: lightness, chroma, hue in degrees.</summary>
    public static Rgb FromLch(double l, double c, double hueDegrees)
    {
        var h = hueDegrees * Math.PI / 180;
        return new Oklab(l, c * Math.Cos(h), c * Math.Sin(h)).ToRgb();
    }

    /// <summary>Lightness, chroma and hue (degrees) of a color.</summary>
    public static (double L, double C, double H) ToLch(Rgb color)
    {
        var lab = Oklab.From(color);
        var hue = Math.Atan2(lab.B, lab.A) * 180 / Math.PI;
        return (lab.L, Math.Sqrt(lab.A * lab.A + lab.B * lab.B), hue < 0 ? hue + 360 : hue);
    }

    /// <summary>The color as the browser gets it: rounded to whole 8-bit channels, so contrast is checked on what's drawn.</summary>
    public static Rgb Snap(Rgb color) => Rgb.Parse(color.ToHex());
}
