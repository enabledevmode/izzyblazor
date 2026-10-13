using System.Text.RegularExpressions;

namespace IzzyBlazor;

/// <summary>
/// A color: one of the theme's tokens (so it follows light and dark mode), a custom
/// token from your own theme, or a raw CSS color.
/// </summary>
/// <example>
/// <code>
/// &lt;Box Bg="Color.SurfaceContainer" Color="Color.OnSurface"&gt;
/// &lt;Box Bg="Color.Token(&quot;brand-500&quot;)"&gt;   @* var(--ib-color-brand-500) *@
/// &lt;Box Bg="Color.Of(&quot;#ff6600&quot;)"&gt;
/// </code>
/// </example>
public readonly partial struct Color : IEquatable<Color>
{
    private static readonly HashSet<string> Surfaces = new(StringComparer.Ordinal)
    {
        "background", "surface", "surface-container-low", "surface-container", "surface-container-high", "surface-raised",
    };

    private Color(string css, string? tokenName = null)
    {
        Css = css;
        TokenName = tokenName;
    }

    /// <summary>The CSS value this color renders as.</summary>
    public string Css { get; }

    /// <summary>The theme token name, such as <c>primary</c>, or <c>null</c> for a raw color.</summary>
    public string? TokenName { get; }

    /// <summary>
    /// The text color that pairs with this color used as a background: <c>OnPrimary</c> for
    /// <c>Primary</c>, <c>OnSurface</c> for the surfaces, <c>on-{name}</c> for a custom token
    /// (which falls back to the inherited color when the theme does not define it).
    /// <c>null</c> for raw colors and for colors that are already text colors.
    /// </summary>
    public Color? ContrastText => TokenName switch
    {
        null => null,
        _ when Surfaces.Contains(TokenName) => OnSurface,
        "primary-soft" => PrimaryText,
        _ when TokenName.StartsWith("on-", StringComparison.Ordinal) || TokenName.EndsWith("-text", StringComparison.Ordinal)
               || TokenName is "muted" or "subtle" or "disabled" or "border" => null,
        _ => Token("on-" + TokenName),
    };

    /// <summary>Brand color.</summary>
    public static Color Primary { get; } = Token("primary");

    /// <summary>Text and icons on <see cref="Primary"/>.</summary>
    public static Color OnPrimary { get; } = Token("on-primary");

    /// <summary>Success.</summary>
    public static Color Success { get; } = Token("success");

    /// <summary>Text and icons on <see cref="Success"/>.</summary>
    public static Color OnSuccess { get; } = Token("on-success");

    /// <summary>Warning.</summary>
    public static Color Warning { get; } = Token("warning");

    /// <summary>Text and icons on <see cref="Warning"/>.</summary>
    public static Color OnWarning { get; } = Token("on-warning");

    /// <summary>Danger.</summary>
    public static Color Danger { get; } = Token("danger");

    /// <summary>Text and icons on <see cref="Danger"/>.</summary>
    public static Color OnDanger { get; } = Token("on-danger");

    /// <summary>Informational.</summary>
    public static Color Info { get; } = Token("info");

    /// <summary>Text and icons on <see cref="Info"/>.</summary>
    public static Color OnInfo { get; } = Token("on-info");

    /// <summary>Neutral accent.</summary>
    public static Color Neutral { get; } = Token("neutral");

    /// <summary>Text and icons on <see cref="Neutral"/>.</summary>
    public static Color OnNeutral { get; } = Token("on-neutral");

    /// <summary>The page, behind the surfaces: a warm off-white in the default theme.</summary>
    public static Color Background { get; } = Token("background");

    /// <summary>Cards, tables, fields and bars: white in light mode.</summary>
    public static Color Surface { get; } = Token("surface");

    /// <summary>The lightest fill inside a surface, for large areas: a filled card, a panel.</summary>
    public static Color SurfaceContainerLow { get; } = Token("surface-container-low");

    /// <summary>A fill inside a surface that must show at small sizes: table heads, code blocks.</summary>
    public static Color SurfaceContainer { get; } = Token("surface-container");

    /// <summary>A stronger fill: tracks, chips, skeletons, nested panels.</summary>
    public static Color SurfaceContainerHigh { get; } = Token("surface-container-high");

    /// <summary>Floating surfaces such as dialogs and menus. Lighter than the page in dark mode.</summary>
    public static Color SurfaceRaised { get; } = Token("surface-raised");

    /// <summary>Text on any surface.</summary>
    public static Color OnSurface { get; } = Token("on-surface");

    /// <summary>Secondary text on any surface: descriptions, metadata.</summary>
    public static Color Muted { get; } = Token("muted");

    /// <summary>Tertiary text: captions, hints, placeholders. Still at least 4.5:1 on Surface and SurfaceContainer.</summary>
    public static Color Subtle { get; } = Token("subtle");

    /// <summary>Text of disabled controls. Deliberately low contrast.</summary>
    public static Color Disabled { get; } = Token("disabled");

    /// <summary><see cref="Primary"/> tuned for text and links on surfaces (at least 4.5:1).</summary>
    public static Color PrimaryText { get; } = Token("primary-text");

    /// <summary><see cref="Success"/> tuned for text on surfaces (at least 4.5:1).</summary>
    public static Color SuccessText { get; } = Token("success-text");

    /// <summary><see cref="Warning"/> tuned for text on surfaces (at least 4.5:1).</summary>
    public static Color WarningText { get; } = Token("warning-text");

    /// <summary><see cref="Danger"/> tuned for text on surfaces (at least 4.5:1).</summary>
    public static Color DangerText { get; } = Token("danger-text");

    /// <summary><see cref="Info"/> tuned for text on surfaces (at least 4.5:1).</summary>
    public static Color InfoText { get; } = Token("info-text");

    /// <summary>The accent's soft tint: soft buttons, the current page, chips. Text on it: <see cref="PrimaryText"/>.</summary>
    public static Color PrimarySoft { get; } = Token("primary-soft");

    /// <summary>Borders and dividers. Decorative: too light to be the only edge of a control.</summary>
    public static Color Border { get; } = Token("border");

    /// <summary>
    /// The edge of a control, such as an input or switch track: about 2:1 against
    /// <see cref="Surface"/>, darker than <see cref="Border"/> so an empty field reads as a box.
    /// </summary>
    public static Color Outline { get; } = Token("outline");

    /// <summary>Fully transparent.</summary>
    public static Color Transparent { get; } = new("transparent");

    /// <summary>The current text color.</summary>
    public static Color CurrentColor { get; } = new("currentColor");

    /// <summary>
    /// A theme token by name, rendered as <c>var(--ib-color-{name})</c>. Use it for tokens
    /// your own theme adds.
    /// </summary>
    public static Color Token(string name) =>
        ValidTokenName().IsMatch(name)
            ? new($"var(--ib-color-{name})", name)
            : throw new FormatException($"\"{name}\" is not a valid color token name (lowercase letters, digits and dashes).");

    /// <summary>A raw CSS color such as <c>#ff6600</c> or <c>oklch(70% 0.1 250)</c>. Checked for unsafe characters.</summary>
    public static Color Of(string css) => new(StyleValues.Raw(css, "color"));

    /// <inheritdoc />
    public bool Equals(Color other) => Css == other.Css;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Color other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Css?.GetHashCode(StringComparison.Ordinal) ?? 0;

    /// <inheritdoc />
    public override string ToString() => Css ?? "";

    [GeneratedRegex("^[a-z][a-z0-9-]*$")]
    private static partial Regex ValidTokenName();
}
