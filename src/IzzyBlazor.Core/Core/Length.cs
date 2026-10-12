using System.Globalization;

namespace IzzyBlazor;

/// <summary>
/// A width, height or flex basis: a size token, a keyword, or a CSS length built with
/// a factory such as <see cref="Rem"/>.
/// </summary>
/// <example>
/// <code>
/// &lt;Box MaxWidth="Length.Prose"&gt;
/// &lt;Box Width="new() { Base = Length.Full, Md = Length.Rem(15) }"&gt;
/// &lt;Box MinHeight="Length.Dvh(100)"&gt;
/// </code>
/// </example>
public readonly struct Length : IEquatable<Length>
{
    private Length(string css) => Css = css;

    /// <summary>The CSS value this length renders as.</summary>
    public string Css { get; }

    /// <summary>640px (matches the <c>sm</c> breakpoint).</summary>
    public static Length Sm { get; } = new("var(--ib-size-sm)");

    /// <summary>768px (matches the <c>md</c> breakpoint).</summary>
    public static Length Md { get; } = new("var(--ib-size-md)");

    /// <summary>1024px (matches the <c>lg</c> breakpoint).</summary>
    public static Length Lg { get; } = new("var(--ib-size-lg)");

    /// <summary>1280px (matches the <c>xl</c> breakpoint).</summary>
    public static Length Xl { get; } = new("var(--ib-size-xl)");

    /// <summary>A comfortable reading width, 68 characters.</summary>
    public static Length Prose { get; } = new("var(--ib-size-prose)");

    /// <summary>100% of the containing block.</summary>
    public static Length Full { get; } = new("100%");

    /// <summary><c>auto</c>.</summary>
    public static Length Auto { get; } = new("auto");

    /// <summary><c>fit-content</c>: as wide as the content, up to the available space.</summary>
    public static Length FitContent { get; } = new("fit-content");

    /// <summary><c>min-content</c>.</summary>
    public static Length MinContent { get; } = new("min-content");

    /// <summary><c>max-content</c>.</summary>
    public static Length MaxContent { get; } = new("max-content");

    /// <summary>Pixels.</summary>
    public static Length Px(double value) => Unit(value, "px");

    /// <summary>Root ems (16px each by default).</summary>
    public static Length Rem(double value) => Unit(value, "rem");

    /// <summary>Percent of the containing block.</summary>
    public static Length Percent(double value) => Unit(value, "%");

    /// <summary>Character widths, for text measures.</summary>
    public static Length Ch(double value) => Unit(value, "ch");

    /// <summary>Percent of the viewport width.</summary>
    public static Length Vw(double value) => Unit(value, "vw");

    /// <summary>Percent of the dynamic viewport height (accounts for mobile browser bars).</summary>
    public static Length Dvh(double value) => Unit(value, "dvh");

    /// <summary>Any CSS length, such as <c>calc(100% - 2rem)</c>. Checked for unsafe characters.</summary>
    public static Length Of(string css) => new(StyleValues.Raw(css, "length"));

    private static Length Unit(double value, string unit) =>
        new(value.ToString(CultureInfo.InvariantCulture) + unit);

    /// <inheritdoc />
    public bool Equals(Length other) => Css == other.Css;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Length other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Css?.GetHashCode(StringComparison.Ordinal) ?? 0;

    /// <inheritdoc />
    public override string ToString() => Css ?? "";
}
