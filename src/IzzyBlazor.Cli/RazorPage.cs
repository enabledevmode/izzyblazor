using System.Text.RegularExpressions;

namespace IzzyBlazor.Cli;

/// <summary>An attribute of a tag, with where its name and its value are in the text.</summary>
/// <param name="Name">As written: <c>Variant</c>, <c>@bind-Value</c>.</param>
/// <param name="Start">Where the name starts.</param>
/// <param name="End">Just past the attribute: its closing quote, or its name when it has no value.</param>
/// <param name="ValueStart">Where the value starts, inside its quotes; -1 with no value.</param>
/// <param name="ValueLength">The value's length, without its quotes.</param>
/// <param name="Quote">The quote around the value, or '\0' for a bare one.</param>
sealed record RazorAttribute(string Name, int Start, int End, int ValueStart, int ValueLength, char Quote)
{
    public bool HasValue => ValueStart >= 0;
}

/// <summary>An opening tag.</summary>
/// <param name="Name">The tag's name.</param>
/// <param name="Start">Where its <c>&lt;</c> is.</param>
/// <param name="End">Just past its <c>&gt;</c>.</param>
/// <param name="NameEnd">Just past its name, where a first attribute goes.</param>
/// <param name="SelfClosing">Whether it ends <c>/&gt;</c>.</param>
sealed record RazorTag(string Name, int Start, int End, int NameEnd, bool SelfClosing, IReadOnlyList<RazorAttribute> Attributes);

/// <summary>
/// A .razor file read as far as izzy needs: its opening tags with their attributes, its <c>@code</c> blocks
/// and its directives. It isn't the Razor compiler: it finds what's written plainly and leaves the rest alone.
/// </summary>
static partial class RazorPage
{
    /// <summary>The <c>@code</c> blocks: where each starts, and where its braces are.</summary>
    public static List<(int Start, int Open, int Close)> CodeBlocks(string text)
    {
        var blocks = new List<(int, int, int)>();
        foreach (Match m in CodeStart().Matches(text))
        {
            if (blocks.Count > 0 && m.Index < blocks[^1].Item3) continue;
            var open = m.Index + m.Length - 1;
            var close = MatchBrace(text, open);
            if (close < 0) break;
            blocks.Add((m.Index, open, close));
        }
        return blocks;
    }

