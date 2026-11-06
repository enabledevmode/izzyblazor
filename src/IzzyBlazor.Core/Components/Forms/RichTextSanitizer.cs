using System.Net;
using System.Text;
using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// Makes HTML safe to show: keeps the content a <see cref="RichTextEditor"/> can make and drops
/// everything else, such as scripts, event handlers, styles and <c>javascript:</c> links.
/// </summary>
/// <example>
/// <code>
/// var safe = RichTextSanitizer.Sanitize(htmlFromAnotherSystem);
/// </code>
/// </example>
/// <remarks>
/// <para>
/// It reads the HTML and writes a new document from what it allows, so nothing in the input
/// reaches the output as it was: every text and attribute is encoded again. It allows paragraphs,
/// headings from <c>topHeading</c> to 6 (a higher one moves down to <c>topHeading</c>), lists,
/// quotes, code blocks, rules, bold (<c>strong</c>), italic (<c>em</c>), underline,
/// strikethrough, subscript, superscript, inline code, links, line breaks, images and tables. <c>b</c>, <c>i</c>, <c>strike</c> and <c>del</c> become their
/// equivalents and a <c>div</c> becomes a paragraph. Other elements are dropped and their text
/// kept, except elements whose content is code or another document (<c>script</c>,
/// <c>style</c>, <c>template</c>, <c>iframe</c>, <c>object</c>, <c>svg</c>, <c>math</c> and
/// the like), which go with their content.
/// </para>
/// <para>
/// Attributes: <c>href</c> (http, https, mailto, tel or relative) and <c>target="_blank"</c> on
/// links, which then get <c>rel="noopener noreferrer"</c>; <c>src</c> (http, https or relative),
/// <c>alt</c>, <c>width</c> and <c>height</c> on images; <c>start</c> on numbered lists;
/// <c>colspan</c>, <c>rowspan</c> and <c>scope</c> on table cells. Classes only from the editor's
/// own list: alignment (<c>ib-align-center</c>, <c>ib-align-end</c>) on blocks; text color
/// (<c>ib-color-</c>), highlight (<c>ib-highlight-</c>) and size (<c>ib-size-sm</c>,
/// <c>ib-size-lg</c>, <c>ib-size-xl</c>) on a <c>span</c>, the colors being red, orange, yellow,
/// green, blue, purple and gray. One style: <c>font-family</c> on a <c>span</c>, when its value is
/// exactly one of <c>fonts</c>. No other <c>style</c>, <c>class</c>, <c>id</c> or event handler
/// gets through.
/// </para>
/// <para>
/// <see cref="RichTextEditor"/> runs it on every value it binds, and <see cref="RichText"/> on
/// every value it shows. Call it yourself on HTML from elsewhere before you show it with a
/// <see cref="MarkupString"/>.
/// </para>
/// </remarks>
/// <seealso cref="RichText"/>
/// <seealso cref="RichTextEditor"/>
/// 
public static class RichTextSanitizer
{
    private const int MaxDepth = 64;

    // Allowed elements and the name each is written as.
    private static readonly Dictionary<string, string> Renames = new(StringComparer.Ordinal)
    {
        ["p"] = "p", ["div"] = "p",
        ["h1"] = "h1", ["h2"] = "h2", ["h3"] = "h3", ["h4"] = "h4", ["h5"] = "h5", ["h6"] = "h6",
        ["ul"] = "ul", ["ol"] = "ol", ["li"] = "li",
        ["blockquote"] = "blockquote", ["pre"] = "pre", ["hr"] = "hr",
        ["strong"] = "strong", ["b"] = "strong", ["em"] = "em", ["i"] = "em",
        ["u"] = "u", ["s"] = "s", ["strike"] = "s", ["del"] = "s",
        ["code"] = "code", ["a"] = "a", ["br"] = "br", ["img"] = "img",
        ["span"] = "span", ["font"] = "span", ["sub"] = "sub", ["sup"] = "sup",
        ["table"] = "table", ["thead"] = "thead", ["tbody"] = "tbody",
        ["tr"] = "tr", ["th"] = "th", ["td"] = "td",
    };

    // Dropped along with everything inside them.
    private static readonly HashSet<string> DropWithContent = new(StringComparer.Ordinal)
    {
        "script", "style", "template", "iframe", "object", "embed", "svg", "math", "noscript",
        "textarea", "title", "xmp", "noembed", "noframes", "plaintext", "select", "head", "frameset",
    };

