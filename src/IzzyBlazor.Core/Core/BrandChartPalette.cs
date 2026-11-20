using System.Collections.Concurrent;
using static IzzyBlazor.ColorMath;

namespace IzzyBlazor;

/// <summary>
/// The Brand chart palette (<see cref="ThemeChartPalette.Brand"/>, the default): the brand color first, then the
/// Contrast palette's colors without the one nearest the brand, in the order that keeps neighbors furthest apart
/// for color-blind readers. The sets for exactly two, three or four series start at the brand too, and keep every
/// pair apart where the brand allows it. Distances are the chart palettes' own measure (OKLab, with Machado 2009
/// for protan and deutan vision), in light and dark.
/// </summary>
/// <remarks>
/// The colors are named by their tokens, so the palette is a list of <c>var()</c>s: the brand is
/// <c>--ib-color-primary</c>, read where it's used, and the rest are tokens.css's Contrast colors. tokens.css
/// holds the palette for the default brand (blue); <see cref="IzzyTheme"/> and <see cref="ThemeScope"/> write
/// another brand's.
/// </remarks>
internal static class BrandChartPalette
{
    /// <summary>Neighbors (and every pair of a set) stay this far apart for color-blind readers, and this far for everyone.</summary>
    internal const double CvdTarget = 8, NormalFloor = 15;

    /// <summary>A color of the palette: its token (without <c>--ib-chart-</c>) and its light and dark hex, as in tokens.css.</summary>
    internal readonly record struct Named(string Token, string Light, string Dark);

    /// <summary>The Contrast palette's eight, in its order.</summary>
    internal static readonly Named[] Eight =
    [
        new("contrast-1", "#3186e9", "#4896f5"), new("contrast-2", "#649803", "#72a729"),
        new("contrast-3", "#ca549d", "#d867ac"), new("contrast-4", "#a97f05", "#bb8d09"),
        new("contrast-5", "#09989f", "#01a9b0"), new("contrast-6", "#d75928", "#e56c40"),
        new("contrast-7", "#9769dc", "#a57ae8"), new("contrast-8", "#029e72", "#09af7f"),
    ];

    /// <summary>Contrast's sets for two, three and four series, each in its order.</summary>
    internal static readonly Named[][] Sets =
    [
        [new("contrast-of2-1", "#1b74d5", "#2b7ad6"), new("contrast-of2-2", "#c3470f", "#c55123")],
        [new("contrast-of3-1", "#3084e8", "#4996f5"), new("contrast-of3-2", "#b5418a", "#b94c90"), new("contrast-of3-3", "#609301", "#689b1a")],
        [
            new("contrast-of4-1", "#1f76d8", "#3d8ae8"), new("contrast-of4-2", "#8f6a01", "#ab8102"),
            new("contrast-of4-3", "#ab3882", "#b94c90"), new("contrast-of4-4", "#029e72", "#05af7f"),
        ],
    ];

    /// <summary>The built-in brands' fills, light and dark, as in themes.css (ink is the text color).</summary>
    internal static readonly IReadOnlyDictionary<string, (string Light, string Dark)> Brands = new Dictionary<string, (string, string)>
    {
        ["blue"] = ("#2563eb", "#3d6ae8"), ["indigo"] = ("#4f46e5", "#5b55e9"), ["violet"] = ("#6d28d9", "#7a4be0"),
        ["teal"] = ("#0f766e", "#11807a"), ["green"] = ("#1d7a3e", "#1f8345"), ["rose"] = ("#be1857", "#cc2a63"),
        ["ink"] = ("#18181b", "#ececee"),
    };

    /// <summary>The brand each preset brings, when no Brand is set over it.</summary>
    internal static string BrandOf(ThemePreset? preset) => preset switch
    {
        ThemePreset.Compact => "indigo",
        ThemePreset.Soft => "teal",
        ThemePreset.Editorial => "ink",
        _ => "blue",
    };

    private const string Primary = "var(--ib-color-primary)";

    private static readonly ConcurrentDictionary<(string, string, string), string> Cache = new();

    /// <summary>The default brand's palette, which tokens.css holds: a theme with the same one writes nothing.</summary>
    internal static string Default => Css("blue");

    /// <summary>A built-in brand's palette as CSS declarations.</summary>
    internal static string Css(string brand) => Css(Brands[brand].Light, Brands[brand].Dark, Primary);

    /// <summary>
    /// A hex brand's palette as CSS declarations. A fill too light to show on white (under 3:1) is drawn in the
    /// accent's text color there, as the focus ring is.
    /// </summary>
    internal static string Css(BrandPalette brand) => brand.Focus is { } visible
        ? Css(visible, brand.DarkFill, $"light-dark({visible}, {brand.DarkFill})")
        : Css(brand.Fill, brand.DarkFill, Primary);

    /// <summary>The declarations for a brand of these two colors, written as <paramref name="first"/>.</summary>
    internal static string Css(string light, string dark, string first) => Cache.GetOrAdd((light, dark, first), key =>
    {
        var (eight, sets) = Pick(key.Item1, key.Item2);
        var css = new System.Text.StringBuilder();
        void Add(string token, string? color) => css.Append("--ib-chart-").Append(token).Append(": ")
            .Append(color is null ? key.Item3 : $"var(--ib-chart-{color})").Append("; ");
        for (var i = 0; i < eight.Length; i++) Add((i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), eight[i]);
        for (var n = 2; n <= 4; n++)
        {
            for (var i = 0; i < n; i++) Add($"of{n}-{i + 1}", sets[n - 2][i]);
        }

        return css.ToString().TrimEnd();
    });

