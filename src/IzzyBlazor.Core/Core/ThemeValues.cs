namespace IzzyBlazor;

/// <summary>
/// A look: density, shape and type, with a default accent and gray suited to the products likely
/// to use it. Set it with <see cref="IzzyTheme"/> or <c>data-ib-preset</c> on <c>&lt;html&gt;</c>.
/// </summary>
public enum ThemePreset
{
    /// <summary>The default: 36px controls, medium radius, hairline shadows; Blue on Neutral.</summary>
    Standard,

    /// <summary>Dense data and admin: 32px controls, tight rows, small radius, borders only; Indigo on Slate.</summary>
    Compact,

    /// <summary>Consumer apps and portals: 40px controls, roomy, large radius, gentle shadows; Teal on Neutral.</summary>
    Soft,

    /// <summary>Reading and docs: IBM Plex Sans with Newsreader headings, large radius; Ink on Stone.</summary>
    Editorial,
}

/// <summary>A built-in accent, hand-tuned in light and dark. For your own color, use <see cref="IzzyTheme.Primary"/>.</summary>
public enum ThemeAccent
{
    /// <summary>The default accent.</summary>
    Blue,

    /// <summary>Monochrome: the gray set's text color.</summary>
    Ink,

    /// <summary>Indigo.</summary>
    Indigo,

    /// <summary>Violet.</summary>
    Violet,

    /// <summary>Teal.</summary>
    Teal,

    /// <summary>Green.</summary>
    Green,

    /// <summary>Rose.</summary>
    Rose,
}

/// <summary>The gray set: page, surfaces, fills, borders and text.</summary>
public enum ThemeGray
{
    /// <summary>No tint (the default).</summary>
    Neutral,

    /// <summary>Cool.</summary>
    Slate,

    /// <summary>Warm paper; pairs with serif headings.</summary>
    Stone,
}

/// <summary>The radius scale: controls, cards and dialogs shift together.</summary>
public enum ThemeRadius
{
    /// <summary>Small.</summary>
    Sm,

    /// <summary>Medium (the default).</summary>
    Md,

    /// <summary>Large.</summary>
    Lg,
}

/// <summary>
/// How much room controls and content take: control heights, table rows and card padding together.
/// </summary>
public enum ThemeDensity
{
    /// <summary>More on screen: 32px controls, tight table rows.</summary>
    Compact,

    /// <summary>The default: 36px controls.</summary>
    Standard,

    /// <summary>Roomier: 40px controls, generous rows and padding.</summary>
    Comfortable,
}

/// <summary>How a <see cref="ColorSchemeSwitch"/> looks.</summary>
public enum ColorSchemeSwitchVariant
{
    /// <summary>An icon button (sun or moon) that opens a menu: System, Light, Dark. The default.</summary>
    Menu,

    /// <summary>One icon button that flips between light and dark.</summary>
    Toggle,

    /// <summary>System, Light and Dark side by side, for a settings page.</summary>
    Segmented,
}

/// <summary>The color scheme cookie (<c>ib-scheme</c>) that ColorSchemeSwitch writes and IzzyTheme reads.</summary>
internal static class ColorSchemes
{
    public const string CookieName = "ib-scheme";

    // On the wire (cookie, endpoint, script) Auto is "system", the word the switch shows.
    public static string Name(ColorScheme scheme) => scheme switch
    {
        ColorScheme.Light => "light",
        ColorScheme.Dark => "dark",
        ColorScheme.Auto => "system",
        _ => throw new ArgumentOutOfRangeException(nameof(scheme), scheme, "The app's color scheme is Auto, Light or Dark; Inverse is for part of a page."),
    };

    public static ColorScheme? Parse(string? value) => value switch
    {
        "light" => ColorScheme.Light,
        "dark" => ColorScheme.Dark,
        "system" => ColorScheme.Auto,
        _ => null,
    };
}
