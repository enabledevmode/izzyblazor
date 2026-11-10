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
                "Install the package, add the stylesheet and IzzyTheme, and register the services."),
            new("webassembly-and-auto", "WebAssembly or Auto", "Cpu",
                "Use IzzyBlazor.Core in the .Client project; the server project keeps IzzyBlazor."),
            new("existing-app", "Add to an existing app", "PackagePlus",
                "Bring IzzyBlazor into an app that already has pages, a layout and Bootstrap."),
            new("from-radzen", "Migrating from Radzen", "ArrowRightLeft",
                "Find the IzzyBlazor counterpart of each Radzen component, which members are renamed, and what behaves differently."),
            new("ai-assistants", "AI assistants", "Bot",
                "Connect Claude, Copilot or Cursor to IzzyBlazor's MCP server for components, parameters and examples."),
        ]),
        new("theming", "Theming", "Palette",
        [
            new("themes", "Themes and presets", "Palette",
                "A preset, your brand color, grays, radius, density and fonts, set once in IzzyTheme."),
            new("color-scheme", "Light and dark", "SunMoon",
                "Set the default color scheme, add a switch so users can change it, and give part of a page its own scheme."),
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
            new("session-recovery", "Session recovery", "RotateCcw",
                "Free the server's memory for idle tabs, and bring back what the user was doing after a pause, a lost connection or a restart."),
            new("forms", "Forms and validation", "ClipboardCheck",
                "Forms that post on static pages, rules from your model, columns, read-only forms and errors."),
            new("data", "Data and filters", "Database",
                "Load a Table or DataGrid's rows from a provider or EF Core, and build filters in code with the operators each type allows."),
            new("data-api", "Data from an API", "Server",
                "Send a provider's request from the browser to your own API or an OData endpoint, and limit what the server applies."),
            new("text", "Text and other languages", "Languages",
                "The words components use, changed app-wide or per component, in one language or several."),
        ]),
        new("accessibility", "Accessibility", "Accessibility",
        [
            new("overview", "Accessibility overview", "Accessibility",
                "IzzyBlazor builds to WCAG 2.2 AA from native elements; this lists how it's checked, the gaps left, and your app's share."),
            new("keyboard-and-focus", "Keyboard and focus", "Keyboard",
                "Focus shows on every control and lands somewhere sensible after each change; every component lists its keys."),
            new("names-and-announcements", "Names and announcements", "MessageSquareText",
                "Every control has a name screen readers read, and changes made without a full reload are announced."),
            new("color-and-motion", "Color, contrast and motion", "Contrast",
                "Text keeps 4.5:1 contrast with your own colors too, components adapt to high-contrast mode, and motion stops when the user asks."),
            new("pointer-and-touch", "Pointer and touch", "Pointer",
                "Targets are at least 24 pixels, and most drags have a click-only way to the same result."),
            new("testing", "Testing your app", "ListChecks",
                "Check your own pages with a keyboard, a screen reader, zoom and automated tools."),
        ]),
        new("troubleshooting", "Troubleshooting", "LifeBuoy",
        [
            new("empty-selection", "An empty selection comes back with its defaults", "ListChecks",
                "Unchecked boxes post nothing, so after a form post on static pages the model gets its defaults back, or null.")
                { Area = "Forms and posts", Components = ["CheckboxGroup", "MultiSelect", "Form"], Modes = ["static"] },
            new("form-name", "A form's post fails or arrives empty", "ClipboardX",
                "A form post on static pages finds its form and model by name: FormName on the form, the same name on the model.")
                { Area = "Forms and posts", Components = ["Form"], Modes = ["static"] },
            new("onclick", "OnClick does nothing on a static page", "MousePointerClick",
                "A static page has no connection to run C# on a click: post a form or follow a link instead.")
                { Area = "Static pages", Components = ["Button", "IconButton", "Menu", "Tag"], Modes = ["static"] },
            new("state-between-requests", "A value resets on the next request", "RotateCcw",
                "A static page is built afresh for every request: keep what it needs in the URL, or post it.")
                { Area = "Static pages", Modes = ["static"] },
            new("password-after-post", "The password is empty after a failed post", "KeyRound",
                "A static page doesn't write a password back into its HTML, so the user types it again.")
                { Area = "Forms and posts", Components = ["PasswordField"], Modes = ["static"] },
            new("redirect-after-post", "Code runs after NavigateTo, or the post doesn't redirect", "CornerDownRight",
                "NavigateTo doesn't always stop your code: make it the last line or return after it, and keep it out of try/catch.")
                { Area = "Forms and posts", Components = ["Form"], Modes = ["static", "interactive"] },
            new("control-named-action", "A form posts to [object RadioNodeList]", "TriangleAlert",
                "A control named action, method or enctype hides the form's own property from Blazor's script.")
                { Area = "Forms and posts", Components = ["Button", "Form"], Modes = ["static"] },
            new("interactive-stops-responding", "Interactive parts stop responding in production", "Unplug",
                "An interactive page's circuit lives on one server: behind a load balancer, turn on sticky sessions, or run one server.")
                { Area = "Interactive and hosting", Modes = ["interactive"] },
        ]) { IndexOnly = true },
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
    /// <summary>
    /// Lists the section in the sidebar as one link to its index, which groups its articles by
    /// <see cref="Article.Area"/> under a filter: for a section that keeps growing, such as
    /// Troubleshooting, whose titles are whole symptoms.
    /// </summary>
    public bool IndexOnly { get; init; }

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

    /// <summary>The group an article is listed under on an <see cref="ArticleSection.IndexOnly"/> section's index, such as "Forms and posts".</summary>
    public string? Area { get; init; }

    /// <summary>The modes the problem happens in (<c>off</c>, <c>static</c>, <c>live</c>), shown under its title.</summary>
    public IReadOnlyList<string> Modes { get; init; } = [];
}
