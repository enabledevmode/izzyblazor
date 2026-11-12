using System.Text;
using System.Text.RegularExpressions;

namespace IzzyBlazor.Generation;

/// <summary>
/// Applies conditions and tokens to a template file. Conditions are whole lines in the file type's comment
/// syntax, as <c>dotnet new</c> reads them, so one file works in both: <c>@*#if (X) *@</c> in Razor,
/// <c>#if (X)</c> in C#, <c>/*#if (X) */</c> in CSS, <c>//#if (X)</c> in JSON, <c>&lt;!--#if (X) --&gt;</c>
/// in XML and HTML; then <c>#elif (Y)</c>, <c>#else</c> and <c>#endif</c> in the same syntax. A directive
/// line is removed whole, with its line break; lines in a branch not taken go with it.
/// </summary>
internal static partial class TemplateText
{
    static readonly Dictionary<string, Regex> Directives = new(StringComparer.OrdinalIgnoreCase)
    {
        [".razor"] = Razor(), [".cshtml"] = Razor(),
        [".cs"] = CSharp(),
        [".css"] = Block(), [".scss"] = Block(),
        [".json"] = Line(),
        [".csproj"] = Xml(), [".props"] = Xml(), [".targets"] = Xml(), [".xml"] = Xml(), [".html"] = Xml(), [".config"] = Xml(),
    };

    /// <summary>Files copied byte for byte: no conditions, no tokens.</summary>
    static readonly HashSet<string> Binary = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".ico", ".woff", ".woff2", ".ttf", ".otf", ".pdf", ".zip", ".dll",
    };

    public static bool IsBinary(string path) => Binary.Contains(Path.GetExtension(path));

    public static IEnumerable<string> BinaryExtensions => Binary;

    /// <summary>Applies the conditions for this file's type, then replaces each <c>{{Token}}</c>.</summary>
    public static string Apply(string path, string text, IReadOnlyDictionary<string, object> values, IReadOnlyDictionary<string, string> tokens)
    {
        if (Directives.TryGetValue(Path.GetExtension(path), out var directive)) text = ApplyConditions(path, text, directive, values);
        return ReplaceTokens(text, tokens);
    }

    public static string ReplaceTokens(string text, IReadOnlyDictionary<string, string> tokens)
    {
        if (!text.Contains("{{", StringComparison.Ordinal)) return text;
        foreach (var (name, value) in tokens) text = text.Replace("{{" + name + "}}", value, StringComparison.Ordinal);
        return text;
    }

    static string ApplyConditions(string path, string text, Regex directive, IReadOnlyDictionary<string, object> values)
    {
        var output = new StringBuilder(text.Length);
        // Each open #if: whether its enclosing block is kept, whether a branch was taken, whether this branch is kept.
        var stack = new Stack<(bool Outer, bool Taken, bool Keep)>();
        var keep = true;
        var lineNumber = 0;
        foreach (var line in Lines(text))
        {
            lineNumber++;
            var match = directive.Match(line.TrimEnd('\r', '\n'));
            if (!match.Success)
            {
                if (keep) output.Append(line);
                continue;
            }
            var keyword = match.Groups["k"].Value;
            var expression = match.Groups["e"].Value.Trim();
            switch (keyword)
            {
                case "if":
                    var holds = keep && Holds(expression);
                    stack.Push((keep, holds, holds));
                    keep = holds;
                    break;
                case "elif" or "elseif":
                    {
                        if (stack.Count == 0) throw Unmatched();
                        var (outer, taken, _) = stack.Pop();
                        var now = outer && !taken && Holds(expression);
                        stack.Push((outer, taken || now, now));
                        keep = now;
                        break;
                    }
                case "else":
                    {
                        if (stack.Count == 0) throw Unmatched();
                        var (outer, taken, _) = stack.Pop();
                        var now = outer && !taken;
                        stack.Push((outer, true, now));
                        keep = now;
                        break;
                    }
                case "endif":
                    if (stack.Count == 0) throw Unmatched();
                    keep = stack.Pop().Outer;
                    break;
            }

            bool Holds(string e) => e.Length > 0
                ? Condition.Evaluate(e, values)
                : throw new GenerationException($"{path}, line {lineNumber}: #{keyword} needs a condition.");

            GenerationException Unmatched() => new($"{path}, line {lineNumber}: #{keyword} without #if.");
        }
        if (stack.Count > 0) throw new GenerationException($"{path}: #if without #endif.");
        return output.ToString();
    }

    /// <summary>Lines with their own line breaks, so the text comes back exactly as it went in.</summary>
    static IEnumerable<string> Lines(string text)
    {
        var start = 0;
        while (start < text.Length)
        {
            var end = text.IndexOf('\n', start);
            if (end < 0) { yield return text[start..]; yield break; }
            yield return text[start..(end + 1)];
            start = end + 1;
        }
    }

    [GeneratedRegex(@"^\s*@\*\s*#(?<k>if|elif|elseif|else|endif)\b(?<e>.*?)\*@\s*$")]
    private static partial Regex Razor();

    [GeneratedRegex(@"^\s*#(?<k>if|elif|elseif|else|endif)\b(?<e>.*?)\s*$")]
    private static partial Regex CSharp();

    [GeneratedRegex(@"^\s*/\*\s*#(?<k>if|elif|elseif|else|endif)\b(?<e>.*?)\*/\s*$")]
    private static partial Regex Block();

    [GeneratedRegex(@"^\s*//\s*#(?<k>if|elif|elseif|else|endif)\b(?<e>.*?)\s*$")]
    private static partial Regex Line();

    [GeneratedRegex(@"^\s*<!--\s*#(?<k>if|elif|elseif|else|endif)\b(?<e>.*?)-->\s*$")]
    private static partial Regex Xml();
}
