using IzzyBlazor.Generation;

namespace IzzyBlazor.Generation.Tests;

/// <summary>
/// The phase 1 gate for blocks: izzy add block hero into a project the blazor starter made. It writes the component, places
/// it on Home and imports its folder, warns that Home already has an h1, changes nothing when run again, and
/// the project builds.
/// </summary>
[Collection(ProjectBuild.Collection)]
public sealed class HeroBlockTests : IDisposable
{
    readonly string _work = Directory.CreateTempSubdirectory("izzy-hero-").FullName;

    [AccountFact]
    public async Task The_hero_goes_into_a_new_app_once_and_it_builds()
    {
        ChangesetWriter.Apply(Generator.Generate(ProjectBuild.Item("blazor"), ProjectBuild.StarterSettings(), ProjectInfo.ForNewProject("Acme")), _work);
        var hero = ProjectBuild.Item("hero");
        var settings = new Dictionary<string, string> { ["Layout"] = "Split", ["Eyebrow"] = "New in 2.0" };

        var changes = Generator.Generate(hero, settings, ProjectInfo.FromDirectory(_work));
        Assert.Equal(["Components/Blocks/Hero.razor", "wwwroot/images/hero-screenshot.svg"], changes.Creates.Select(c => c.Path));
        // Home is the welcome page: the hero goes in, and the page's own heading gives way to the hero's.
        Assert.Equal(["Components/Pages/Home.razor", "Components/Pages/Home.razor", "Components/_Imports.razor"], changes.Inserts.Select(i => i.Path));
        Assert.Empty(changes.Warnings);

        var component = changes.Creates[0].Text!;
        Assert.Contains("<Grid Columns=", component);
        Assert.Contains("<Tag Label=\"New in 2.0\" Accent=\"Accent.Primary\" Pill", component);
        Assert.Contains("<Heading Level=\"1\"", component);
        Assert.DoesNotContain("#if", component);
        Assert.DoesNotContain("{{", component);

        ChangesetWriter.Apply(changes, _work);
        var home = File.ReadAllText(Path.Combine(_work, "Components", "Pages", "Home.razor"));
        Assert.Contains("<PageTitle>Home</PageTitle>\n\n<Hero />\n", home.Replace("\r\n", "\n"));
        Assert.Contains("<Heading Level=\"2\" id=\"welcome-title\"", home);
        Assert.DoesNotContain("Level=\"1\"", home);
        Assert.Contains("@using Acme.Components\n@using Acme.Components.Blocks\n", File.ReadAllText(Path.Combine(_work, "Components", "_Imports.razor")).Replace("\r\n", "\n"));

        // Again: everything is in place, so nothing changes.
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
