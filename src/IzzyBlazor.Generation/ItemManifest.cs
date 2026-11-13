using System.Text.Json;
using System.Text.Json.Nodes;

namespace IzzyBlazor.Generation;

/// <summary>What an item is and how it's generated: the contents of its <c>izzy.json</c>.</summary>
public sealed class ItemManifest
{
    /// <summary>The id the CLI and <c>dotnet new</c> know it by, such as <c>izzy-blazor</c>.</summary>
    public required string Id { get; init; }

    /// <summary>The name shown in lists and forms.</summary>
    public required string Name { get; init; }

    /// <summary>What it creates: a whole project, a page, a block on a page, and so on.</summary>
    public required ItemKind Kind { get; init; }

    /// <summary>The catalogue group, such as Starters or Marketing.</summary>
    public string? Category { get; init; }

    /// <summary>Free items run in the CLI; Pro items run on IzzyBlazor's server.</summary>
    public ItemTier Tier { get; init; } = ItemTier.Free;

    /// <summary>One sentence on what it's for.</summary>
    public string? Summary { get; init; }

    /// <summary>The IzzyBlazor components it uses.</summary>
    public IReadOnlyList<string> Components { get; init; } = [];

    /// <summary>Support per render mode (Static SSR, Server, WebAssembly, Auto), as the docs and the MCP mode check read it.</summary>
    public IReadOnlyDictionary<string, string> Modes { get; init; } = new Dictionary<string, string>();

    /// <summary>The settings, as a JSON Schema object; the IDE forms and the MCP tools take it as is.</summary>
    public JsonObject Schema { get; init; } = new() { ["type"] = "object", ["properties"] = new JsonObject() };

    /// <summary>The settings, read from <see cref="Schema"/>, in the order written.</summary>
    public IReadOnlyList<ItemParameter> Parameters { get; init; } = [];

    /// <summary>Values worked out from the settings, by name, each a condition expression.</summary>
    public IReadOnlyDictionary<string, string> Computed { get; init; } = new Dictionary<string, string>();

    /// <summary>Files left out when a condition holds.</summary>
    public IReadOnlyList<ExcludeRule> Exclude { get; init; } = [];

    /// <summary>NuGet packages the item needs.</summary>
    public IReadOnlyList<PackageSpec> Packages { get; init; } = [];

    /// <summary>Text added to files: ones this item creates, or ones already in the project.</summary>
    public IReadOnlyList<InsertSpec> Inserts { get; init; } = [];

    /// <summary>Paths moved when a condition holds, applied in order before tokens (a starter's folder layout per mode).</summary>
    public IReadOnlyList<RenameRule> Rename { get; init; } = [];

    /// <summary>When packages are restored after a starter is made: a condition over the settings, or null for never.</summary>
    public string? Restore { get; init; }

    /// <summary>Things to tell the user about the project as it is, such as a page that already has an h1.</summary>
    public IReadOnlyList<CheckSpec> Checks { get; init; } = [];

    /// <summary>What the item does for accessibility, one point each, for its docs page.</summary>
    public IReadOnlyList<string> Accessibility { get; init; } = [];

    /// <summary>
    /// The settings its docs page previews, each a switch: every combination of their options is a preview.
    /// Generation ignores them.
    /// </summary>
    public IReadOnlyList<VariationSpec> Variations { get; init; } = [];

    /// <summary>Settings every preview uses, such as a heading level that fits the docs page. Generation ignores them.</summary>
    public IReadOnlyDictionary<string, string> PreviewSettings { get; init; } = new Dictionary<string, string>();

