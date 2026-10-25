using System.Text.RegularExpressions;

namespace IzzyBlazor.Site.Services;

/// <summary>Short text taken from doc comments for an Overview's lists (Parts, See also), search results and descriptions.</summary>
public static partial class OverviewText
{
    /// <summary>The first sentence of a doc comment's text.</summary>
    public static string? FirstSentence(string? html) => Sentence(ApiMetadata.PlainText(html), int.MaxValue);

    /// <summary>
    /// The first sentence of plain text, cut to <paramref name="max"/> characters with an ellipsis:
    /// a search result's summary, a page's description (Google shows about 155 characters).
    /// </summary>
    public static string? Sentence(string? text, int max = 160)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var end = SentenceEnd().Match(text);
        var sentence = end.Success ? text[..(end.Index + 1)] : text;
        return sentence.Length <= max ? sentence : sentence[..(max - 3)].TrimEnd() + "…";
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

    [GeneratedRegex(@"[.!?](?=\s|$)")]
    private static partial Regex SentenceEnd();
}
