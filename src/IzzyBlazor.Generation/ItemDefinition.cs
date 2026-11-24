using System.Text.Json.Nodes;

namespace IzzyBlazor.Generation;

/// <summary>An item ready to generate: its manifest and its template files, by path inside its <c>files</c> folder.</summary>
public sealed class ItemDefinition
{
    /// <summary>The manifest.</summary>
    public required ItemManifest Manifest { get; init; }

    /// <summary>The template files, keyed by their path with forward slashes (<c>Components/Pages/Home.razor</c>).</summary>
    public required IReadOnlyDictionary<string, byte[]> Files { get; init; }

    /// <summary>
    /// The most it can generate: <see cref="ItemTier.Free"/> from the public catalog alone;
    /// <see cref="ItemTier.Account"/> with its files from an account's copy of the private catalog;
    /// <see cref="ItemTier.Pro"/> with the whole private catalog, so its Pro choices, or the item itself when
    /// it's Pro, generate too (<see cref="Catalog.Load"/>).
    /// </summary>
    public ItemTier Access { get; init; }

    /// <summary>
    /// Reads an item from a catalog folder: <c>izzy.json</c> beside a <c>files</c> folder whose contents are
    /// the template, laid out as they'll be in the project.
    /// </summary>
    public static ItemDefinition FromDirectory(string directory)
    {
        var manifestPath = Path.Combine(directory, "izzy.json");
        if (!File.Exists(manifestPath)) throw new GenerationException($"No izzy.json in {directory}.");
        return new ItemDefinition { Manifest = ItemManifest.Parse(File.ReadAllText(manifestPath)), Files = ReadFiles(directory) };
    }

    /// <summary>
    /// A kit made over its base (<see cref="ItemManifest.Base"/>): the base's files with the kit's added, a kit
    /// file replacing the base's at the same path; the kit's settings, then the base's less those it fixes; the
    /// base's rules (excludes, renames, computed values, packages and the rest) before the kit's.
    /// </summary>
    internal static ItemDefinition OnBase(ItemDefinition kit, ItemDefinition based)
    {
        var k = kit.Manifest;
        var b = based.Manifest;
        var spec = k.Base!;
        if (b.Base is not null) throw new GenerationException($"{k.Id} is made over {b.Id}, which is made over another; a kit's base is a starter.");
        if (!b.Kind.MakesProject()) throw new GenerationException($"{k.Id} is made over {b.Id}, a {b.Kind.ToString().ToLowerInvariant()}; a kit's base makes a project.");

        var fixedValues = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var (name, text) in spec.Fixed)
        {
            var p = b.Parameters.FirstOrDefault(p => p.Name == name)
                ?? throw new GenerationException($"{k.Id} fixes \"{name}\", which isn't a setting of {b.Id}.");
            fixedValues[name] = Generator.Typed(k.Id, p, text);
        }

        var properties = new JsonObject();
        foreach (var (name, node) in k.Schema["properties"] as JsonObject ?? [])
            properties[name] = node?.DeepClone();
        foreach (var (name, node) in b.Schema["properties"] as JsonObject ?? [])
        {
            if (spec.Fixed.ContainsKey(name)) continue;
            if (properties.ContainsKey(name)) throw new GenerationException($"{k.Id} has a setting \"{name}\", and so does its base {b.Id}; fix the base's or name it otherwise.");
            var copy = node!.DeepClone().AsObject();
            if (spec.Defaults.TryGetValue(name, out var value)) copy["default"] = value;
            properties[name] = copy;
        }
        foreach (var name in spec.Defaults.Keys)
            if (!properties.ContainsKey(name) || k.Schema["properties"]?[name] is not null)
                throw new GenerationException($"{k.Id} gives \"{name}\" a default, but it isn't an unfixed setting of {b.Id}.");
        var schema = new JsonObject { ["type"] = "object", ["properties"] = properties };

        var files = new SortedDictionary<string, byte[]>(based.Files.ToDictionary(), StringComparer.Ordinal);
        foreach (var (path, bytes) in kit.Files) files[path] = bytes;

        var computed = new Dictionary<string, string>(b.Computed);
        foreach (var (name, expression) in k.Computed) computed[name] = expression;

        var manifest = new ItemManifest
        {
            Id = k.Id, Name = k.Name, Kind = k.Kind, Category = k.Category, Tier = k.Tier, Summary = k.Summary, Writes = k.Writes,
            Components = [.. b.Components.Union(k.Components).Order(StringComparer.Ordinal)],
            Modes = k.Modes.Count > 0 ? k.Modes : b.Modes,
            ModesNote = k.Modes.Count > 0 ? k.ModesNote : b.ModesNote,
            Screens = k.Screens,
            Schema = schema,
            Parameters = ItemParameter.FromSchema(schema),
            Computed = computed,
            Exclude = [.. b.Exclude, .. k.Exclude],
            Packages = [.. b.Packages, .. k.Packages],
            Inserts = [.. b.Inserts, .. k.Inserts],
            // A path moves by the first rule that names it, so the kit's own renames go first.
            Rename = [.. k.Rename, .. b.Rename],
            Server = [.. b.Server, .. k.Server],
            Restore = k.Restore ?? b.Restore,
            Checks = [.. b.Checks, .. k.Checks],
            Accessibility = k.Accessibility.Count > 0 ? k.Accessibility : b.Accessibility,
            Variations = k.Variations,
            PreviewSettings = k.PreviewSettings,
            Base = spec,
            Fixed = fixedValues,
        };
        return new ItemDefinition { Manifest = manifest, Files = files, Access = kit.Access };
    }

    // The files under a catalog folder's "files" folder, by path with forward slashes.
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
