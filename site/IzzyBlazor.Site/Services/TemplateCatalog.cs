using IzzyBlazor.Generation;

namespace IzzyBlazor.Site.Services;

/// <summary>A starter or an app kit in the catalog (<c>catalog/&lt;id&gt;/izzy.json</c>), for the Templates pages.</summary>
public sealed record Template(ItemDefinition Definition)
{
    /// <summary>The manifest, a kit's over its base.</summary>
    public ItemManifest Manifest => Definition.Manifest;

    /// <summary>The id, as in <c>izzy new kit crm</c>, <c>dotnet new kit-crm</c> and <c>/templates/crm</c>.</summary>
    public string Id => Manifest.Id;

    /// <summary>The name, such as "Izzy CRM".</summary>
    public string Name => Manifest.Name;

    /// <summary>A whole app for a purpose, made over a starter; else a starter, an empty app.</summary>
    public bool IsKit => Manifest.Kind == ItemKind.Kit;

    /// <summary>The heading it's listed under.</summary>
    public string Group => IsKit ? TemplateCatalog.Kits : TemplateCatalog.Starters;

    /// <summary>Free to use, with or without an account.</summary>
    public bool Free => Manifest.Tier != ItemTier.Pro;

    /// <summary>Comes with a free IzzyBlazor account, through <c>izzy new</c>; the others are in the dotnet new package too.</summary>
    public bool Account => Manifest.Tier == ItemTier.Account;

    /// <summary>Its page.</summary>
    public string Href => $"templates/{Id}";

    /// <summary>The picture on its card (<c>wwwroot/img/templates</c>), and the same in the dark scheme.</summary>
    public string Picture => $"img/templates/{Id}.webp";

    /// <inheritdoc cref="Picture"/>
    public string PictureDark => $"img/templates/{Id}-dark.webp";

    /// <summary>The options a person can set: not the hidden ones, nor those a kit fixes.</summary>
    public IEnumerable<ItemParameter> Options => Manifest.Parameters.Where(p => !p.Hidden && !Manifest.Fixed.ContainsKey(p.Name));
}

/// <summary>A screenshot of a starter or kit: its file name under <c>img/templates/{id}/</c> without the extension, what it shows, and whether it was taken at a phone's width.</summary>
public sealed record TemplateScreen(string File, string Caption, string Alt, bool Phone = false);

/// <summary>The starters and app kits, read from the catalog copied beside the site (<c>catalog/</c> in its output).</summary>
public sealed class TemplateCatalog
{
    /// <summary>The group of empty apps.</summary>
    public const string Starters = "Starters";

    /// <summary>The group of whole apps.</summary>
    public const string Kits = "App kits";

    private readonly Dictionary<string, Template> _byId;

    /// <summary>Reads every starter and kit under <paramref name="catalogRoot"/>.</summary>
    public TemplateCatalog(string catalogRoot)
    {
        _byId = Directory.Exists(catalogRoot)
            ? Catalog.Load(catalogRoot).Items.Values
                .Where(d => d.Manifest.Kind is ItemKind.Starter or ItemKind.Kit)
                .Select(d => new Template(d))
                .ToDictionary(t => t.Id, StringComparer.OrdinalIgnoreCase)
            : [];
        All = _byId.Values.OrderBy(t => t.IsKit)
            .ThenBy(t => Array.IndexOf(Order, t.Id.ToLowerInvariant()) is >= 0 and var place ? place : Order.Length)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    // The starters' order, as the design lists them (board T1): the open one first, then the shells from the
    // plainest. One that isn't named here follows them, A to Z.
    private static readonly string[] Order = ["blazor", "sidebar", "topnav", "rail", "docs", "inbox", "site"];

    /// <summary>Every template: the starters in the design's order (IzzyBlazor App first), then the kits.</summary>
    public IReadOnlyList<Template> All { get; }

    /// <summary>The groups that have a template, in order.</summary>
    public IEnumerable<string> Groups => All.Select(t => t.Group).Distinct();

    /// <summary>A group's templates.</summary>
    public IEnumerable<Template> InGroup(string group) => All.Where(t => t.Group == group);

    /// <summary>A template by its id, or null.</summary>
    public Template? ById(string id) => _byId.GetValueOrDefault(id);

    /// <summary>A group's anchor, such as <c>app-kits</c>.</summary>
    public static string GroupAnchor(string group) => Heading.SlugOf(group);

    /// <summary>
    /// A starter's or kit's screenshots, in the order its page shows them. Each is four files under
    /// <c>wwwroot/img/templates/{id}/</c>: <c>{file}.webp</c>, <c>{file}-dark.webp</c> and a
    /// <c>-thumb</c> of each, taken from the app running with its sample data. The list is the manifest's <c>screens</c>, which izzy gives its front ends too.
    /// </summary>
    public static IReadOnlyList<TemplateScreen> Screens(Template template) =>
        [.. template.Manifest.Screens.Select(s => new TemplateScreen(s.File, s.Caption, s.Alt, s.Phone))];

}
