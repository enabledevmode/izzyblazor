using IzzyBlazor.Generation;

namespace IzzyBlazor.Site.Services;

/// <summary>A block in the catalogue (<c>catalog/&lt;id&gt;/izzy.json</c>), for the Blocks pages.</summary>
public sealed record Block(ItemDefinition Definition)
{
    /// <summary>The manifest.</summary>
    public ItemManifest Manifest => Definition.Manifest;

    /// <summary>The id, as in <c>izzy add hero</c> and <c>/blocks/hero</c>.</summary>
    public string Id => Manifest.Id;

    /// <summary>The name, such as "Hero".</summary>
    public string Name => Manifest.Name;

    /// <summary>The category heading it's listed under, such as "Heroes".</summary>
    public string Category => Manifest.Category ?? "Other";

    /// <summary>Free blocks ship with the CLI; Pro ones are generated on IzzyBlazor's server.</summary>
    public bool Free => Manifest.Tier == ItemTier.Free;

    /// <summary>Its page.</summary>
    public string Href => $"blocks/{Id}";

    /// <summary>The picture on its card, a render of its first preview (<c>wwwroot/img/blocks</c>).</summary>
    public string Picture => $"img/blocks/{Id}.webp";
}

/// <summary>
/// One preview of a block: an option picked for each of its variations (<see cref="ItemManifest.Variations"/>).
/// </summary>
public sealed record BlockVariant(Block Block, IReadOnlyList<VariationOption> Picks)
{
    /// <summary>The generated component's name, such as <c>Hero_split_screenshot_on_off</c>.</summary>
    public string ComponentName => string.Join('_', Picks.Select(p => p.Slug.Replace('-', '_')).Prepend(Block.Name));

    /// <summary>The settings it's generated with: the variations' values over the preview's own.</summary>
    public Dictionary<string, string> Settings(bool preview)
    {
        var settings = preview ? new Dictionary<string, string>(Block.Manifest.PreviewSettings) : [];
        for (var i = 0; i < Picks.Count; i++)
        {
            settings[Block.Manifest.Variations[i].Setting] = Picks[i].Value;
        }
        return settings;
    }

    /// <summary>The address's query for it, such as <c>layout=split&amp;media=image</c>.</summary>
    public string Query => string.Join('&', Block.Manifest.Variations.Select((v, i) => $"{v.Key}={Picks[i].Slug}"));
}

/// <summary>
/// The blocks, read from the catalogue copied beside the site (<c>catalog/</c> in its output). Each
/// block's code is generated from its template as the CLI would; its previews are that code too,
/// generated into <c>Blocks/Previews</c> (<see cref="PreviewFiles"/>, kept current by a test).
/// </summary>
public sealed class BlockCatalog
{
    /// <summary>Where the generated previews live in the site's source, and their namespace below the site's.</summary>
    public const string PreviewFolder = "Blocks/Previews";

    private readonly Dictionary<string, Block> _byId;

    /// <summary>Reads every block under <paramref name="catalogRoot"/>.</summary>
    public BlockCatalog(string catalogRoot)
    {
        _byId = Directory.Exists(catalogRoot)
            ? Directory.EnumerateDirectories(catalogRoot)
                .Where(d => File.Exists(Path.Combine(d, "izzy.json")))
                .Select(ItemDefinition.FromDirectory)
                .Where(d => d.Manifest.Kind == ItemKind.Block)
                .Select(d => new Block(d))
                .ToDictionary(b => b.Id, StringComparer.OrdinalIgnoreCase)
            : [];
        All = _byId.Values.OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase).ToList();
        Categories = All.Select(b => b.Category).Distinct().Order(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Every block, A to Z.</summary>
    public IReadOnlyList<Block> All { get; }

    /// <summary>The categories, A to Z.</summary>
    public IReadOnlyList<string> Categories { get; }

    /// <summary>A block by its id, or null.</summary>
    public Block? ById(string id) => _byId.GetValueOrDefault(id);

    /// <summary>A category's blocks, A to Z.</summary>
    public IEnumerable<Block> InCategory(string category) => All.Where(b => b.Category == category);

    /// <summary>A category's anchor and filter value, such as <c>heroes</c>.</summary>
    public static string CategoryAnchor(string category) => Heading.SlugOf(category);

    /// <summary>The blocks that use a component, by its name, such as <c>BrowserFrame</c>.</summary>
    public IEnumerable<Block> Using(string component) => All.Where(b => b.Manifest.Components.Contains(component));

    /// <summary>The variant a query picks: each variation's option by its slug, else its first.</summary>
    public static BlockVariant Variant(Block block, Func<string, string?> query) =>
        new(block, block.Manifest.Variations
            .Select(v => v.Options.FirstOrDefault(o => o.Slug == query(v.Key)) ?? v.Options[0])
            .ToList());

    /// <summary>Every variant: each combination of the variations' options.</summary>
    public static IEnumerable<BlockVariant> Variants(Block block)
    {
        IEnumerable<IReadOnlyList<VariationOption>> combos = [[]];
        foreach (var variation in block.Manifest.Variations)
        {
            combos = combos.SelectMany(c => variation.Options.Select(o => (IReadOnlyList<VariationOption>)[.. c, o]));
        }
        return combos.Select(c => new BlockVariant(block, c));
    }

    /// <summary>The Razor files <c>izzy add</c> writes for a variant, by path, as a new app named MyApp gets them.</summary>
    public static IReadOnlyList<(string Path, string Text)> Code(BlockVariant variant)
    {
        var changes = Generator.Generate(variant.Block.Definition, variant.Settings(preview: false), ProjectInfo.ForNewProject("MyApp"));
        return changes.Creates.Where(c => c.Path.EndsWith(".razor", StringComparison.Ordinal)).Select(c => (c.Path, c.Text!)).ToList();
    }

    /// <summary>The files <c>izzy add</c> writes, such as <c>Components/Blocks/Hero.razor</c>: its Razor ones, with the default name.</summary>
    public static IReadOnlyList<string> Writes(Block block) =>
        Code(Variant(block, _ => null)).Select(c => c.Path).ToList();

    /// <summary>
    /// The generated preview components, by path under the site: each variant's Razor, named for it, with
    /// the preview settings; and the block's own wwwroot files (its placeholder images).
    /// </summary>
    public static IEnumerable<(string Path, byte[] Content)> PreviewFiles(Block block)
    {
        foreach (var variant in Variants(block))
        {
            var settings = variant.Settings(preview: true);
            settings["Name"] = variant.ComponentName;
            var changes = Generator.Generate(block.Definition, settings, ProjectInfo.ForNewProject("MyApp"));
            var razor = changes.Creates.Single(c => c.Path.EndsWith(".razor", StringComparison.Ordinal));
            var text = $"@* Generated from catalog/{block.Id} by BlockPreviewTests: don't edit. *@\n" + razor.Text!.Replace("\r\n", "\n");
            yield return ($"{PreviewFolder}/{block.Name}/{variant.ComponentName}.razor", System.Text.Encoding.UTF8.GetBytes(text));
        }
        foreach (var (path, content) in block.Definition.Files.Where(f => f.Key.StartsWith("wwwroot/", StringComparison.Ordinal)))
        {
            yield return (path, content);
        }
    }

    /// <summary>A variant's preview component, or null if it hasn't been generated.</summary>
    public static Type? PreviewType(BlockVariant variant) =>
        typeof(BlockCatalog).Assembly.GetType($"IzzyBlazor.Site.{PreviewFolder.Replace('/', '.')}.{variant.Block.Name}.{variant.ComponentName}");
}
