using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace IzzyBlazor.Site.Services.Mcp;

/// <summary>
/// HTML to Markdown for assistants: doc comments from the API metadata and the site's rendered
/// articles. Only what the site writes: headings, paragraphs, lists, code, links, tables, alerts.
/// A <c>cref:</c> link keeps its text (the assistant asks for the member by name); a link into the
/// site becomes absolute; an example's preview becomes its source. Controls, icons and text only
/// screen readers get are left out.
/// </summary>
public static partial class Markdown
{
    private static readonly HashSet<string> Void = ["area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "source", "track", "wbr"];
    private static readonly HashSet<string> Inline = ["a", "abbr", "b", "br", "code", "em", "i", "kbd", "label", "mark", "s", "small", "span", "strong", "sub", "sup", "time", "u"];
    private static readonly HashSet<string> Skipped = ["button", "fieldset", "form", "iframe", "input", "nav", "select", "svg", "template", "textarea"];

    /// <summary>Markdown for HTML. <paramref name="example"/> gives an example's Markdown by its id (a section's <c>data-ex</c>).</summary>
    public static string FromHtml(string? html, Func<string, string, string?>? example = null)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return "";
        }

        var root = Parse(html);
        return string.Join("\n\n", new Writer(example).Blocks(root.Children)).Trim();
    }

    /// <summary>A fenced code block, with a fence longer than any run of backticks in the code.</summary>
    public static string Fence(string code, string? language = null)
    {
        var fence = new string('`', Math.Max(3, LongestRun(code, '`') + 1));
        return $"{fence}{language}\n{code.TrimEnd('\n')}\n{fence}";
    }

    /// <summary>Inline code.</summary>
    public static string Code(string text)
    {
        var ticks = new string('`', LongestRun(text, '`') + 1);
        var pad = text.StartsWith('`') || text.EndsWith('`') ? " " : "";
        return $"{ticks}{pad}{text}{pad}{ticks}";
    }

    /// <summary>A link, its text's brackets escaped ("A form posts to [object RadioNodeList]").</summary>
    public static string Link(string text, string url) => $"[{text.Replace("[", @"\[").Replace("]", @"\]")}]({url})";

    /// <summary>The first sentence of a doc comment, as Markdown on one line.</summary>
    public static string? FirstSentence(string? html) =>
        OverviewText.Sentence(Whitespace().Replace(FromHtml(html), " "), int.MaxValue);

    /// <summary>A table cell's text: one line, pipes escaped.</summary>
    public static string Cell(string text) => Whitespace().Replace(text, " ").Replace("|", "\\|").Trim();

    /// <summary>A link into the site, made absolute on izzyblazor.com; null for a link to the same page.</summary>
    public static string? Absolute(string href)
    {
        // A fragment or query alone ("?mode=off") is the page itself.
        if (href.Length == 0 || href[0] is '#' or '?')
        {
            return null;
        }

        if (href.StartsWith("//", StringComparison.Ordinal))
        {
            return "https:" + href;
        }

        if (href.Contains("://", StringComparison.Ordinal) || href.StartsWith("mailto:", StringComparison.Ordinal))
        {
            return href;
        }

        // Spaces and parentheses would end a Markdown link's address.
        return Sitemap.Origin + href.TrimStart('.', '/').Replace(" ", "%20").Replace("(", "%28").Replace(")", "%29");
    }

    private static int LongestRun(string text, char c)
    {
        int longest = 0, run = 0;
        foreach (var ch in text)
        {
            run = ch == c ? run + 1 : 0;
            longest = Math.Max(longest, run);
        }

        return longest;
    }

    // ---- Parsing: Blazor writes well-formed HTML, so a stack of open elements is enough. ----

    private sealed class Node(string tag, Dictionary<string, string> attributes)
    {
        public string Tag { get; } = tag;
        public Dictionary<string, string> Attributes { get; } = attributes;
        public List<object> Children { get; } = [];

        public string? this[string name] => Attributes.GetValueOrDefault(name);

        public bool HasClass(string name) => this["class"] is { } classes && classes.Split(' ').Contains(name);
    }

    // Deeper elements are read as siblings, so a pathological page can't overflow the stack.
    private const int MaxDepth = 200;

    private static Node Parse(string html)
    {
        var root = new Node("#root", []);
        var stack = new Stack<Node>();
        stack.Push(root);
        var source = StyleBlock().Replace(ScriptBlock().Replace(html, ""), "");
        var at = 0;
        foreach (Match match in TagOrComment().Matches(source))
        {
            if (match.Index > at)
            {
                stack.Peek().Children.Add(WebUtility.HtmlDecode(source[at..match.Index]));
            }

            at = match.Index + match.Length;
            if (match.Value.StartsWith("<!", StringComparison.Ordinal))
            {
                continue;
            }

            var tag = match.Groups["tag"].Value.ToLowerInvariant();
            if (match.Groups["close"].Success)
            {
                if (stack.Any(n => n.Tag == tag))
                {
                    while (stack.Pop().Tag != tag)
                    {
                    }
                }

                continue;
            }

            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match attribute in Attribute().Matches(match.Groups["attrs"].Value))
            {
                var value = attribute.Groups["v1"].Success ? attribute.Groups["v1"].Value
                    : attribute.Groups["v2"].Success ? attribute.Groups["v2"].Value
                    : attribute.Groups["v3"].Value;
                attributes[attribute.Groups["name"].Value] = WebUtility.HtmlDecode(value);
            }

            var node = new Node(tag, attributes);
            stack.Peek().Children.Add(node);
            if (!Void.Contains(tag) && !match.Groups["self"].Success && stack.Count < MaxDepth)
            {
                stack.Push(node);
            }
        }

        if (at < source.Length)
        {
            stack.Peek().Children.Add(WebUtility.HtmlDecode(source[at..]));
        }

        return root;
    }

    // ---- Writing ----

    private sealed class Writer(Func<string, string, string?>? example)
    {
        /// <summary>A container's children as Markdown blocks: runs of inline content become paragraphs.</summary>
        public List<string> Blocks(IEnumerable<object> children)
        {
            var blocks = new List<string>();
            var paragraph = new StringBuilder();

            void Flush()
            {
                var text = Tidy(paragraph.ToString());
                if (text.Length > 0)
                {
                    blocks.Add(text);
                }

                paragraph.Clear();
            }

            foreach (var child in children)
            {
                if (child is string text)
                {
                    paragraph.Append(Prose(text));
                }
                else if (child is Node node && !Hidden(node))
                {
                    if (IsInline(node))
                    {
                        paragraph.Append(InlineOf(node));
                    }
                    else
                    {
                        Flush();
                        blocks.AddRange(Block(node));
                    }
                }
            }

            Flush();
            return blocks;
        }

        // Text that reads as a tag ("a <div> per pair") stays text in Markdown.
        private static string Prose(string text) => TagLike().Replace(Whitespace().Replace(text, " "), @"\<");

        private static bool IsInline(Node node) => Inline.Contains(node.Tag) && !node.HasClass("ib-tabs");

        private static bool Hidden(Node node) =>
            Skipped.Contains(node.Tag) || node["aria-hidden"] == "true" || node.HasClass("ib-visually-hidden")
            || node.HasClass("ib-code-bar") || node.HasClass("ib-tab-panel-label") || node.HasClass("site-modeswitch");

        private IEnumerable<string> Block(Node node)
        {
            if (node["data-ex"] is { } id && example?.Invoke(id, ExampleTitle(node)) is { } shown)
            {
                return [shown];
            }

            switch (node.Tag)
            {
                case "h1" or "h2" or "h3" or "h4" or "h5" or "h6":
                    return [$"{new string('#', node.Tag[1] - '0')} {Tidy(InlineChildren(node))}"];
                case "p" or "dt" or "dd" or "figcaption" or "summary":
                    var text = Tidy(InlineChildren(node));
                    return text.Length == 0 ? [] : [node.Tag is "dt" or "summary" ? $"**{text}**" : text];
                case "ul" or "ol":
                    return [List(node)];
                case "pre":
                    return [Markdown.Fence(TextOf(node), LanguageOf(node))];
                case "ib-code":
                    return [CodeBlock(node)];
                case "table":
                    return [Table(node)];
                case "hr":
                    return ["---"];
                case "blockquote":
                    return [Quote(Blocks(node.Children))];
            }

            if (node.HasClass("ib-alert"))
            {
                return [Quote(Blocks(node.Children))];
            }

            return Blocks(node.Children);
        }

        private static string ExampleTitle(Node node) =>
            node["aria-label"] is { } label && label.StartsWith("Example: ", StringComparison.Ordinal) ? label["Example: ".Length..] : "";

        private static string Quote(IEnumerable<string> blocks) =>
            string.Join("\n>\n", blocks.Select(b => string.Join('\n', b.Split('\n').Select(l => l.Length == 0 ? ">" : "> " + l))));

        private string List(Node list)
        {
            var items = new List<string>();
            var number = 1;
            foreach (var item in list.Children.OfType<Node>().Where(n => n.Tag == "li" && !Hidden(n)))
            {
                var marker = list.Tag == "ol" ? $"{number++}. " : "- ";
                var indent = new string(' ', marker.Length);
                var blocks = Blocks(item.Children);
                var lines = string.Join("\n\n", blocks).Split('\n');
                items.Add(marker + string.Join('\n', lines.Select((l, i) => i == 0 || l.Length == 0 ? l : indent + l)));
            }

            return string.Join('\n', items);
        }

        private static string CodeBlock(Node node)
        {
            var title = Descendants(node).FirstOrDefault(n => n.HasClass("ib-code-title")) is { } t ? TextOf(t).Trim() : null;
            var language = Descendants(node).FirstOrDefault(n => n.HasClass("ib-code-language")) is { } l ? TextOf(l).Trim() : null;
            var pre = Descendants(node).FirstOrDefault(n => n.Tag == "pre");
            language ??= LanguageOfFile(title);
            var code = Markdown.Fence(pre is null ? "" : TextOf(pre), language);
            return title is { Length: > 0 } ? $"{Markdown.Code(title)}:\n\n{code}" : code;
        }

        // A titled block shows its file's name, not its language: Program.cs is csharp.
        private static string? LanguageOfFile(string? title) => Path.GetExtension(title ?? "").ToLowerInvariant() switch
        {
            ".cs" => "csharp",
            ".razor" => "razor",
            ".json" => "json",
            ".css" => "css",
            ".js" => "javascript",
            ".ts" => "typescript",
            ".html" => "html",
            ".csproj" or ".props" or ".xml" => "xml",
            _ => null,
        };

        private static string? LanguageOf(Node pre) =>
            Descendants(pre).Prepend(pre).Select(n => n["class"]).OfType<string>()
                .SelectMany(c => c.Split(' ')).FirstOrDefault(c => c.StartsWith("language-", StringComparison.Ordinal))?["language-".Length..];

        private string Table(Node table)
        {
            var rows = Descendants(table).Where(n => n.Tag == "tr").ToList();
            if (rows.Count == 0)
            {
                return "";
            }

            var cells = rows.Select(r => r.Children.OfType<Node>().Where(c => c.Tag is "th" or "td")
                .Select(c => Markdown.Cell(string.Join(" ", Blocks(c.Children)))).ToList()).ToList();
            var width = cells.Max(r => r.Count);
            var lines = cells.Select(r => "| " + string.Join(" | ", r.Concat(Enumerable.Repeat("", width - r.Count))) + " |").ToList();
            lines.Insert(1, "|" + string.Concat(Enumerable.Repeat(" --- |", width)));
            return string.Join('\n', lines);
        }

        private string InlineChildren(Node node)
        {
            var text = new StringBuilder();
            foreach (var child in node.Children)
            {
                if (child is string s)
                {
                    text.Append(Prose(s));
                }
                else if (child is Node n && !Hidden(n))
                {
                    // A block inside inline content (a card's title in a link) reads as words.
                    text.Append(IsInline(n) ? InlineOf(n) : " " + string.Join(" ", Blocks(n.Children)) + " ");
                }
            }

            return text.ToString();
        }

        private string InlineOf(Node node)
        {
            switch (node.Tag)
            {
                case "br":
                    return "\n";
                case "code" or "kbd":
                    var code = Whitespace().Replace(TextOf(node), " ");
                    return code.Trim().Length == 0 ? code : Markdown.Code(code);
                case "strong" or "b":
                    return Wrap("**", InlineChildren(node));
                case "em" or "i":
                    return Wrap("*", InlineChildren(node));
                case "a":
                    var inner = InlineChildren(node);
                    var href = node["href"];
                    if (href is null || href.StartsWith("cref:", StringComparison.Ordinal) || node.HasClass("ib-heading-anchor")
                        || Markdown.Absolute(href) is not { } url || inner.Trim().Length == 0)
                    {
                        return inner;
                    }

                    return Markdown.Link(inner.Trim(), url);
                default:
                    return InlineChildren(node);
            }
        }

        // "**text** " rather than "** text **", which isn't bold.
        private static string Wrap(string mark, string text)
        {
            var trimmed = text.Trim();
            if (trimmed.Length == 0)
            {
                return text;
            }

            var lead = text.Length > text.TrimStart().Length ? " " : "";
            var trail = text.Length > text.TrimEnd().Length ? " " : "";
            return $"{lead}{mark}{trimmed}{mark}{trail}";
        }

        private static string Tidy(string text) =>
            string.Join('\n', text.Split('\n').Select(l => SpaceRun().Replace(l, " ").Trim())).Trim();
    }

    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.Children.OfType<Node>())
        {
            yield return child;
            foreach (var below in Descendants(child))
            {
                yield return below;
            }
        }
    }

    private static string TextOf(Node node)
    {
        var text = new StringBuilder();
        foreach (var child in node.Children)
        {
            text.Append(child is Node n ? (n.Tag == "br" ? "\n" : TextOf(n)) : (string)child);
        }

        return text.ToString();
    }

    // Linear time (NonBacktracking), so a tag without its ">" can't make a page cost seconds.
    [GeneratedRegex(@"<!--.*?-->|<(?<close>/)?(?<tag>[a-zA-Z][\w:-]*)(?<attrs>(?:[^>""']|""[^""]*""|'[^']*')*?)(?<self>/)?>", RegexOptions.Singleline | RegexOptions.NonBacktracking)]
    private static partial Regex TagOrComment();

    [GeneratedRegex(@"(?<name>[^\s=/""']+)(?:\s*=\s*(?:""(?<v1>[^""]*)""|'(?<v2>[^']*)'|(?<v3>[^\s>]+)))?")]
    private static partial Regex Attribute();

    [GeneratedRegex(@"<script[\s>].*?</script\s*>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.NonBacktracking)]
    private static partial Regex ScriptBlock();

    [GeneratedRegex(@"<style[\s>].*?</style\s*>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.NonBacktracking)]
    private static partial Regex StyleBlock();

    [GeneratedRegex(@"<(?=[A-Za-z/!])")]
    private static partial Regex TagLike();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"[ \t]{2,}")]
    private static partial Regex SpaceRun();
}
