using System.Globalization;
using System.Text.Json.Nodes;

namespace IzzyBlazor.Generation;

/// <summary>One setting of an item, read from its JSON Schema.</summary>
public sealed class ItemParameter
{
    /// <summary>The name used in conditions and as the <c>{{Name}}</c> token.</summary>
    public required string Name { get; init; }

    /// <summary>Its type.</summary>
    public required ParameterType Type { get; init; }

    /// <summary>The allowed values of a <see cref="ParameterType.Choice"/>.</summary>
    public IReadOnlyList<string> Choices { get; init; } = [];

    /// <summary>
    /// Choices only Pro can generate (<c>"x-tiers": { "Rows": "pro" }</c>), such as a block's Pro layouts; the
    /// rest are free. Their files are in the Pro catalogue, so the free engine refuses them.
    /// </summary>
    public IReadOnlyList<string> ProChoices { get; init; } = [];

    /// <summary>The value when none is given, as text (<c>true</c>, <c>Server</c>, <c>3</c>).</summary>
    public required string Default { get; init; }

    /// <summary>Help shown beside the field.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// For a port (<c>"x-port": [5000, 5300]</c>): the range a port is picked from when none is given, as
    /// <c>dotnet new</c> does for launchSettings.json. Such a setting has no default.
    /// </summary>
    public (int Low, int High)? Port { get; init; }

    /// <summary>
    /// For a GUID (<c>"x-guid": true</c>): a new one, lowercase with hyphens, when none is given, as
    /// <c>dotnet new</c>'s guid generator makes, such as a user secrets id. Such a setting is text with no default.
    /// </summary>
    public bool Guid { get; init; }

    /// <summary>The command-line option, without dashes: <c>"x-option"</c>, else the name in kebab case (<c>AllInteractive</c> → <c>all-interactive</c>).</summary>
    public required string Option { get; init; }

    /// <summary>Left out of help and forms (<c>"x-hidden": true</c>), such as the ports, but still accepted.</summary>
    public bool Hidden { get; init; }

    /// <summary>For text: a regular expression the whole value must match (JSON Schema's <c>pattern</c>), checked ignoring case.</summary>
    public string? Pattern { get; init; }

    internal static IReadOnlyList<ItemParameter> FromSchema(JsonObject schema)
    {
        if (schema["properties"] is not JsonObject properties) return [];
        var list = new List<ItemParameter>();
        foreach (var (name, node) in properties)
        {
            if (node is not JsonObject p) throw new GenerationException($"Parameter \"{name}\" must be an object.");
            var type = (string?)p["type"];
            var choices = (p["enum"] as JsonArray)?.Select(c => (string)c!).ToList() ?? [];
            var kind = type switch
            {
                "boolean" => ParameterType.Bool,
                "integer" => ParameterType.Int,
                "string" when choices.Count > 0 => ParameterType.Choice,
                "string" => ParameterType.Text,
                _ => throw new GenerationException($"Parameter \"{name}\" has type \"{type}\"; use boolean, integer or string (with enum for a choice)."),
            };
            var fallback = kind switch { ParameterType.Bool => "false", ParameterType.Int => "0", ParameterType.Choice => choices[0], _ => "" };
            (int, int)? port = null;
            if (p["x-port"] is JsonArray { Count: 2 } range)
            {
                if (kind != ParameterType.Int || p["default"] is not null)
                    throw new GenerationException($"Parameter \"{name}\" has x-port, so it's an integer with no default.");
                port = ((int)range[0]!, (int)range[1]!);
            }
            var guid = (bool?)p["x-guid"] ?? false;
            if (guid && (kind != ParameterType.Text || p["default"] is not null))
                throw new GenerationException($"Parameter \"{name}\" has x-guid, so it's a string with no default.");
            var pro = new List<string>();
            if (p["x-tiers"] is JsonObject tiers)
            {
                if (kind != ParameterType.Choice) throw new GenerationException($"Parameter \"{name}\" has x-tiers, so it needs an enum.");
                foreach (var (choice, tier) in tiers)
                {
                    if (!choices.Contains(choice)) throw new GenerationException($"Parameter \"{name}\": x-tiers names \"{choice}\", which isn't one of its choices.");
                    if ((string?)tier is not ("pro" or "free")) throw new GenerationException($"Parameter \"{name}\": x-tiers \"{choice}\" is \"pro\" or \"free\".");
                    if ((string?)tier == "pro") pro.Add(choice);
                }
            }
            var fallbackOrDefault = p["default"] is { } dv ? Text(dv) : fallback;
            if (pro.Contains(fallbackOrDefault))
                throw new GenerationException($"Parameter \"{name}\" defaults to \"{fallbackOrDefault}\", a Pro choice; the default must be free.");
            list.Add(new ItemParameter
            {
                Name = name,
                Type = kind,
                Choices = choices,
                ProChoices = pro,
                Default = fallbackOrDefault,
                Description = (string?)p["description"],
                Port = port,
                Guid = guid,
                Option = (string?)p["x-option"] ?? TemplateExport.OptionName(name),
                Hidden = (bool?)p["x-hidden"] ?? false,
                Pattern = (string?)p["pattern"],
            });
        }
        return list;
    }

    static string Text(JsonNode node) => node.GetValueKind() switch
    {
        System.Text.Json.JsonValueKind.True => "true",
        System.Text.Json.JsonValueKind.False => "false",
        System.Text.Json.JsonValueKind.Number => node.GetValue<long>().ToString(CultureInfo.InvariantCulture),
        _ => (string)node!,
    };
}

/// <summary>The types a setting can have; each maps to a <c>dotnet new</c> symbol type.</summary>
public enum ParameterType
{
    /// <summary>On or off.</summary>
    Bool,
    /// <summary>One of a fixed list.</summary>
    Choice,
    /// <summary>Free text.</summary>
    Text,
    /// <summary>A whole number.</summary>
    Int,
}
