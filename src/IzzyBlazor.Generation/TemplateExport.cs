using System.Text.Json;
using System.Text.Json.Nodes;

namespace IzzyBlazor.Generation;

/// <summary>
/// Writes a starter as a <c>dotnet new</c> template: its files as they are, with a <c>template.json</c> made from
/// its manifest. Template files already use <c>dotnet new</c>'s condition syntax and the <c>{{Token}}</c> names
/// the symbols replace, so <c>dotnet new</c> and the engine make the same project (a test checks every starter).
/// </summary>
public static class TemplateExport
{
    static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>Writes <paramref name="item"/> to <paramref name="directory"/> as a template folder.</summary>
    public static void WriteTemplateFolder(ItemDefinition item, string directory)
    {
        var configDirectory = Path.Combine(directory, ".template.config");
        Directory.CreateDirectory(configDirectory);
        File.WriteAllText(Path.Combine(configDirectory, "template.json"), TemplateJson(item.Manifest));
        File.WriteAllText(Path.Combine(configDirectory, "dotnetcli.host.json"), CliHostJson(item.Manifest));
        foreach (var (path, bytes) in item.Files)
        {
            var full = Path.Combine(directory, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllBytes(full, bytes);
        }
    }

    /// <summary>
    /// The <c>dotnetcli.host.json</c>: each setting's option is its name in kebab case (<c>AllInteractive</c> →
    /// <c>--all-interactive</c>), as Microsoft's templates spell theirs, with no generated short form.
    /// </summary>
    public static string CliHostJson(ItemManifest manifest)
    {
        var info = new JsonObject();
        foreach (var p in manifest.Parameters)
        {
            info[p.Name] = new JsonObject { ["longName"] = p.Option, ["shortName"] = "" };
            if (p.Hidden) info[p.Name]!["isHidden"] = true;
        }
        return new JsonObject { ["$schema"] = "http://json.schemastore.org/dotnetcli.host", ["symbolInfo"] = info }.ToJsonString(Indented) + "\n";
    }

    /// <summary>A setting's command-line option name: <c>AllInteractive</c> → <c>all-interactive</c>.</summary>
    public static string OptionName(string name)
    {
        var result = new System.Text.StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0 && (char.IsLower(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1])))) result.Append('-');
            result.Append(char.ToLowerInvariant(name[i]));
        }
        return result.ToString();
    }

    /// <summary>The <c>template.json</c> for a starter.</summary>
    public static string TemplateJson(ItemManifest manifest)
    {
        if (manifest.Kind != ItemKind.Starter)
            throw new GenerationException($"{manifest.Id} is a {manifest.Kind.ToString().ToLowerInvariant()}; only starters ship through dotnet new. Use izzy add.");
        if (manifest.Inserts.Count > 0)
            throw new GenerationException($"{manifest.Id} has inserts, which dotnet new can't make; write the text into the starter's files.");

        var symbols = new JsonObject
        {
            ["ProjectName"] = new JsonObject
            {
                ["type"] = "derived", ["valueSource"] = "name", ["valueTransform"] = "identity",
                ["replaces"] = "{{ProjectName}}", ["fileRename"] = "{{ProjectName}}",
            },
            ["RootNamespace"] = new JsonObject
            {
                ["type"] = "derived", ["valueSource"] = "name", ["valueTransform"] = "safeNamespace",
                ["replaces"] = "{{RootNamespace}}", ["fileRename"] = "{{RootNamespace}}",
            },
            ["HostName"] = new JsonObject
            {
                ["type"] = "derived", ["valueSource"] = "name", ["valueTransform"] = "hostName",
                ["replaces"] = "{{HostName}}",
            },
            ["IzzyBlazorVersion"] = new JsonObject
            {
                ["type"] = "generated", ["generator"] = "constant",
                ["parameters"] = new JsonObject { ["value"] = Generator.IzzyBlazorVersion },
                ["replaces"] = "{{IzzyBlazorVersion}}",
            },
        };
        foreach (var p in manifest.Parameters)
        {
            var symbol = new JsonObject
            {
                ["type"] = "parameter",
                ["datatype"] = p.Type switch { ParameterType.Bool => "bool", ParameterType.Choice => "choice", ParameterType.Int => "integer", _ => "text" },
            };
            if (p.Port is null) symbol["defaultValue"] = p.Default;
            if (p.Description is not null) symbol["description"] = p.Description;
            if (p.Type == ParameterType.Choice)
                symbol["choices"] = new JsonArray(p.Choices.Select(c => (JsonNode)new JsonObject { ["choice"] = c }).ToArray());
            symbols[p.Name] = symbol;
            if (p.Port is var (low, high))
            {
                // Microsoft's pattern: a generated port, and the given one when there is one.
                symbols[p.Name + "Generated"] = new JsonObject
                {
                    ["type"] = "generated", ["generator"] = "port",
                    ["parameters"] = new JsonObject { ["low"] = low, ["high"] = high },
                };
                symbols[p.Name + "Replacer"] = new JsonObject
                {
                    ["type"] = "generated", ["generator"] = "coalesce",
                    ["parameters"] = new JsonObject { ["sourceVariableName"] = p.Name, ["fallbackVariableName"] = p.Name + "Generated" },
                    ["replaces"] = "{{" + p.Name + "}}",
                };
            }
            else if (p.Type != ParameterType.Bool)
            {
                symbol["replaces"] = "{{" + p.Name + "}}";
                symbol["fileRename"] = "{{" + p.Name + "}}";
            }
        }
        foreach (var (name, expression) in manifest.Computed)
            symbols[name] = new JsonObject { ["type"] = "computed", ["value"] = $"({expression})" };

        var template = new JsonObject
        {
            ["$schema"] = "http://json.schemastore.org/template",
            ["author"] = "IzzyBlazor",
            ["classifications"] = new JsonArray("Web", "Blazor", "IzzyBlazor"),
            ["identity"] = $"IzzyBlazor.Templates.{manifest.Id}",
            ["name"] = manifest.Name,
            ["shortName"] = manifest.Id,
            ["preferNameDirectory"] = true,
            ["tags"] = new JsonObject { ["language"] = "C#", ["type"] = "project" },
            ["forms"] = new JsonObject
            {
                ["safeNamespace"] = new JsonObject { ["identifier"] = "safe_namespace" },
                // Names.HostName's steps, as Microsoft's templates spell them.
                ["hostName"] = new JsonObject
                {
                    ["identifier"] = "chain",
                    ["steps"] = new JsonArray("lowerCaseInvariant", "dnsInvalidToHyphen", "repeatedHyphens", "edgeHyphens"),
                },
                ["dnsInvalidToHyphen"] = new JsonObject { ["identifier"] = "replace", ["pattern"] = "[^a-z0-9-]", ["replacement"] = "-" },
                ["repeatedHyphens"] = new JsonObject { ["identifier"] = "replace", ["pattern"] = "-{2,}", ["replacement"] = "-" },
                ["edgeHyphens"] = new JsonObject { ["identifier"] = "replace", ["pattern"] = "^-+|-+$", ["replacement"] = "" },
            },
            ["symbols"] = symbols,
        };
        if (manifest.Summary is not null) template["description"] = manifest.Summary;
        // Binary files are copied as they are; excludes leave files out when their condition holds.
        var source = new JsonObject
        {
            ["copyOnly"] = new JsonArray(TemplateText.BinaryExtensions.Order(StringComparer.Ordinal).Select(e => (JsonNode)("**/*" + e)).ToArray()),
        };
        var modifiers = manifest.Exclude.Select(rule => (JsonNode)new JsonObject
        {
            ["condition"] = $"({rule.If})",
            ["exclude"] = new JsonArray(rule.Paths.Select(path => (JsonNode)path).ToArray()),
        }).Concat(manifest.Rename.Select(rule => (JsonNode)new JsonObject
        {
            ["condition"] = $"({rule.If})",
            ["rename"] = new JsonObject(rule.Paths.Select(p => KeyValuePair.Create(p.Key, (JsonNode?)p.Value))),
        })).ToArray();
        if (modifiers.Length > 0) source["modifiers"] = new JsonArray(modifiers);
        template["sources"] = new JsonArray(source);
        if (manifest.Restore is not null)
        {
            template["postActions"] = new JsonArray(new JsonObject
            {
                ["id"] = "restore",
                ["condition"] = $"({manifest.Restore})",
                ["description"] = "Restore NuGet packages required by this project.",
                ["manualInstructions"] = new JsonArray(new JsonObject { ["text"] = "Run 'dotnet restore'" }),
                ["actionId"] = "210D431B-A78B-4D2F-B762-4ED3E3EA9025",
                ["continueOnError"] = true,
            });
        }
        return template.ToJsonString(Indented) + "\n";
    }
}
