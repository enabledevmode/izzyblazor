using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace IzzyBlazor;

/// <summary>
/// Whether a link points at the current page, shared by <see cref="NavItem"/> and
/// <see cref="TabLink"/>. Worked out on the server, so it's right on static pages and after
/// enhanced navigation.
/// </summary>
internal static class LinkMatch
{
    /// <summary>
    /// Whether <paramref name="currentUri"/> matches <paramref name="href"/>, and whether it's the
    /// page itself (<c>aria-current="page"</c>) rather than a page below it (<c>"true"</c>).
    /// <c>Prefix</c> also matches pages below the link; a root link ("/") always needs an exact
    /// match; links to other sites never match.
    /// </summary>
    public static (bool Active, bool Exact) Of(NavigationManager navigation, string? href, string currentUri, NavLinkMatch match)
    {
        if (string.IsNullOrEmpty(href))
        {
            return (false, false);
        }

        var target = navigation.ToAbsoluteUri(href);
        var current = new Uri(currentUri);
        if (!string.Equals(target.Authority, current.Authority, StringComparison.OrdinalIgnoreCase))
        {
            return (false, false);
        }

        var targetPath = Path(target);
        var currentPath = Path(current);
        if (string.Equals(currentPath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            return (true, true);
        }

        var below = match == NavLinkMatch.Prefix
                    && targetPath != "/"
                    && currentPath.StartsWith(targetPath + "/", StringComparison.OrdinalIgnoreCase);
        return (below, false);
    }

    /// <summary>The value for <c>aria-current</c>: "page" for the page itself, "true" for a section above it.</summary>
    public static string? AriaCurrent((bool Active, bool Exact) state) =>
        state.Active ? (state.Exact ? "page" : "true") : null;

    private static string Path(Uri uri)
    {
        var path = uri.AbsolutePath;
        return path.Length > 1 ? path.TrimEnd('/') : path;
    }
}
