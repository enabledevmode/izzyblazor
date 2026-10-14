namespace IzzyBlazor;

/// <summary>
/// Light, dark, or automatic. For the app (<see cref="IzzyTheme.ColorScheme"/>, the user's pick in
/// a <see cref="ColorSchemeSwitch"/>), <see cref="Auto"/> follows the device. For part of a page
/// (<c>ColorScheme</c> on Box and the App layout parts), <see cref="Auto"/> follows the page, and
/// <see cref="Inverse"/> is the opposite of it. Every color token switches with the scheme, so
/// links, buttons, badges, fields and focus rings inside keep their contrast.
/// </summary>
public enum ColorScheme
{
    /// <summary>Follow the device for the app, or the page for part of it (the default).</summary>
    Auto,

    /// <summary>Always light.</summary>
    Light,

    /// <summary>Always dark, such as a dark sidebar in a light app.</summary>
    Dark,

    /// <summary>For part of a page: the opposite of the page, dark on a light page and light on a dark one.</summary>
    Inverse,
}

internal static class SchemeNames
{
    /// <summary>The class that applies the scheme to part of a page, or null for <see cref="ColorScheme.Auto"/>.</summary>
    public static string? ClassName(this ColorScheme scheme) => scheme switch
    {
        ColorScheme.Auto => null,
        ColorScheme.Light => "ib-scheme-light",
        ColorScheme.Dark => "ib-scheme-dark",
        ColorScheme.Inverse => "ib-scheme-inverse",
        _ => throw new ArgumentOutOfRangeException(nameof(scheme)),
    };
}
