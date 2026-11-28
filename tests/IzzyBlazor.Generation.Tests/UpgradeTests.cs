using System.Text.Json.Nodes;
using IzzyBlazor.Cli;

namespace IzzyBlazor.Generation.Tests;

/// <summary>
/// An app moved across releases that renamed and removed things: izzy update lists them, and izzy upgrade
/// renames what has a new name in the app's files, once --apply approves the plan, and lists the rest by file and line. The changes are the
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

    // An app on a release lists, for free, what it still uses that its release or one before renamed or removed:
    // a component's option, a type, and a CSS variable in its stylesheet, each with where and what replaces it.
    [Fact]
    public void Check_lists_uses_of_renamed_names_in_code_and_stylesheets()
    {
        File.WriteAllText(Path.Combine(_app, "Acme.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <ItemGroup>
                <PackageReference Include="IzzyBlazor" Version="0.1.0-alpha.22" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(_app, "Home.razor"), "<Icon Role=\"IconRole.Close\" />\n");
        Directory.CreateDirectory(Path.Combine(_app, "wwwroot"));
        File.WriteAllText(Path.Combine(_app, "wwwroot", "app.css"), ".promo { background: var(--ib-color-surface-container); }\n");

        var check = Izzy(ItemTier.Free, "check", "--json");
        Assert.Equal(0, check.Exit);
        var uses = JsonNode.Parse(check.Out)!["uses"]!.AsArray().Select(u => ((string)u!["path"]!, (int)u["line"]!, (string)u["name"]!, (string?)u["to"])).ToList();
        Assert.Contains(("Home.razor", 1, "Icon", "Purpose"), uses);
        Assert.Contains(("Home.razor", 1, "IconRole", "IconPurpose"), uses);
        Assert.Contains(("wwwroot/app.css", 1, "--ib-color-surface-container", "--ib-color-fill"), uses);
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

        // The plan and the renames are Pro's (izzy check lists the uses for free).
        Assert.Equal(3, Izzy(ItemTier.Free, "upgrade").Exit);

        // The release the app came from is the one update recorded. Without --apply it is a plan.
        var before = File.ReadAllText(page);
        Assert.Contains("Home.razor:1:7  Icon.Role → Purpose", Izzy(ItemTier.Pro, "upgrade").Out);
        Assert.Equal(before, File.ReadAllText(page));
        var upgrade = Izzy(ItemTier.Pro, "upgrade", "--apply");
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
