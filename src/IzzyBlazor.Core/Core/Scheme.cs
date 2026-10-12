namespace IzzyBlazor;

/// <summary>
/// Which color scheme part of a page uses: the page's own, or always light, always dark, or
/// the opposite of the page. Every color token switches with it, so links, buttons, badges,
/// fields and focus rings inside keep their contrast.
/// </summary>
public enum Scheme
{
    /// <summary>Follow the page (the default).</summary>
    Page,

    /// <summary>Always light.</summary>
    Light,

    /// <summary>Always dark, such as a dark sidebar in a light app.</summary>
    Dark,

    /// <summary>The opposite of the page: dark on a light page, light on a dark page.</summary>
    Inverse,
}

internal static class SchemeNames
{
    /// <summary>The class that applies the scheme, or null for <see cref="Scheme.Page"/>.</summary>
    public static string? ClassName(this Scheme scheme) => scheme switch
    {
        Scheme.Page => null,
        Scheme.Light => "ib-scheme-light",
        Scheme.Dark => "ib-scheme-dark",
        Scheme.Inverse => "ib-scheme-inverse",
        _ => throw new ArgumentOutOfRangeException(nameof(scheme)),
    };
}
