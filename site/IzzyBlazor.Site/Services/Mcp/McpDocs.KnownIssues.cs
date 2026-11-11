using System.Text;
using IzzyBlazor.DocsGen;

namespace IzzyBlazor.Site.Services.Mcp;

public sealed partial class McpDocs
{
    /// <summary>
    /// What the docs already say about a problem, for find_known_issues: the troubleshooting
    /// articles about the component or matching the words, and the component's mode limits.
    /// </summary>
    public string KnownIssues(string? problem, string? component)
    {
        var words = SearchIndex.Terms(problem ?? "").Where(w => !Filler.Contains(w)).Distinct().ToList();
        var found = string.IsNullOrWhiteSpace(component) ? null : FindComponent(Input(component, "a component"));
        if (words.Count == 0 && found is null)
        {
            throw new McpDocsException("Describe the problem in a few words, name the component, or both.");
        }

        var owner = found is null ? null : registry.ByName(found.Name) ?? registry.OwnerOfPart(found.Name);
        var section = Articles.Sections.First(s => s.Slug == "troubleshooting");
        var articles = section.Articles
            .Select(a => (Article: a, Score:
                (found is not null && (a.Components.Contains(found.Name, StringComparer.OrdinalIgnoreCase) || (owner is not null && a.Components.Contains(owner.Name, StringComparer.OrdinalIgnoreCase))) ? 3 : 0)
                + SearchIndex.Terms($"{a.Title} {a.Summary}").Intersect(words).Count()))
            // The component, or two of the words: one shared word ("open", "rows") matches too much.
            .Where(m => m.Score >= Math.Min(2, words.Count))
            .OrderByDescending(m => m.Score)
            .Take(5)
            .ToList();

        var text = new StringBuilder($"{Release}: what the docs already say");
        text.Append(found is null ? ".\n" : $" about {found.Name}.\n");
        if (articles.Count > 0)
        {
            text.Append("\n## Troubleshooting articles\n\n");
            foreach (var (article, _) in articles)
            {
                text.Append($"- **{article.Title}** ({section.HrefOf(article)}): {article.Summary} get_article gives the fix.\n");
            }
        }

        List<(string Feature, string NoScript, string Static, string Interactive)> limited = found is null ? [] : ModeRows(found, owner).Where(r => (r.Static, r.Interactive) != ("full", "full")).ToList();
        if (found is not null && limited.Count > 0)
        {
            text.Append($"\n## {found.Name}'s mode limits\n\n");
            foreach (var row in limited)
            {
                text.Append($"- {row.Feature}: {Limits(new ApiModes { NoScript = row.NoScript, Static = row.Static, Interactive = row.Interactive })}\n");
            }
        }

        if (articles.Count == 0 && limited.Count == 0)
        {
            text.Append("\nNothing in the docs covers this.\n");
        }

        return text.ToString();
    }
}
