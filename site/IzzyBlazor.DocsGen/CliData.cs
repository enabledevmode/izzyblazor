using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace IzzyBlazor.DocsGen;

// The components as the izzy CLI reads them (components.json beside its catalog): what
// izzyblazor.api.json holds, trimmed to plain text, with each documented component's category,
// docs page and examples from the site's registry, and every example's source. The CLI has no
// manifest per component; this file is made from what the docs already hold.

/// <summary>The whole file.</summary>
public sealed record CliFile
{
    public required string Version { get; init; }

    /// <summary>The docs' groups, in their order.</summary>
    public required List<string> Categories { get; init; }

    /// <summary>The shared bases' parameters, recorded once; a component names the ones it takes.</summary>
    public required List<CliBase> Bases { get; init; }
    public required List<CliComponent> Components { get; init; }

    /// <summary>Every example a component lists, by id (<c>button-usage</c>).</summary>
    public required SortedDictionary<string, CliExample> Examples { get; init; }
}

public sealed record CliBase(string Name, List<CliParameter> Parameters);

/// <summary>A component, or a part of one (<c>DataGridColumn</c>).</summary>
public sealed record CliComponent
{
    public required string Name { get; init; }

    /// <summary>The name with its type parameters, as the docs write it: <c>Select&lt;TValue&gt;</c>.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Its docs id (<c>data-grid</c>); null for a part and for a component with no page.</summary>
    public string? Id { get; init; }

    /// <summary>For a part, the id of the component whose page documents it.</summary>
    public string? PartOf { get; init; }
    public string? Category { get; init; }
    public string? Summary { get; init; }
    public string? Docs { get; init; }
    public CliModes? Modes { get; init; }
    public List<string>? TypeParameters { get; init; }
    public required List<string> Bases { get; init; }
    public required List<CliParameter> Parameters { get; init; }

    /// <summary>Render fragments: content, not options.</summary>
    public required List<CliMember> Slots { get; init; }
    public required List<CliMember> Events { get; init; }
    public List<string>? Parts { get; init; }
    public List<CliExampleRef>? Examples { get; init; }
}

public sealed record CliModes(string Static, string Interactive, string? Notes);

public sealed record CliMember(string Name, string Type, string? Summary);

/// <summary>An option a tag can set.</summary>
public sealed record CliParameter
{
    public required string Name { get; init; }

    /// <summary>The type as C# writes it.</summary>
    public required string Type { get; init; }

    /// <summary>What a form draws for it: "bool", "enum", "string", "number", or "other" (shown with its type, edited as text).</summary>
    public required string Kind { get; init; }

    /// <summary>An enum's values.</summary>
    public List<string>? Values { get; init; }

    /// <summary>How an enum value is written in Razor: <c>ButtonVariant.{value}</c>.</summary>
    public string? Write { get; init; }
    public string? Default { get; init; }
    public string? Summary { get; init; }
    public bool Required { get; init; }
    public bool Bindable { get; init; }
    public bool Responsive { get; init; }
}

/// <summary>An example on a component's pages: <c>name</c> is what <c>--example</c> takes.</summary>
public sealed record CliExampleRef(string Id, string Name, string Title, bool InteractiveOnly);

/// <summary>An example's source, with the lines the docs hide (it doesn't run without them).</summary>
public sealed record CliExample(string File, string Source, List<CliExampleFile>? Files);

public sealed record CliExampleFile(string Name, string Source);

public static partial class CliData
{
    const string Site = "https://izzyblazor.com/docs/components/";

    static readonly JsonSerializerOptions Json = new(ApiFile.JsonOptions) { WriteIndented = false, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault };

    static readonly string[] NeedsSite = ["IzzyBlazor.Site", "./examples/", "Microsoft.Extensions.AI"];

