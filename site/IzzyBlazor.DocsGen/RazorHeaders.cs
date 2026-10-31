using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace IzzyBlazor.DocsGen;

/// <summary>
/// The header comments of components written as <c>.razor</c> files, which can't carry an XML doc
/// comment: a <c>@* *@</c> block after the directives with a description, usage markup, and often
/// more text. Used where a component has no <c>&lt;summary&gt;</c> or <c>&lt;remarks&gt;</c>: the
/// text before the first markup becomes its summary, that markup its example, and everything after
/// it its remarks (paragraphs, "- " bullet lists and more markup as code). A line reading
/// "Accessibility:" starts the accessibility section, "Keyboard:" the keys, and a "See also:" line
/// names related components, comma-separated; each runs to the next of these lines or the end.
/// In "Keyboard:", a line is the title of the keys under it, "- Up, Shift+Up | What they do" a row,
/// and an indented line continues a row. A key can't be a comma or a bar.
/// </summary>
public sealed partial class RazorHeaders
{
    /// <summary>A header: summary, remarks and accessibility as HTML, the example as plain text, related components by name.</summary>
    public sealed record Header(string? Summary, string? Example, string? Remarks, string? Accessibility = null, List<string>? SeeAlso = null, List<ApiKeyboardGroup>? Keyboard = null);

    private readonly Dictionary<string, Header> _byName = new(StringComparer.Ordinal);

