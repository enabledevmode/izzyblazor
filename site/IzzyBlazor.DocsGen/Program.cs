using System.Reflection;
using System.Text.Json;
using IzzyBlazor.DocsGen;

// Writes izzyblazor.api.json from the built library and its XML docs.
//
//   dotnet run --project site/IzzyBlazor.DocsGen -- --out izzyblazor.api.json [--previous last-release.api.json]
//
// With --previous (the last release's file), members new since then get this version as
// "addedIn", and anything removed is listed for the Upgrade page.

var output = Arg("--out") ?? "izzyblazor.api.json";
var previousPath = Arg("--previous");

var assemblies = new[]
{
    typeof(IzzyBlazor.Button).Assembly,
    typeof(Microsoft.Extensions.DependencyInjection.IzzyBlazorServiceCollectionExtensions).Assembly,
};

var docs = new XmlDocs();
foreach (var assembly in assemblies)
{
    docs.Load(Path.ChangeExtension(assembly.Location, ".xml"));
}

var version = assemblies[0].GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
var plus = version.IndexOf('+');
version = plus < 0 ? version : version[..plus];

var file = new ApiReader(docs, version).Read(assemblies);

if (previousPath is not null)
{
    var previous = JsonSerializer.Deserialize<ApiFile>(File.ReadAllText(previousPath), ApiFile.JsonOptions)!;
    (file, var removed) = ReleaseComparer.Apply(file, previous);
    foreach (var name in removed)
    {
        Console.WriteLine($"Removed since {previous.Version}: {name}");
    }
}

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
var json = JsonSerializer.Serialize(file, ApiFile.JsonOptions).Replace("\r\n", "\n");
if (!File.Exists(output) || File.ReadAllText(output) != json)
{
    File.WriteAllText(output, json);
}

Console.WriteLine($"DocsGen: {file.Components.Count} components, {file.Types.Count} types ({version}) -> {output}");
return 0;

string? Arg(string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