    public static string Write(ApiFile api, string registryPath, string examplesRoot)
    {
        var registry = JsonNode.Parse(File.ReadAllText(registryPath), documentOptions: new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })!;
        var documented = registry["components"]!.AsArray().Select(c => c!.AsObject()).ToList();
        var byName = documented.ToDictionary(c => (string)c["name"]!, StringComparer.Ordinal);
        // A part two components list (MenuItem: Menu and ContextMenu) belongs to the one it's named for.
        var partOf = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var owners in documented.SelectMany(c => (c["parts"]?.AsArray() ?? []).Select(p => (Part: (string)p!, Owner: c))).GroupBy(p => p.Part))
        {
            static int Shared(string a, string b) => a.Zip(b).TakeWhile(p => p.First == p.Second).Count();
            partOf[owners.Key] = (string)owners.MaxBy(o => Shared(owners.Key, (string)o.Owner["name"]!)).Owner["id"]!;
        }

        var enums = api.Types.Where(t => t.Kind == "enum").ToDictionary(t => t.Name, StringComparer.Ordinal);
        var sources = Sources(examplesRoot);
        var examples = new SortedDictionary<string, CliExample>(StringComparer.Ordinal);

        var components = new List<CliComponent>();
        foreach (var c in api.Components.Where(c => c.Obsolete is null).OrderBy(c => c.Name, StringComparer.Ordinal))
        {
            byName.TryGetValue(c.Name, out var entry);
            var id = (string?)entry?["id"];
            List<CliExampleRef>? refs = null;
            if (entry is not null)
            {
                refs = [];
                var listed = (entry["examples"]?.AsArray() ?? []).Concat((entry["topics"]?.AsArray() ?? []).SelectMany(t => t!["examples"]?.AsArray() ?? []));
                foreach (var e in listed)
                {
                    var exampleId = (string)e!["id"]!;
                    // An example that uses the docs site's own types can't go into another app.
                    // So can't one that loads the site's own script, or needs a package the app may not have.
                    if (!sources.TryGetValue(exampleId, out var source) || NeedsSite.Any(mark => source.Source.Contains(mark, StringComparison.Ordinal))) continue;
                    examples[exampleId] = source;
                    var modes = e["modes"]?.AsArray().Select(m => (string)m!).ToList();
                    refs.Add(new CliExampleRef(exampleId, exampleId.StartsWith(id + "-", StringComparison.Ordinal) ? exampleId[(id!.Length + 1)..] : exampleId,
                        (string)e["title"]!, modes is ["interactive"]));
                }
            }

            components.Add(new CliComponent
            {
                Name = c.Name, DisplayName = c.DisplayName, Id = id, PartOf = id is null ? partOf.GetValueOrDefault(c.Name) : null,
                Category = (string?)entry?["category"],
                Summary = (string?)entry?["summary"] ?? Plain(c.Summary),
                Docs = id is not null ? Site + id : partOf.TryGetValue(c.Name, out var parent) ? Site + parent : null,
                Modes = c.Modes is null ? null : new CliModes(c.Modes.Static, c.Modes.Interactive, c.Modes.Notes),
                TypeParameters = c.TypeParameters,
                Bases = c.Bases,
                Parameters = Parameters(c.Properties, enums),
                Slots = c.Properties.Where(IsSlot).Select(Member).ToList(),
                Events = c.Events.Where(e => e.Obsolete is null).Select(Member).ToList(),
                Parts = entry?["parts"]?.AsArray().Select(p => (string)p!).ToList(),
                Examples = refs,
            });
        }

        var file = new CliFile
        {
            Version = api.Version,
            Categories = registry["categories"]!.AsArray().Select(c => (string)c!).ToList(),
            Bases = api.Bases.Select(b => new CliBase(b.Name, Parameters(b.Properties, enums))).ToList(),
            Components = components,
            Examples = examples,
        };
        return JsonSerializer.Serialize(file, Json);
    }

    static List<CliParameter> Parameters(List<ApiMember> properties, Dictionary<string, ApiType> enums) =>
        properties.Where(p => p.Obsolete is null && !IsSlot(p) && !p.Type.StartsWith("EventCallback", StringComparison.Ordinal)).Select(p =>
        {
            var type = p.Type.TrimEnd('?');
            // Responsive<ButtonSize> takes one value or one per breakpoint: a form offers the one.
            var inner = Regex.Match(type, @"^Responsive<(.+)>$") is { Success: true } m ? m.Groups[1].Value.TrimEnd('?') : type;
            enums.TryGetValue(inner, out var values);
            return new CliParameter
            {
                Name = p.Name, Type = p.Type,
                Kind = values is not null ? "enum" : inner switch
                {
                    "bool" => "bool",
                    "string" => "string",
                    "int" or "long" or "double" or "decimal" or "float" => "number",
                    _ => "other",
                },
                Values = values?.Members.Where(v => v.Obsolete is null).Select(v => v.Name).ToList(),
                Write = values is null ? null : inner + ".{value}",
                Default = p.Default, Summary = Plain(p.Summary),
                Required = p.Required, Bindable = p.Bindable, Responsive = p.Responsive,
            };
        }).ToList();

    static bool IsSlot(ApiMember member) => member.Type.StartsWith("RenderFragment", StringComparison.Ordinal);

    static CliMember Member(ApiMember member) => new(member.Name, member.Type, Plain(member.Summary));

    /// <summary>A doc comment's HTML as one line of text.</summary>
    static string? Plain(string? html) =>
        html is null ? null : Spaces().Replace(WebUtility.HtmlDecode(Tags().Replace(BlockTags().Replace(html, " "), "")), " ").Trim();

    /// <summary>Stands for the namespace of the page an example lands on, in a <c>@using Alias = ...</c> line.</summary>
    public const string PageNamespace = "__PageNamespace__";

    // Every example under the folder, by the id the site gives it: Examples/Button/MultiSubmit.razor
    // is button-multi-submit. An example made to go into another app: with the lines the docs hide, the
    // sample-data files it uses (named by a file line, used by type name, or reached by a using of
    // another example's folder), and the using the docs site gives every page for 24.Rem.
    static Dictionary<string, CliExample> Sources(string root)
    {
        // The .cs files beside the examples, by folder, each with the types it declares.
        var beside = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Select(path => (Folder: Path.GetFileName(Path.GetDirectoryName(path)!), Name: Path.GetFileName(path),
                Source: File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal)))
            .Select(f => (f.Folder, f.Name, f.Source, Types: TypeName().Matches(f.Source).Select(m => m.Groups[1].Value).ToList()))
            .ToList();

        var sources = new Dictionary<string, CliExample>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(root, "*.razor", SearchOption.AllDirectories))
        {
            var folder = Path.GetRelativePath(root, Path.GetDirectoryName(path)!).Replace('\\', '/');
            if (folder == ".") continue;
            var raw = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
            var files = new List<CliExampleFile>();
            var declared = new List<string>();
            var clash = false;
            void Take(string from, string name)
            {
                if (files.Any(f => f.Name == name)) return;
                // Two files that declare one name (an OrderStatus each) can't share the page's namespace.
                if (beside.FirstOrDefault(f => f.Folder == from && f.Name == name) is { Types: not null } taken)
                {
                    clash |= taken.Types.Any(declared.Contains);
                    declared.AddRange(taken.Types);
                }
                // The docs site imports IzzyBlazor for all its code; another app's .cs files may not.
                if (beside.FirstOrDefault(f => f.Folder == from && f.Name == name) is { Source: not null } file)
                    files.Add(new CliExampleFile(name, file.Source.Contains("using IzzyBlazor;", StringComparison.Ordinal) ? file.Source : "using IzzyBlazor;\n\n" + file.Source));
            }
            foreach (Match file in FileLine().Matches(raw)) Take(folder, file.Groups[1].Value);

            var source = new StringBuilder();
            var froms = new List<string>();
            foreach (var line in raw.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed is "@* hide *@" or "@* /hide *@" || FileLine().IsMatch(trimmed)) continue;
                // @using IzzyBlazor.Site.Examples.Table: that folder's types come along, into the page's namespace.
                if (SiteUsing().Match(trimmed) is { Success: true } used)
                {
                    var from = used.Groups["folder"].Value;
                    var type = used.Groups["type"].Value;
                    foreach (var f in beside.Where(f => f.Folder == from && (type.Length == 0 ? f.Types.Any(t => Regex.IsMatch(raw, $@"\b{Regex.Escape(t)}\b")) : f.Types.Contains(type))))
                        Take(from, f.Name);
                    if (used.Groups["alias"].Success) source.Append($"@using {used.Groups["alias"].Value} = {PageNamespace}.{type}\n");
                    froms.Add(from);
                    continue;
                }
                source.Append(line).Append('\n');
            }
            var text = source.ToString().Trim('\n') + "\n";
            // Table.OrderStatus, a name by its folder on the site, is OrderStatus in the page's namespace.
            foreach (var from in froms)
                foreach (var type in beside.Where(f => f.Folder == from && files.Any(taken => taken.Name == f.Name)).SelectMany(f => f.Types))
                    text = Regex.Replace(text, $@"(?<![\w.]){Regex.Escape(from)}\.{Regex.Escape(type)}\b", type);
            // The example's own folder: a file with a type it names that no file taken already declares.
            foreach (var f in beside.Where(f => f.Folder == folder && f.Types.Any(t => !declared.Contains(t) && Regex.IsMatch(text, $@"\b{Regex.Escape(t)}\b"))).ToList()) Take(folder, f.Name);
            if (clash) text += "@* IzzyBlazor.Site *@\n";
            // 24.Rem: the docs site imports the unit shorthands for every page.
            if (UnitShorthand().IsMatch(text) && !text.Contains("@using IzzyBlazor.Units", StringComparison.Ordinal)) text = "@using IzzyBlazor.Units\n" + text;
            // A file that needs the docs site beyond its own namespace line can't travel: the mark keeps the example out.
            if (files.Any(f => NamespaceLine().Replace(f.Source, "").Contains("IzzyBlazor.Site", StringComparison.Ordinal))) text += "@* IzzyBlazor.Site *@\n";
            var id = $"{Kebab(folder.Replace('/', '-'))}-{Kebab(Path.GetFileNameWithoutExtension(path))}";
            sources[id] = new CliExample(Path.GetFileName(path), text, files.Count > 0 ? files : null);
        }
        return sources;
    }

    static string Kebab(string name) => WordStart().Replace(name, "-").ToLowerInvariant();

    // As the site's ExampleCatalog: InARow is in-a-row, CsvExport csv-export, CSVExport csv-export.
    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")]
    private static partial Regex WordStart();

    [GeneratedRegex(@"^\s*@\* file: (\S+\.cs) \*@\s*$", RegexOptions.Multiline)]
    private static partial Regex FileLine();

    [GeneratedRegex(@"\b(?:class|record|struct|enum|interface)\s+(\w+)")]
    private static partial Regex TypeName();

    [GeneratedRegex(@"^@using\s+(?:(?<alias>\w+)\s*=\s*)?IzzyBlazor\.Site\.Examples\.(?<folder>\w+)(?:\.(?<type>\w+))?\s*$")]
    private static partial Regex SiteUsing();

    [GeneratedRegex(@"\b\d+(?:\.\d+)?\.[A-Z][a-z]+\b")]
    private static partial Regex UnitShorthand();

    [GeneratedRegex(@"^namespace [\w.]+;", RegexOptions.Multiline)]
    private static partial Regex NamespaceLine();

    [GeneratedRegex("</?(p|li|ul|ol|br|div|pre)[^>]*>")]
    private static partial Regex BlockTags();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
