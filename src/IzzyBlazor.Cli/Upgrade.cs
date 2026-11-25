using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using IzzyBlazor.Generation;

namespace IzzyBlazor.Cli;

/// <summary>
/// Something a release removed or renamed: a component, a base or a type, or one member of it.
/// </summary>
/// <param name="Release">The release that made the change.</param>
/// <param name="Of">What <paramref name="Name"/> is: component, base or type.</param>
/// <param name="Member">The parameter, enum member or other member; null when the whole of it changed.</param>
/// <param name="To">The new name, when the change is a plain rename.</param>
/// <param name="Note">What to do instead, when there is more to it than a new name.</param>
sealed record ReleaseChange(string Release, string Of, string Name, string? Member, string? To, string? Note)
{
    public string Subject => Member is null ? Name : $"{Name}.{Member}";

    /// <summary>One line for a person: Icon.Role → Purpose, or Step.Hidden: Use Visible…</summary>
    public string Line => (To, Note) switch
    {
        ({ } to, { } note) => $"{Subject} → {to}. {note}",
        ({ } to, null) => $"{Subject} → {to}",
        (null, { } note) => $"{Subject}: {note}",
        _ => $"{Subject} is removed",
    };

    public JsonObject Json()
    {
        var json = new JsonObject { ["release"] = Release, ["of"] = Of, ["name"] = Name };
        if (Member is not null) json["member"] = Member;
        if (To is not null) json["to"] = To;
        if (Note is not null) json["note"] = Note;
        return json;
    }
}

/// <summary>A place in the app that uses what a release changed, and the rename that mends it, if one does.</summary>
sealed record ChangeUse(ReleaseChange Change, string Path, int Line, int Column, TextInsert? Fix)
{
    /// <summary>The tag that closes a renamed component: renamed with its opening tag, and not a use of its own.</summary>
    public bool Closing => Line == 0;
}

/// <summary>
/// What each release removed or renamed (changes.json beside the tool, made as releases are cut), and where
/// an app's .razor and .cs files use it. A parameter is looked for as an attribute of its component's tags
/// (with @bind- before it, or Changed or Expression after); a type and its members as words, anywhere in a
/// file. A method is listed but not looked for: the build names it.
/// </summary>
sealed partial class ReleaseChanges(IReadOnlyList<ReleaseChange> all)
{
    /// <summary>The record beside the tool.</summary>
    public static string Beside => Path.Combine(AppContext.BaseDirectory, "changes.json");

    public static ReleaseChanges Load(string path)
    {
        if (!File.Exists(path)) throw new UsageException("This izzy has no record of what each release changed (changes.json is missing beside it). Reinstall the tool.");
        var releases = JsonNode.Parse(File.ReadAllText(path))!["releases"]!.AsArray();
        return new(releases.SelectMany(r => r!["changes"]!.AsArray().Select(c => new ReleaseChange(
            (string)r["version"]!, (string)c!["of"]!, (string)c["name"]!, (string?)c["member"], (string?)c["to"], (string?)c["note"]))).ToList());
    }

    /// <summary>The changes of the releases after <paramref name="from"/>, up to and with <paramref name="to"/>, oldest first.</summary>
    public List<ReleaseChange> Between(string from, string to) =>
        all.Where(c => Releases.Compare(c.Release, from) > 0 && Releases.Compare(c.Release, to) <= 0)
            .OrderBy(c => c.Release, Comparer<string>.Create(Releases.Compare)).ToList();

