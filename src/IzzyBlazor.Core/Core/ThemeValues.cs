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

/// <summary>
/// The values <see cref="IzzyTheme.Brand"/> and <see cref="IzzyTheme.Gray"/> take: a built-in name
/// or a hex color.
/// </summary>
internal static class ThemeColors
{
    /// <summary>The built-in brand colors, hand-tuned in light and dark: blue (the default), ink (monochrome), indigo, violet, teal, green, rose.</summary>
    public static readonly string[] Brands = ["blue", "ink", "indigo", "violet", "teal", "green", "rose"];

    /// <summary>The built-in gray sets: neutral (the default), slate (cool), stone (warm paper).</summary>
    public static readonly string[] Grays = ["neutral", "slate", "stone"];

    /// <summary>A built-in name (lowercase) or a normalized hex color; anything else throws, listing what's allowed.</summary>
    public static (string? Name, string? Hex) Parse(string value, string[] names, string parameter)
    {
        var text = value.Trim();
        if (names.Contains(text.ToLowerInvariant()))
        {
            return (text.ToLowerInvariant(), null);
        }

        if (text.StartsWith('#') && ColorValues.NormalizeHex(text) is { } hex)
        {
            return (null, hex);
        }

        throw new ArgumentException($"{parameter} is a hex color such as #0a7d5a, or one of: {string.Join(", ", names)}. Got \"{value}\".", parameter);
    }
}

/// <summary>The radius scale: controls, cards and dialogs shift together.</summary>
public enum ThemeRadius
{
    /// <summary>Square corners everywhere.</summary>
    None,

    /// <summary>Small.</summary>
    Sm,

    /// <summary>Medium (the default).</summary>
    Md,

    /// <summary>Large.</summary>
    Lg,

    /// <summary>
    /// Pill-shaped controls: buttons, fields, segmented controls, tags and badges. Cards, popovers
    /// and dialogs take the large scale; joined button groups round only their outer ends.
    /// </summary>
    Full,
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
