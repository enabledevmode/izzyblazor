using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace IzzyBlazor.DocsGen;

/// <summary>
/// A CSS variable as izzy describe tokens gives it. <see cref="Value"/> is as tokens.css writes it, and
/// <see cref="Light"/> and <see cref="Dark"/> are that with each light-dark() pair and each variable it reads
/// from tokens.css worked out; a variable tokens.css doesn't declare has <see cref="Default"/> in words instead.
/// </summary>
public sealed record CliToken
{
    public required string Name { get; init; }

    /// <summary>The Tokens page's group; null for an internal variable.</summary>
    public string? Group { get; init; }

    /// <summary>color, length, font, number, shadow, time, easing or other; null for an internal variable.</summary>
    public string? Type { get; init; }

    public string? Description { get; init; }

    /// <summary>False for a variable the library uses for itself, which may change without notice.</summary>
    public bool Public { get; init; }

    public string? Value { get; init; }
    public string? Light { get; init; }
    public string? Dark { get; init; }

    /// <summary>What it is when unset, in words or as the variable it falls back to.</summary>
    public string? Default { get; init; }

    /// <summary>The components whose own variable it is.</summary>
    public List<string>? Components { get; init; }
}

/// <summary>
/// The library's CSS variables: the registry (the site's tokens.json) for the public ones, tokens.css for
/// their values, and every other <c>--ib-*</c> name the library's CSS declares or reads, as internal.
/// </summary>
public static partial class TokenData
{
    static readonly string[] Types = ["color", "length", "font", "number", "shadow", "time", "easing", "other"];

    public static List<CliToken> Read(string registryPath, string stylesRoot, ISet<string> components)
    {
        var registry = JsonNode.Parse(File.ReadAllText(registryPath), documentOptions: new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })!;
        var css = Directory.EnumerateFiles(stylesRoot, "*.css", SearchOption.AllDirectories)
            .Where(f => !f.Replace('\\', '/').Contains("/_generated/", StringComparison.Ordinal))
            .ToDictionary(f => Path.GetRelativePath(stylesRoot, f).Replace('\\', '/'), f => Comment().Replace(File.ReadAllText(f), " "));

