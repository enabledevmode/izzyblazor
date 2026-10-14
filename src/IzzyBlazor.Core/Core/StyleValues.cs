using System.Text.RegularExpressions;

namespace IzzyBlazor;

/// <summary>Maps typed parameter values to CSS.</summary>
public static partial class StyleValues
{
    /// <summary>The largest spacing step.</summary>
    public const int MaxSpacing = 16;

    /// <summary>A spacing step from the scale, 0 to 16 (one step = 4px by default).</summary>
    public static string Spacing(int step) => step switch
    {
        < 0 => throw new ArgumentOutOfRangeException(nameof(step), step, "Spacing steps start at 0. Use a negative margin step for negative margins."),
        > MaxSpacing => throw new ArgumentOutOfRangeException(nameof(step), step, $"Spacing steps go up to {MaxSpacing}."),
        _ => $"var(--ib-space-{step})",
    };

    /// <summary>A margin step from the scale, -16 to 16. Negative values pull the element outward.</summary>
    public static string Margin(int step) => step switch
    {
        < -MaxSpacing or > MaxSpacing => throw new ArgumentOutOfRangeException(nameof(step), step, $"Margin steps go from -{MaxSpacing} to {MaxSpacing}."),
        < 0 => $"calc(var(--ib-space-{-step}) * -1)",
        _ => $"var(--ib-space-{step})",
    };

    /// <summary>CSS <c>flex-direction</c>.</summary>
    public static string Direction(Direction direction) => direction switch
    {
        IzzyBlazor.Direction.Row => "row",
        IzzyBlazor.Direction.Column => "column",
        IzzyBlazor.Direction.RowReverse => "row-reverse",
        IzzyBlazor.Direction.ColumnReverse => "column-reverse",
        _ => throw new ArgumentOutOfRangeException(nameof(direction)),
    };

    /// <summary>CSS <c>align-items</c>.</summary>
    public static string Align(Align align) => align switch
    {
        IzzyBlazor.Align.Stretch => "stretch",
        IzzyBlazor.Align.Start => "flex-start",
        IzzyBlazor.Align.Center => "center",
        IzzyBlazor.Align.End => "flex-end",
        IzzyBlazor.Align.Baseline => "baseline",
        _ => throw new ArgumentOutOfRangeException(nameof(align)),
    };

    /// <summary>CSS <c>justify-content</c>.</summary>
    public static string Justify(Justify justify) => justify switch
    {
        IzzyBlazor.Justify.Start => "flex-start",
        IzzyBlazor.Justify.Center => "center",
        IzzyBlazor.Justify.End => "flex-end",
        IzzyBlazor.Justify.Between => "space-between",
        IzzyBlazor.Justify.Around => "space-around",
        IzzyBlazor.Justify.Evenly => "space-evenly",
        _ => throw new ArgumentOutOfRangeException(nameof(justify)),
    };

    /// <summary>CSS <c>border-radius</c> from a token.</summary>
    public static string Radius(Radius radius) => radius switch
    {
        IzzyBlazor.Radius.None => "var(--ib-radius-none)",
        IzzyBlazor.Radius.Sm => "var(--ib-radius-sm)",
        IzzyBlazor.Radius.Md => "var(--ib-radius-md)",
        IzzyBlazor.Radius.Lg => "var(--ib-radius-lg)",
        IzzyBlazor.Radius.Xl => "var(--ib-radius-xl)",
        IzzyBlazor.Radius.Full => "var(--ib-radius-full)",
        _ => throw new ArgumentOutOfRangeException(nameof(radius)),
    };

    /// <summary>CSS <c>box-shadow</c> from a token.</summary>
    public static string Shadow(Shadow shadow) => shadow switch
    {
        IzzyBlazor.Shadow.None => "var(--ib-shadow-none)",
        IzzyBlazor.Shadow.Sm => "var(--ib-shadow-sm)",
        IzzyBlazor.Shadow.Md => "var(--ib-shadow-md)",
        IzzyBlazor.Shadow.Lg => "var(--ib-shadow-lg)",
        _ => throw new ArgumentOutOfRangeException(nameof(shadow)),
    };