    /// <summary>The index of the brace that closes the one at <paramref name="open"/>, past strings and comments; -1 when there is none.</summary>
    public static int MatchBrace(string text, int open)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                i = text.IndexOf('\n', i) is var end and >= 0 ? end : text.Length;
            }
            else if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                i = text.IndexOf("*/", i + 2, StringComparison.Ordinal) is var end and >= 0 ? end + 1 : text.Length;
            }
            else if (c == '@' && i + 1 < text.Length && text[i + 1] == '*')
            {
                i = text.IndexOf("*@", i + 2, StringComparison.Ordinal) is var end and >= 0 ? end + 1 : text.Length;
            }
            else if (c == '"')
            {
                i = StringEnd(text, i);
            }
            else if (c == '\'' && CharEnd(text, i) is var charEnd and > 0)
            {
                i = charEnd;
            }
            else if (c == '{') depth++;
            else if (c == '}' && --depth == 0) return i;
        }
        return -1;
    }

    // The last character of the string starting at the quote: plain, verbatim (@"") or raw (""").
    static int StringEnd(string text, int quote)
    {
        if (text.AsSpan(quote).StartsWith("\"\"\""))
        {
            var count = 3;
            while (quote + count < text.Length && text[quote + count] == '"') count++;
            var end = text.IndexOf(new string('"', count), quote + count, StringComparison.Ordinal);
            return end < 0 ? text.Length : end + count - 1;
        }
        var verbatim = quote > 0 && (text[quote - 1] == '@' || (quote > 1 && text[quote - 1] == '$' && text[quote - 2] == '@'));
        for (var i = quote + 1; i < text.Length; i++)
        {
            if (verbatim)
            {
                if (text[i] != '"') continue;
                if (i + 1 < text.Length && text[i + 1] == '"') i++;
                else return i;
            }
            else if (text[i] == '\\') i++;
            else if (text[i] is '"' or '\n') return i;
        }
        return text.Length;
    }

    // 'a' or '\n': an apostrophe in markup text (It's) is not a character.
    static int CharEnd(string text, int quote) =>
        quote + 2 < text.Length && text[quote + 1] != '\\' && text[quote + 2] == '\'' ? quote + 2
        : quote + 3 < text.Length && text[quote + 1] == '\\' && text[quote + 3] == '\'' ? quote + 3
        : -1;

    /// <summary>Every opening tag outside the <c>@code</c> blocks and comments, in order.</summary>
    public static List<RazorTag> Tags(string text)
    {
        var code = CodeBlocks(text);
        var tags = new List<RazorTag>();
        for (var i = 0; i < text.Length; i++)
        {
            if (code.FirstOrDefault(b => i >= b.Open && i <= b.Close) is { Close: > 0 } block)
            {
                i = block.Close;
                continue;
            }
            if (text[i] == '@' && i + 1 < text.Length && text[i + 1] == '*')
            {
                i = text.IndexOf("*@", i + 2, StringComparison.Ordinal) is var end and >= 0 ? end + 1 : text.Length;
                continue;
            }
            if (text[i] != '<') continue;
            if (text.AsSpan(i).StartsWith("<!--"))
            {
                i = text.IndexOf("-->", i + 4, StringComparison.Ordinal) is var end and >= 0 ? end + 2 : text.Length;
                continue;
            }
            if (i + 1 >= text.Length || !char.IsAsciiLetter(text[i + 1])) continue;
            if (Tag(text, i) is { } tag)
            {
                tags.Add(tag);
                i = tag.End - 1;
            }
        }
        return tags;
    }

    static RazorTag? Tag(string text, int start)
    {
        var i = start + 1;
        while (i < text.Length && (char.IsAsciiLetterOrDigit(text[i]) || text[i] is '.' or '-' or '_' or ':')) i++;
        var name = text[(start + 1)..i];
        var nameEnd = i;
        var attributes = new List<RazorAttribute>();
        while (i < text.Length)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            if (i >= text.Length) return null;
            if (text[i] == '>') return new RazorTag(name, start, i + 1, nameEnd, false, attributes);
            if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '>') return new RazorTag(name, start, i + 2, nameEnd, true, attributes);
            // Not a tag after all (a < b in an expression), or something this doesn't read: leave it.
            if (!(char.IsAsciiLetter(text[i]) || text[i] is '@' or '_')) return null;
            var attributeStart = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] is not ('=' or '>' or '/')) i++;
            var attribute = text[attributeStart..i];
            var afterName = i;
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            if (i >= text.Length || text[i] != '=')
            {
                attributes.Add(new RazorAttribute(attribute, attributeStart, afterName, -1, 0, '\0'));
                i = afterName;
                continue;
            }
            i++;
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            if (i >= text.Length) return null;
            if (text[i] is '"' or '\'')
            {
                var quote = text[i];
                var end = ValueEnd(text, i + 1, quote);
                if (end < 0) return null;
                attributes.Add(new RazorAttribute(attribute, attributeStart, end + 1, i + 1, end - i - 1, quote));
                i = end + 1;
            }
            else
            {
                var valueStart = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != '>' && !(text[i] == '/' && i + 1 < text.Length && text[i + 1] == '>')) i++;
                attributes.Add(new RazorAttribute(attribute, attributeStart, i, valueStart, i - valueStart, '\0'));
            }
        }
        return null;
    }

    // The closing quote of a value: one inside @( ... ) is the expression's own.
    static int ValueEnd(string text, int from, char quote)
    {
        var parens = 0;
        for (var i = from; i < text.Length; i++)
        {
            if (text[i] == '(' && (parens > 0 || (i > from - 1 && i > 0 && text[i - 1] == '@'))) parens++;
            else if (text[i] == ')' && parens > 0) parens--;
            else if (text[i] == quote && parens == 0) return i;
        }
        return -1;
    }

    /// <summary>The offset of a line and column (both from 1); a column past the line's end is its end.</summary>
    public static int Offset(string text, string path, int line, int column)
    {
        var offset = 0;
        for (var at = 1; at < line; at++)
        {
            var newline = text.IndexOf('\n', offset);
            if (newline < 0) throw new UsageException($"{path} has no line {line}.");
            offset = newline + 1;
        }
        var end = text.IndexOf('\n', offset) is var found and >= 0 ? found : text.Length;
        if (end > offset && text[end - 1] == '\r') end--;
        return Math.Min(offset + column - 1, end);
    }

    [GeneratedRegex(@"^[ \t]*@code\s*\{", RegexOptions.Multiline)]
    private static partial Regex CodeStart();
}
