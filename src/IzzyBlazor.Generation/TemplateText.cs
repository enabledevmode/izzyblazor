using System.Text;
using System.Text.RegularExpressions;

namespace IzzyBlazor.Generation;

/// <summary>
/// Applies conditions and tokens to a template file. Conditions are whole lines in the file type's comment
/// syntax, as <c>dotnet new</c> reads them, so one file works in both: <c>@*#if (X) *@</c> in Razor,
/// <c>#if (X)</c> in C#, <c>/*#if (X) */</c> in CSS, <c>//#if (X)</c> in JSON, <c>&lt;!--#if (X) --&gt;</c>
/// in XML and HTML; then <c>#elif (Y)</c>, <c>#else</c> and <c>#endif</c> in the same syntax. A directive
/// line is removed whole, with its line break; lines in a branch not taken go with it.
/// <para>
/// A loop, in the same syntax, repeats its lines for each record of a list the project gives (a fact whose
/// value is a list of records, such as crud's fields): <c>@*#each (Field in FormFields) *@</c> …
/// <c>@*#endeach *@</c>. Inside it, <c>{{Field.Name}}</c> is the record's Name and conditions can ask
/// <c>Field.Kind == "Enum"</c>; <c>Field.Index</c>, <c>Field.First</c> and <c>Field.Last</c> say where it is.
/// A record's list is looped the same way (<c>#each (Value in Field.Values)</c>). Loops aren't <c>dotnet new</c>
/// syntax, so an item that uses them can't be exported.
/// </para>
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
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".ico", ".woff", ".woff2", ".ttf", ".otf", ".pdf", ".zip", ".dll", ".db",
    };

    public static bool IsBinary(string path) => Binary.Contains(Path.GetExtension(path));

    public static IEnumerable<string> BinaryExtensions => Binary;

    /// <summary>Applies the conditions for this file's type, then replaces each <c>{{Token}}</c>.</summary>
    public static string Apply(string path, string text, IReadOnlyDictionary<string, object> values, IReadOnlyDictionary<string, string> tokens)
    {
        if (Directives.TryGetValue(Path.GetExtension(path), out var directive))
        {
            if (text.Contains("#each", StringComparison.Ordinal)) text = ExpandLoops(path, text, directive, values);
            text = ApplyConditions(path, text, directive, values);
        }
        return ReplaceTokens(text, tokens);
    }

    public static string ReplaceTokens(string text, IReadOnlyDictionary<string, string> tokens)
    {
        if (!text.Contains("{{", StringComparison.Ordinal)) return text;
        foreach (var (name, value) in tokens) text = text.Replace("{{" + name + "}}", value, StringComparison.Ordinal);
        return text;
    }

    /// <summary>Whether a template uses a loop, which only this engine reads.</summary>
    public static bool HasLoop(string path, string text) =>
        Directives.TryGetValue(Path.GetExtension(path), out var directive)
        && Lines(text).Any(l => directive.Match(l.TrimEnd('\r', '\n')) is { Success: true } m && m.Groups["k"].Value == "each");

    // Each outermost loop: its lines once per record, with the record's values in the conditions and its
    // tokens replaced, inner loops first. What's outside the loops is left for the conditions after.
    static string ExpandLoops(string path, string text, Regex directive, IReadOnlyDictionary<string, object> values)
    {
        var output = new StringBuilder(text.Length);
        var body = new StringBuilder();
        var depth = 0;
        var lineNumber = 0;
        var startLine = 0;
        string? head = null;
        foreach (var line in Lines(text))
        {
            lineNumber++;
            var match = directive.Match(line.TrimEnd('\r', '\n'));
            var keyword = match.Success ? match.Groups["k"].Value : null;
            if (keyword == "each")
            {
                if (depth++ == 0) { head = match.Groups["e"].Value.Trim(); startLine = lineNumber; body.Clear(); continue; }
            }
            else if (keyword == "endeach")
            {
                if (depth == 0) throw new GenerationException($"{path}, line {lineNumber}: #endeach without #each.");
                if (--depth == 0) { output.Append(Repeat(head!, body.ToString())); continue; }
            }
            (depth > 0 ? body : output).Append(line);
        }
        if (depth > 0) throw new GenerationException($"{path}, line {startLine}: #each without #endeach.");
        return output.ToString();

        string Repeat(string expression, string lines)
        {
            var loop = LoopHead().Match(expression);
            if (!loop.Success) throw new GenerationException($"{path}, line {startLine}: #each is (Name in List), not \"{expression}\".");
            var (name, listName) = (loop.Groups["n"].Value, loop.Groups["l"].Value);
            if (!values.TryGetValue(listName, out var list) || list is not IReadOnlyList<IReadOnlyDictionary<string, object>> records)
                throw new GenerationException($"{path}, line {startLine}: #each names \"{listName}\", which is not a list.");
            var repeated = new StringBuilder();
            for (var i = 0; i < records.Count; i++)
            {
                var scope = new Dictionary<string, object>(values, StringComparer.Ordinal);
                var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var (key, value) in records[i])
                {
                    scope[$"{name}.{key}"] = value;
                    if (value is not IReadOnlyList<IReadOnlyDictionary<string, object>>)
                        tokens[$"{name}.{key}"] = value is bool b ? (b ? "true" : "false") : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
                }
                scope[$"{name}.Index"] = (long)i;
                scope[$"{name}.First"] = i == 0;
                scope[$"{name}.Last"] = i == records.Count - 1;
                tokens[$"{name}.Index"] = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var text = lines.Contains("#each", StringComparison.Ordinal) ? ExpandLoops(path, lines, directive, scope) : lines;
                repeated.Append(ReplaceTokens(ApplyConditions(path, text, directive, scope), tokens));
            }
            return repeated.ToString();
        }
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
                case "each" or "endeach":
                    throw new GenerationException($"{path}, line {lineNumber}: #{keyword} doesn't match a loop.");
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

    [GeneratedRegex(@"^\(?\s*(?<n>\w+)\s+in\s+(?<l>[\w.]+)\s*\)?$")]
    private static partial Regex LoopHead();

    [GeneratedRegex(@"^\s*@\*\s*#(?<k>if|elif|elseif|else|endif|each|endeach)\b(?<e>.*?)\*@\s*$")]
    private static partial Regex Razor();

    [GeneratedRegex(@"^\s*#(?<k>if|elif|elseif|else|endif|each|endeach)\b(?<e>.*?)\s*$")]
    private static partial Regex CSharp();

    [GeneratedRegex(@"^\s*/\*\s*#(?<k>if|elif|elseif|else|endif|each|endeach)\b(?<e>.*?)\*/\s*$")]
    private static partial Regex Block();

    [GeneratedRegex(@"^\s*//\s*#(?<k>if|elif|elseif|else|endif|each|endeach)\b(?<e>.*?)\s*$")]
    private static partial Regex Line();

    [GeneratedRegex(@"^\s*<!--\s*#(?<k>if|elif|elseif|else|endif|each|endeach)\b(?<e>.*?)-->\s*$")]
    private static partial Regex Xml();
}