    /// <summary>The app's files to look in, from its folder.</summary>
    public static IEnumerable<string> Files(string root) =>
        Directory.EnumerateFiles(root, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true })
            .Where(f => f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Where(p => !p.Split('/').Any(part => part is "bin" or "obj" or "node_modules" || part.StartsWith('.')))
            .Order(StringComparer.Ordinal);

    /// <summary>Every use of the changes in one file, in the file's order.</summary>
    public static List<ChangeUse> Find(string path, string text, IReadOnlyList<ReleaseChange> changes, Components library)
    {
        var uses = new List<ChangeUse>();
        var hash = new Lazy<string>(() => Inserts.Hash(text));
        void Add(ReleaseChange change, int at, int length, string? replacement)
        {
            var (line, column) = Place(text, at);
            var fix = replacement is null ? null : new TextInsert(path, at, replacement, hash.Value) { Length = length };
            uses.Add(new ChangeUse(change, path, line, column, fix));
        }

        if (path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var tag in RazorPage.Tags(text))
            {
                var name = tag.Name[(tag.Name.LastIndexOf('.') + 1)..];
                foreach (var change in changes.Where(c => c.Of != "type" && Owns(c, name, library)))
                {
                    if (change.Member is null)
                    {
                        // The tag itself: its name here, and in the tag that closes it below.
                        if (change.Of == "component") Add(change, tag.NameEnd - name.Length, name.Length, change.To);
                        continue;
                    }
                    foreach (var attribute in tag.Attributes)
                    {
                        if (Part(attribute.Name, change.Member) is not { } member) continue;
                        Add(change, attribute.Start + member, change.Member.Length, change.To);
                    }
                }
            }
            foreach (var change in changes.Where(c => c is { Of: "component", Member: null, To: not null }))
            {
                var open = uses.Where(u => u.Change == change).Select(u => u.Fix!.Offset).ToHashSet();
                if (open.Count == 0) continue;
                foreach (Match close in Regex.Matches(text, $@"</((?:[\w.]+\.)?{Regex.Escape(change.Name)})\s*>"))
                {
                    var at = close.Groups[1].Index + close.Groups[1].Length - change.Name.Length;
                    uses.Add(new ChangeUse(change, path, 0, 0, new TextInsert(path, at, change.To!, hash.Value) { Length = change.Name.Length }));
                }
            }
        }

        var comments = path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) ? RazorPage.Comments(text) : [];
        // A type and its members, as code writes them: in a .cs file, an @code block or an attribute's value.
        foreach (var change in changes.Where(c => c.Of == "type" && (c.Member is null || Identifier().IsMatch(c.Member))))
        {
            var pattern = change.Member is null ? $@"(?<!\w){Regex.Escape(change.Name)}\b" : $@"(?<!\w){Regex.Escape(change.Name)}\s*\.\s*{Regex.Escape(change.Member)}\b";
            foreach (Match match in Regex.Matches(text, pattern))
            {
                // A Razor comment is left as it was written.
                if (comments.Any(c => match.Index >= c.Start && match.Index < c.End)) continue;
                var length = (change.Member ?? change.Name).Length;
                Add(change, match.Index + match.Length - length, length, change.To);
            }
        }
        return uses.OrderBy(u => u.Line).ThenBy(u => u.Column).ToList();
    }

    // Whether a tag by this name is the change's component, or a component on the change's base.
    static bool Owns(ReleaseChange change, string tag, Components library) =>
        change.Of == "component" ? change.Name == tag : library.HasBase(tag, change.Name);

    // Where the parameter's name starts in an attribute's name: Expanded, ExpandedChanged, ExpandedExpression,
    // @bind-Expanded and @bind-Expanded:after are all the parameter Expanded.
    static int? Part(string attribute, string member)
    {
        var start = attribute.StartsWith("@bind-", StringComparison.Ordinal) ? 6 : 0;
        var name = attribute[start..];
        if (name.IndexOf(':') is var colon and >= 0) name = name[..colon];
        return name == member || name == member + "Changed" || name == member + "Expression" ? start : null;
    }

    static (int Line, int Column) Place(string text, int offset)
    {
        var line = 1;
        var start = 0;
        for (var i = 0; i < offset; i++)
        {
            if (text[i] != '\n') continue;
            line++;
            start = i + 1;
        }
        return (line, offset - start + 1);
    }

    [GeneratedRegex(@"^\w+$")]
    private static partial Regex Identifier();
}
