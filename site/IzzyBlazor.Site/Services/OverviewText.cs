namespace IzzyBlazor.Site.Services;

/// <summary>Short text taken from doc comments for an Overview's lists (Parts, See also).</summary>
public static class OverviewText
{
    /// <summary>The first sentence of a doc comment's text.</summary>
    public static string? FirstSentence(string? html)
    {
        var text = ApiMetadata.PlainText(html);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var end = text.IndexOf(". ", StringComparison.Ordinal);
        return end < 0 ? text : text[..(end + 1)];
    }

    /// <summary>
    /// "A square button…" reads as "IconButton: a square button…", but a name or code stays as
    /// written.
    /// </summary>
    public static string? Lower(ApiMetadata api, string? text)
    {
        if (text is not { Length: > 1 } || !char.IsUpper(text[0]) || !(char.IsLower(text[1]) || text[1] == ' '))
        {
            return text;
        }

        var word = new string(text.TakeWhile(char.IsLetterOrDigit).ToArray());
        var name = word.Skip(1).Any(char.IsUpper) || api.Component(word) is not null || api.Type(word) is not null;
        return name ? text : char.ToLowerInvariant(text[0]) + text[1..];
    }
}
