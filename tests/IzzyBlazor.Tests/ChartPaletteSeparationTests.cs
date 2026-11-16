using System.Globalization;
using System.Text.RegularExpressions;

namespace IzzyBlazor.Tests;

// The chart palettes, checked on every build the way the dataviz validator checks them: distance is
// Euclidean in OKLab x100, and color blindness is simulated with Machado, Oliveira and Fernandes (2009)
// at full severity, in linear RGB. Neighbors stay 8 apart for protan and deutan readers (the target)
// and 15 apart for everyone else; a set made for a fixed count (two to four series) keeps every pair
// apart, since any two of its series can sit side by side. The default palette and its sets keep 3:1
// on the surface (WCAG 1.4.11); Okabe-Ito and Calm have lighter colors and say so in their docs. The
// colors are read from tokens.css, so a change there that breaks a palette fails here.
public class ChartPaletteSeparationTests
{
    private const double CvdTarget = 8;
    private const double NormalFloor = 15;
    private const double GraphicContrast = 3;

    public static TheoryData<string, bool, bool> Palettes => new()
    {
        // Name in tokens.css, every pair (else neighbors), every color 3:1 on the surface.
        { "contrast", false, true },
        { "contrast-of2", true, true },
        { "contrast-of3", true, true },
        { "contrast-of4", true, true },
        { "okabe-ito", false, false },
        { "calm", false, false },
    };

    [Theory]
    [MemberData(nameof(Palettes))]
    public void Each_palette_keeps_its_colors_apart_in_light_and_dark(string name, bool everyPair, bool contrast)
    {
        var tokens = Tokens();
        var slots = Slots(tokens, name);
        Assert.True(slots.Length >= 2, $"{name}: no colors found in tokens.css");
        foreach (var dark in new[] { false, true })
        {
            var mode = dark ? "dark" : "light";
            var colors = slots.Select(s => dark ? s.Dark : s.Light).ToArray();
            var surface = dark ? tokens["color-surface"].Dark : tokens["color-surface"].Light;
            foreach (var (i, j) in Pairs(colors.Length, everyPair))
            {
                var pair = $"{name} {mode}, {i + 1} {colors[i]} and {j + 1} {colors[j]}";
                var cvd = Math.Min(DeltaE(colors[i], colors[j], Protan), DeltaE(colors[i], colors[j], Deutan));
                Assert.True(cvd >= CvdTarget, $"{pair}: {cvd:F1} apart for color-blind readers, under {CvdTarget}");
                var normal = DeltaE(colors[i], colors[j], null);
                Assert.True(normal >= NormalFloor, $"{pair}: {normal:F1} apart, under {NormalFloor}");
            }

            if (contrast)
            {
                for (var k = 0; k < colors.Length; k++)
                {
                    var ratio = Ratio(Lin(colors[k]), Lin(surface));
                    Assert.True(ratio >= GraphicContrast, $"{name} {mode} {k + 1} {colors[k]}: {ratio:F2}:1 on the surface {surface}");
                }
            }
        }
    }

    // Emphasis's gray is the text color mixed into the surface; it has to stay readable as data.
    [Fact]
    public void The_emphasis_gray_keeps_3_to_1_on_the_surface()
    {
        var tokens = Tokens();
        foreach (var dark in new[] { false, true })
        {
            var text = dark ? tokens["color-on-surface"].Dark : tokens["color-on-surface"].Light;
            var surface = dark ? tokens["color-surface"].Dark : tokens["color-surface"].Light;
            var gray = MixOklab(Lin(text), Lin(surface), ChartPalette.EmphasisGrayPercent / 100.0);
            var ratio = Ratio(gray, Lin(surface));
            Assert.True(ratio >= GraphicContrast, $"{(dark ? "dark" : "light")}: the gray is {ratio:F2}:1 on {surface}");
        }
    }

    private static IEnumerable<(int, int)> Pairs(int n, bool every)
    {
        for (var i = 0; i < n - 1; i++)
        {
            if (!every)
            {
                yield return (i, i + 1);
                continue;
            }

            for (var j = i + 1; j < n; j++) yield return (i, j);
        }
    }

    // A palette's slots in order: --ib-chart-{name}-1 onward.
    private static (string Light, string Dark)[] Slots(Dictionary<string, (string Light, string Dark)> tokens, string name)
    {
        var list = new List<(string Light, string Dark)>();
        while (tokens.TryGetValue($"chart-{name}-{list.Count + 1}", out var slot)) list.Add(slot);
        return [.. list];
    }

