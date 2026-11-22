namespace IzzyBlazor.Generation;

/// <summary>
/// The items to generate from, by id: the public catalog, and the private catalog over it where there is one
/// (an account's copy, or all of it on IzzyBlazor's server).
/// </summary>
public sealed class Catalog
{
    /// <summary>The items, by id.</summary>
    public required IReadOnlyDictionary<string, ItemDefinition> Items { get; init; }

    /// <summary>
    /// Loads the free catalog from <paramref name="freeRoot"/>, one folder per item named by its id, and the Pro
    /// catalog from <paramref name="proRoot"/> when given. In the Pro root, a folder with its own
    /// <c>izzy.json</c> is a whole Pro item; one without adds files to the public item of the same id: a
    /// block's Pro layouts, or all the files of an account item (<see cref="ItemTier.Account"/>), whose
    /// manifest is public and whose files never are. An account item with no files of its own, such as a
    /// starter that only fixes its base's settings, still has its folder there.
    /// <paramref name="access"/> is what the Pro root holds: all of it, or an account's copy, which has no
    /// Pro layouts or items.
    /// </summary>
    public static Catalog Load(string freeRoot, string? proRoot = null, ItemTier access = ItemTier.Pro)
    {
        // Ids are found whatever their case ("CRM", "crm"); two items can't differ by case alone.
        var items = new SortedDictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in Folders(freeRoot))
        {
            var item = Read(folder);
            if (item.Manifest.Tier == ItemTier.Pro)
                throw new GenerationException($"{item.Manifest.Id} is a Pro item in the free catalog; Pro items live in the Pro catalog.");
            if (item.Manifest.Tier == ItemTier.Account && item.Files.Count > 0)
                throw new GenerationException($"{item.Manifest.Id} comes with an account, so only its izzy.json is in the public catalog; its files live in the private one.");
            if (!items.TryAdd(item.Manifest.Id, item))
                throw new GenerationException($"Two items are named {item.Manifest.Id}; ids differ by more than case.");
        }
        if (proRoot is not null)
        {
            foreach (var folder in Folders(proRoot))
            {
                var id = Path.GetFileName(folder);
                if (File.Exists(Path.Combine(folder, "izzy.json")))
                {
                    if (items.ContainsKey(id)) throw new GenerationException($"{id} is in both catalogs; an overlay of a free item has no izzy.json.");
                    var item = Read(folder);
                    items[id] = new ItemDefinition { Manifest = item.Manifest, Files = item.Files, Access = access };
                    continue;
                }
                if (!items.TryGetValue(id, out var free))
                    throw new GenerationException($"The Pro catalog's {id} has no izzy.json and no free item of that id to add to.");
                var files = new SortedDictionary<string, byte[]>(free.Files.ToDictionary(), StringComparer.Ordinal);
                foreach (var (path, bytes) in ItemDefinition.ReadFiles(folder))
                    if (!files.TryAdd(path, bytes)) throw new GenerationException($"{id}: {path} is in both catalogs.");
                items[id] = new ItemDefinition { Manifest = free.Manifest, Files = files, Access = access };
            }
        }
        // Kits last, over their bases as loaded (a Pro kit can be made over a free starter).
        foreach (var (id, item) in items.ToList())
            if (item.Manifest.Base is { } spec)
                items[id] = ItemDefinition.OnBase(item, items.TryGetValue(spec.Item, out var based)
                    ? based : throw new GenerationException($"{id} is made over {spec.Item}, which isn't in the catalog."));
        return new Catalog { Items = items };
    }

    /// <summary>The item with this id, in any case.</summary>
    public ItemDefinition this[string id] =>
        Items.TryGetValue(id, out var item) ? item : throw new GenerationException($"No item \"{id}\". Items: {string.Join(", ", Items.Keys)}.");

    static IEnumerable<string> Folders(string root) =>
        Directory.Exists(root)
            // Not a checkout's own folders (.git, .github).
            ? Directory.EnumerateDirectories(root).Where(d => !Path.GetFileName(d).StartsWith('.')).Order(StringComparer.Ordinal)
            : throw new GenerationException($"No catalog at {root}.");

    static ItemDefinition Read(string folder)
    {
        var item = ItemDefinition.FromDirectory(folder);
        if (item.Manifest.Id != Path.GetFileName(folder))
            throw new GenerationException($"{folder} holds item \"{item.Manifest.Id}\"; name the folder by the item's id.");
        return item;
    }
}
