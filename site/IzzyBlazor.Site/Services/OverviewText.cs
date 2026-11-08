using System.Net;
using System.Text.RegularExpressions;

namespace IzzyBlazor.Site.Services;

/// <summary>Short text taken from doc comments for an Overview's lists (Parts, See also), search results and descriptions.</summary>
public static partial class OverviewText
{
    /// <summary>The first sentence of a doc comment's text.</summary>
    public static string? FirstSentence(string? html) => Sentence(ApiMetadata.PlainText(html), int.MaxValue);

    /// <summary>
    /// The first sentence of a doc comment with its code kept in backticks, for <c>InlineCode</c>: a
    /// part's line or a parameter's description, where <c>&lt;Tag Context="c"&gt;</c> must read as code.
    /// </summary>
    public static string? FirstSentenceWithCode(string? html)
    {
        if (html is null)
        {
            return null;
        }

        var text = WebUtility.HtmlDecode(Tags().Replace(CodeSpan().Replace(html, m => $"`{m.Groups[1].Value.Replace("`", "", StringComparison.Ordinal)}`"), "")).Trim();
        if (text.Length == 0)
        {
            return null;
        }

        // The first end outside code: a dot followed by a space inside `a. b` isn't one.
        foreach (Match end in SentenceEnd().Matches(text))
        {
            if (text.AsSpan(0, end.Index).Count('`') % 2 == 0)
            {
                return text[..(end.Index + 1)];
            }
        }

        return text;
    }

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

    [GeneratedRegex("<code>(.*?)</code>", RegexOptions.Singleline)]
    private static partial Regex CodeSpan();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"[.!?](?=\s|$)")]
    private static partial Regex SentenceEnd();
}
