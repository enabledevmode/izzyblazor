using System.Text.Json;
using System.Text.Json.Serialization;

namespace IzzyBlazor.Site.Services;

/// <summary>
/// components.json, the hand-kept list the site is organized by: each documented component's id,
/// category and one-line summary, the parts documented on its page, its examples and demo
/// scenarios, and what works in each mode feature by feature.
/// </summary>
public sealed class Registry
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly Dictionary<string, RegistryComponent> _byId;
    private readonly Dictionary<string, RegistryComponent> _byName;
    private readonly Dictionary<string, RegistryComponent> _byPart;
    private readonly Dictionary<string, (RegistryComponent Component, RegistryExample Example)> _examples;

    public Registry(RegistryFile file)
    {
        Categories = file.Categories;
        AllComponents = Categories.SelectMany(c => file.Components.Where(x => x.Category == c)).ToList();
        Components = AllComponents.Where(c => !c.Preview).ToList();
        var stray = file.Components.Where(c => !Categories.Contains(c.Category)).Select(c => c.Id).ToList();
        if (stray.Count > 0)
        {
            throw new InvalidOperationException($"components.json: unknown category for {string.Join(", ", stray)}.");
        }

        _byId = AllComponents.ToDictionary(c => c.Id, StringComparer.Ordinal);
        _byName = AllComponents.ToDictionary(c => c.Name, StringComparer.Ordinal);
        _byPart = AllComponents.SelectMany(c => c.Parts.Select(p => (Part: p, Component: c))).ToDictionary(x => x.Part, x => x.Component, StringComparer.Ordinal);
        _examples = AllComponents
            .SelectMany(c => c.Examples.Concat(c.Scenarios.Select(s => s.Example)).Select(e => (Component: c, Example: e)))
            .ToDictionary(x => x.Example.Id, StringComparer.Ordinal);
    }

    public IReadOnlyList<string> Categories { get; }

    /// <summary>Every listed component, in category order, then file order.</summary>
    public IReadOnlyList<RegistryComponent> Components { get; }

    /// <summary>The listed components and the previews, whose pages work but nothing links to.</summary>
    public IReadOnlyList<RegistryComponent> AllComponents { get; }

    /// <summary>The component count rounded down to ten, as "80+", for links and marketing copy.</summary>
    public string ComponentCountLabel => Components.Count < 10 ? $"{Components.Count}" : $"{Components.Count / 10 * 10}+";

    public static Registry Load(ApiMetadata api)
    {
        using var stream = typeof(Registry).Assembly.GetManifestResourceStream("components.json")
            ?? throw new InvalidOperationException("components.json isn't embedded.");
        var registry = new Registry(JsonSerializer.Deserialize<RegistryFile>(stream, Options)!);
        api.ApiHref = registry.ApiHref;
        api.OverviewHref = name => registry.ByName(name) is { } component ? OverviewHref(component) : null;
        return registry;
    }

    public RegistryComponent? ById(string id) => _byId.GetValueOrDefault(id);

    public RegistryComponent? ByName(string name) => _byName.GetValueOrDefault(name);

    /// <summary>The component whose page documents this part (<c>CardHeader</c> is on Card's).</summary>
    public RegistryComponent? OwnerOfPart(string name) => _byPart.GetValueOrDefault(name);

    public IEnumerable<RegistryComponent> InCategory(string category) => Components.Where(c => c.Category == category);

    public (RegistryComponent Component, RegistryExample Example)? Example(string id) =>
        _examples.TryGetValue(id, out var found) ? found : null;

    public IEnumerable<(RegistryComponent Component, RegistryExample Example)> AllExamples => _examples.Values;

    public static string OverviewHref(RegistryComponent component) => $"docs/components/{component.Id}";

    public static string ApiPageHref(RegistryComponent component) => $"docs/components/{component.Id}/api";

    /// <summary>Where a component's Demos tab goes: its first scenario (it has no demos page of its own).</summary>
    public static string DemosHref(RegistryComponent component) => ScenarioHref(component, component.Scenarios[0]);

    public static string ScenarioHref(RegistryComponent component, RegistryScenario scenario) => $"demos/{component.Id}/{scenario.Id}";

    /// <summary>The API page (and anchor) for a component, a part or one of their members.</summary>
    public string? ApiHref(string type, string? member)
    {
        if (ByName(type) is { } component)
        {
            return member is null ? ApiPageHref(component) : $"{ApiPageHref(component)}#{ApiMetadata.Anchor(member)}";
        }

        if (OwnerOfPart(type) is { } owner)
        {
            var part = ApiMetadata.Anchor(type);
            return member is null ? $"{ApiPageHref(owner)}#{part}" : $"{ApiPageHref(owner)}#{part}-{ApiMetadata.Anchor(member)}";
        }

        return null;
    }

    /// <summary>The components before and after this one, for the links at the end of its pages.</summary>
    public (RegistryComponent? Previous, RegistryComponent? Next) Around(RegistryComponent component)
    {
        var index = Components.ToList().IndexOf(component);
        if (index < 0)
        {
            return (null, null);
        }

        return (index > 0 ? Components[index - 1] : null, index < Components.Count - 1 ? Components[index + 1] : null);
    }
}

