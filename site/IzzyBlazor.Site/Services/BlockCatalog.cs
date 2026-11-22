using IzzyBlazor.Generation;

namespace IzzyBlazor.Site.Services;

/// <summary>A block in the catalog (<c>catalog/&lt;id&gt;/izzy.json</c>), for the Blocks pages.</summary>
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

    /// <summary>Free to use, with or without an account; Pro ones are generated on IzzyBlazor's server.</summary>
    public bool Free => Manifest.Tier != ItemTier.Pro;

    /// <summary>Comes with a free IzzyBlazor account: its template isn't in the CLI or the site.</summary>
    public bool Account => Manifest.Tier == ItemTier.Account;

    /// <summary>Its page.</summary>
    public string Href => $"blocks/{Id}";

    /// <summary>Its name as a C# identifier, for its previews' folder and namespace, such as <c>FeatureGrid</c>.</summary>
    public string TypeName => BlockCatalog.Identifier(Name.Replace(" ", ""));

    /// <summary>The picture on its card, a render of its first preview (<c>wwwroot/img/blocks</c>, taken by the site tests' BlockPictureTests).</summary>
    public string Picture => $"img/blocks/{Id}.webp";

    /// <summary>The same picture in the dark scheme (<c>img/blocks/{id}-dark.webp</c>).</summary>
    public string PictureDark => $"img/blocks/{Id}-dark.webp";
}

/// <summary>
/// One preview of a block: an option picked for each of its variations (<see cref="ItemManifest.Variations"/>).
/// </summary>
public sealed record BlockVariant(Block Block, IReadOnlyList<VariationOption> Picks)
{
    /// <summary>The generated component's name, such as <c>Hero_split_screenshot_on_off</c>: letters, digits and underscores.</summary>
    public string ComponentName => string.Join('_', Picks.Select(p => BlockCatalog.Identifier(p.Slug)).Prepend(Block.TypeName));

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
/// The blocks, read from the catalog copied beside the site (<c>catalog/</c> in its output): their
/// manifests, since an account block's template is in the private catalog and never in the site. Each
/// block's previews are the code <c>izzy add</c> writes, generated into <c>Blocks/Previews</c>
/// (<see cref="PreviewFiles"/>) by a test that runs where the private catalog is.
/// </summary>
public sealed class BlockCatalog
{
    /// <summary>Where the generated previews live in the site's source, and their namespace below the site's.</summary>
    public const string PreviewFolder = "Blocks/Previews";

    private readonly Dictionary<string, Block> _byId;

    /// <summary>
    /// Reads every block under <paramref name="catalogRoot"/>, with its template files from
    /// <paramref name="privateRoot"/> when given (the tests that generate the previews).
    /// </summary>
    public BlockCatalog(string catalogRoot, string? privateRoot = null)
    {
        _byId = Directory.Exists(catalogRoot)
            ? Catalog.Load(catalogRoot, privateRoot).Items.Values
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

    /// <summary>Text as a C# identifier: anything but letters, digits and underscores becomes an underscore.</summary>
    public static string Identifier(string text)
    {
        var name = new string(text.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '_').ToArray());
        return name.Length == 0 || char.IsAsciiDigit(name[0]) ? "_" + name : name;
    }

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

    /// <summary>
    /// The code files <c>izzy add</c> writes with the default settings (Razor, then C#), generated from the
    /// template: what the manifest's <c>writes</c> must list. Needs the block's files.
    /// </summary>
    public static IReadOnlyList<string> GeneratedWrites(Block block)
    {
        var changes = Generator.Generate(block.Definition, new Dictionary<string, string>(), ProjectInfo.ForNewProject("MyApp"));
        return changes.Creates.Where(c => c.Path.EndsWith(".razor", StringComparison.Ordinal))
            .Concat(changes.Creates.Where(c => c.Path.EndsWith(".cs", StringComparison.Ordinal)))
            .Select(c => c.Path).ToList();
    }

    // A recipe's markup, as izzy add --at puts it on a page: generated onto an empty one.
    static string Recipe(Block block, IReadOnlyDictionary<string, string> settings)
    {
        const string page = "Page.razor";
        var project = ProjectInfo.ForNewProject("MyApp") with { ReadFile = path => path == page ? "" : null };
        var changes = Generator.Generate(block.Definition, settings, project, new IzzyBlazor.Generation.Placement(page, 1));
        return changes.Inserts.Single(i => i.Path == page).Text;
    }

    /// <summary>The files <c>izzy add</c> writes, such as <c>Components/Blocks/Hero.razor</c>: its code files, with the default name.</summary>
    public static IReadOnlyList<string> Writes(Block block) => block.Manifest.Writes;

    /// <summary>
    /// The generated preview components, by path under the site: each variant's Razor, named for it, with
    /// the preview settings; and the block's own wwwroot files (its placeholder images). None for a Pro
    /// block: its template never reaches the site's source, so its page shows its picture. Needs the
    /// block's files (a catalog read with its private root).
    /// </summary>
    public static IEnumerable<(string Path, byte[] Content)> PreviewFiles(Block block)
    {
        if (!block.Free)
        {
            yield break;
        }

        foreach (var variant in Variants(block))
        {
            var settings = variant.Settings(preview: true);
            if (block.Manifest.Parameters.Any(p => p.Name == "Name")) settings["Name"] = variant.ComponentName;
            var changes = Generator.Generate(block.Definition, settings, ProjectInfo.ForNewProject("MyApp"));
            var razor = changes.Creates.FirstOrDefault(c => c.Path.EndsWith(".razor", StringComparison.Ordinal))?.Text ?? Recipe(block, settings);
            var text = $"@* Generated from catalog/{block.Id} by BlockPreviewTests: don't edit. *@\n" + razor.Replace("\r\n", "\n");
            yield return ($"{PreviewFolder}/{block.TypeName}/{variant.ComponentName}.razor", System.Text.Encoding.UTF8.GetBytes(text));
        }
        foreach (var (path, content) in block.Definition.Files.Where(f => f.Key.StartsWith("wwwroot/", StringComparison.Ordinal)))
        {
            yield return (path, content);
        }
    }

    /// <summary>A variant's preview component, or null if it hasn't been generated.</summary>
    public static Type? PreviewType(BlockVariant variant) =>
        variant.Block.Free
            ? typeof(BlockCatalog).Assembly.GetType($"IzzyBlazor.Site.{PreviewFolder.Replace('/', '.')}.{variant.Block.TypeName}.{variant.ComponentName}")
            : null;
}
