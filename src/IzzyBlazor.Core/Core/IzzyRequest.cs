using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// What kind of request the page is rendering for, when IzzyBlazor's script fetches the page
/// itself for one part of it.
/// </summary>
public static class IzzyRequest
{
    /// <summary>The query key naming the field a fetch asks suggestions for.</summary>
    internal const string SuggestKey = "ib-suggest";

    /// <summary>The query key holding the typed text a fetch asks suggestions for.</summary>
    internal const string SuggestTextKey = "ib-suggest-q";

    /// <summary>
    /// Whether this render answers a Combobox or Autocomplete asking the server for suggestions on a
    /// static page. Only that field's options are kept from the response; the rest is thrown away,
    /// so a page with costly loads of its own can skip them. IzzyBlazor's other fields and data
    /// views skip theirs.
    /// </summary>
    public static bool IsSuggestion(NavigationManager navigation) =>
        QueryString.Of(navigation.Uri).ContainsKey(SuggestKey);

    /// <summary>
    /// For a field that fetches suggestions under <paramref name="key"/>: <c>null</c> when this isn't
    /// a suggestion request, "" kept out (<paramref name="asked"/> false) when it's another field's,
    /// else the typed text.
    /// </summary>
    internal static string? SuggestionFor(NavigationManager navigation, string key, out bool asked)
    {
        var query = QueryString.Of(navigation.Uri);
        asked = false;
        if (!query.TryGetValue(SuggestKey, out var field))
        {
            return null;
        }

        asked = string.Equals(field.ToString(), key, StringComparison.Ordinal);
        return asked && query.TryGetValue(SuggestTextKey, out var text) ? text.ToString() : "";
    }
}
