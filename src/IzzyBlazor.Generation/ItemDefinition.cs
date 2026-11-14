namespace IzzyBlazor.Generation;

/// <summary>An item ready to generate: its manifest and its template files, by path inside its <c>files</c> folder.</summary>
public sealed class ItemDefinition
{
    /// <summary>The manifest.</summary>
    public required ItemManifest Manifest { get; init; }

    /// <summary>The template files, keyed by their path with forward slashes (<c>Components/Pages/Home.razor</c>).</summary>
    public required IReadOnlyDictionary<string, byte[]> Files { get; init; }

    /// <summary>
    /// Whether the Pro catalogue's files were loaded with it (<see cref="Catalog.Load"/> with a Pro root), so its
    /// Pro choices, or the whole item when it's Pro, can be generated.
    /// </summary>
    public bool IncludesPro { get; init; }

    /// <summary>
    /// Reads an item from a catalogue folder: <c>izzy.json</c> beside a <c>files</c> folder whose contents are
    /// the template, laid out as they'll be in the project.
    /// </summary>
    public static ItemDefinition FromDirectory(string directory)
    {
        var manifestPath = Path.Combine(directory, "izzy.json");
        if (!File.Exists(manifestPath)) throw new GenerationException($"No izzy.json in {directory}.");
        return new ItemDefinition { Manifest = ItemManifest.Parse(File.ReadAllText(manifestPath)), Files = ReadFiles(directory) };
    }

    // The files under a catalogue folder's "files" folder, by path with forward slashes.
    internal static SortedDictionary<string, byte[]> ReadFiles(string directory)
    {
        var filesRoot = Path.Combine(directory, "files");
        var files = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        if (Directory.Exists(filesRoot))
        {
            foreach (var path in Directory.EnumerateFiles(filesRoot, "*", SearchOption.AllDirectories))
                files[Path.GetRelativePath(filesRoot, path).Replace('\\', '/')] = File.ReadAllBytes(path);
        }
        return files;
    }
}