    // Every light-dark(#hex, #hex) token in tokens.css, and tokens that are another token (var(--ib-x)).
    private static Dictionary<string, (string Light, string Dark)> Tokens()
    {
        var css = File.ReadAllText(Path.Combine(RepoRoot(), "src", "IzzyBlazor.Core", "Styles", "tokens.css"));
        var tokens = new Dictionary<string, (string Light, string Dark)>();
        foreach (Match m in Regex.Matches(css, @"--ib-([\w-]+):\s*light-dark\(\s*(#[0-9a-fA-F]{6})\s*,\s*(#[0-9a-fA-F]{6})\s*\)"))
        {
            tokens.TryAdd(m.Groups[1].Value, (m.Groups[2].Value, m.Groups[3].Value));
        }

        foreach (Match m in Regex.Matches(css, @"--ib-([\w-]+):\s*var\(--ib-([\w-]+)\)\s*;"))
        {
            if (tokens.TryGetValue(m.Groups[2].Value, out var target)) tokens.TryAdd(m.Groups[1].Value, target);
        }

        return tokens;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "izzyblazor.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("izzyblazor.sln not found.");
    }

    // Machado, Oliveira and Fernandes (2009), severity 1.0, applied in linear RGB.
    private static readonly double[,] Protan =
    {
        { 0.152286, 1.052583, -0.204868 },
        { 0.114503, 0.786281, 0.099216 },
        { -0.003882, -0.048116, 1.051998 },
    };

    private static readonly double[,] Deutan =
    {
        { 0.367322, 0.860646, -0.227968 },
        { 0.280085, 0.672501, 0.047413 },
        { -0.011820, 0.042940, 0.968881 },
    };

    private static double[] Lin(string hex)
    {
        var v = int.Parse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        static double L(int c) => c / 255.0 <= 0.04045 ? c / 255.0 / 12.92 : Math.Pow((c / 255.0 + 0.055) / 1.055, 2.4);
        return [L((v >> 16) & 0xFF), L((v >> 8) & 0xFF), L(v & 0xFF)];
    }

    private static double[] Simulate(double[] rgb, double[,] m) =>
        [.. Enumerable.Range(0, 3).Select(r => Math.Clamp(m[r, 0] * rgb[0] + m[r, 1] * rgb[1] + m[r, 2] * rgb[2], 0, 1))];

    private static double[] Oklab(double[] rgb)
    {
        var l = Math.Cbrt(0.4122214708 * rgb[0] + 0.5363325363 * rgb[1] + 0.0514459929 * rgb[2]);
        var m = Math.Cbrt(0.2119034982 * rgb[0] + 0.6806995451 * rgb[1] + 0.1073969566 * rgb[2]);
        var s = Math.Cbrt(0.0883024619 * rgb[0] + 0.2817188376 * rgb[1] + 0.6299787005 * rgb[2]);
        return
        [
            0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
            1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
            0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s,
        ];
    }

    private static double[] LinFromOklab(double[] lab)
    {
        var l = Math.Pow(lab[0] + 0.3963377774 * lab[1] + 0.2158037573 * lab[2], 3);
        var m = Math.Pow(lab[0] - 0.1055613458 * lab[1] - 0.0638541728 * lab[2], 3);
        var s = Math.Pow(lab[0] - 0.0894841775 * lab[1] - 1.2914855480 * lab[2], 3);
        return
        [
            Math.Clamp(4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s, 0, 1),
            Math.Clamp(-1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s, 0, 1),
            Math.Clamp(-0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s, 0, 1),
        ];
    }

    // color-mix(in oklab, a p, b): p of a, the rest b.
    private static double[] MixOklab(double[] a, double[] b, double p)
    {
        var (x, y) = (Oklab(a), Oklab(b));
        return LinFromOklab([.. Enumerable.Range(0, 3).Select(k => x[k] * p + y[k] * (1 - p))]);
    }

    private static double DeltaE(string a, string b, double[,]? cvd)
    {
        var (x, y) = (Lin(a), Lin(b));
        if (cvd is not null) (x, y) = (Simulate(x, cvd), Simulate(y, cvd));
        var (p, q) = (Oklab(x), Oklab(y));
        return 100 * Math.Sqrt(Math.Pow(p[0] - q[0], 2) + Math.Pow(p[1] - q[1], 2) + Math.Pow(p[2] - q[2], 2));
    }

    private static double Ratio(double[] a, double[] b)
    {
        static double Y(double[] c) => 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2];
        var (x, y) = (Y(a), Y(b));
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }
}