    /// <summary>
    /// The palette for a brand: the eight in order and the sets for two, three and four series, each color by its
    /// token, with null for the brand itself (always first).
    /// </summary>
    internal static (string?[] Eight, string?[][] Sets) Pick(string light, string dark)
    {
        var brand = new Named("", light, dark);

        // The eight: the brand takes the place of the Contrast color nearest it; the other seven go in the order
        // that keeps the closest neighbors furthest apart, moving as few as it can from Contrast's own.
        var nearest = Enumerable.Range(0, Eight.Length).MinBy(i => Apart(brand, Eight[i]).Normal);
        var rest = Enumerable.Range(0, Eight.Length).Where(i => i != nearest).ToArray();
        var order = rest;
        var (bestScore, bestMoved) = (double.MinValue, int.MaxValue);
        var fromBrand = Eight.Select(c => Score(Apart(brand, c))).ToArray();
        var between = new double[Eight.Length, Eight.Length];
        foreach (var i in rest)
        {
            foreach (var j in rest) between[i, j] = i == j ? 0 : Score(Apart(Eight[i], Eight[j]));
        }

        foreach (var candidate in Permutations(rest))
        {
            var score = fromBrand[candidate[0]];
            for (var k = 0; k + 1 < candidate.Length; k++) score = Math.Min(score, between[candidate[k], candidate[k + 1]]);

            score = Math.Round(score, 2);
            var moved = candidate.Where((index, k) => index != rest[k]).Count();
            if (score > bestScore || (score == bestScore && moved < bestMoved))
            {
                (bestScore, bestMoved, order) = (score, moved, [.. candidate]);
            }
        }

        string?[] eight = [null, .. order.Select(i => Eight[i].Token)];

        // The sets: Contrast's own, with the brand in place of its nearest color, when every pair then stays
        // apart; else the brand with the colors that keep its closest pair furthest apart, on target or not.
        var pool = Eight.Concat(Sets.SelectMany(set => set)).ToArray();
        var sets = new string?[3][];
        for (var n = 2; n <= 4; n++)
        {
            var own = Sets[n - 2];
            var replaced = own.MinBy(c => Apart(brand, c).Normal);
            var kept = own.Where(c => c != replaced).ToArray();
            if (!(SetScore(brand, kept) >= 1))
            {
                kept = Combinations(pool, n - 1).Select(c => (Colors: c, Score: SetScore(brand, c)))
                    .OrderByDescending(c => Math.Round(c.Score, 2)).Select(c => c.Colors).First();
            }

            sets[n - 2] = [null, .. kept.Select(c => c.Token)];
        }

        return (eight, sets);
    }

    /// <summary>How far apart two colors are, in the worse of light and dark: for color-blind readers, and for everyone.</summary>
    internal static (double Cvd, double Normal) Apart(Named a, Named b) =>
        (Math.Min(Cvd(a.Light, b.Light), Cvd(a.Dark, b.Dark)), Math.Min(DeltaE(a.Light, b.Light, null), DeltaE(a.Dark, b.Dark, null)));

    // 1 or more when a pair meets both targets.
    private static double Score((double Cvd, double Normal) apart) => Math.Min(apart.Cvd / CvdTarget, apart.Normal / NormalFloor);

    // The closest pair of the brand and these colors.
    private static double SetScore(Named brand, Named[] colors)
    {
        var all = colors.Prepend(brand).ToArray();
        var score = double.MaxValue;
        for (var i = 0; i < all.Length; i++)
        {
            for (var j = i + 1; j < all.Length; j++) score = Math.Min(score, Score(Apart(all[i], all[j])));
        }

        return score;
    }

    private static IEnumerable<int[]> Permutations(int[] items)
    {
        var current = (int[])items.Clone();
        var used = new bool[items.Length];
        return Fill(0);

        IEnumerable<int[]> Fill(int at)
        {
            if (at == items.Length)
            {
                yield return current;
                yield break;
            }

            for (var i = 0; i < items.Length; i++)
            {
                if (used[i]) continue;
                (used[i], current[at]) = (true, items[i]);
                foreach (var done in Fill(at + 1)) yield return done;
                used[i] = false;
            }
        }
    }

    private static IEnumerable<Named[]> Combinations(Named[] items, int count, int from = 0)
    {
        if (count == 0)
        {
            yield return [];
            yield break;
        }

        for (var i = from; i <= items.Length - count; i++)
        {
            foreach (var tail in Combinations(items, count - 1, i + 1)) yield return [items[i], .. tail];
        }
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

    private static double Cvd(string a, string b) => Math.Min(DeltaE(a, b, Protan), DeltaE(a, b, Deutan));

    // Euclidean distance in OKLab, times 100, as the eye with this color vision sees the two.
    private static double DeltaE(string a, string b, double[,]? vision)
    {
        var (p, q) = (Lab(a, vision), Lab(b, vision));
        return 100 * Math.Sqrt(Math.Pow(p[0] - q[0], 2) + Math.Pow(p[1] - q[1], 2) + Math.Pow(p[2] - q[2], 2));
    }

    private static double[] Lab(string hex, double[,]? vision)
    {
        var color = Rgb.Parse(hex);
        double[] rgb = [Rgb.Linear(color.R), Rgb.Linear(color.G), Rgb.Linear(color.B)];
        if (vision is not null)
        {
            rgb = [.. Enumerable.Range(0, 3).Select(r => Math.Clamp(vision[r, 0] * rgb[0] + vision[r, 1] * rgb[1] + vision[r, 2] * rgb[2], 0, 1))];
        }

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
}
