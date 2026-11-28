using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using IzzyBlazor.Generation;

namespace IzzyBlazor.Cli;

/// <summary>
/// An app's own color tokens, as the Tokens docs describe them: <c>--ib-color-{name}</c> and
/// <c>--ib-color-on-{name}</c> as <c>light-dark()</c> pairs in the app's <c>wwwroot/theme.css</c>, and an
/// <c>Accent</c> and a <c>Color</c> member for each, in extension classes (one per type) that pages see
/// without a using: they're in the app's root namespace.
/// </summary>
static partial class AppTokens
{
    /// <summary>What a token is: its fill and the text on it, in light and dark.</summary>
    public sealed record Colors(string Light, string Dark, string OnLight, string OnDark);

    /// <summary>A token's name as a C# member: promo-gold is PromoGold.</summary>
    public static string Member(string name) =>
        string.Concat(name.Split('-', StringSplitOptions.RemoveEmptyEntries).Select(p => char.ToUpperInvariant(p[0]) + p[1..]));

    public static void CheckName(string name, string what = "A token's name")
    {
        if (!TokenName().IsMatch(name))
            throw new UsageException($"{what} is lowercase letters, digits and dashes, starting with a letter, such as promo or brand-gold. Got \"{name}\".");
    }

    /// <summary>The app's stylesheet for its tokens.</summary>
    public static string Stylesheet(ProjectInfo project) => project.Host + "wwwroot/theme.css";

    /// <summary>The tokens the app's theme.css declares, by name (promo), with where each declaration's value is.</summary>
    public static Dictionary<string, (int Start, int Length)> Declared(string css, string prefix = "--ib-color-")
    {
        var found = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
        foreach (Match m in Declaration().Matches(Comments().Replace(css, c => new string(' ', c.Length))))
            if (m.Groups["name"].Value.StartsWith(prefix, StringComparison.Ordinal))
                found.TryAdd(m.Groups["name"].Value[prefix.Length..], (m.Groups["value"].Index, m.Groups["value"].Length));
        return found;
    }

    /// <summary>
    /// The changes that add the token, or set its colors when the app has it: theme.css (made, and linked
    /// after izzyblazor.css, when there's none) and the extension members of the types asked for.
    /// </summary>
    public static Changeset Add(ProjectInfo project, string name, Colors colors, bool accent, bool color, List<string> warnings)
    {
        var creates = new List<FileCreate>();
        var inserts = new List<TextInsert>();
        var path = Stylesheet(project);
        var fill = $"light-dark({colors.Light}, {colors.Dark})";
        var on = $"light-dark({colors.OnLight}, {colors.OnDark})";
        if (project.ReadFile(path) is not { } css)
        {
            creates.Add(new FileCreate(path, $"/* The app's own tokens: izzy add token writes them here. */\n:root {{\n  --ib-color-{name}: {fill};\n  --ib-color-on-{name}: {on};\n}}\n", null));
            Link(project, inserts, warnings);
        }
        else
        {
            var hash = Inserts.Hash(css);
            var nl = css.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var declared = Declared(css);
            var missing = new StringBuilder();
            foreach (var (token, value) in new[] { (name, fill), ("on-" + name, on) })
            {
                if (declared.TryGetValue(token, out var at))
                {
                    if (css.Substring(at.Start, at.Length).Trim() != value) inserts.Add(new TextInsert(path, at.Start, " " + value, hash) { Length = at.Length });
                }
                else missing.Append($"  --ib-color-{token}: {value};{nl}");
            }
            if (missing.Length > 0)
            {
                // Into the first :root block, before its closing brace; else a block of its own at the end.
                var blank = Comments().Replace(css, c => new string(' ', c.Length));
                if (Regex.Match(blank, @":root\s*\{[^{}]*\}") is { Success: true } root)
                {
                    var close = root.Index + root.Length - 1;
                    // On the closing brace's own line: before it. After a declaration on that line: a new line first.
                    var lineStart = css.LastIndexOf('\n', close - 1) + 1;
                    var alone = css[lineStart..close].Trim().Length == 0;
                    inserts.Add(new TextInsert(path, alone ? lineStart : close, (alone ? "" : nl) + missing, hash));
                }
                else inserts.Add(new TextInsert(path, css.Length, $"{(css.Length == 0 || css.EndsWith('\n') ? "" : nl)}{nl}:root {{{nl}{missing}}}{nl}", hash));
            }
            if (declared.ContainsKey(name)) warnings.Insert(0, $"{path} already has --ib-color-{name}: its colors are set to these.");
        }

        var types = new List<string>();
        if (accent) types.Add("Accent");
        if (color) types.Add("Color");
        Members(project, name, types, creates, inserts);
        return new Changeset { Creates = creates, Inserts = inserts, Packages = [], Conflicts = [], Warnings = warnings };
    }