    /// <summary>Reads a manifest from the text of an <c>izzy.json</c>.</summary>
    public static ItemManifest Parse(string json)
    {
        var root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })
            as JsonObject ?? throw new GenerationException("izzy.json must hold a JSON object.");

        var schema = root["parameters"] as JsonObject ?? new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() };
        return new ItemManifest
        {
            Id = Required(root, "id"),
            Name = Required(root, "name"),
            Kind = ParseEnum<ItemKind>(Required(root, "kind"), "kind"),
            Category = (string?)root["category"],
            Tier = root["tier"] is { } tier ? ParseEnum<ItemTier>((string)tier!, "tier") : ItemTier.Free,
            Summary = (string?)root["summary"],
            Components = (root["components"] as JsonArray)?.Select(c => (string)c!).ToList() ?? [],
            Modes = (root["modes"] as JsonObject)?.ToDictionary(p => p.Key, p => (string)p.Value!) ?? new Dictionary<string, string>(),
            Schema = schema,
            Parameters = ItemParameter.FromSchema(schema),
            Computed = (root["computed"] as JsonObject)?.ToDictionary(p => p.Key, p => (string)p.Value!) ?? new Dictionary<string, string>(),
            Exclude = (root["exclude"] as JsonArray)?.Select(e => new ExcludeRule(
                Required(e!.AsObject(), "if"),
                (e["paths"] as JsonArray)?.Select(p => (string)p!).ToList() ?? [])).ToList() ?? [],
            Packages = (root["packages"] as JsonArray)?.Select(p => new PackageSpec(
                Required(p!.AsObject(), "id"), Required(p.AsObject(), "version"), (string?)p["if"])).ToList() ?? [],
            Inserts = (root["inserts"] as JsonArray)?.Select(i => InsertSpec.Parse(i!.AsObject())).ToList() ?? [],
            Rename = (root["rename"] as JsonArray)?.Select(r => new RenameRule(
                Required(r!.AsObject(), "if"),
                (r["paths"] as JsonObject)?.ToDictionary(p => p.Key, p => (string)p.Value!) ?? new Dictionary<string, string>())).ToList() ?? [],
            Restore = (string?)root["restore"],
            Checks = (root["checks"] as JsonArray)?.Select(c => new CheckSpec(
                (string?)c!["if"], Required(c.AsObject(), "file"), Required(c.AsObject(), "pattern"), Required(c.AsObject(), "message"))).ToList() ?? [],
            Accessibility = (root["accessibility"] as JsonArray)?.Select(a => (string)a!).ToList() ?? [],
            Variations = (root["variations"] as JsonArray)?.Select(v => VariationSpec.Parse(v!.AsObject())).ToList() ?? [],
            PreviewSettings = (root["preview"] as JsonObject)?.ToDictionary(p => p.Key, p => (string)p.Value!) ?? new Dictionary<string, string>(),
        };
    }

    internal static string Required(JsonObject o, string name) =>
        (string?)o[name] is { Length: > 0 } value ? value : throw new GenerationException($"izzy.json needs \"{name}\".");

    static T ParseEnum<T>(string value, string name) where T : struct, Enum =>
        Enum.TryParse<T>(value, ignoreCase: true, out var result)
            ? result
            : throw new GenerationException($"izzy.json \"{name}\" is \"{value}\"; expected one of {string.Join(", ", Enum.GetNames<T>().Select(n => n.ToLowerInvariant()))}.");
}

/// <summary>What an item creates.</summary>
public enum ItemKind
{
    /// <summary>A whole project.</summary>
    Starter,
    /// <summary>A page in an existing project.</summary>
    Page,
    /// <summary>A part of a page.</summary>
    Block,
    /// <summary>A part of a page wired to data or services.</summary>
    Section,
    /// <summary>A worked pattern, with the files it touches.</summary>
    Recipe,
}

/// <summary>Who can generate an item.</summary>
public enum ItemTier
{
    /// <summary>Anyone; runs in the CLI.</summary>
    Free,
    /// <summary>A Pro key; runs on IzzyBlazor's server.</summary>
    Pro,
}

/// <summary>Files left out when <see cref="If"/> holds.</summary>
/// <param name="If">A condition expression over the settings.</param>
/// <param name="Paths">Globs over paths in the item's <c>files</c> folder: <c>*</c> within a folder, <c>**</c> across folders.</param>
public sealed record ExcludeRule(string If, IReadOnlyList<string> Paths);

