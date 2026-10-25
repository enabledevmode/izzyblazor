using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace IzzyBlazor.Site.Services;

/// <summary>
/// Every example: a Razor component under <c>Examples/{Folder}/{Name}.razor</c>, known by the id
/// <c>{folder}-{name}</c> (<c>Examples/Button/MultiSubmit.razor</c> is <c>button-multi-submit</c>).
/// Its source is embedded at build time, so production never reads the source tree; lines between
/// <c>@* hide *@</c> and <c>@* /hide *@</c> (scaffolding, sample data) are left out of what's shown.
/// </summary>
public sealed partial class ExampleCatalog
{
    private const string Namespace = "IzzyBlazor.Site.Examples.";

    private readonly Dictionary<string, Type> _types = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string FileName, string Source)> _sources = new(StringComparer.Ordinal);

    public ExampleCatalog()
    {
        var assembly = typeof(ExampleCatalog).Assembly;
        foreach (var type in assembly.GetTypes().Where(t => t.Namespace?.StartsWith(Namespace, StringComparison.Ordinal) == true
                     && typeof(Microsoft.AspNetCore.Components.IComponent).IsAssignableFrom(t) && !t.IsAbstract && !t.IsNested))
        {
            _types[IdOf(type.Namespace![Namespace.Length..], type.Name)] = type;
        }

        foreach (var resource in assembly.GetManifestResourceNames().Where(n => n.StartsWith("example:", StringComparison.Ordinal)))
        {
            // example:Button\Variants on Windows, example:Button/Variants elsewhere.
            var path = resource["example:".Length..].Replace('\\', '/');
            var slash = path.LastIndexOf('/');
            if (slash < 0)
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            var name = path[(slash + 1)..];
            _sources[IdOf(path[..slash].Replace('/', '.'), name)] = (name + ".razor", Clean(reader.ReadToEnd()));
        }
    }

    public IEnumerable<string> Ids => _types.Keys;

    public Type? TypeOf(string id) => _types.GetValueOrDefault(id);

    public (string FileName, string Source)? SourceOf(string id) => _sources.TryGetValue(id, out var source) ? source : null;

    /// <summary><c>Button</c> and <c>MultiSubmit</c> make <c>button-multi-submit</c>.</summary>
    public static string IdOf(string folder, string name) => $"{Kebab(folder.Replace('.', '-'))}-{Kebab(name)}";

    private static string Kebab(string name) => WordStart().Replace(name, "-").ToLowerInvariant();

    /// <summary>The source as shown: no hidden lines, LF line ends, no blank lines at the ends.</summary>
    public static string Clean(string source)
    {
        var shown = new StringBuilder();
        var hiding = false;
        foreach (var line in source.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed == "@* hide *@")
            {
                hiding = true;
                continue;
            }

            if (trimmed == "@* /hide *@")
            {
                hiding = false;
                continue;
            }

            if (!hiding)
            {
                shown.Append(line).Append('\n');
            }
        }

        return shown.ToString().Trim('\n');
    }

    // Before a capital after a lowercase letter or digit, and before the last capital of a run when
    // a word follows: InARow is in-a-row, CsvExport csv-export, CSVExport csv-export.
    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")]
    private static partial Regex WordStart();
}