    /// <summary>
    /// A font size that grows from <paramref name="min"/> on a 360px screen to <paramref name="max"/>
    /// at 1280px: <c>clamp(min, intercept + slope·vw, max)</c>, the ends being the size tokens.
    /// </summary>
    public static string FluidTextSize(TextSize min, TextSize max)
    {
        if (min > max)
        {
            (min, max) = (max, min);
        }

        // Interpolate between 22.5rem (360px) and 80rem (1280px) of viewport, using the tokens'
        // default sizes; clamp() holds the ends to the tokens themselves.
        var low = Rem(min);
        var high = Rem(max);
        var slope = (high - low) / (80 - 22.5);
        var intercept = low - slope * 22.5;
        var css = System.FormattableString.Invariant($"clamp({TextSize(min)}, {intercept:0.####}rem + {slope * 100:0.####}vw, {TextSize(max)})");
        return css;

        static double Rem(TextSize size) => size switch
        {
            IzzyBlazor.TextSize.Xs => 0.75,
            IzzyBlazor.TextSize.Sm => 0.875,
            IzzyBlazor.TextSize.Md => 1,
            IzzyBlazor.TextSize.Lg => 1.125,
            IzzyBlazor.TextSize.Xl => 1.25,
            IzzyBlazor.TextSize.Xl2 => 1.5,
            IzzyBlazor.TextSize.Xl3 => 1.875,
            IzzyBlazor.TextSize.Xl4 => 2.25,
            IzzyBlazor.TextSize.Xl5 => 3,
            _ => 3.75,
        };
    }

    /// <summary>CSS <c>font-size</c> from a token.</summary>
    public static string TextSize(TextSize size) => size switch
    {
        IzzyBlazor.TextSize.Xs => "var(--ib-text-xs)",
        IzzyBlazor.TextSize.Sm => "var(--ib-text-sm)",
        IzzyBlazor.TextSize.Md => "var(--ib-text-md)",
        IzzyBlazor.TextSize.Lg => "var(--ib-text-lg)",
        IzzyBlazor.TextSize.Xl => "var(--ib-text-xl)",
        IzzyBlazor.TextSize.Xl2 => "var(--ib-text-2xl)",
        IzzyBlazor.TextSize.Xl3 => "var(--ib-text-3xl)",
        IzzyBlazor.TextSize.Xl4 => "var(--ib-text-4xl)",
        IzzyBlazor.TextSize.Xl5 => "var(--ib-text-5xl)",
        IzzyBlazor.TextSize.Xl6 => "var(--ib-text-6xl)",
        _ => throw new ArgumentOutOfRangeException(nameof(size)),
    };

    /// <summary>CSS <c>text-align</c>.</summary>
    public static string TextAlign(TextAlign align) => align switch
    {
        IzzyBlazor.TextAlign.Start => "start",
        IzzyBlazor.TextAlign.Center => "center",
        IzzyBlazor.TextAlign.End => "end",
        IzzyBlazor.TextAlign.Justify => "justify",
        _ => throw new ArgumentOutOfRangeException(nameof(align)),
    };

    /// <summary>CSS <c>flex-wrap</c>.</summary>
    public static string Wrap(bool wrap) => wrap ? "wrap" : "nowrap";

    /// <summary>A flex grow or shrink factor: 1 or 0.</summary>
    public static string Factor(bool on) => on ? "1" : "0";

    /// <summary>A <see cref="IzzyBlazor.Length"/>'s CSS.</summary>
    public static string Length(Length length) => length.Css ?? throw new ArgumentException("A default Length has no value.", nameof(length));

    /// <summary>A <see cref="IzzyBlazor.Color"/>'s CSS.</summary>
    public static string Color(Color color) => color.Css ?? throw new ArgumentException("A default Color has no value.", nameof(color));

    /// <summary>A raw CSS value, after checking it cannot escape the declaration.</summary>
    public static string Raw(string value, string what = "value")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (UnsafeCss().IsMatch(value))
        {
            throw new FormatException($"The {what} \"{value}\" contains characters that are not allowed in a style value.");
        }

        return value;
    }

    [GeneratedRegex(@"[;{}<>""'\\]|/\*|url\(", RegexOptions.IgnoreCase)]
    private static partial Regex UnsafeCss();
}