/// <summary>A warning when a file in the project matches a pattern, such as a page that already has an h1.</summary>
/// <param name="If">A condition expression; checked only when it holds. Null always.</param>
/// <param name="File">The file, relative to the project folder; tokens allowed.</param>
/// <param name="Pattern">A regular expression; a match anywhere in the file gives the warning.</param>
/// <param name="Message">The warning; tokens allowed.</param>
public sealed record CheckSpec(string? If, string File, string Pattern, string Message);

/// <summary>Paths moved when <see cref="If"/> holds.</summary>
/// <param name="If">A condition expression over the settings.</param>
/// <param name="Paths">From → to: a folder (ending in <c>/</c>) moves everything under it, a file moves itself; <c>./</c> is the project folder.</param>
public sealed record RenameRule(string If, IReadOnlyDictionary<string, string> Paths);

/// <summary>A NuGet package the item adds.</summary>
/// <param name="Id">The package id.</param>
/// <param name="Version">The version to reference.</param>
/// <param name="If">A condition expression; the package is added only when it holds. Null adds it always.</param>
public sealed record PackageSpec(string Id, string Version, string? If);

/// <summary>
/// Text added to a file, beside the line that holds an anchor. Skipped when the file already holds the text, so
/// generating twice changes nothing; a file without the anchor gets a warning, not a guess.
/// </summary>
/// <param name="File">The file, relative to the project folder; tokens allowed.</param>
/// <param name="Anchor">Text a line holds, such as a marker comment (<c>@* izzy:nav *@</c>), or null with <paramref name="Pattern"/>.</param>
/// <param name="Pattern">A regular expression a line matches, when there's no marker to look for.</param>
/// <param name="Position">Whether the text goes before or after that line.</param>
/// <param name="Text">The text, with tokens; it takes the file's line endings.</param>
/// <param name="If">A condition expression; the insert happens only when it holds. Null always.</param>
public sealed record InsertSpec(string File, string? Anchor, string? Pattern, InsertPosition Position, string Text, string? If)
{
    internal static InsertSpec Parse(JsonObject o)
    {
        var anchor = (string?)o["anchor"];
        var pattern = (string?)o["pattern"];
        if (anchor is null == pattern is null) throw new GenerationException("An insert needs \"anchor\" or \"pattern\", not both.");
        var position = (string?)o["position"] ?? "before";
        return new InsertSpec(
            ItemManifest.Required(o, "file"), anchor, pattern,
            position switch
            {
                "before" => InsertPosition.Before,
                "after" => InsertPosition.After,
                _ => throw new GenerationException($"An insert's \"position\" is before or after, not \"{position}\"."),
            },
            ItemManifest.Required(o, "text"), (string?)o["if"]);
    }
}

/// <summary>Where inserted text goes, next to the anchor's line.</summary>
public enum InsertPosition
{
    /// <summary>On the lines above it.</summary>
    Before,
    /// <summary>On the lines below it.</summary>
    After,
}

/// <summary>A switch on an item's docs page: a setting and the values it's previewed with.</summary>
/// <param name="Key">The switch's name in the address, such as <c>layout</c>.</param>
/// <param name="Label">Its label, such as "Layout".</param>
/// <param name="Setting">The setting it sets.</param>
/// <param name="Options">Each option's label and the setting's value; the first is shown first.</param>
public sealed record VariationSpec(string Key, string Label, string Setting, IReadOnlyList<VariationOption> Options)
{
    internal static VariationSpec Parse(JsonObject o) => new(
        ItemManifest.Required(o, "key"),
        ItemManifest.Required(o, "label"),
        ItemManifest.Required(o, "setting"),
        (o["options"] as JsonObject)?.Select(p => new VariationOption(p.Key, (string?)p.Value ?? "")).ToList()
            ?? throw new GenerationException($"izzy.json: the variation \"{(string?)o["key"]}\" needs \"options\"."));
}

/// <summary>One option of a <see cref="VariationSpec"/>.</summary>
/// <param name="Label">What the switch shows, such as "Split".</param>
/// <param name="Value">The setting's value, such as <c>Split</c>, or empty text to leave a part out.</param>
public sealed record VariationOption(string Label, string Value)
{
    /// <summary>The option in the address, such as <c>split</c>.</summary>
    public string Slug => Label.ToLowerInvariant().Replace(' ', '-');
}

