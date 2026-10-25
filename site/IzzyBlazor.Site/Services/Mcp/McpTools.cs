using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace IzzyBlazor.Site.Services.Mcp;

/// <summary>
/// The tools at <c>/mcp</c>: read-only lookups in IzzyBlazor's docs for an assistant writing code
/// with it. The answers come from <see cref="McpDocs"/>; the descriptions are what the assistant
/// reads to pick a tool, so they say when to use each.
/// </summary>
[McpServerToolType]
public sealed class McpTools(McpDocs docs)
{
    public const string Instructions = """
        IzzyBlazor is a Blazor component library for .NET 10, built for static server rendering first: most components
        work on static pages, many with JavaScript off, and become interactive where a page has a render mode. Install
        with `dotnet add package IzzyBlazor --prerelease` (IzzyBlazor.Core in a WebAssembly project) and
        `@using IzzyBlazor`. Look components up before writing markup: names, parameters and defaults are
        IzzyBlazor's own, not Bootstrap's or MudBlazor's. Start with search or list_components, then get_component for
        the API and get_example for working code. Check a component's mode support before relying on OnClick or
        @bind on a static page; get_article covers setup, theming, forms and static-rendering pitfalls. If nothing
        fits, say so and use plain Blazor or HTML: don't invent IzzyBlazor components or parameters.
        """;

    [McpServerTool(Name = "search", Title = "Search IzzyBlazor docs", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Search IzzyBlazor's components, parameters, types, examples and articles. A few keywords work best (\"modal\", \"file upload\", \"toast redirect\"); a sentence falls back to the entries matching most of its words. Each result names the call that gives its details (get_component, get_type, get_example or get_article).")]
    public string Search(
        [Description("Words to look for.")] string query,
        [Description("Most results per group, 1 to 50. Defaults to 10.")] int limit = 10) =>
        Run(() => docs.Search(query, limit));

    [McpServerTool(Name = "list_components", Title = "List IzzyBlazor components", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Every IzzyBlazor component by category, with a one-line summary, its parts and any mode limits. Use it to find the right component when you don't know its name.")]
    public string ListComponents(
        [Description("Only this category (as listed, such as \"Forms\"). Leave out for all.")] string? category = null) =>
        Run(() => docs.ListComponents(category));

    [McpServerTool(Name = "get_component", Title = "Get an IzzyBlazor component", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("A component's API: summary, usage, what works on static pages and with JavaScript off, its own parameters with types and defaults, the names of the shared ones it takes (Label, Disabled, margins; get_type gives them), events, methods, parts, example ids, accessibility and related components. Takes a component or part name (Button, Select, CardHeader).")]
    public string GetComponent(
        [Description("The component's name, as written in Razor.")] string name) =>
        Run(() => docs.GetComponent(name));

    [McpServerTool(Name = "get_type", Title = "Get an IzzyBlazor type", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("An enum's values or a type's members (ButtonVariant, ControlSize, Length, Responsive), or a shared base's parameters (FieldBase for a field's Label, Hint, Required and Disabled; LayoutItemBase and StyledComponentBase for the style props). Use it for the types and bases get_component lists.")]
    public string GetType(
        [Description("The type's name.")] string name) =>
        Run(() => docs.GetType(name));

    [McpServerTool(Name = "get_example", Title = "Get an IzzyBlazor example", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("An example's whole .razor source, as shown in the docs, by its id (button-variants) from get_component or search; a demo's address works too. The best start for working code.")]
    public string GetExample(
        [Description("The example's id.")] string id) =>
        Run(() => docs.GetExample(id));

    [McpServerTool(Name = "get_article", Title = "Get an IzzyBlazor docs page", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("A docs page as Markdown, examples included: setup (docs/get-started/blazor-web-app, webassembly-and-auto, existing-app), theming (docs/theming/themes, color-scheme, tokens), common features (docs/common/style-props, responsive, render-modes, forms, text, accessibility), troubleshooting static pages (docs/troubleshooting/...), or a component's Overview (docs/components/button). Leave the path empty to list every article.")]
    public Task<string> GetArticle(
        [Description("The page's path (docs/common/forms), its last segment (forms) or a component name for its Overview.")] string path = "") =>
        RunAsync(() => docs.GetArticleAsync(path));

    // An unknown name is the assistant's to fix: its message goes back as the tool's error.
    private static string Run(Func<string> answer)
    {
        try
        {
            return answer();
        }
        catch (McpDocsException e)
        {
            throw new McpException(e.Message);
        }
    }

    private static async Task<string> RunAsync(Func<Task<string>> answer)
    {
        try
        {
            return await answer();
        }
        catch (McpDocsException e)
        {
            throw new McpException(e.Message);
        }
    }
}
