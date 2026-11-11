using System.Globalization;
using System.Text;

namespace IzzyBlazor;

/// <summary>
/// The colors a <see cref="Chart{TItem}"/> or <see cref="PieChart{TItem}"/> gives its series or
/// slices, or a <see cref="Gauge"/> its bands, in order: <see cref="Colors"/> (the default), <see cref="Shades"/> of one color,
/// <see cref="Diverging"/> between two, or your own with <see cref="Of"/>. Shades and diverging
/// ramps are sampled for the number of series, so three get three even steps, and they turn
/// round in dark mode, where the strongest color is the lightest.
/// </summary>
public sealed class ChartPalette
{
    private enum Kind { Colors, Shades, Tints, Diverging, Of }

    private readonly Kind _kind;
    private readonly string[] _colors;

    private ChartPalette(Kind kind, params string[] colors)
    {
        _kind = kind;
        _colors = colors;
    }

    /// <summary>
    /// Eight distinct colors, for series that aren't in an order, such as regions or products: the
    /// theme's <c>--ib-chart-1</c> to <c>--ib-chart-8</c>. The default.
    /// </summary>
    public static ChartPalette Colors { get; } = new(Kind.Colors);

    /// <summary>
    /// Light to dark shades of one color, for series in an order, such as age bands or plan tiers:
    /// the first series is the lightest. Past about six, shades are hard to tell apart.
    /// </summary>
    /// <param name="color">The color, such as <c>Color.Primary</c> or <c>Color.Of("#0f766e")</c>.</param>
    public static ChartPalette Shades(Color color) => new(Kind.Shades, StyleValues.Color(color));

    /// <summary>
    /// Light tints of one color up to the color itself, for ranges drawn as large areas, such as a
    /// <see cref="LinearGauge"/>'s bands under a needle: the first is the lightest, the last the full
    /// color, with no black mixed in, so even the strongest stays the color. Tints mix toward the
    /// surface, so they follow the color scheme.
    /// </summary>
    /// <param name="color">The color, such as <c>Color.Warning</c>.</param>
    public static ChartPalette Tints(Color color) => new(Kind.Tints, StyleValues.Color(color));

    /// <summary>
    /// Two colors that meet at a neutral middle, for series on either side of one, such as answers
    /// from "Strongly disagree" to "Strongly agree": the first series is the strongest
    /// <paramref name="low"/>, the last the strongest <paramref name="high"/>, and with an odd
    /// count the middle one is gray.
    /// </summary>
    public static ChartPalette Diverging(Color low, Color high) =>
        new(Kind.Diverging, StyleValues.Color(low), StyleValues.Color(high));

    /// <summary>Your own colors, one per series in order; they repeat past the last, up to eight.</summary>
    public static ChartPalette Of(params Color[] colors)
    {
        if (colors is not { Length: > 0 })
        {
            throw new ArgumentException("ChartPalette.Of needs at least one color.", nameof(colors));
        }

        return new(Kind.Of, colors.Select(StyleValues.Color).ToArray());
    }

    /// <summary>
    /// The style that sets <c>--ib-chart-1</c> onward for <paramref name="count"/> series (up to
    /// eight), or null for the theme's colors.
    /// </summary>
    internal string? Style(int count)
    {
        if (_kind == Kind.Colors || count <= 0) return null;
        var n = Math.Min(count, 8);
        var sb = new StringBuilder();
        for (var i = 0; i < n; i++)
        {
            sb.Append("--ib-chart-").Append(i + 1).Append(':').Append(Color(i, n)).Append(';');
        }

        return sb.ToString();
    }

    /// <summary>The theme's distinct colors, which say which, not how much: no use on a heatmap.</summary>
    internal bool IsCategorical => _kind == Kind.Colors;

    internal bool IsDiverging => _kind == Kind.Diverging;

    /// <summary>Your own colors' count (<see cref="Of"/>), or null.</summary>
    internal int? FixedCount => _kind == Kind.Of ? _colors.Length : null;

    /// <summary>
    /// A heatmap's colors: <c>--ib-heat-1</c> onward for <paramref name="classes"/> classes, or
    /// for a continuous scale (0) its ends, <c>--ib-heat-a</c> (the lightest, or a diverging
    /// scale's middle), <c>--ib-heat-b</c> (the strongest) and <c>--ib-heat-c</c> (a diverging
    /// scale's strongest low).
    /// </summary>
    internal string HeatStyle(int classes)
    {
        var sb = new StringBuilder();
        void Set(string name, string value) => sb.Append("--ib-heat-").Append(name).Append(':').Append(value).Append(';');
        if (classes > 0)
        {
            for (var i = 0; i < classes; i++) Set((i + 1).ToString(CultureInfo.InvariantCulture), Color(i, classes));
        }
        else if (_kind == Kind.Diverging)
        {
            Set("a", Color(1, 3));
            Set("b", Color(2, 3));
            Set("c", Color(0, 3));
        }
        else
        {
            Set("a", _kind == Kind.Of ? _colors[0] : Color(0, 2));
            Set("b", _kind == Kind.Of ? _colors[^1] : Color(1, 2));
        }

        return sb.ToString();
    }

