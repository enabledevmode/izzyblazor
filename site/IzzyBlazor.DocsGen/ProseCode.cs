using System.Text.RegularExpressions;

namespace IzzyBlazor.DocsGen;

/// <summary>
/// Shows what's plainly code in a component's prose as code, so a comment reads as it should without
/// backticks around every name: attributes and address parts by their form, and the names of its own
/// parameters and of other components and parts. A parameter that's a single word (<c>Max</c>) is a name
/// only inside a sentence, where a capital says so: at a sentence's start it could be an ordinary word,
/// so there it takes backticks.
/// </summary>
public static partial class ProseCode
{
    /// <summary>The HTML with code marked, leaving alone what's already code, a link or a tag.</summary>
    /// <param name="html">A comment's HTML, its text encoded.</param>
    /// <param name="own">The component's parameters and events.</param>
    /// <param name="others">Components and parts other than this one, and the types its values can be.</param>
    public static string? Apply(string? html, IReadOnlySet<string> own, IReadOnlySet<string> others)
    {
        if (html is null)
        {
            return null;
        }

        var result = new System.Text.StringBuilder();
        var at = 0;
        foreach (Match skip in Skipped().Matches(html))
        {
            result.Append(Text(html[at..skip.Index], own, others));
            result.Append(skip.Value);
            at = skip.Index + skip.Length;
        }

        return result.Append(Text(html[at..], own, others)).ToString();
    }

    private static string Text(string text, IReadOnlySet<string> own, IReadOnlySet<string> others) =>
        // One pattern, so that what one alternative takes, no other looks inside.
        Code().Replace(text, m =>
        {
            var word = m.Groups["word"];
            if (!word.Success)
            {
                return $"<code>{m.Value}</code>";
            }

            var dotted = word.Value.Split('.')[0];
            var named = word.Value.Contains('.')
                ? others.Contains(dotted)
                : others.Contains(word.Value) || (own.Contains(word.Value) && (InnerCapital().IsMatch(word.Value) || !StartsSentence(text, m.Index)));
            return named ? $"<code>{m.Value}</code>" : m.Value;
        });

    // After a stop, or with nothing before it in this run of text. A tag's end leaves this run, so the
    // first word of a paragraph or a list item counts as a start, and so does the first after inline code.
    private static bool StartsSentence(string text, int index)
    {
        var before = text[..index].TrimEnd();
        return before.Length == 0 || before[^1] is '.' or '!' or '?' or ':' or ';';
    }

    [GeneratedRegex(@"<pre>.*?</pre>|<code>.*?</code>|<a\b.*?</a>|<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex Skipped();

    // In encoded text, so a quote is &quot;. A query's value stops at punctuation that ends the clause.
    [GeneratedRegex(@"(?<![\w.-])[A-Z]\w*=&quot;.*?&quot;|(?<![\w\]])\[[A-Z]\w*(?:\([^\]]*\))?\]|(?<![\w/])\?[a-z][\w-]*=(?:[\w.%-]+(?:,[\w.%-]+)*)?|(?<![\w.-])(?<word>[A-Z]\w*(?:\.[A-Z]\w*)*)(?![\w-])")]
    private static partial Regex Code();

    [GeneratedRegex(@"^[A-Z][a-z0-9]*[A-Z]")]
    private static partial Regex InnerCapital();
}
