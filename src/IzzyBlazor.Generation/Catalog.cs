namespace IzzyBlazor.Generation;

/// <summary>
/// The items to generate from, by id: the free catalogue, and on IzzyBlazor's server the Pro catalogue over it.
/// </summary>
public sealed class Catalog
{
    /// <summary>The items, by id.</summary>
    public required IReadOnlyDictionary<string, ItemDefinition> Items { get; init; }

    /// <summary>
    /// Loads the free catalogue from <paramref name="freeRoot"/>, one folder per item named by its id, and the Pro
    /// catalogue from <paramref name="proRoot"/> when given. In the Pro root, a folder with its own
    /// <c>izzy.json</c> is a whole Pro item; one without adds files to the free item of the same id, such as a
    /// block's Pro layouts.
    /// </summary>
    public static Catalog Load(string freeRoot, string? proRoot = null)
    {
        var items = new SortedDictionary<string, ItemDefinition>(StringComparer.Ordinal);
        foreach (var folder in Folders(freeRoot))
        {
            var item = Read(folder);
            if (item.Manifest.Tier == ItemTier.Pro)
                throw new GenerationException($"{item.Manifest.Id} is a Pro item in the free catalogue; Pro items live in the Pro catalogue.");
            items[item.Manifest.Id] = item;
        }
        if (proRoot is not null)
        {
            foreach (var folder in Folders(proRoot))
            {
                var id = Path.GetFileName(folder);
                if (File.Exists(Path.Combine(folder, "izzy.json")))
                {
                    if (items.ContainsKey(id)) throw new GenerationException($"{id} is in both catalogues; an overlay of a free item has no izzy.json.");
                    var item = Read(folder);
                    items[id] = new ItemDefinition { Manifest = item.Manifest, Files = item.Files, IncludesPro = true };
                    continue;
                }
                if (!items.TryGetValue(id, out var free))
                    throw new GenerationException($"The Pro catalogue's {id} has no izzy.json and no free item of that id to add to.");
                var files = new SortedDictionary<string, byte[]>(free.Files.ToDictionary(), StringComparer.Ordinal);
                foreach (var (path, bytes) in ItemDefinition.ReadFiles(folder))
                    if (!files.TryAdd(path, bytes)) throw new GenerationException($"{id}: {path} is in both catalogues.");
                items[id] = new ItemDefinition { Manifest = free.Manifest, Files = files, IncludesPro = true };
            }
        }
        return new Catalog { Items = items };
    }

    /// <summary>The item with this id.</summary>
    public ItemDefinition this[string id] =>
        Items.TryGetValue(id, out var item) ? item : throw new GenerationException($"No item \"{id}\". Items: {string.Join(", ", Items.Keys)}.");

    static IEnumerable<string> Folders(string root) =>
        Directory.Exists(root)
            ? Directory.EnumerateDirectories(root).Order(StringComparer.Ordinal)
            : throw new GenerationException($"No catalogue at {root}.");

    static ItemDefinition Read(string folder)
    {
        var item = ItemDefinition.FromDirectory(folder);
        if (item.Manifest.Id != Path.GetFileName(folder))
            throw new GenerationException($"{folder} holds item \"{item.Manifest.Id}\"; name the folder by the item's id.");
        return item;
    }
}
