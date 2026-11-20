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

    /// <summary>
    /// The shells with files of their own, each in the settings that reach the most of them: accounts for the
    /// sidebar's foot and the inbox's prompt, and every page interactive in WebAssembly for the pages that
    /// move to the .Client project. rail also without accounts, the default.
    /// The sidebar and top navigation shells are izzy-blazor's own layouts.
    /// </summary>
    public static TheoryData<string, string> Shells() => new()
    {
        { "rail", "" },
        { "rail", "Auth=Individual" },
        { "site", "" },
        { "docs", "Interactivity=WebAssembly;AllInteractive=true;Auth=Individual" },
        { "inbox", "Interactivity=WebAssembly;AllInteractive=true;Auth=Individual" },
    };

    [Theory]
    [MemberData(nameof(Shells))]
    public async Task The_shell_starter_builds(string id, string settings)
    {
        var changes = Generator.Generate(ProjectBuild.Item(id), ProjectBuild.StarterSettings(settings), ProjectInfo.ForNewProject("MyApp"));
        ChangesetWriter.Apply(changes, _work);
        ProjectBuild.PointAtRepo(_work);
        await ProjectBuild.BuildAsync(_work, $"{id} {settings}");
    }

    [Fact]
    public async Task The_crm_kit_builds()
    {
        var changes = Generator.Generate(ProjectBuild.Item("crm"), ProjectBuild.StarterSettings(), ProjectInfo.ForNewProject("Acme"));
        ChangesetWriter.Apply(changes, _work);
        ProjectBuild.PointAtRepo(_work);
        await ProjectBuild.BuildAsync(_work, "crm");
    }

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
