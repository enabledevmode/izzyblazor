using System.Globalization;
using System.Text;

namespace IzzyBlazor;

/// <summary>
/// The colors a <see cref="Chart{TItem}"/> or <see cref="PieChart{TItem}"/> gives its series or
/// slices, in order: <see cref="Colors"/> (the default), <see cref="Shades"/> of one color,
/// <see cref="Diverging"/> between two, or your own with <see cref="Of"/>. Shades and diverging
/// ramps are sampled for the number of series, so three get three even steps, and they turn
/// round in dark mode, where the strongest color is the lightest.
/// </summary>
public sealed class ChartPalette
{
    private enum Kind { Colors, Shades, Diverging, Of }

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

    internal string Color(int i, int n) => _kind switch
    {
        Kind.Shades => Shade(_colors[0], n == 1 ? 0.5 : (double)i / (n - 1)),
        Kind.Diverging => Diverge(_colors[0], _colors[1], n == 1 ? 0 : -1 + 2.0 * i / (n - 1)),
        Kind.Of => _colors[i % _colors.Length],
        _ => $"var(--ib-chart-{i % 8 + 1})",
    };

    private static string Mix(string color, double percent, string with) =>
        $"color-mix(in oklab, {color} {percent.ToString("0.#", CultureInfo.InvariantCulture)}%, {with})";

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
