using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace IzzyBlazor.DocsGen;

/// <summary>
/// The header comments of components written as <c>.razor</c> files, which can't carry an XML doc
/// comment: a <c>@* *@</c> block after the directives with a description, then usage lines. Used
/// when a component has no <c>&lt;summary&gt;</c>: the description becomes its summary and the
/// first run of usage lines its example. Anything after that (implementation notes) is left out.
/// </summary>
public sealed partial class RazorHeaders
{
    private readonly Dictionary<string, (string? Summary, string? Example)> _byName = new(StringComparer.Ordinal);

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

            if (Parse(File.ReadAllLines(path)) is { } header)
            {
                _byName[Path.GetFileNameWithoutExtension(path)] = header;
            }
        }
    }

    public int Count => _byName.Count;

    /// <summary>The header's description as HTML paragraphs, and its first example, for a component's plain name.</summary>
    public (string? Summary, string? Example) For(string name) => _byName.GetValueOrDefault(name);

    private static (string? Summary, string? Example)? Parse(string[] lines)
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

        var text = Dedent(body);
        var exampleStart = text.FindIndex(l => l.TrimStart().StartsWith('<'));
        var prose = (exampleStart < 0 ? text : text.Take(exampleStart)).ToList();
        string? example = null;
        if (exampleStart >= 0)
        {
            // The usage block: from the first markup line until a blank line that isn't followed by more markup.
            var block = new List<string>();
            for (var j = exampleStart; j < text.Count; j++)
            {
                if (text[j].Trim().Length == 0)
                {
                    var next = text.Skip(j + 1).FirstOrDefault(l => l.Trim().Length > 0);
                    if (next is null || !next.TrimStart().StartsWith('<'))
                    {
                        break;
                    }
                }

                block.Add(text[j]);
            }

            example = string.Join("\n", block).Trim('\n');
        }

        var summary = Paragraphs(prose);
        return summary is null && example is null ? null : (summary, example);
    }

    private static string? Paragraphs(List<string> lines)
    {
        var html = new StringBuilder();
        var paragraph = new List<string>();

        void Flush()
        {
            if (paragraph.Count > 0)
            {
                html.Append("<p>").Append(Inline(string.Join(" ", paragraph))).Append("</p>");
                paragraph.Clear();
            }
        }

        foreach (var line in lines)
        {
            if (line.Trim().Length == 0)
            {
                Flush();
            }
            else
            {
                paragraph.Add(line.Trim());
            }
        }

        Flush();
        return html.Length == 0 ? null : html.ToString();
    }

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
