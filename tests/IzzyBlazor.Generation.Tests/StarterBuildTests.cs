using IzzyBlazor.Generation;

namespace IzzyBlazor.Generation.Tests;

/// <summary>
/// The other half of the phase 1 gate: what izzy-blazor makes builds. Five projects cover its layouts: the
/// default (Server, pages static by default); the default with Individual accounts; static with a Main method
/// and accounts, whose Counter answers by request; Auto with every page interactive (the layout and pages in
/// the .Client project) and accounts in LocalDB, under a name that needs a safe namespace; and a top bar with
/// a preset, a hex brand, a shell, a column width and no samples. The
/// IzzyBlazor packages are swapped for this repo's projects, so it builds against the code here, not a release.
/// </summary>
[Collection(ProjectBuild.Collection)]
public sealed class StarterBuildTests : IDisposable
{
    readonly string _work = Directory.CreateTempSubdirectory("izzy-starter-build-").FullName;

    public static TheoryData<string, string> Cases() => new()
    {
        { "MyApp", "" },
        { "MyApp", "Auth=Individual" },
        { "MyApp", "Interactivity=None;UseProgramMain=true;Auth=Individual" },
        { "My-App.2", "Interactivity=Auto;AllInteractive=true;Auth=Individual;UseLocalDB=true" },
        { "MyApp", "Nav=Top;Preset=Compact;Brand=#0a7d5a;Shell=Inset;ContentWidth=64;Empty=true" },
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
