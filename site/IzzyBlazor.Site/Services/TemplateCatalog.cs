using IzzyBlazor.Generation;

namespace IzzyBlazor.Site.Services;

/// <summary>A starter or an app kit in the catalogue (<c>catalog/&lt;id&gt;/izzy.json</c>), for the Templates pages.</summary>
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

    /// <summary>Free templates ship with the CLI and the dotnet new package.</summary>
    public bool Free => Manifest.Tier == ItemTier.Free;

    /// <summary>Its page.</summary>
    public string Href => $"templates/{Id}";

    /// <summary>The picture on its card (<c>wwwroot/img/templates</c>), and the same in the dark scheme.</summary>
    public string Picture => $"img/templates/{Id}.webp";

    /// <inheritdoc cref="Picture"/>
    public string PictureDark => $"img/templates/{Id}-dark.webp";

    /// <summary>The options a person can set: not the hidden ones, nor those a kit fixes.</summary>
    public IEnumerable<ItemParameter> Options => Manifest.Parameters.Where(p => !p.Hidden && !Manifest.Fixed.ContainsKey(p.Name));
}

/// <summary>A screenshot of a kit: its file name under <c>img/templates/{id}/</c> without the extension, what it shows, and whether it was taken at a phone's width.</summary>
public sealed record TemplateScreen(string File, string Caption, string Alt, bool Phone = false);

/// <summary>The starters and app kits, read from the catalogue copied beside the site (<c>catalog/</c> in its output).</summary>
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
        All = _byId.Values.OrderBy(t => t.IsKit).ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Every template: the starters, then the kits, each A to Z.</summary>
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
    /// A kit's screenshots, in the order its page shows them. Each is four files under
    /// <c>wwwroot/img/templates/{id}/</c>: <c>{file}.webp</c>, <c>{file}-dark.webp</c> and a
    /// <c>-thumb</c> of each, taken from the kit running with its sample data.
    /// </summary>
    public static IReadOnlyList<TemplateScreen> Screens(Template template) =>
        ScreensById.GetValueOrDefault(template.Id) ?? [];

    // Taken Nov 22, 2026 from `izzy new kit crm` running with its sample data, at 1440×900 and 375×812.
    private static readonly Dictionary<string, IReadOnlyList<TemplateScreen>> ScreensById = new(StringComparer.OrdinalIgnoreCase)
    {
        ["crm"] =
        [
            new("dashboard", "My day", "My day: the open pipeline, follow-ups due, next moves and recent activity"),
            new("contacts", "Contacts", "The contacts list, with search and filters"),
            new("contact", "A contact", "A contact's page: their details, deals, activities and notes"),
            new("company", "A company", "A company's page: its details, people and deals"),
            new("pipeline", "Deal pipeline", "The deal pipeline: each deal with its company, stage, value and expected close"),
            new("board", "Deal pipeline as a board", "The deal pipeline as a board: a column for each stage with its count and total, and a card for each deal"),
            new("deal", "A deal", "A deal's page: its stage, value and history"),
            new("calendar", "Activities", "The week's activities on a calendar"),
            new("reports", "Reports", "The sales overview: charts of the pipeline and the team's results"),
            new("team", "Settings and team", "Settings and team: the workspace's members and their roles"),
            new("audit-log", "Audit log", "The audit log: who changed what, newest first"),
            new("sign-in", "Log in", "The log-in page"),
            new("phone-dashboard", "My day on a phone", "My day at a phone's width", Phone: true),
            new("phone-pipeline", "Deal pipeline on a phone", "The deal pipeline at a phone's width", Phone: true),
            new("phone-contact", "A contact on a phone", "A contact's page at a phone's width", Phone: true),
        ],
    };
}