    internal string Color(int i, int n) => _kind switch
    {
        Kind.Shades => Shade(_colors[0], n == 1 ? 0.5 : (double)i / (n - 1)),
        Kind.Tints => n == 1 ? _colors[0] : Tint(_colors[0], (double)i / (n - 1)),
        Kind.Diverging => Diverge(_colors[0], _colors[1], n == 1 ? 0 : -1 + 2.0 * i / (n - 1)),
        Kind.Of => _colors[i % _colors.Length],
        _ => $"var(--ib-chart-{i % 8 + 1})",
    };

    // Few's bullet-graph grays (percent of black on white), darkest first, for 1 to 5 ranges.
    private static readonly double[][] FewGrays =
    [
        [25], [35, 10], [40, 25, 10], [50, 35, 20, 10], [50, 40, 30, 20, 10],
    ];

    /// <summary>
    /// A thin gauge band's default gray for band <paramref name="i"/> of <paramref name="n"/>,
    /// darkest first: the text color mixed into the surface, so dark mode turns it round. A thin
    /// band is drawn alone on the surface, so its grays run darker than Few's (60% to 25%) for every
    /// band to show. Bands under a bar or needle take <see cref="BulletGray"/>.
    /// </summary>
    internal static string Gray(int i, int n) =>
        Mix("var(--ib-color-on-surface)", n <= 1 ? 45 : 60 - 35.0 * i / (n - 1), "var(--ib-color-surface)");

    /// <summary>
    /// A bullet's gray for range <paramref name="i"/> of <paramref name="n"/> (and a thick linear
    /// gauge's): Few's in light mode, at 60% strength in dark, where the text color mixed in is
    /// light: kept near the surface, the ranges stay behind the bar or needle instead of glaring.
    /// </summary>
    internal static string BulletGray(int i, int n)
    {
        var percent = n <= FewGrays.Length ? FewGrays[Math.Max(n, 1) - 1][i] : 50 - 40.0 * i / (n - 1);
        return $"light-dark({Mix("var(--ib-color-on-surface)", percent, "var(--ib-color-surface)")}, {Mix("var(--ib-color-on-surface)", percent * 0.6, "var(--ib-color-surface)")})";
    }

    private static string Mix(string color, double percent, string with) =>
        $"color-mix(in oklab, {color} {percent.ToString("0.#", CultureInfo.InvariantCulture)}%, {with})";

    // A tint t of the way from the lightest (0) to the color itself (1). Dark mode starts nearer the
    // color: a light tint of it on a near-black surface reads as empty track.
    private static string Tint(string color, double t) =>
        $"light-dark({Mix(color, 30 + 70 * t, "var(--ib-color-surface)")}, {Mix(color, 45 + 55 * t, "var(--ib-color-surface)")})";

    // t runs 0 to 1 along the ramp. In light mode it goes from the color mixed with white to the
    // color mixed with black; in dark mode the other way, so the last is always the strongest.
    private static string Shade(string color, double t)
    {
        var u = 2 * t - 1;
        var light = u <= 0 ? Mix(color, 100 + u * 58, "white") : Mix(color, 100 - u * 40, "black");
        // Dark mode reaches less far toward black: a deep shade goes dim on a near-black surface.
        var dark = u <= 0 ? Mix(color, 100 + u * 30, "black") : Mix(color, 100 - u * 45, "white");
        return $"light-dark({light}, {dark})";
    }

    // p runs -1 (the strongest low) to 1 (the strongest high); 0 is the neutral middle.
    private static string Diverge(string low, string high, double p)
    {
        if (Math.Abs(p) < 1e-9)
        {
            return $"light-dark({Mix("var(--ib-color-neutral)", 45, "white")}, {Mix("var(--ib-color-neutral)", 85, "black")})";
        }

        var color = p < 0 ? low : high;
        var s = Math.Abs(p);
        return $"light-dark({Mix(color, 30 + 70 * s, "white")}, {Mix(color, 60 + 40 * s, "black")})";
    }
}
