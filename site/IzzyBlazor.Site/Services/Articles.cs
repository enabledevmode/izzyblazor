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
            new("text", "Text and other languages", "Languages",
                "The words components use, changed app-wide or per component, in one language or several."),
        ]),
    ];

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
public sealed record Article(string Slug, string Title, string Icon, string Summary);
