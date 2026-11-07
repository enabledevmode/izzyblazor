using System.Globalization;
using System.Text;

namespace IzzyBlazor;

/// <summary>
/// Writes a parsed Markdown document as HTML, as the reference renderer does (raw HTML passes
/// through; <see cref="RichTextMarkdown"/> sanitizes the result). It walks the tree without
/// recursion, so text nested thousands deep can't overflow the stack.
/// </summary>
internal sealed class MdHtml
{
    private readonly StringBuilder _out = new();
    private string _lastOut = "\n";
    private int _disableTags;

    // For showing: HTML the sanitizer wouldn't keep is shown as text (List<string> in an answer
    // stays List<string>, not List).
    private bool _display;

    public static string Render(MdNode document, bool display = false)
    {
        var html = new MdHtml { _display = display };
        var node = document;
        var entering = true;
        while (true)
        {
            if (entering)
            {
                html.Enter(node);
                if (node.FirstChild is { } first)
                {
                    node = first;
                    continue;
                }
            }

            html.Exit(node);
            if (node == document) break;
            if (node.Next is { } next)
            {
                node = next;
                entering = true;
            }
            else
            {
                node = node.Parent!;
                entering = false;
            }
        }

        return html._out.ToString();
    }

    private void Lit(string text)
    {
        _out.Append(text);
        _lastOut = text;
    }

    private void Out(string text)
    {
        MdSyntax.Escape(text, _out);
        _lastOut = text;
    }

    private void Tag(string tag)
    {
        if (_disableTags > 0) return;
        _out.Append(tag);
        _lastOut = ">";
    }

    private void Cr()
    {
        if (_lastOut != "\n") Lit("\n");
    }

    private static string Attr(string value)
    {
        var output = new StringBuilder();
        MdSyntax.Escape(value, output);
        return output.ToString();
    }

    // Whether raw HTML starts with a tag the sanitizer keeps; a comment or declaration counts as
    // kept, since it shows nothing either way.
    private static bool Kept(string html)
    {
        var i = html.Length > 1 && html[1] == '/' ? 2 : 1;
        if (i >= html.Length || !char.IsAsciiLetter(html[i])) return true;
        var end = i;
        while (end < html.Length && (char.IsAsciiLetterOrDigit(html[end]) || html[end] == '-')) end++;
        return RichTextSanitizer.Keeps(html[i..end].ToLowerInvariant());
    }

    // A tight list's paragraphs are only their text.
    private static bool Tight(MdNode paragraph) => paragraph.Parent?.Parent is { Type: MdType.List, List.Tight: true };

    private static string ListTag(MdNode list) => list.List!.Ordered ? "ol" : "ul";

    private void Enter(MdNode node)
    {
        switch (node.Type)
        {
            case MdType.Paragraph when !Tight(node):
                Cr();
                Tag("<p>");
                break;
            case MdType.Heading:
                Cr();
                Tag($"<h{node.Level}>");
                break;
            case MdType.BlockQuote:
                Cr();
                Tag("<blockquote>");
                Cr();
                break;
            case MdType.List:
                var start = node.List!.Ordered && node.List.Start != 1 ? $" start=\"{node.List.Start.ToString(CultureInfo.InvariantCulture)}\"" : "";
                Cr();
                Tag($"<{ListTag(node)}{start}>");
                Cr();
                break;
            case MdType.Item:
                Tag("<li>");
                break;
            case MdType.CodeBlock:
                var language = node.Info.Split([' ', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                Cr();
                Tag("<pre>");
                Tag(language is null ? "<code>" : $"<code class=\"language-{Attr(language)}\">");
                Out(node.Literal);
                Tag("</code>");
                Tag("</pre>");
                Cr();
                break;
            case MdType.HtmlBlock when _display && !Kept(node.Literal):
                Cr();
                Tag("<p>");
                Out(node.Literal);
                Tag("</p>");
                Cr();
                break;
            case MdType.HtmlBlock:
                Cr();
                Lit(node.Literal);
                Cr();
                break;
            case MdType.ThematicBreak:
                Cr();
                Tag("<hr />");
                Cr();
                break;
            case MdType.Table:
                Cr();
                Tag("<table>");
                Cr();
                break;
            case MdType.TableRow:
                if (node.Head)
                {
                    Tag("<thead>");
                    Cr();
                }
                else if (node.Prev is null or { Head: true })
                {
                    Tag("<tbody>");
                    Cr();
                }

                Tag("<tr>");
                Cr();
                break;
            case MdType.TableCell:
                var align = node.Parent!.Parent!.Align is { } columns && node.Level < columns.Count && columns[node.Level] is { } a ? $" align=\"{a}\"" : "";
                Tag($"<{(node.Head ? "th" : "td")}{align}>");
                break;
            case MdType.Text:
                Out(node.Literal);
                break;
            case MdType.SoftBreak:
                Lit("\n");
                break;
            case MdType.LineBreak:
                Tag("<br />");
                Cr();
                break;
            case MdType.Code:
                Tag("<code>");
                Out(node.Literal);
                Tag("</code>");
                break;
            case MdType.HtmlInline when _display && !Kept(node.Literal):
                Out(node.Literal);
                break;
            case MdType.HtmlInline:
                Lit(node.Literal);
                break;
            case MdType.Emph:
                Tag("<em>");
                break;
            case MdType.Strong:
                Tag("<strong>");
                break;
            case MdType.Strike:
                Tag("<del>");
                break;
            case MdType.Link:
                var title = node.Title.Length > 0 ? $" title=\"{Attr(node.Title)}\"" : "";
                Tag($"<a href=\"{Attr(node.Destination)}\"{title}>");
                break;
            case MdType.Image:
                // The alt text is the description's text, without its tags.
                if (_disableTags == 0) Lit($"<img src=\"{Attr(node.Destination)}\" alt=\"");
                _disableTags++;
                break;
            case MdType.TaskBox:
                Tag(node.Checked ? "<input checked=\"\" disabled=\"\" type=\"checkbox\">" : "<input disabled=\"\" type=\"checkbox\">");
                Lit(" ");
                break;
        }
    }

    private void Exit(MdNode node)
    {
        switch (node.Type)
        {
            case MdType.Paragraph when !Tight(node):
                Tag("</p>");
                Cr();
                break;
            case MdType.Heading:
                Tag($"</h{node.Level}>");
                Cr();
                break;
            case MdType.BlockQuote:
                Cr();
                Tag("</blockquote>");
                Cr();
                break;
            case MdType.List:
                Cr();
                Tag($"</{ListTag(node)}>");
                Cr();
                break;
            case MdType.Item:
                Tag("</li>");
                Cr();
                break;
            case MdType.Table:
                Tag("</table>");
                Cr();
                break;
            case MdType.TableRow:
                Tag("</tr>");
                Cr();
                if (node.Head)
                {
                    Tag("</thead>");
                    Cr();
                }
                else if (node.Next is null)
                {
                    Tag("</tbody>");
                    Cr();
                }

                break;
            case MdType.TableCell:
                Tag(node.Head ? "</th>" : "</td>");
                Cr();
                break;
            case MdType.Emph:
                Tag("</em>");
                break;
            case MdType.Strong:
                Tag("</strong>");
                break;
            case MdType.Strike:
                Tag("</del>");
                break;
            case MdType.Link:
                Tag("</a>");
                break;
            case MdType.Image:
                _disableTags--;
                if (_disableTags == 0)
                {
                    if (node.Title.Length > 0) Lit($"\" title=\"{Attr(node.Title)}");
                    Lit("\" />");
                }

                break;
        }
    }
}
