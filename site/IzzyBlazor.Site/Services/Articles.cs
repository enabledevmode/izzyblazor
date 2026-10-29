namespace IzzyBlazor.Site.Services;

/// <summary>
/// The hand-written articles, by section, in reading order. The docs sidebar, the section index
/// pages and each article's previous and next links read this list; an article's page is a Razor
/// page at its <see cref="Article.Href"/>.
/// </summary>
public static class Articles
{
    public static IReadOnlyList<ArticleSection> Sections { get; } =
    [
        new("get-started", "Get started", "Rocket",
        [
            new("blazor-web-app", "Blazor Web App", "Rocket",
                "Install the package, add the stylesheet and IzzyTheme, register the services and map the endpoints."),
            new("webassembly-and-auto", "WebAssembly or Auto", "Cpu",
                "Use IzzyBlazor.Core in the .Client project; the server project keeps IzzyBlazor."),
            new("existing-app", "Add to an existing app", "PackagePlus",
                "Bring IzzyBlazor into an app that already has pages, a layout and Bootstrap."),
            new("from-radzen", "Migrating from Radzen", "ArrowRightLeft",
                "Radzen's buttons, data grid and dialogs as IzzyBlazor components: renamed members and what behaves differently."),
            new("ai-assistants", "AI assistants", "Bot",
                "Connect Claude, Copilot or Cursor to IzzyBlazor's MCP server for components, parameters and examples."),
        ]),
        new("theming", "Theming", "Palette",
        [
            new("themes", "Themes and presets", "Palette",
                "A preset, your brand color, grays, radius, density and fonts, set once in IzzyTheme."),
            new("color-scheme", "Light and dark", "SunMoon",
                "The default scheme, a switch for readers, and a scheme for part of a page."),
            new("tokens", "Tokens and customizing", "SlidersHorizontal",
                "CSS variables for exact values, the surface ladder, and theming part of a page."),
        ]),
        new("common", "Common features", "Blocks",
        [
            new("style-props", "Style props", "Ruler",
                "Spacing, sizes, borders and colors as parameters, and which components take which."),
            new("responsive", "Responsive values", "MonitorSmartphone",
                "One value per breakpoint, hiding by screen size, and fluid text."),
            new("render-modes", "Render modes", "Layers",
                "What components do on static pages, with IzzyBlazor's script, and on interactive pages."),
            new("forms", "Forms and validation", "ClipboardCheck",
                "Forms that post on static pages, rules from your model, columns, read-only forms and errors."),
            new("data", "Data and filters", "Database",
                "A provider's request and result, EF Core, filters in code and as text, fields, and calling an API."),
            new("text", "Text and other languages", "Languages",
                "The words components use, changed app-wide or per component, in one language or several."),
            new("accessibility", "Accessibility", "Accessibility",
                "What the components do for keyboard and screen reader users, and what's left to your app."),
        ]),
        new("troubleshooting", "Troubleshooting", "LifeBuoy",
        [
            new("empty-selection", "An empty selection comes back with its defaults", "ListChecks",
                "Unchecked boxes post nothing, so after a static post the model gets its defaults back, or null.")
                { Components = ["CheckboxGroup", "MultiSelect", "Form"], Modes = ["off", "static"] },
            new("form-name", "A form's post fails or arrives empty", "ClipboardX",
                "A static post finds its form and model by name: FormName on the form, the same name on the model.")
                { Components = ["Form"], Modes = ["off", "static"] },
            new("onclick", "OnClick does nothing on a static page", "MousePointerClick",
                "A static page has no connection to run C# on a click: post a form or follow a link instead.")
                { Components = ["Button", "IconButton", "Menu", "Tag"], Modes = ["off", "static"] },
            new("state-between-requests", "A value resets on the next request", "RotateCcw",
                "A static page is built afresh for every request: keep what it needs in the address, or post it.")
                { Modes = ["off", "static"] },
            new("password-after-post", "The password is empty after a failed post", "KeyRound",
                "A static page doesn't write a password back into its HTML, so the reader types it again.")
                { Components = ["TextField"], Modes = ["off", "static"] },
            new("redirect-after-post", "Code runs after NavigateTo, or the post doesn't redirect", "CornerDownRight",
                "NavigateTo doesn't always stop your code: make it the last line or return after it, and keep it out of try/catch.")
                { Components = ["Form"], Modes = ["off", "static", "live"] },
            new("control-named-action", "A form posts to [object RadioNodeList]", "TriangleAlert",
                "A control named action, method or enctype hides the form's own property from Blazor's script.")
                { Components = ["Button", "Form"], Modes = ["static"] },
            new("interactive-stops-responding", "Interactive parts stop responding in production", "Unplug",
                "An interactive page's circuit lives on one server: behind a load balancer, turn on sticky sessions, or run one server.")
                { Modes = ["live"] },
        ]),
    ];

    /// <summary>The troubleshooting articles about a component, for its Overview's See also.</summary>
    public static IEnumerable<(ArticleSection Section, Article Article)> About(string component) =>
        Sections.SelectMany(s => s.Articles.Where(a => a.Components.Contains(component)).Select(a => (s, a)));

    public static ArticleSection? Section(string slug) => Sections.FirstOrDefault(s => s.Slug == slug);

    /// <summary>The section and article at <c>docs/{section}/{slug}</c>, if there is one.</summary>
    public static (ArticleSection Section, Article Article)? Find(string section, string slug) =>
        Section(section) is { } found && found.Articles.FirstOrDefault(a => a.Slug == slug) is { } article ? (found, article) : null;

    /// <summary>The articles before and after this one in its section.</summary>
    public static (Article? Previous, Article? Next) Around(ArticleSection section, Article article)
    {
        var index = section.Articles.ToList().IndexOf(article);
        return (index > 0 ? section.Articles[index - 1] : null, index < section.Articles.Count - 1 ? section.Articles[index + 1] : null);
    }
}

/// <summary>A docs section of articles, at <c>docs/{Slug}</c>.</summary>
public sealed record ArticleSection(string Slug, string Title, string Icon, IReadOnlyList<Article> Articles)
{
    public string Href => $"docs/{Slug}";

    public string HrefOf(Article article) => $"docs/{Slug}/{article.Slug}";
}

/// <summary>An article: its URL segment, title, pictogram (a Lucide name) and one-line summary.</summary>
public sealed record Article(string Slug, string Title, string Icon, string Summary)
{
    /// <summary>
    /// The components a troubleshooting article is about, by type name: shown as tags under its
    /// title, and the article is listed in their Overviews' See also.
    /// </summary>
    public IReadOnlyList<string> Components { get; init; } = [];

    /// <summary>The modes the problem happens in (<c>off</c>, <c>static</c>, <c>live</c>), shown under its title.</summary>
    public IReadOnlyList<string> Modes { get; init; } = [];
}
