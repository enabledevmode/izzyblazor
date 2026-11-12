namespace IzzyBlazor;

/// <summary>
/// One mention in text, such as <c>@ana</c> in "Thanks @ana!": the trigger it starts with, the name
/// after it, and where it starts. <see cref="Parse"/> finds them, as <see cref="Mentions{TItem}"/>
/// writes them and <see cref="MentionText"/> shows them.
/// </summary>
/// <param name="Prefix">The trigger, such as <c>@</c> or <c>#</c>.</param>
/// <param name="Text">The name after the trigger, such as <c>ana</c>.</param>
/// <param name="Index">Where the trigger starts in the text.</param>
public readonly record struct Mention(string Prefix, string Text, int Index)
{
    /// <summary>How long the mention is in the text, trigger and name.</summary>
    public int Length => Prefix.Length + Text.Length;

    /// <summary>
    /// The mentions in <paramref name="text"/>, in order: a trigger at the start or after
    /// whitespace, then the name up to the next whitespace, without punctuation that ends it
    /// ("Thanks @ana." mentions <c>ana</c>; "@j.doe" mentions <c>j.doe</c>). An email address isn't
    /// one: its <c>@</c> follows a letter. With no prefixes given, <c>@</c>.
    /// </summary>
    /// <example><c>Mention.Parse(Model.Bio).Count</c>, <c>Mention.Parse(note, "@", "#")</c></example>
    public static IReadOnlyList<Mention> Parse(string? text, params string[] prefixes)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        if (prefixes is not { Length: > 0 })
        {
            prefixes = ["@"];
        }

        // Longest first, so "@@" wins over "@".
        var ordered = prefixes.Where(p => !string.IsNullOrEmpty(p)).OrderByDescending(p => p.Length).ToArray();
        var found = new List<Mention>();
        var at = 0;
        while (at < text.Length)
        {
            if (at > 0 && !char.IsWhiteSpace(text[at - 1]))
            {
                at++;
                continue;
            }

            var prefix = Array.Find(ordered, p => string.CompareOrdinal(text, at, p, 0, p.Length) == 0);
            if (prefix is null)
            {
                at++;
                continue;
            }

            var start = at + prefix.Length;
            var end = start;
            while (end < text.Length && !char.IsWhiteSpace(text[end]))
            {
                end++;
            }

            var stop = end;
            while (stop > start && IsTrailing(text[stop - 1]))
            {
                stop--;
            }

            // A possessive: "@ana's PR" mentions ana.
            if (stop - start > 2 && text[stop - 1] is 's' or 'S' && text[stop - 2] is '\'' or '\u2019')
            {
                stop -= 2;
            }

            if (stop > start)
            {
                found.Add(new Mention(prefix, text[start..stop], at));
            }

            at = Math.Max(end, at + 1);
        }

        return found;
    }

    // Punctuation that ends a sentence or closes a quote or bracket, typographic and CJK too.
    private static bool IsTrailing(char c) => c is '.' or ',' or ';' or ':' or '!' or '?' or ')' or ']' or '}' or '"' or '\'' or '…'
        or '\u2019' or '\u201D' or '\u00BB' or '\u203A' or '\u3002' or '\uFF0C' or '\u3001' or '\uFF09' or '\u300D' or '\u300F' or '\uFF01' or '\uFF1F';
}

/// <summary>What <see cref="Mentions{TItem}"/>'s <c>Search</c> is asked for: a trigger and the text typed after it.</summary>
/// <param name="Prefix">The trigger typed, such as <c>@</c> or <c>#</c>.</param>
/// <param name="Text">What's typed after it so far; "" when the trigger was just typed.</param>
public sealed record MentionQuery(string Prefix, string Text);

/// <summary>Where <see cref="Mentions{TItem}"/>'s suggestions open, next to the line being typed.</summary>
public enum MentionsPlacement
{
    /// <summary>Below the line (the default).</summary>
    Bottom,

    /// <summary>Above the line, for a field near the bottom of the page, such as a chat box.</summary>
    Top,
}