    public RazorHeaders(string? sourceRoot)
    {
        if (sourceRoot is null || !Directory.Exists(sourceRoot))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(sourceRoot, "*.razor", SearchOption.AllDirectories))
        {
            if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            try
            {
                if (Parse(File.ReadAllLines(path)) is { } header)
                {
                    _byName[Path.GetFileNameWithoutExtension(path)] = header;
                }
            }
            catch (FormatException e)
            {
                throw new FormatException($"{path}: {e.Message}", e);
            }
        }
    }

    public int Count => _byName.Count;

    /// <summary>The header of the component with this plain name, or null.</summary>
    public Header? For(string name) => _byName.GetValueOrDefault(name);

    private static Header? Parse(string[] lines)
    {
        var i = 0;
        while (i < lines.Length && (lines[i].Trim().Length == 0 || (lines[i].TrimStart().StartsWith('@') && !lines[i].TrimStart().StartsWith("@*", StringComparison.Ordinal))))
        {
            i++;
        }

        if (i >= lines.Length || !lines[i].TrimStart().StartsWith("@*", StringComparison.Ordinal))
        {
            return null;
        }

        var body = new List<string>();
        var first = lines[i].TrimStart()[2..];
        if (first.Contains("*@", StringComparison.Ordinal))
        {
            body.Add(first[..first.IndexOf("*@", StringComparison.Ordinal)]);
        }
        else
        {
            if (first.Trim().Length > 0)
            {
                body.Add(first);
            }

            for (i++; i < lines.Length; i++)
            {
                var end = lines[i].IndexOf("*@", StringComparison.Ordinal);
                if (end >= 0)
                {
                    body.Add(lines[i][..end]);
                    break;
                }

                body.Add(lines[i]);
            }
        }

        var (main, accessibilityLines, seeAlso, keyboardLines) = Sections(Dedent(body));
        var keyboard = Keys(keyboardLines);
        var accessibility = accessibilityLines.Count > 0 ? Html(Blocks(Dedent(accessibilityLines))) : null;
        var blocks = Blocks(main);

        // The example is the first markup, at the top level or under a bullet; what comes before
        // the block that has it is the summary, and the rest (without the example) the remarks.
        var at = blocks.FindIndex(b => b.Code is not null || b.Items.Any(i => i.Code is not null));
        if (at < 0)
        {
            return Html(blocks) is { } only ? new Header(only, null, null, accessibility, seeAlso, keyboard)
                : keyboard is not null ? new Header(null, null, null, accessibility, seeAlso, keyboard) : null;
        }

        var holder = blocks[at];
        List<string> code;
        List<Block> rest;
        if (holder.Code is not null)
        {
            code = holder.Code;
            rest = blocks.Skip(at + 1).ToList();
        }
        else
        {
            var item = holder.Items.FindIndex(i => i.Code is not null);
            code = holder.Items[item].Code!;
            var items = holder.Items.Select((i, n) => n == item ? i with { Code = null } : i).ToList();
            rest = [holder with { Items = items }, .. blocks.Skip(at + 1)];
        }

        return new Header(Html(blocks.Take(at)), string.Join("\n", Dedent(code)), Html(rest), accessibility, seeAlso, keyboard);
    }

    private static List<ApiKeyboardGroup>? Keys(List<string> lines)
    {
        var groups = new List<ApiKeyboardGroup>();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            // Under a row, a row wrapped onto the next line.
            if (char.IsWhiteSpace(raw[0]) && groups.Count > 0 && groups[^1].Rows.Count > 0)
            {
                var last = groups[^1].Rows[^1];
                groups[^1].Rows[^1] = last with { Action = $"{last.Action} {line}" };
                continue;
            }

            var bar = line.IndexOf('|', StringComparison.Ordinal);
            if (!line.StartsWith("- ", StringComparison.Ordinal))
            {
                if (bar >= 0)
                {
                    throw new FormatException($"A key's row is \"- keys | what they do\": {line}");
                }

                groups.Add(new ApiKeyboardGroup(line, []));
                continue;
            }

            if (bar < 0)
            {
                throw new FormatException($"A key's row is \"- keys | what they do\": {line}");
            }

            if (groups.Count == 0)
            {
                groups.Add(new ApiKeyboardGroup(null, []));
            }

            var keys = line[2..bar].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
            if (keys.Count == 0 || keys.Exists(k => k.EndsWith('+')))
            {
                throw new FormatException($"A key's row needs its keys, and none can be a comma or a bar: {line}");
            }

            groups[^1].Rows.Add(new ApiKeyRow(keys, line[(bar + 1)..].Trim()));
        }

        if (groups.Find(g => g.Rows.Count == 0) is { } empty)
        {
            throw new FormatException($"Keys titled \"{empty.Title}\" have no rows.");
        }

        return groups.Count > 0 ? groups : null;
    }

    // Splits off the "Accessibility:", "Keyboard:" and "See also:" sections, which start at a line of
    // their own that isn't indented.
    private static (List<string> Main, List<string> Accessibility, List<string>? SeeAlso, List<string> Keyboard) Sections(List<string> lines)
    {
        var main = new List<string>();
        var accessibility = new List<string>();
        var seeAlso = new List<string>();
        var keyboard = new List<string>();
        var current = main;
        foreach (var line in lines)
        {
            var top = line.Length > 0 && !char.IsWhiteSpace(line[0]);
            if (top && line.Trim() == "Accessibility:")
            {
                current = accessibility;
                continue;
            }

            if (top && line.Trim() == "Keyboard:")
            {
                current = keyboard;
                continue;
            }

            if (top && line.StartsWith("See also:", StringComparison.Ordinal))
            {
                current = seeAlso;
                current.Add(line["See also:".Length..]);
                continue;
            }

            current.Add(line);
        }

        var names = string.Join(",", seeAlso.Select(l => l.Trim().TrimStart('-').Trim()))
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(n => n.Trim('`', '.', ' '))
            .Where(n => n.Length > 0)
            .Distinct()
            .ToList();
        return (main, accessibility, names.Count > 0 ? names : null, keyboard);
    }

    // A block of the header: a paragraph (Lines), markup (Code) or a bullet list (Items, each with
    // its text and any markup written under it).
    private sealed record Block(List<string> Lines, List<string>? Code, List<Item> Items);

    private sealed record Item(string Text, List<string>? Code);

    private static Block Paragraph(string line) => new([line], null, []);

    // Markup starts a block when it opens one: after a blank line, or under a line that ends a
    // sentence (".", ":"). It runs until a blank line that isn't followed by more markup (or the
    // sample's @code block), or a line indented less than it. "- " starts a bullet; a line indented under one continues it.
    private static List<Block> Blocks(List<string> text)
    {
        var blocks = new List<Block>();
        Block? current = null;
        List<string>? code = null;
        var codeIndent = 0;
        var bulletIndent = 0;
        for (var i = 0; i < text.Count; i++)
        {
            var line = text[i];
            var trimmed = line.Trim();
            var indent = line.Length - line.TrimStart().Length;
            if (code is not null)
            {
                if (trimmed.Length == 0)
                {
                    var next = text.Skip(i + 1).FirstOrDefault(l => l.Trim().Length > 0);
                    if (next is null || !(next.TrimStart().StartsWith('<') || next.TrimStart().StartsWith("@code", StringComparison.Ordinal)))
                    {
                        code = null;
                        current = null;
                        continue;
                    }
                }

                if (trimmed.Length == 0 || indent >= codeIndent)
                {
                    code.Add(line);
                    continue;
                }

                code = null;
                if (current is { Code: not null })
                {
                    current = null;
                }
            }

            if (trimmed.Length == 0)
            {
                current = null;
                continue;
            }

            var previous = current is null ? null : current.Items.Count > 0 ? current.Items[^1].Text : current.Lines.LastOrDefault();
            var opens = previous is null || previous.EndsWith('.') || previous.EndsWith(':');
            if (trimmed.StartsWith('<') && opens)
            {
                code = [line];
                codeIndent = indent;
                if (current is { Items.Count: > 0 } && indent > bulletIndent)
                {
                    current.Items[^1] = current.Items[^1] with { Code = code };
                }
                else
                {
                    current = new Block([], code, []);
                    blocks.Add(current);
                }
            }
            else if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                if (current is not { Items.Count: > 0 })
                {
                    current = new Block([], null, []);
                    blocks.Add(current);
                }

                current.Items.Add(new Item(trimmed[2..].Trim(), null));
                bulletIndent = indent;
            }
            else if (current is { Items.Count: > 0 } && indent > bulletIndent)
            {
                current.Items[^1] = current.Items[^1] with { Text = current.Items[^1].Text + " " + trimmed };
            }
            else if (current is { Code: null, Items.Count: 0 })
            {
                current.Lines.Add(trimmed);
            }
            else
            {
                current = Paragraph(trimmed);
                blocks.Add(current);
            }
        }

        return blocks;
    }

    private static string? Html(IEnumerable<Block> blocks)
    {
        var html = new StringBuilder();
        foreach (var block in blocks)
        {
            if (block.Code is not null)
            {
                html.Append(Pre(block.Code));
            }
            else if (block.Items.Count > 0)
            {
                html.Append("<ul>");
                foreach (var item in block.Items)
                {
                    html.Append("<li>").Append(Inline(item.Text));
                    if (item.Code is not null)
                    {
                        html.Append(Pre(item.Code));
                    }

                    html.Append("</li>");
                }

                html.Append("</ul>");
            }
            else
            {
                html.Append("<p>").Append(Inline(string.Join(" ", block.Lines))).Append("</p>");
            }
        }

        return html.Length == 0 ? null : html.ToString();
    }

    private static string Pre(List<string> code) => $"<pre><code>{WebUtility.HtmlEncode(string.Join("\n", Dedent(code)))}</code></pre>";

    // Encodes the text, then shows `code` and <tags> mentioned in prose as code.
    private static string Inline(string text)
    {
        var html = Tags().Replace(WebUtility.HtmlEncode(text), m => $"<code>{m.Value}</code>");
        return Ticks().Replace(html, m => $"<code>{m.Groups[1].Value}</code>");
    }

    private static List<string> Dedent(List<string> lines)
    {
        var indent = lines.Where(l => l.Trim().Length > 0).Select(l => l.Length - l.TrimStart().Length).DefaultIfEmpty(0).Min();
        var result = lines.Select(l => l.Length >= indent ? l[indent..].TrimEnd() : l.Trim()).ToList();
        while (result.Count > 0 && result[0].Length == 0)
        {
            result.RemoveAt(0);
        }

        while (result.Count > 0 && result[^1].Length == 0)
        {
            result.RemoveAt(result.Count - 1);
        }

        return result;
    }

    [GeneratedRegex("`([^`]+)`")]
    private static partial Regex Ticks();

    [GeneratedRegex("&lt;/?[A-Za-z][^&]*?&gt;")]
    private static partial Regex Tags();
}
