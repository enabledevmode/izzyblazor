using System.Security;
using System.Text;

namespace IzzyBlazor.Site.Services;

/// <summary>
/// sitemap.xml and robots.txt for search engines: every page worth indexing, from the registry and
/// the articles as the search index reads them, at its canonical address on izzyblazor.com. No
/// <c>?mode=</c> or other query variants, and no demo frames or search pages.
/// </summary>
public static class Sitemap
{
    public static string Origin => $"https://{CanonicalHost.Host}/";

    /// <summary>The pages, as paths relative to the site's root (<c>""</c> is home).</summary>
    public static IEnumerable<string> Paths(Registry registry)
    {
        yield return "";
        yield return "docs";
        foreach (var section in Articles.Sections)
        {
            yield return section.Href;
            foreach (var article in section.Articles)
            {
                yield return section.HrefOf(article);
            }
        }

        yield return "docs/components";
        foreach (var component in registry.Components)
        {
            yield return Registry.OverviewHref(component);
            yield return Registry.ApiPageHref(component);
        }

        yield return "demos";
        foreach (var component in registry.Components)
        {
            foreach (var scenario in component.Scenarios)
            {
                yield return Registry.ScenarioHref(component, scenario);
            }
        }
    }

    public static string Xml(Registry registry)
    {
        var xml = new StringBuilder("""<?xml version="1.0" encoding="UTF-8"?>""").Append('\n')
            .Append("""<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">""").Append('\n');
        foreach (var path in Paths(registry))
        {
            xml.Append("<url><loc>").Append(SecurityElement.Escape(Origin + path)).Append("</loc></url>\n");
        }

        return xml.Append("</urlset>\n").ToString();
    }

    public static string Robots => $"""
        User-agent: *
        Disallow: /frame/
        Disallow: /search
        Disallow: /mcp
        Disallow: /*?

        Sitemap: {Origin}sitemap.xml

        """;
}
