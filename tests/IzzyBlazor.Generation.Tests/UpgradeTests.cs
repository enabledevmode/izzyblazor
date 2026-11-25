using IzzyBlazor.Cli;

namespace IzzyBlazor.Generation.Tests;

/// <summary>
/// An app moved across releases that renamed and removed things: izzy update lists them, and izzy upgrade
/// renames what has a new name in the app's files and lists the rest by file and line. The changes are the
/// library's own, from alpha.16 to alpha.22.
/// </summary>
public sealed class UpgradeTests : IDisposable
{
    readonly string _app = Directory.CreateTempSubdirectory("izzy-upgrade-").FullName;

    (int Exit, string Out, string Error) Izzy(ItemTier access, params string[] args)
    {
        var (output, error) = (new StringWriter(), new StringWriter());
        return (new Cli.Cli(Path.Combine(ProjectBuild.RepoRoot, "catalog"), _app, output, error, access: access).Run(args), output.ToString(), error.ToString());
    }

    [Fact]
    public void Update_lists_what_the_releases_changed_and_upgrade_renames_it_in_the_app()
    {
        File.WriteAllText(Path.Combine(_app, "Acme.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <ItemGroup>
                <PackageReference Include="IzzyBlazor" Version="0.1.0-alpha.15" />
              </ItemGroup>
            </Project>
            """);
        var page = Path.Combine(_app, "Home.razor");
        File.WriteAllText(page, """
            <Icon Role="IconRole.Close" />
            <TreeView @bind-Expanded="_open" AriaLabel="Files" />
            <Step Hidden="true" Title="One" />

            @code {
                Color _fill = Color.SurfaceContainer;
            }
            """);

        var update = Izzy(ItemTier.Free, "update", "--to", "0.1.0-alpha.22", "--no-build");
        Assert.Equal(0, update.Exit);
        Assert.Contains("    Icon.Role → Purpose", update.Out);
        Assert.Contains("    Step.Hidden: Use Visible", update.Out);

        // Finding the uses is Pro's.
        Assert.Equal(3, Izzy(ItemTier.Free, "upgrade").Exit);

        // The release the app came from is the one update recorded.
        var upgrade = Izzy(ItemTier.Pro, "upgrade");
        Assert.Equal(0, upgrade.Exit);
        Assert.Equal("""
            <Icon Purpose="IconPurpose.Close" />
            <TreeView @bind-ExpandedItems="_open" Label="Files" />
            <Step Hidden="true" Title="One" />

            @code {
                Color _fill = Color.Fill;
            }
            """, File.ReadAllText(page));
        Assert.Contains("Home.razor:3:7  Step.Hidden: Use Visible", upgrade.Out);
        Assert.Contains("1 to change by hand", upgrade.Out);
    }

    public void Dispose() => Directory.Delete(_app, recursive: true);
}
