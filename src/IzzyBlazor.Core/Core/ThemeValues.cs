namespace IzzyBlazor;

/// <summary>
/// A look: density, shape and type, with a default accent and gray suited to the products likely
/// to use it. Set it with <see cref="IzzyTheme"/> or <c>data-ib-theme</c> on <c>&lt;html&gt;</c>.
/// </summary>
public enum ThemeLook
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
