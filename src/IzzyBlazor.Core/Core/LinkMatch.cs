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
    /// match; links to other sites never match. A link with a query string (<c>?view=list</c>) is
    /// the page only when the current query string is the same, in any order, and never matches
    /// pages below it, so links that differ only in their query aren't all current at once.
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
        if (target.Query.Length > 1)
        {
            var same = string.Equals(currentPath, targetPath, StringComparison.OrdinalIgnoreCase)
                       && Query(target).SetEquals(Query(current));
            return (same, same);
        }

        if (string.Equals(currentPath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            return (true, true);
        }

        // The app's root (/, or the base path such as /app) only ever matches exactly; otherwise it
        // would be current on every page.
        var root = Path(new Uri(navigation.BaseUri));
        var below = match == NavLinkMatch.Prefix
                    && targetPath != "/"
                    && !string.Equals(targetPath, root, StringComparison.OrdinalIgnoreCase)
                    && currentPath.StartsWith(targetPath + "/", StringComparison.OrdinalIgnoreCase);
        return (below, false);
    }

    /// <summary>The value for <c>aria-current</c>: "page" for the page itself, "true" for a section above it.</summary>
    public static string? AriaCurrent((bool Active, bool Exact) state) =>
        state.Active ? (state.Exact ? "page" : "true") : null;

    // The query's key=value pairs, decoded, so their order and encoding don't matter.
    private static HashSet<string> Query(Uri uri) =>
        uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => Uri.UnescapeDataString(pair.Replace('+', ' ')))
            .ToHashSet(StringComparer.Ordinal);

    private static string Path(Uri uri)
    {
        var path = uri.AbsolutePath;
        return path.Length > 1 ? path.TrimEnd('/') : path;
    }
}
