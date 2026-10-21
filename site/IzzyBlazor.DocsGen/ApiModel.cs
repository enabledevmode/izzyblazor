using System.Text.Json;
using System.Text.Json.Serialization;

namespace IzzyBlazor.DocsGen;

// The shape of izzyblazor.api.json. DocsGen writes it; the docs site (which links this file),
// llms.txt and the MCP server read it. Text fields marked "html" hold HTML made from the XML doc
// comments, with links to other members written as href="cref:{doc id}" for the reader to resolve.

/// <summary>The whole file: every public component, the shared bases and the types they use.</summary>
public sealed record ApiFile
{
    public required string Version { get; init; }
    public required List<ApiComponent> Components { get; init; }
    public required List<ApiBase> Bases { get; init; }
    public required List<ApiType> Types { get; init; }

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}

/// <summary>A component: a public, non-abstract <c>IComponent</c>.</summary>
public sealed record ApiComponent
{
    public required string Name { get; init; }

    /// <summary>The name as written in Razor, with type parameters: <c>Select&lt;TValue&gt;</c>.</summary>
    public required string DisplayName { get; init; }
    public required string FullName { get; init; }
    public required string Package { get; init; }
    public required string Tier { get; init; }

    /// <summary>html</summary>
    public string? Summary { get; init; }

    /// <summary>html</summary>
    public string? Remarks { get; init; }

    /// <summary>Code from the doc comment's <c>example</c>, as plain text.</summary>
    public List<string>? Examples { get; init; }
    public List<string>? TypeParameters { get; init; }

    /// <summary>Shared bases whose props it takes (see <see cref="ApiFile.Bases"/>).</summary>
    public required List<string> Bases { get; init; }
    public ApiModes? Modes { get; init; }
    public required List<ApiMember> Properties { get; init; }
    public required List<ApiMember> Events { get; init; }
    public required List<ApiMember> Methods { get; init; }

    /// <summary>Names of the types its members use (see <see cref="ApiFile.Types"/>).</summary>
    public required List<string> Types { get; init; }
    public string? AddedIn { get; init; }
    public string? Obsolete { get; init; }
}

/// <summary>A base class whose props many components share, recorded once.</summary>
public sealed record ApiBase
{
    public required string Name { get; init; }

    /// <summary>html</summary>
    public string? Summary { get; init; }
    public required List<ApiMember> Properties { get; init; }
}

/// <summary>A parameter, event or method.</summary>
public sealed record ApiMember
{
    public required string Name { get; init; }

    /// <summary>The type as C# writes it (<c>string?</c>, <c>EventCallback&lt;MouseEventArgs&gt;</c>), or a method's signature.</summary>
    public required string Type { get; init; }

    /// <summary>The default as C#, or "computed" when it's worked out at render time.</summary>
    public string? Default { get; init; }

    /// <summary>html</summary>
    public string? Summary { get; init; }

    /// <summary>html</summary>
    public string? Remarks { get; init; }

    /// <summary>A two-way pair: <c>Value</c> with <c>ValueChanged</c>, written <c>@bind-Value</c>.</summary>
    public bool Bindable { get; init; }

    /// <summary>Where the two-way binding works: the <c>[ModeSupport]</c> on the <c>...Changed</c> callback.</summary>
    public ApiModes? BindModes { get; init; }

    /// <summary>Marked <c>[EditorRequired]</c>.</summary>
    public bool Required { get; init; }

    /// <summary>Takes a different value per breakpoint (<c>Responsive&lt;T&gt;</c>).</summary>
    public bool Responsive { get; init; }
    public ApiModes? Modes { get; init; }
    public string? AddedIn { get; init; }
    public string? Obsolete { get; init; }

    /// <summary>For a member a component inherits from a non-shared base, that base's name.</summary>
    public string? DeclaredOn { get; init; }
}

/// <summary>What works in each mode: "full", "partial" or "none".</summary>
public sealed record ApiModes
{
    public required string Off { get; init; }
    public required string Static { get; init; }
    public required string Live { get; init; }
    public string? Notes { get; init; }
}

/// <summary>An enum, class, struct or record that components' members use.</summary>
public sealed record ApiType
{
    public required string Name { get; init; }
    public required string FullName { get; init; }

    /// <summary>"enum", "class", "struct", "record", "interface" or "delegate".</summary>
    public required string Kind { get; init; }

    /// <summary>html</summary>
    public string? Summary { get; init; }

    /// <summary>Enum values, or the public static members of a class (presets such as <c>Accent.Primary</c>).</summary>
    public required List<ApiMember> Members { get; init; }
    public string? AddedIn { get; init; }
}
