using System.Text.RegularExpressions;
using IzzyBlazor.Generation;

namespace IzzyBlazor.Generation.Tests;

/// <summary>
/// Free and Pro in one block: a Pro layout's file lives only in the Pro catalogue, which the server loads over
/// the free one. Without it the engine refuses the Pro layout.
/// </summary>
public sealed class CatalogTests : IDisposable
{
    readonly string _work = Directory.CreateTempSubdirectory("izzy-catalog-").FullName;

    const string Manifest = """
        {
          "id": "features", "name": "Features", "kind": "block",
          "parameters": { "type": "object", "properties": {
            "Layout": { "type": "string", "enum": ["Grid", "Rows"], "default": "Grid", "x-tiers": { "Rows": "pro" } }
          } },
          "exclude": [
            { "if": "Layout != \"Grid\"", "paths": ["layouts/Grid.razor"] },
            { "if": "Layout != \"Rows\"", "paths": ["layouts/Rows.razor"] }
          ],
          "rename": [
            { "if": "Layout == \"Grid\"", "paths": { "layouts/Grid.razor": "Components/Blocks/Features.razor" } },
            { "if": "Layout == \"Rows\"", "paths": { "layouts/Rows.razor": "Components/Blocks/Features.razor" } }
          ]
        }
        """;

    void Write(string path, string text)
    {
        var full = Path.Combine(_work, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }

    static string Features(Catalog catalog, string layout) =>
        Generator.Generate(catalog["features"], new Dictionary<string, string> { ["Layout"] = layout }, ProjectInfo.ForNewProject("Acme"))
            .Creates.Single().Text!;

    [Fact]
    public void A_Pro_layout_generates_only_with_the_Pro_catalogue()
    {
        Write("free/features/izzy.json", Manifest);
        Write("free/features/files/layouts/Grid.razor", "<Grid />\n");
        Write("pro/features/files/layouts/Rows.razor", "<Rows />\n");
        var free = Catalog.Load(Path.Combine(_work, "free"));
        var both = Catalog.Load(Path.Combine(_work, "free"), Path.Combine(_work, "pro"));

        Assert.Equal("<Grid />\n", Features(free, "Grid"));
        var refused = Assert.Throws<ProRequiredException>(() => Features(free, "rows"));
        Assert.Equal("Layout Rows is a Pro choice of features. Free choices: Grid.", refused.Message);
        Assert.Equal("<Rows />\n", Features(both, "Rows"));
        Assert.Equal("<Grid />\n", Features(both, "Grid"));
    }

    // Pro markup in a free file would ship to everyone: no condition in catalog/ may name a Pro choice.
    [Fact]
    public void The_free_catalogue_holds_no_Pro_markup()
    {
        var catalog = Catalog.Load(Path.Combine(ProjectBuild.RepoRoot, "catalog"));
        foreach (var item in catalog.Items.Values)
        foreach (var choice in item.Manifest.Parameters.SelectMany(p => p.ProChoices))
        foreach (var (path, bytes) in item.Files.Where(f => !TemplateText.IsBinary(f.Key)))
        {
            var conditions = Regex.Matches(System.Text.Encoding.UTF8.GetString(bytes), @"#(?:if|elif)\b.*");
            Assert.False(conditions.Any(c => c.Value.Contains($"\"{choice}\"")),
                $"{item.Manifest.Id}/{path} has a condition on the Pro choice {choice}; put that layout in its own file in the Pro catalogue.");
        }
    }

    public void Dispose() => Directory.Delete(_work, recursive: true);
}
