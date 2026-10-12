using Microsoft.Extensions.Primitives;

namespace IzzyBlazor;

/// <summary>
/// Parses a URL's query string like ASP.NET Core's <c>QueryHelpers.ParseQuery</c>, which lives in
/// the server framework and isn't available in WebAssembly: keys ignore case, <c>+</c> is a
/// space, and a repeated key collects its values.
/// </summary>
internal static class QueryString
{
    public static Dictionary<string, StringValues> Parse(string? query)
    {
        var result = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query))
        {
            return result;
        }

        foreach (var pair in query.TrimStart('?').Split('&'))
        {
            if (pair.Length == 0)
            {
                continue;
            }

            var equals = pair.IndexOf('=');
            var key = Decode(equals < 0 ? pair : pair[..equals]);
            var value = equals < 0 ? "" : Decode(pair[(equals + 1)..]);
            result[key] = result.TryGetValue(key, out var existing) ? StringValues.Concat(existing, value) : new StringValues(value);
        }

        return result;
    }

    /// <summary>The query of a URI string, parsed.</summary>
    public static Dictionary<string, StringValues> Of(string uri) => Parse(new Uri(uri).Query);

    private static string Decode(string text) => Uri.UnescapeDataString(text.Replace('+', ' '));
}
