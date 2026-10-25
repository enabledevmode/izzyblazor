using System.Text;

namespace IzzyBlazor.Site.Services.Mcp;

/// <summary>
/// <c>/llms.txt</c> (llmstxt.org): what IzzyBlazor is and where its docs are, for an assistant
/// that reads the site rather than connecting to the MCP server. Built from the same lists as the
/// sitemap.
/// </summary>
public static class LlmsTxt
{
    public static string Of(Registry registry, ApiMetadata api)
    {
        var text = new StringBuilder($"""
            # IzzyBlazor

            > A Blazor component library for .NET 10 ({registry.ComponentCountLabel} components), built for static server rendering first: most components work on static pages, many with JavaScript off, and become interactive where a page has a render mode. This describes {api.Version}.

            Install with `dotnet add package IzzyBlazor --prerelease`, add `@using IzzyBlazor` to `_Imports.razor`, and follow the Blazor Web App article for the stylesheet, `IzzyTheme`, `AddIzzyBlazor()` and `MapIzzyBlazor()`. Component names and parameters are IzzyBlazor's own; look them up rather than guessing.

            For assistants: the MCP server at {Sitemap.Origin}mcp (Streamable HTTP, no sign-in) searches these docs and returns any component's API and examples' source as Markdown. Every component's API is also in one JSON file: [API metadata]({Sitemap.Origin}izzyblazor.api.json).

            """);

        foreach (var section in Articles.Sections)
        {
            text.Append($"\n## {section.Title}\n\n");
            foreach (var article in section.Articles)
            {
                text.Append($"- [{article.Title}]({Sitemap.Origin}{section.HrefOf(article)}): {article.Summary}\n");
            }
        }

        foreach (var category in registry.Categories)
        {
            text.Append($"\n## Components: {category}\n\n");
            foreach (var component in registry.InCategory(category))
            {
                text.Append($"- [{component.Name}]({Sitemap.Origin}{Registry.OverviewHref(component)}): {component.Summary}");
                text.Append($" [API]({Sitemap.Origin}{Registry.ApiPageHref(component)})\n");
            }
        }

        text.Append($"""

            ## Optional

            - [Demos]({Sitemap.Origin}demos): whole pages built with the components.

            """);
        return text.ToString();
    }
}
