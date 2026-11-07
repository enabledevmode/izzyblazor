using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace IzzyBlazor;

/// <summary>Markdown's shared pieces: HTML tag patterns, escapes, entities, link labels and URLs.</summary>
internal static partial class MdSyntax
{
    private const string TagName = "[A-Za-z][A-Za-z0-9-]*";
    private const string AttributeName = "[a-zA-Z_:][a-zA-Z0-9:._-]*";
    private const string UnquotedValue = "[^\"'=<>`\\x00-\\x20]+";
    private const string SingleQuotedValue = "'[^']*'";
    private const string DoubleQuotedValue = "\"[^\"]*\"";
    private const string AttributeValue = "(?:" + UnquotedValue + "|" + SingleQuotedValue + "|" + DoubleQuotedValue + ")";
    private const string AttributeValueSpec = "(?:\\s*=\\s*" + AttributeValue + ")";
    private const string Attribute = "(?:\\s+" + AttributeName + AttributeValueSpec + "?)";
    public const string OpenTag = "<" + TagName + Attribute + "*\\s*/?>";
    public const string CloseTag = "</" + TagName + "\\s*[>]";
    private const string HtmlComment = "<!-->|<!--->|<!--[\\s\\S]*?-->";
    private const string ProcessingInstruction = "[<][?][\\s\\S]*?[?][>]";
    private const string Declaration = "<![A-Za-z]+[^>]*>";
    private const string Cdata = "<!\\[CDATA\\[[\\s\\S]*?\\]\\]>";
    public const string HtmlTag = "(?:" + OpenTag + "|" + CloseTag + "|" + HtmlComment + "|" + ProcessingInstruction + "|" + Declaration + "|" + Cdata + ")";

    public const string Entity = "&(?:#x[a-f0-9]{1,6}|#[0-9]{1,7}|[a-z][a-z0-9]{1,31});";

    [GeneratedRegex(@"\\[!""#$%&'()*+,./:;<=>?@[\\\]^_`{|}~-]|" + Entity, RegexOptions.IgnoreCase)]
    private static partial Regex EntityOrEscapedChar();

    [GeneratedRegex(@"[ \t\r\n]+")]
    private static partial Regex Whitespace();

    public static bool IsAsciiPunctuation(int c) => c is (>= '!' and <= '/') or (>= ':' and <= '@') or (>= '[' and <= '`') or (>= '{' and <= '~');

    /// <summary>Unicode punctuation as CommonMark 0.31 defines it: the P and S categories.</summary>
    public static bool IsPunctuation(char c) => IsAsciiPunctuation(c) || char.IsPunctuation(c) || char.IsSymbol(c);

    /// <summary>A tab, line feed, form feed, carriage return, or a Zs space.</summary>
    public static bool IsUnicodeWhitespace(char c) => c is '\t' or '\n' or '\f' or '\r' || char.GetUnicodeCategory(c) == UnicodeCategory.SpaceSeparator;

    /// <summary>Backslash escapes and entities replaced by the characters they stand for.</summary>
    public static string Unescape(string text) =>
        text.Contains('\\') || text.Contains('&')
            ? EntityOrEscapedChar().Replace(text, m => m.Value[0] == '\\' ? m.Value[1..] : DecodeEntity(m.Value))
            : text;

    /// <summary>An entity's character (a numeric one that isn't a valid character gives U+FFFD); an unknown name stays as written.</summary>
    public static string DecodeEntity(string entity)
    {
        if (entity.Length > 2 && entity[1] == '#')
        {
            var hex = entity[2] is 'x' or 'X';
            var digits = entity[(hex ? 3 : 2)..^1];
            if (!int.TryParse(digits, hex ? NumberStyles.HexNumber : NumberStyles.None, CultureInfo.InvariantCulture, out var code)
                || code == 0 || code > 0x10FFFF || code is >= 0xD800 and <= 0xDFFF)
            {
                return ((char)0xFFFD).ToString();
            }

            return char.ConvertFromUtf32(code);
        }

        return WebUtility.HtmlDecode(entity);
    }

    /// <summary>A link label as matched: brackets off, white space collapsed, case folded.</summary>
    public static string NormalizeReference(string label)
    {
        var inner = label[1..^1].Trim();
        inner = Whitespace().Replace(inner, " ");
        // Case folding, as JavaScript's toLowerCase().toUpperCase(): ß folds to SS.
        return inner.ToLowerInvariant().Replace("ß", "ss", StringComparison.Ordinal).ToUpperInvariant();
    }

    /// <summary>A URL percent-encoded where it has characters a URL can't (spaces, brackets, non-ASCII), as markdown-it's mdurl does; existing escapes are kept.</summary>
    public static string NormalizeUri(string uri)
    {
        var output = new StringBuilder(uri.Length);
        for (var i = 0; i < uri.Length; i++)
        {
            var c = uri[i];
            if (c == '%' && i + 2 < uri.Length && char.IsAsciiHexDigit(uri[i + 1]) && char.IsAsciiHexDigit(uri[i + 2]))
            {
                output.Append(uri, i, 3);
                i += 2;
            }
            else if (char.IsAsciiLetterOrDigit(c) || ";/?:@&=+$,-_.!~*'()#".Contains(c))
            {
                output.Append(c);
            }
            else
            {
                string chunk;
                if (char.IsHighSurrogate(c) && i + 1 < uri.Length && char.IsLowSurrogate(uri[i + 1]))
                {
                    chunk = uri.Substring(i, 2);
                    i++;
                }
                else
                {
                    chunk = char.IsSurrogate(c) ? ((char)0xFFFD).ToString() : c.ToString();
                }

                foreach (var b in Encoding.UTF8.GetBytes(chunk)) output.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return output.ToString();
    }

    /// <summary>Text for HTML: &amp;, &lt;, &gt; and &quot; escaped.</summary>
    public static void Escape(string text, StringBuilder output)
    {
        foreach (var c in text)
        {
            switch (c)
            {
                case '&': output.Append("&amp;"); break;
                case '<': output.Append("&lt;"); break;
                case '>': output.Append("&gt;"); break;
                case '"': output.Append("&quot;"); break;
                default: output.Append(c); break;
            }
        }
    }
}
