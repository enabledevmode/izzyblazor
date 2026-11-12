using IzzyBlazor.Generation;

namespace IzzyBlazor.Generation.Tests;

/// <summary>
/// The other half of the phase 1 gate: what izzy-blazor makes builds. Three projects cover its layouts: the
/// default (Server, pages static by default), static with no samples and a Main method, and Auto with every page
/// interactive (the layout and pages in the .Client project) under a name that needs a safe namespace. The
/// IzzyBlazor packages are swapped for this repo's projects, so it builds against the code here, not a release.
/// </summary>
[Collection(ProjectBuild.Collection)]
public sealed class StarterBuildTests : IDisposable
{
    readonly string _work = Directory.CreateTempSubdirectory("izzy-starter-build-").FullName;

    public static TheoryData<string, string> Cases() => new()
    {
        { "MyApp", "" },
        { "MyApp", "Interactivity=None;Empty=true;UseProgramMain=true" },
        { "My-App.2", "Interactivity=Auto;AllInteractive=true" },
        { "MyApp", "Nav=Top;Preset=Compact;Brand=#0a7d5a;Shell=Inset;ContentWidth=64" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task The_starter_builds(string name, string settings)
    {
        var changes = Generator.Generate(ProjectBuild.Item("izzy-blazor"), ProjectBuild.StarterSettings(settings), ProjectInfo.ForNewProject(name));
        ChangesetWriter.Apply(changes, _work);
        ProjectBuild.PointAtRepo(_work);
        await ProjectBuild.BuildAsync(_work, $"{name} {settings}");
    }

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