    // Elements the tokenizer reads as raw text: their content is not markup.
    private static readonly HashSet<string> RawText = new(StringComparer.Ordinal)
    {
        "script", "style", "textarea", "title", "xmp", "iframe", "noembed", "noframes", "noscript",
    };

    private static readonly HashSet<string> Void = new(StringComparer.Ordinal) { "br", "hr", "img" };

    private static readonly HashSet<string> Inline = new(StringComparer.Ordinal)
    {
        "strong", "em", "u", "s", "code", "a", "span", "sub", "sup",
    };

    private static readonly HashSet<string> Headings = new(StringComparer.Ordinal) { "h1", "h2", "h3", "h4", "h5", "h6" };

    // The editor's own classes: alignment on blocks, color, highlight and size on a span.
    private static readonly string[] Palette = ["red", "orange", "yellow", "green", "blue", "purple", "gray"];

    private static readonly HashSet<string> BlockClasses = new(StringComparer.Ordinal) { "ib-align-center", "ib-align-end" };

    private static readonly HashSet<string> SpanClasses = new(
        Palette.Select(c => "ib-color-" + c).Concat(Palette.Select(c => "ib-highlight-" + c)).Concat(["ib-size-sm", "ib-size-lg", "ib-size-xl"]),
        StringComparer.Ordinal);

    // Elements that hold blocks: opening a block closes everything open up to one of these.
    private static readonly HashSet<string> Containers = new(StringComparer.Ordinal)
    {
        "li", "blockquote", "td", "th",
    };

    // Elements kept when empty.
    private static readonly HashSet<string> KeepEmpty = new(StringComparer.Ordinal) { "li", "td", "th" };

    // Elements whose text children are dropped (a browser would move the text out of them).
    private static readonly HashSet<string> NoText = new(StringComparer.Ordinal)
    {
        "ul", "ol", "table", "thead", "tbody", "tr",
    };

    private static readonly string[] ImageTypes = ["image/png", "image/jpeg", "image/gif", "image/webp", "image/avif"];

