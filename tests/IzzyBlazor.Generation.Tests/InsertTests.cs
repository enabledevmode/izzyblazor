using System.Text;
using IzzyBlazor.Generation;

namespace IzzyBlazor.Generation.Tests;

public sealed class InsertTests : IDisposable
{
    readonly string _project = Directory.CreateTempSubdirectory("izzy-gen-").FullName;

    const string Manifest = """
        {
          "id": "page", "name": "Page", "kind": "page",
          "parameters": { "type": "object", "properties": { "PageName": { "type": "string", "default": "Reports" } } },
          "inserts": [
            { "file": "Components/Layout/NavMenu.razor", "anchor": "@* izzy:nav *@", "position": "before",
              "text": "    <NavItem Href=\"{{PageName}}\">{{PageName}}</NavItem>\n" },
            { "file": "Components/Pages/{{PageName}}.razor", "anchor": "<h1>", "position": "after", "text": "<p>Made by izzy.</p>\n" }
          ]
        }
        """;

    static readonly ItemDefinition Page = new()
    {
        Manifest = ItemManifest.Parse(Manifest),
        Files = new Dictionary<string, byte[]> { ["Components/Pages/{{PageName}}.razor"] = Encoding.UTF8.GetBytes("@page \"/{{PageName}}\"\n<h1>{{PageName}}</h1>\n") },
    };

    const string NavMenu = "<NavMenu>\r\n    <NavItem Href=\"\">Home</NavItem>\r\n    @* izzy:nav *@\r\n</NavMenu>\r\n";

    void Write(string path, string text)
    {
        var full = Path.Combine(_project, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }

    string Read(string path) => File.ReadAllText(Path.Combine(_project, path));

    Changeset Generate() => Generator.Generate(Page, new Dictionary<string, string>(), ProjectInfo.FromDirectory(_project));

    [Fact]
    public void A_page_goes_in_with_its_nav_entry_once()
    {
        Write("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
        Write("Components/Layout/NavMenu.razor", NavMenu);

        var changes = Generate();
        Assert.Empty(changes.Warnings);
        Assert.Equal(["Components/Pages/Reports.razor"], changes.Creates.Select(c => c.Path));
        Assert.Equal("@page \"/Reports\"\n<h1>Reports</h1>\n<p>Made by izzy.</p>\n", changes.Creates[0].Text);

        var diff = ChangesetWriter.Diff(changes, ProjectInfo.FromDirectory(_project));
        Assert.Contains("--- a/Components/Layout/NavMenu.razor\n+++ b/Components/Layout/NavMenu.razor\n@@ -1,4 +1,5 @@\n <NavMenu>\n     <NavItem Href=\"\">Home</NavItem>\n+    <NavItem Href=\"Reports\">Reports</NavItem>\n     @* izzy:nav *@\n </NavMenu>\n", diff);

        ChangesetWriter.Apply(changes, _project);
        Assert.Equal("<NavMenu>\r\n    <NavItem Href=\"\">Home</NavItem>\r\n    <NavItem Href=\"Reports\">Reports</NavItem>\r\n    @* izzy:nav *@\r\n</NavMenu>\r\n", Read("Components/Layout/NavMenu.razor"));
        Assert.Equal(changes.Creates[0].Text, Read("Components/Pages/Reports.razor"));

        // Again: the page is there and the nav holds the entry, so nothing changes.
        var again = Generate();
        Assert.Empty(again.Creates);
        Assert.Empty(again.Inserts);
        Assert.Empty(again.Conflicts);
        Assert.Empty(again.Warnings);
    }

    [Fact]
    public void A_file_with_other_content_is_kept_unless_overwriting()
    {
        Write("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
        Write("Components/Layout/NavMenu.razor", NavMenu);
        Write("Components/Pages/Reports.razor", "mine\n");

        var changes = Generate();
        Assert.Equal(["Components/Pages/Reports.razor"], changes.Conflicts);
        Assert.Throws<GenerationException>(() => ChangesetWriter.Apply(changes, _project));
        Assert.Equal("mine\n", Read("Components/Pages/Reports.razor"));
        Assert.Equal(NavMenu, Read("Components/Layout/NavMenu.razor"));

        ChangesetWriter.Apply(changes, _project, overwrite: true);
        Assert.StartsWith("@page", Read("Components/Pages/Reports.razor"));
    }

    public void Dispose() => Directory.Delete(_project, recursive: true);
}
