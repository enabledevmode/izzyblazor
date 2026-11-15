using IzzyBlazor.Generation;

namespace IzzyBlazor.Generation.Tests;

/// <summary>
/// Blocks in an app whose pages run in the browser (Auto, every page interactive): the router, layout and pages
/// are in the .Client project, so a section goes on the .Client's Home, a page goes beside it, and what reads
/// the request (the cookie notice and its settings page, the maintenance page) stays in the server project. The
/// app then builds against this repo.
/// </summary>
[Collection(ProjectBuild.Collection)]
public sealed class ClientProjectTests : IDisposable
{
    readonly string _work = Directory.CreateTempSubdirectory("izzy-client-").FullName;

    [Fact]
    public async Task Blocks_go_where_the_pages_are_and_the_app_builds()
    {
        ChangesetWriter.Apply(Generator.Generate(ProjectBuild.Item("izzy-blazor"),
            ProjectBuild.StarterSettings("Interactivity=Auto;AllInteractive=true"), ProjectInfo.ForNewProject("Acme")), _work);
        // Found from either project.
        var project = ProjectInfo.FromProject(Path.Combine(_work, "Acme.Client", "Acme.Client.csproj"));
        Assert.Equal(("Acme/", "Acme.Client/", true), (project.Host, project.Client, project.LayoutInBrowser));

        var paths = new List<string>();
        foreach (var (id, settings) in new[] { ("hero", "HeadingLevel=2"), ("features", ""), ("legal", ""), ("cookies", ""), ("maintenance", "") })
        {
            var values = settings.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Split('=')).ToDictionary(s => s[0], s => s[1]);
            var changes = Generator.Generate(ProjectBuild.Item(id), values, ProjectInfo.FromProject(Path.Combine(_work, "Acme", "Acme.csproj")));
            Assert.Empty(changes.Warnings);
            ChangesetWriter.Apply(changes, _work);
            paths.AddRange(changes.Creates.Select(c => c.Path).Concat(changes.Inserts.Select(i => i.Path)));
        }

        Assert.Contains("Acme.Client/Blocks/Hero.razor", paths);
        Assert.Contains("Acme.Client/Pages/Home.razor", paths);
        Assert.Contains("Acme.Client/Pages/Privacy.razor", paths);
        Assert.Contains("Acme/Components/Blocks/CookieNotice.razor", paths);
        Assert.Contains("Acme/Components/Pages/Maintenance.razor", paths);
        Assert.Contains("@using Acme.Client.Blocks", File.ReadAllText(Path.Combine(_work, "Acme.Client", "_Imports.razor")));
        // The notice reads the request, so it's rendered on the server, outside the pages that run in the browser.
        Assert.Matches(@"<Routes [^\n]*/>\s*<CookieNotice />", File.ReadAllText(Path.Combine(_work, "Acme", "Components", "App.razor")));

        ProjectBuild.PointAtRepo(_work);
        await ProjectBuild.BuildAsync(_work, "Auto, every page interactive, with blocks");
    }

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
