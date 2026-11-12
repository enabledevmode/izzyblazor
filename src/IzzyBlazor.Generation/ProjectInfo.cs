using System.Xml.Linq;

namespace IzzyBlazor.Generation;

/// <summary>
/// The project an item is generated into: its name and namespace (the <c>{{ProjectName}}</c> and
/// <c>{{RootNamespace}}</c> tokens) and a way to read its files, for inserts and conflicts.
/// </summary>
/// <param name="Name">The project's name, as given to <c>-n</c> or read from its .csproj file name.</param>
/// <param name="RootNamespace">Its root namespace.</param>
public sealed record ProjectInfo(string Name, string RootNamespace)
{
    /// <summary>Reads a file by its path relative to the project folder (forward slashes); null when there's none.</summary>
    public Func<string, string?> ReadFile { get; init; } = _ => null;

    /// <summary>A new project: the namespace comes from the name, as <c>dotnet new</c> works it out, and no files exist yet.</summary>
    public static ProjectInfo ForNewProject(string name) => new(name, Names.SafeNamespace(name));

    /// <summary>
    /// An existing project, from its folder: the one .csproj file names it, its <c>RootNamespace</c> (or the
    /// name made safe) is the namespace, and files are read from the folder.
    /// </summary>
    public static ProjectInfo FromDirectory(string directory)
    {
        var projects = Directory.GetFiles(directory, "*.csproj");
        if (projects.Length != 1)
            throw new GenerationException(projects.Length == 0 ? $"No .csproj file in {directory}." : $"More than one .csproj file in {directory}; pick one.");
        var name = Path.GetFileNameWithoutExtension(projects[0]);
        var rootNamespace = XDocument.Load(projects[0]).Descendants("RootNamespace").LastOrDefault()?.Value.Trim();
        var root = Path.GetFullPath(directory);
        return new ProjectInfo(name, string.IsNullOrEmpty(rootNamespace) ? Names.SafeNamespace(name) : rootNamespace)
        {
            ReadFile = path =>
            {
                var full = Path.GetFullPath(Path.Combine(root, path));
                if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new GenerationException($"{path} is outside the project.");
                return File.Exists(full) ? File.ReadAllText(full) : null;
            },
        };
    }
}