public sealed record RegistryFile
{
    public required List<string> Categories { get; init; }
    public required List<RegistryComponent> Components { get; init; }
}

public sealed record RegistryComponent
{
    /// <summary>The URL segment: <c>button</c>, <c>app-shell</c>.</summary>
    public required string Id { get; init; }

    /// <summary>The component's type name, as in the API metadata.</summary>
    public required string Name { get; init; }
    public required string Category { get; init; }

    /// <summary>A Lucide icon's name (<c>MousePointerClick</c>), the component's pictogram on cards and its page.</summary>
    public required string Icon { get; init; }

    /// <summary>Not ready: its pages work by address, but the index, search, sitemap and MCP leave it out.</summary>
    public bool Preview { get; init; }

    /// <summary>One line for cards and the index.</summary>
    public required string Summary { get; init; }

    /// <summary>Other names people search for it by (<c>modal</c> for Dialog), matched like its name.</summary>
    public List<string> Keywords { get; init; } = [];

    /// <summary>Components documented on this one's page (<c>CardHeader</c>, <c>CardFooter</c>).</summary>
    public List<string> Parts { get; init; } = [];

    /// <summary>What works in each mode, a row per feature, for the Overview's table.</summary>
    public List<RegistryFeature> Features { get; init; } = [];
    public List<RegistryExample> Examples { get; init; } = [];
    public List<RegistryScenario> Scenarios { get; init; } = [];
}

public sealed record RegistryFeature
{
    public required string Feature { get; init; }

    /// <summary>
    /// A member whose <c>[ModeSupport]</c> gives the row's levels (<c>OnClick</c>, or a bindable's
    /// <c>@bind</c>), so they aren't written twice. Without one, the row's own levels.
    /// </summary>
    public string? Member { get; init; }

    /// <summary>With no script: the fallback, shown under "Without JavaScript", not as a mode.</summary>
    [JsonPropertyName("noscript")]
    public ModeLevel NoScript { get; init; } = ModeLevel.Full;
    public ModeLevel Static { get; init; } = ModeLevel.Full;
    public ModeLevel Interactive { get; init; } = ModeLevel.Full;
}

public sealed record RegistryExample
{
    /// <summary><c>button-variants</c>: the example in <c>Examples/Button/Variants.razor</c>.</summary>
    public required string Id { get; init; }
    public required string Title { get; init; }

    /// <summary>
    /// One sentence a generated Overview shows above the example, leading with the prop ("Use
    /// `Status` for the meaning."). Backticks mark code. Hand-written Overviews write their own.
    /// </summary>
    public string? Lead { get; init; }

    /// <summary>A demo's (<see cref="Page"/>) window height in pixels; it scrolls inside past that.</summary>
    public int Height { get; init; } = 600;

    /// <summary>The modes it's shown in. Defaults to all three.</summary>
    public List<string> Modes { get; init; } = ["noscript", "static", "interactive"];

    /// <summary>The mode it opens in. Defaults to Static where it's shown, else the first of <see cref="Modes"/>.</summary>
    public string? Mode { get; init; }

    /// <summary>Notes under the preview per mode, in place of the general ones.</summary>
    public Dictionary<string, string> Notes { get; init; } = [];

    /// <summary>
    /// A page of its own (a demo scenario), sized by the window: shown in a frame <see cref="Height"/>
    /// tall, rather than in the docs page as a centered sample.
    /// </summary>
    public bool Page { get; init; }

    public string StartMode => Mode ?? (Modes.Contains("static") ? "static" : Modes[0]);
}

public sealed record RegistryScenario
{
    public required string Id { get; init; }
    public required string Title { get; init; }

    /// <summary>A sentence saying what it shows.</summary>
    public required string Description { get; init; }
    public required RegistryExample Example { get; init; }

    /// <summary>The API member the scenario is about, for the link back.</summary>
    public string? Member { get; init; }
}