        // tokens.css's values: the first declaration of each, which is its :root's.
        var declared = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in Declaration().Matches(css["tokens.css"])) declared.TryAdd(m.Groups["name"].Value, Spaces().Replace(m.Groups["value"].Value, " ").Trim());
        var used = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var text in css.Values)
        {
            foreach (Match m in Declaration().Matches(text)) used.Add(m.Groups["name"].Value);
            foreach (Match m in Read().Matches(text)) used.Add(m.Groups["name"].Value);
        }

        var tokens = new List<CliToken>();
        var listed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var block in registry["groups"]!.AsArray().Select(g => g!.AsObject()))
        {
            var group = (string)block["group"]!;
            var type = (string)block["type"]!;
            var owners = block["components"]?.AsArray().Select(c => (string)c!).ToList();
            foreach (var owner in owners ?? [])
                if (!components.Contains(owner)) throw new InvalidOperationException($"tokens.json: {group} names {owner}, which isn't a component.");
            foreach (var (key, entry) in block["tokens"]!.AsObject())
            {
                var (description, own, fallback) = entry is JsonObject o
                    ? ((string?)o["description"], (string?)o["type"], (string?)o["default"])
                    : ((string?)entry, null, null);
                var kind = own ?? type;
                if (!Types.Contains(kind)) throw new InvalidOperationException($"tokens.json: {key} has type \"{kind}\"; a type is one of {string.Join(", ", Types)}.");
                var names = key.EndsWith('*')
                    ? declared.Keys.Where(n => n.StartsWith(key[..^1], StringComparison.Ordinal) && !listed.Contains(n)).ToList()
                    : [key];
                if (names.Count == 0) throw new InvalidOperationException($"tokens.json: {key} matches no variable tokens.css declares (or only ones listed above it).");
                foreach (var name in names)
                {
                    if (!listed.Add(name)) throw new InvalidOperationException($"tokens.json lists {name} twice.");
                    if (!used.Contains(name)) throw new InvalidOperationException($"tokens.json lists {name}, which the library's CSS doesn't declare or use.");
                    tokens.Add(Token(name, declared) with { Group = group, Type = kind, Description = description, Public = true, Default = declared.ContainsKey(name) ? null : fallback, Components = owners });
                }
            }
        }

        foreach (var name in registry["internal"]?.AsArray().Select(n => (string)n!) ?? [])
            if (!declared.ContainsKey(name)) throw new InvalidOperationException($"tokens.json marks {name} internal, which tokens.css doesn't declare.");
        if (declared.Keys.Where(n => !listed.Contains(n) && registry["internal"]?.AsArray().Any(i => (string?)i == n) != true).ToList() is { Count: > 0 } missing)
            throw new InvalidOperationException($"tokens.css declares {string.Join(", ", missing)}, which tokens.json doesn't list: add each to a group, or to \"internal\".");

        // Everything else is the library's own.
        foreach (var name in used.Where(n => !listed.Contains(n))) tokens.Add(Token(name, declared));
        return tokens;
    }

    static CliToken Token(string name, Dictionary<string, string> declared)
    {
        if (!declared.TryGetValue(name, out var value)) return new CliToken { Name = name };
        var (light, dark) = (Resolve(value, declared, dark: false, 0), Resolve(value, declared, dark: true, 0));
        return new CliToken { Name = name, Value = value, Light = light, Dark = dark };
    }

    // A value with light-dark() taken for one scheme and the tokens.css variables it reads put in.
    static string Resolve(string value, Dictionary<string, string> declared, bool dark, int depth)
    {
        if (depth > 8) return value;
        var text = new StringBuilder();
        for (var i = 0; i < value.Length;)
        {
            if (Starts(value, i, "light-dark(") && Arguments(value, i + "light-dark(".Length) is ({ } args, var end) && args.Count == 2)
            {
                text.Append(Resolve(args[dark ? 1 : 0], declared, dark, depth + 1));
                i = end;
            }
            else if (Starts(value, i, "var(") && Arguments(value, i + "var(".Length) is ({ } parts, var close))
            {
                var name = parts[0].Trim();
                text.Append(declared.TryGetValue(name, out var inner) ? Resolve(inner, declared, dark, depth + 1)
                    : parts.Count > 1 ? Resolve(string.Join(",", parts.Skip(1)).Trim(), declared, dark, depth + 1)
                    : value[i..close]);
                i = close;
            }
            else text.Append(value[i++]);
        }
        return text.ToString();
    }

    static bool Starts(string value, int at, string word) =>
        string.CompareOrdinal(value, at, word, 0, word.Length) == 0 && (at == 0 || !(char.IsLetterOrDigit(value[at - 1]) || value[at - 1] == '-'));

    // The arguments of a call whose "(" ends just before start, split at its own commas, and where it ends.
    static (List<string>? Args, int End) Arguments(string value, int start)
    {
        var args = new List<string>();
        var depth = 0;
        var from = start;
        for (var i = start; i < value.Length; i++)
        {
            switch (value[i])
            {
                case '(': depth++; break;
                case ',' when depth == 0: args.Add(value[from..i].Trim()); from = i + 1; break;
                case ')' when depth == 0: args.Add(value[from..i].Trim()); return (args, i + 1);
                case ')': depth--; break;
            }
        }
        return (null, value.Length);
    }

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comment();

    [GeneratedRegex(@"(?<![\w-])(?<name>--ib-[a-z0-9-]+)\s*:(?<value>[^;{}]*)")]
    private static partial Regex Declaration();

    [GeneratedRegex(@"var\(\s*(?<name>--ib-[a-z0-9-]+)")]
    private static partial Regex Read();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
