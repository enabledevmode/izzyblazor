using IzzyBlazor.Generation;

namespace IzzyBlazor.Generation.Tests;

/// <summary>
/// The phase 1 gate for blocks: izzy add hero into a project izzy-blazor made. It writes the component, places
/// it on Home and imports its folder, warns that Home already has an h1, changes nothing when run again, and
/// the project builds.
/// </summary>
[Collection(ProjectBuild.Collection)]
public sealed class HeroBlockTests : IDisposable
{
    readonly string _work = Directory.CreateTempSubdirectory("izzy-hero-").FullName;

    [Fact]
    public async Task The_hero_goes_into_a_new_app_once_and_it_builds()
    {
        ChangesetWriter.Apply(Generator.Generate(ProjectBuild.Item("izzy-blazor"), ProjectBuild.StarterSettings(), ProjectInfo.ForNewProject("Acme")), _work);
        var hero = ProjectBuild.Item("hero");
        var settings = new Dictionary<string, string> { ["Layout"] = "Split", ["Eyebrow"] = "New in 2.0" };

        var changes = Generator.Generate(hero, settings, ProjectInfo.FromDirectory(_work));
        Assert.Equal(["Components/Blocks/Hero.razor", "wwwroot/images/hero-screenshot.svg"], changes.Creates.Select(c => c.Path));
        Assert.Equal(["Components/Pages/Home.razor", "Components/_Imports.razor"], changes.Inserts.Select(i => i.Path));
        var warning = Assert.Single(changes.Warnings);
        Assert.Contains("Components/Pages/Home.razor already has a level-1 heading", warning);

        var component = changes.Creates[0].Text!;
        Assert.Contains("<Grid Columns=", component);
        Assert.Contains("<Badge Status=\"Status.Info\" Pill>New in 2.0</Badge>", component);
        Assert.Contains("<Heading Level=\"1\"", component);
        Assert.DoesNotContain("#if", component);
        Assert.DoesNotContain("{{", component);

        ChangesetWriter.Apply(changes, _work);
        var home = File.ReadAllText(Path.Combine(_work, "Components", "Pages", "Home.razor"));
        Assert.Contains("<PageTitle>Home</PageTitle>\n\n<Hero />\n", home.Replace("\r\n", "\n"));
        Assert.Contains("@using Acme.Components\n@using Acme.Components.Blocks\n", File.ReadAllText(Path.Combine(_work, "Components", "_Imports.razor")).Replace("\r\n", "\n"));

        // Again: everything is in place, so nothing changes; the h1 is still worth saying.
        var again = Generator.Generate(hero, settings, ProjectInfo.FromDirectory(_work));
        Assert.Empty(again.Creates);
        Assert.Empty(again.Inserts);
        Assert.Empty(again.Conflicts);

        ProjectBuild.PointAtRepo(_work);
        await ProjectBuild.BuildAsync(_work, "Acme with a hero");
    }

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