    // <link> for theme.css after the library's stylesheet in App.razor, written as that link is.
    static void Link(ProjectInfo project, List<TextInsert> inserts, List<string> warnings)
    {
        var app = project.Host + "Components/App.razor";
        var text = project.ReadFile(app);
        var link = text is null ? null : Regex.Match(text, @"^(?<indent>[ \t]*)<link\b[^>]*izzyblazor\.css[^>]*>[^\S\n]*\r?\n", RegexOptions.Multiline);
        if (link is not { Success: true })
        {
            warnings.Add($"Link {Stylesheet(project)["{project.Host}wwwroot/".Length..]} in your page's <head>, after izzyblazor.css: izzy found no link to izzyblazor.css{(text is null ? $" ({app} isn't there)" : $" in {app}")}.");
            return;
        }
        var href = link.Value.Contains("@Assets[", StringComparison.Ordinal) ? "@Assets[\"theme.css\"]" : "theme.css";
        var nl = link.Value.EndsWith("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        inserts.Add(new TextInsert(app, link.Index + link.Length, $"{link.Groups["indent"].Value}<link rel=\"stylesheet\" href=\"{href}\" />{nl}", Inserts.Hash(text!)));
    }

    // An extension member for each type, in the app's extension block for it, else in Theme/AppTokens.cs.
    static void Members(ProjectInfo project, string name, List<string> types, List<FileCreate> creates, List<TextInsert> inserts)
    {
        var member = Member(name);
        var sources = Sources(project).ToList();
        var missing = new List<string>();
        foreach (var type in types)
        {
            var block = sources.Select(s => (s.Path, s.Text, Match: Regex.Match(s.Text, $@"extension\s*\(\s*{type}\s*\)\s*\{{"))).FirstOrDefault(s => s.Match.Success);
            if (block.Match is null) { missing.Add(type); continue; }
            var body = Body(block.Text, block.Match.Index + block.Match.Length);
            // Already there, by name: left as it is.
            if (Regex.IsMatch(body, $@"\b{type}\s+{member}\b")) continue;
            var nl = block.Text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var indent = Regex.Match(block.Text[..block.Match.Index], @"[ \t]*$").Value + "    ";
            inserts.Add(new TextInsert(block.Path, block.Match.Index + block.Match.Length, $"{nl}{indent}public static {type} {member} => {type}.Token(\"{name}\");", Inserts.Hash(block.Text)));
        }
        if (missing.Count == 0) return;
        var path = (project.Client ?? project.Host) + "Theme/AppTokens.cs";
        if (project.ReadFile(path) is { } existing)
        {
            // The file is there but lacks a block for these types: add their classes at its end.
            var nl = existing.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var classes = string.Concat(missing.Select(t => nl + Class(t, name, member).ReplaceLineEndings(nl)));
            inserts.Add(new TextInsert(path, existing.Length, (existing.EndsWith('\n') ? "" : nl) + classes, Inserts.Hash(existing)));
            return;
        }
        creates.Add(new FileCreate(path, $"using IzzyBlazor;\n\nnamespace {project.RootNamespace};\n\n// The app's own tokens as Accent and Color members (izzy add token), beside the built-in ones.\n" + string.Join("\n", missing.Select(t => Class(t, name, member))), null));
    }

    static string Class(string type, string name, string member) =>
        $"public static class {type}Extensions\n{{\n    extension({type})\n    {{\n        public static {type} {member} => {type}.Token(\"{name}\");\n    }}\n}}\n";

    // The text of a block from just inside its opening brace to its closing one.
    static string Body(string text, int start)
    {
        var depth = 1;
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0) return text[start..i];
        }
        return text[start..];
    }

    /// <summary>The app's .razor and .cs files, by path from its folder, with their text.</summary>
    public static IEnumerable<(string Path, string Text)> Sources(ProjectInfo project) =>
        ReleaseChanges.Files(project.Directory!).Select(p => (p, project.ReadFile(p) ?? "")).Where(s => s.Item2.Length > 0);

    /// <summary>Where the app uses a token: its members (Accent.Promo, Color.Promo), Token("promo"), and var(--ib-color-promo) in its CSS.</summary>
    public static List<(string Path, int Offset, int Length, string Kind)> Uses(ProjectInfo project, string name)
    {
        var member = Member(name);
        var uses = new List<(string, int, int, string)>();
        foreach (var (path, text) in Sources(project))
        {
            foreach (Match m in Regex.Matches(text, $@"\b(?:Accent|Color)\s*\.\s*(?<m>{member})\b(?!\s*=>)")) uses.Add((path, m.Groups["m"].Index, m.Groups["m"].Length, "member"));
            foreach (Match m in Regex.Matches(text, $@"\bToken\(\s*""(?<n>{Regex.Escape(name)})""\s*\)")) uses.Add((path, m.Groups["n"].Index, m.Groups["n"].Length, "token"));
        }
        foreach (var path in Stylesheets(project))
        {
            var text = project.ReadFile(path) ?? "";
            foreach (Match m in Regex.Matches(text, $@"var\(\s*--ib-color-(?:on-)?(?<n>{Regex.Escape(name)})(?![\w-])")) uses.Add((path, m.Groups["n"].Index, m.Groups["n"].Length, "var"));
        }
        return uses;
    }

    // The app's own stylesheets: under wwwroot and beside its components (.razor.css).
    static IEnumerable<string> Stylesheets(ProjectInfo project) =>
        Directory.EnumerateFiles(project.Directory!, "*.css", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true })
            .Select(f => Path.GetRelativePath(project.Directory!, f).Replace('\\', '/'))
            .Where(p => !p.Split('/').Any(part => part is "bin" or "obj" or "node_modules" or "lib" || part.StartsWith('.')))
            .Order(StringComparer.Ordinal);

    /// <summary>
    /// The changes that rename the token everywhere the app has it: its declarations in theme.css, its
    /// members and their Token("…") calls, and its uses in the app's code and stylesheets.
    /// </summary>
    public static Changeset Rename(ProjectInfo project, string from, string to)
    {
        var path = Stylesheet(project);
        var css = project.ReadFile(path) ?? throw new UsageException($"The app has no {path}, so no tokens of its own to rename.");
        var declared = Declared(css);
        if (!declared.ContainsKey(from)) throw new UsageException($"{path} has no --ib-color-{from}. Its tokens: {Names(declared)}.");
        if (declared.ContainsKey(to)) throw new UsageException($"{path} already has --ib-color-{to}.");
        var edits = new Dictionary<string, List<(int Offset, int Length, string Text)>>(StringComparer.Ordinal);
        void Edit(string file, int offset, int length, string text) =>
            (edits.TryGetValue(file, out var list) ? list : edits[file] = []).Add((offset, length, text));

        foreach (Match m in Regex.Matches(css, $@"--ib-color-(?:on-)?(?<n>{Regex.Escape(from)})(?![\w-])(?=\s*:)")) Edit(path, m.Groups["n"].Index, m.Groups["n"].Length, to);
        var (oldMember, newMember) = (Member(from), Member(to));
        foreach (var (file, text) in Sources(project))
            foreach (Match m in Regex.Matches(text, $@"\b(?:Accent|Color)\s+(?<m>{oldMember})\s*=>")) Edit(file, m.Groups["m"].Index, m.Groups["m"].Length, newMember);
        foreach (var (file, offset, length, kind) in Uses(project, from))
            Edit(file, offset, length, kind == "member" ? newMember : to);

        var inserts = new List<TextInsert>();
        foreach (var (file, list) in edits)
        {
            var hash = Inserts.Hash(project.ReadFile(file)!);
            foreach (var (offset, length, text) in list.Distinct()) inserts.Add(new TextInsert(file, offset, text, hash) { Length = length });
        }
        return new Changeset { Creates = [], Inserts = inserts, Packages = [], Conflicts = [], Warnings = [] };
    }

    /// <summary>The changes that take the token out: its declarations and members. Refused while the app uses it.</summary>
    public static Changeset Remove(ProjectInfo project, string name)
    {
        var path = Stylesheet(project);
        var css = project.ReadFile(path) ?? throw new UsageException($"The app has no {path}, so no tokens of its own to remove.");
        var declared = Declared(css);
        if (!declared.ContainsKey(name)) throw new UsageException($"{path} has no --ib-color-{name}. Its tokens: {Names(declared)}.");
        var member = Member(name);
        var uses = Uses(project, name).Where(u => !IsDefinition(project, u, member)).ToList();
        if (uses.Count > 0)
            throw new UsageException($"The app still uses {name}, so nothing was removed: {string.Join(", ", uses.Select(u => Where(project, u.Path, u.Offset)).Distinct())}. Change those first, or rename it with izzy rename token.");

        var inserts = new List<TextInsert>();
        var hash = Inserts.Hash(css);
        foreach (Match m in Regex.Matches(css, $@"^[ \t]*--ib-color-(?:on-)?{Regex.Escape(name)}\s*:[^;]*;[^\S\n]*\r?\n?", RegexOptions.Multiline))
            inserts.Add(new TextInsert(path, m.Index, "", hash) { Length = m.Length });
        foreach (var (file, text) in Sources(project))
        {
            var lines = Regex.Matches(text, $@"^[ \t]*public\s+static\s+(?:Accent|Color)\s+{member}\s*=>[^;]*;[^\S\n]*\r?\n?", RegexOptions.Multiline);
            foreach (Match m in lines) inserts.Add(new TextInsert(file, m.Index, "", Inserts.Hash(text)) { Length = m.Length });
        }
        return new Changeset { Creates = [], Inserts = inserts, Packages = [], Conflicts = [], Warnings = [] };
    }

    // A Token("promo") in the member that defines it is the definition, not a use.
    static bool IsDefinition(ProjectInfo project, (string Path, int Offset, int Length, string Kind) use, string member)
    {
        if (use.Kind != "token") return false;
        var text = project.ReadFile(use.Path) ?? "";
        var lineStart = text.LastIndexOf('\n', Math.Max(0, use.Offset - 1)) + 1;
        return Regex.IsMatch(text[lineStart..use.Offset], $@"\bstatic\s+(?:Accent|Color)\s+{member}\s*=>");
    }

    static string Where(ProjectInfo project, string path, int offset)
    {
        var text = project.ReadFile(path) ?? "";
        return $"{path}:{text[..offset].Count(c => c == '\n') + 1}";
    }

    static string Names(Dictionary<string, (int, int)> declared) =>
        declared.Keys.Where(k => !k.StartsWith("on-", StringComparison.Ordinal)) is var names && names.Any() ? string.Join(", ", names) : "none";

    /// <summary>The WCAG contrast of two hex colors; null when either isn't one.</summary>
    public static double? Contrast(string a, string b) =>
        Luminance(a) is { } x && Luminance(b) is { } y ? (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05) : null;

    static double? Luminance(string hex)
    {
        if (!IsHex(hex)) return null;
        var h = hex[1..];
        if (h.Length == 3) h = string.Concat(h.Select(c => new string(c, 2)));
        double Channel(int i)
        {
            var c = int.Parse(h.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(0) + 0.7152 * Channel(1) + 0.0722 * Channel(2);
    }

    public static bool IsHex(string value) => HexColor().IsMatch(value);

    [GeneratedRegex(@"^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$")]
    private static partial Regex TokenName();

    [GeneratedRegex(@"^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")]
    private static partial Regex HexColor();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comments();

    [GeneratedRegex(@"(?<![\w-])(?<name>--ib-[a-z0-9-]+)\s*:(?<value>[^;{}]*)")]
    private static partial Regex Declaration();
}