    /// <summary>Returns <paramref name="html"/> with only the allowed elements and attributes, encoded again.</summary>
    /// <param name="html">The HTML to clean. <see langword="null"/> gives an empty string.</param>
    /// <param name="topHeading">The highest heading level kept, 1 to 6. Higher headings move down to it. Defaults to 1.</param>
    /// <param name="fonts">The <c>font-family</c> values a <c>span</c> may set, each exactly as written, such as <c>Georgia, serif</c>. None by default.</param>
    /// <param name="allowDataImages">Keep images whose <c>src</c> is a <c>data:</c> URL of a PNG, JPEG, GIF, WebP or AVIF image (never SVG).</param>
    public static string Sanitize(string? html, int topHeading = 1, IReadOnlyList<string>? fonts = null, bool allowDataImages = false)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        var root = new Node(null);
        new Builder(root, allowDataImages, Math.Clamp(topHeading, 1, 6), fonts ?? []).Run(html);
        var output = new StringBuilder(html.Length);
        foreach (var child in root.Children) Write(child, output);
        return output.ToString();
    }

    private sealed class Node(string? name)
    {
        public string? Name { get; } = name;

        // An element that was dropped but kept its content: open, so its end tag closes it, never written.
        public bool Unwrapped { get; init; }
        public string? Text { get; init; }
        public List<(string Name, string Value)> Attributes { get; } = [];
        public List<Node> Children { get; } = [];
        public Node? Parent { get; set; }
    }

    private sealed class Builder(Node root, bool allowDataImages, int topHeading, IReadOnlyList<string> fonts)
    {
        private readonly List<Node> _open = [root];
        private Node Current => _open.FindLast(n => !n.Unwrapped)!;

        // The name an element is written as: a heading above the top one moves down to it.
        private string? Map(string tag) =>
            !Renames.TryGetValue(tag, out var name) ? null
            : Headings.Contains(name) && name[1] - '0' < topHeading ? "h" + topHeading
            : name;

        public void Run(string html)
        {
            var i = 0;
            var text = new StringBuilder();
            while (i < html.Length)
            {
                var c = html[i];
                if (c != '<' || i + 1 >= html.Length)
                {
                    text.Append(c);
                    i++;
                    continue;
                }

                var next = html[i + 1];
                if (IsAsciiLetter(next))
                {
                    Flush(text);
                    i = ReadTag(html, i + 1, end: false);
                }
                else if (next == '/' && i + 2 < html.Length && IsAsciiLetter(html[i + 2]))
                {
                    Flush(text);
                    i = ReadTag(html, i + 2, end: true);
                }
                else if (string.CompareOrdinal(html, i, "<!--", 0, 4) == 0)
                {
                    Flush(text);
                    i = SkipComment(html, i + 4);
                }
                else if (next is '!' or '?' or '/')
                {
                    // A doctype, processing instruction or bogus end tag: skipped up to '>'.
                    Flush(text);
                    var close = html.IndexOf('>', i + 2);
                    i = close < 0 ? html.Length : close + 1;
                }
                else
                {
                    text.Append(c);
                    i++;
                }
            }

            Flush(text);
        }

        private static int SkipComment(string html, int start)
        {
            // "<!-->" and "<!--->" end at once, as in a browser.
            if (start < html.Length && html[start] == '>') return start + 1;
            if (start + 1 < html.Length && html[start] == '-' && html[start + 1] == '>') return start + 2;
            for (var j = start; j < html.Length; j++)
            {
                if (html[j] != '-' || j + 2 >= html.Length || html[j + 1] != '-') continue;
                if (html[j + 2] == '>') return j + 3;
                if (j + 3 < html.Length && html[j + 2] == '!' && html[j + 3] == '>') return j + 4;
            }

            return html.Length;
        }

        private int ReadTag(string html, int i, bool end)
        {
            var nameStart = i;
            while (i < html.Length && !IsSpace(html[i]) && html[i] != '/' && html[i] != '>') i++;
            var name = html[nameStart..i].ToLowerInvariant();
            var attributes = new List<(string, string)>();

            while (i < html.Length && html[i] != '>')
            {
                if (IsSpace(html[i]) || html[i] == '/') { i++; continue; }

                var attrStart = i;
                i++; // An attribute name may start with '='.
                while (i < html.Length && !IsSpace(html[i]) && html[i] is not ('/' or '>' or '=')) i++;
                var attrName = html[attrStart..i].ToLowerInvariant();
                while (i < html.Length && IsSpace(html[i])) i++;

                var value = string.Empty;
                if (i < html.Length && html[i] == '=')
                {
                    i++;
                    while (i < html.Length && IsSpace(html[i])) i++;
                    if (i < html.Length && html[i] is '"' or '\'')
                    {
                        var quote = html[i];
                        var close = html.IndexOf(quote, i + 1);
                        if (close < 0) close = html.Length;
                        value = html[(i + 1)..close];
                        i = Math.Min(close + 1, html.Length);
                    }
                    else
                    {
                        var valueStart = i;
                        while (i < html.Length && !IsSpace(html[i]) && html[i] != '>') i++;
                        value = html[valueStart..i];
                    }
                }

                attributes.Add((attrName, Decode(value)));
            }

            i = Math.Min(i + 1, html.Length);

            if (end)
            {
                EndTag(name);
                return i;
            }

            if (name == "plaintext") return html.Length;
            if (RawText.Contains(name)) return SkipRawText(html, i, name);
            if (DropWithContent.Contains(name)) return SkipElement(html, i, name);
            StartTag(name, attributes);
            return i;
        }

        // Skips to just past the matching end tag, counting nested ones of the same name.
        private static int SkipElement(string html, int i, string name)
        {
            var depth = 1;
            while (i < html.Length)
            {
                var lt = html.IndexOf('<', i);
                if (lt < 0) return html.Length;
                var isEnd = lt + 1 < html.Length && html[lt + 1] == '/';
                var start = lt + (isEnd ? 2 : 1);
                if (MatchesTagName(html, start, name))
                {
                    depth += isEnd ? -1 : 1;
                    var close = html.IndexOf('>', start);
                    i = close < 0 ? html.Length : close + 1;
                    if (depth == 0) return i;
                }
                else
                {
                    i = lt + 1;
                }
            }

            return html.Length;
        }

        private static int SkipRawText(string html, int i, string name)
        {
            while (i < html.Length)
            {
                var lt = html.IndexOf("</", i, StringComparison.Ordinal);
                if (lt < 0) return html.Length;
                if (MatchesTagName(html, lt + 2, name))
                {
                    var close = html.IndexOf('>', lt + 2);
                    return close < 0 ? html.Length : close + 1;
                }

                i = lt + 2;
            }

            return html.Length;
        }

        private static bool MatchesTagName(string html, int start, string name)
        {
            if (start + name.Length > html.Length) return false;
            if (string.Compare(html, start, name, 0, name.Length, StringComparison.OrdinalIgnoreCase) != 0) return false;
            var after = start + name.Length;
            return after == html.Length || IsSpace(html[after]) || html[after] is '/' or '>';
        }

        private void Flush(StringBuilder text)
        {
            if (text.Length == 0) return;
            var value = Decode(text.ToString());
            text.Clear();
            if (value.Length == 0) return;
            if (NoText.Contains(Current.Name ?? "")) return;
            Append(new Node(null) { Text = value });
        }

        private void StartTag(string tag, List<(string Name, string Value)> attributes)
        {
            if (Map(tag) is not { } name) return;
            if (_open.Count > MaxDepth && !Void.Contains(name)) return;

            if (!Inline.Contains(name) && name is not ("br" or "img"))
            {
                if (!PlaceBlock(name)) return;
            }
            else if (name == "a" && _open.Exists(n => n.Name == "a"))
            {
                CloseTo("a");
            }

            if (NoText.Contains(Current.Name ?? "") && name is not ("li" or "thead" or "tbody" or "tr" or "td" or "th"))
            {
                // Inline content straight in a list or table: a browser moves it out; drop the tag.
                return;
            }

            var node = new Node(name);
            AddAttributes(node, attributes);
            if (name is "a" && !node.Attributes.Exists(a => a.Name == "href")) return;
            if (name is "img" && !node.Attributes.Exists(a => a.Name == "src")) return;
            if (name is "span" && node.Attributes.Count == 0)
            {
                // A span with nothing kept is only its content; it stays open so its end tag
                // doesn't close a span around it.
                _open.Add(new Node("span") { Unwrapped = true });
                return;
            }
            Append(node);
            if (!Void.Contains(name)) _open.Add(node);
        }

        // Closes what a new block can't sit in. Returns false when the block has no valid place.
        private bool PlaceBlock(string name)
        {
            switch (name)
            {
                case "li":
                    return CloseUntilParent("ul", "ol");
                case "thead" or "tbody":
                    return CloseUntilParent("table");
                case "tr":
                    return CloseUntilParent("table", "thead", "tbody");
                case "td" or "th":
                    return CloseUntilParent("tr");
            }

            // Any other block closes open paragraphs, headings and inline elements up to its container.
            while (_open.Count > 1)
            {
                var top = Current.Name!;
                if (Containers.Contains(top) || top is "ul" or "ol" or "table" or "thead" or "tbody" or "tr") break;
                _open.RemoveAt(_open.Count - 1);
            }

            // A browser's indent puts a list straight in a list: it goes in the item before it.
            if (name is "ul" or "ol" && Current.Name is "ul" or "ol" && Current.Children.LastOrDefault(c => c.Name == "li") is { } item)
            {
                _open.Add(item);
                return true;
            }

            // Other blocks can't sit straight in a list or table row; they'd be moved by a browser.
            return Current.Name is not ("ul" or "ol" or "table" or "thead" or "tbody" or "tr");
        }

        // Closes everything above the nearest open element named in parents; false when none is open.
        private bool CloseUntilParent(params string[] parents)
        {
            var j = _open.FindLastIndex(n => Array.IndexOf(parents, n.Name) >= 0);
            if (j <= 0) return false;
            _open.RemoveRange(j + 1, _open.Count - j - 1);
            return true;
        }

        private void CloseTo(string name)
        {
            var j = _open.FindLastIndex(n => n.Name == name);
            if (j > 0) _open.RemoveRange(j, _open.Count - j);
        }

        private void EndTag(string tag)
        {
            if (Map(tag) is not { } name || Void.Contains(name)) return;
            var j = _open.FindLastIndex(n => n.Name == name);
            if (j <= 0) return;

            // A text-level end tag (bold, a paragraph) doesn't reach out of a list item, quote or cell.
            if (Inline.Contains(name) || Headings.Contains(name) || name is "p" or "pre")
            {
                for (var k = _open.Count - 1; k > j; k--)
                {
                    if (Containers.Contains(_open[k].Name!)) return;
                }
            }

            _open.RemoveRange(j, _open.Count - j);
        }

        private void Append(Node node)
        {
            node.Parent = Current;
            Current.Children.Add(node);
        }

        private void AddAttributes(Node node, List<(string Name, string Value)> attributes)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (name, value) in attributes)
            {
                if (!seen.Add(name)) continue; // A browser keeps the first of a repeated attribute.
                var kept = (node.Name, name) switch
                {
                    ("a", "href") => CleanUrl(value, image: false),
                    ("a", "target") => value.Trim().Equals("_blank", StringComparison.OrdinalIgnoreCase) ? "_blank" : null,
                    ("img", "src") => CleanUrl(value, image: true),
                    ("img", "alt") => value,
                    ("img", "width" or "height") => Number(value, 1, 10000),
                    ("ol", "start") => Number(value, 0, 100000),
                    ("td" or "th", "colspan") => Number(value, 1, 1000),
                    ("td" or "th", "rowspan") => Number(value, 0, 65534),
                    ("th", "scope") => value.Trim().ToLowerInvariant() is var s && s is "row" or "col" or "rowgroup" or "colgroup" ? s : null,
                    ("span", "class") => Classes(value, SpanClasses),
                    ("span", "style") => Font(value),
                    ("p" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "li" or "blockquote" or "td" or "th", "class") => Classes(value, BlockClasses),
                    _ => null,
                };
                if (kept is not null) node.Attributes.Add((name, kept));
            }

            if (node.Name == "a" && node.Attributes.Exists(a => a.Name == "target"))
            {
                node.Attributes.Add(("rel", "noopener noreferrer"));
            }
        }

        // The classes from the editor's list, in their order; null when none is left.
        private static string? Classes(string value, HashSet<string> allowed)
        {
            var kept = value.Split([' ', '\t', '\n', '\r', '\f'], StringSplitOptions.RemoveEmptyEntries).Where(allowed.Contains).Distinct().ToList();
            return kept.Count == 0 ? null : string.Join(' ', kept);
        }

        // "font-family: X" when X is exactly one of the app's fonts, written as the app wrote it.
        private string? Font(string value)
        {
            var style = value.Trim().TrimEnd(';').Trim();
            const string property = "font-family:";
            if (!style.StartsWith(property, StringComparison.OrdinalIgnoreCase)) return null;
            var family = style[property.Length..].Trim();
            var match = fonts.FirstOrDefault(f => string.Equals(f.Trim(), family, StringComparison.OrdinalIgnoreCase));
            return match is null ? null : "font-family: " + match.Trim();
        }

        private string? CleanUrl(string value, bool image)
        {
            // As a browser's URL parser: leading and trailing C0 controls and spaces go, and tabs
            // and newlines anywhere are ignored, so they can't hide a scheme.
            var url = new StringBuilder(value.Length);
            foreach (var ch in value)
            {
                if (ch < ' ' || ch == '\u007F' || (ch == ' ' && url.Length == 0)) continue;
                url.Append(ch);
            }

            while (url.Length > 0 && url[^1] == ' ') url.Length--;

            var cleaned = url.ToString();
            if (cleaned.Length == 0) return null;

            var colon = cleaned.IndexOf(':');
            var boundary = cleaned.IndexOfAny(['/', '?', '#']);
            if (colon < 0 || (boundary >= 0 && boundary < colon)) return cleaned; // Relative.

            var scheme = cleaned[..colon].ToLowerInvariant();
            if (scheme is "http" or "https") return cleaned;
            if (!image && scheme is "mailto" or "tel") return cleaned;
            if (image && allowDataImages && scheme == "data")
            {
                var rest = cleaned[(colon + 1)..];
                foreach (var type in ImageTypes)
                {
                    if (rest.StartsWith(type + ";", StringComparison.OrdinalIgnoreCase) ||
                        rest.StartsWith(type + ",", StringComparison.OrdinalIgnoreCase)) return cleaned;
                }
            }

            return null;
        }

        private static string? Number(string value, int min, int max) =>
            int.TryParse(value.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n) && n >= min && n <= max
                ? n.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : null;
    }

    private static void Write(Node node, StringBuilder output)
    {
        if (node.Name is null)
        {
            Encode(node.Text!, output, attribute: false);
            return;
        }

        if (node.Children.Count == 0 && !Void.Contains(node.Name) && !KeepEmpty.Contains(node.Name)) return;

        output.Append('<').Append(node.Name);
        foreach (var (name, value) in node.Attributes)
        {
            output.Append(' ').Append(name).Append("=\"");
            Encode(value, output, attribute: true);
            output.Append('"');
        }

        output.Append('>');
        if (Void.Contains(node.Name)) return;
        foreach (var child in node.Children) Write(child, output);
        output.Append("</").Append(node.Name).Append('>');
    }

    private static void Encode(string value, StringBuilder output, bool attribute)
    {
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '&': output.Append("&amp;"); break;
                case '<': output.Append("&lt;"); break;
                case '>': output.Append("&gt;"); break;
                case '"' when attribute: output.Append("&quot;"); break;
                case '\0': break;
                default: output.Append(ch); break;
            }
        }
    }

    private static string Decode(string value) =>
        value.Contains('&') ? WebUtility.HtmlDecode(value) : value;

    private static bool IsAsciiLetter(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

    private static bool IsSpace(char c) => c is ' ' or '\t' or '\n' or '\r' or '\f';
}
