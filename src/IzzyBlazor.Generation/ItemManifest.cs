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
        };
    }

    static string Required(JsonObject o, string name) =>
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
/// <param name="Paths">Paths in the item's <c>files</c> folder, as written there.</param>
public sealed record ExcludeRule(string If, IReadOnlyList<string> Paths);

/// <summary>A NuGet package the item adds.</summary>
/// <param name="Id">The package id.</param>
/// <param name="Version">The version to reference.</param>
/// <param name="If">A condition expression; the package is added only when it holds. Null adds it always.</param>
public sealed record PackageSpec(string Id, string Version, string? If);
