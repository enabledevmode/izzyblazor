using System.Diagnostics;
using Microsoft.AspNetCore.Components;

namespace IzzyBlazor.Generation.Tests;

/// <summary>
/// izzy init on Microsoft's own templates: the full Blazor Web App (its NavMenu clashes with ours, and it links
/// Bootstrap), Auto with every page interactive (the layout in the .Client project) and MVC (a Blazor root added
/// beside its views). Each builds afterwards against this repo.
/// </summary>
[Collection(ProjectBuild.Collection)]
public sealed class SetupTests : IDisposable
{
    readonly string _work = Directory.CreateTempSubdirectory("izzy-init-").FullName;

    [Theory]
    [InlineData("blazor")]
    [InlineData("blazor --interactivity Auto --all-interactive")]
    [InlineData("mvc")]
    public async Task Init_sets_up_Microsofts_template_and_it_builds(string template)
    {
        await DotnetNew(template);
        var app = Path.Combine(_work, "Acme");
        var host = File.Exists(Path.Combine(app, "Acme.csproj")) ? Path.Combine(app, "Acme.csproj") : Path.Combine(app, "Acme", "Acme.csproj");
        var project = Setup.Inspect(ProjectInfo.FromProject(host));
        var generated = Generator.Generate(ProjectBuild.Item("init"), new Dictionary<string, string>(), project);
        Assert.Empty(generated.Warnings);
        ChangesetWriter.Apply(new Changeset
        {
            Creates = generated.Creates, Inserts = [.. generated.Inserts, .. Setup.QualifyClashes(project)],
            Packages = generated.Packages, Conflicts = generated.Conflicts, Warnings = generated.Warnings,
        }, project.Directory!);

        if (template == "blazor")
        {
            Assert.Contains("<Acme.Components.Layout.NavMenu />", File.ReadAllText(Path.Combine(app, "Components", "Layout", "MainLayout.razor")));
            Assert.StartsWith("/* Bootstrap in a layer", File.ReadAllText(Path.Combine(app, "wwwroot", "app.css")));
            Assert.DoesNotContain("bootstrap", File.ReadAllText(Path.Combine(app, "Components", "App.razor")));
        }

        // The packages izzy init adds with dotnet add package, as this repo's projects.
        foreach (var package in generated.Packages)
        {
            var csproj = Directory.GetFiles(Path.Combine(project.Directory!, package.Project), "*.csproj").Single();
            File.WriteAllText(csproj, File.ReadAllText(csproj).Replace("</Project>",
                $"  <ItemGroup><ProjectReference Include=\"{Path.Combine(ProjectBuild.RepoRoot, "src", package.Id, package.Id + ".csproj")}\" /></ItemGroup>\n</Project>"));
        }
        await ProjectBuild.BuildAsync(app, $"dotnet new {template}, then init");
    }

    // A component added to the library and missing here would clash in an app with no fix written for it.
    [Fact]
    public void The_clash_list_names_every_component()
    {
        var names = typeof(IzzyBlazor.Button).Assembly.GetExportedTypes()
            .Where(t => !t.IsAbstract && typeof(IComponent).IsAssignableFrom(t) && t.Namespace == "IzzyBlazor")
            .Select(t => t.Name.Split('`')[0])
            .Distinct().Order(StringComparer.Ordinal);
        var expected = string.Join("\n", names) + "\n";
        var path = Path.Combine(ProjectBuild.RepoRoot, "src", "IzzyBlazor.Generation", "components.txt");
        if (Environment.GetEnvironmentVariable("IZZY_UPDATE_COMPONENTS") == "1") File.WriteAllText(path, expected);
        Assert.True(expected == File.ReadAllText(path).ReplaceLineEndings("\n"),
            "src/IzzyBlazor.Generation/components.txt is out of date; set IZZY_UPDATE_COMPONENTS=1 and run this test.");
    }

    async Task DotnetNew(string template)
    {
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = _work, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in $"new {template} -o Acme --no-restore".Split(' ')) start.ArgumentList.Add(arg);
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";
        using var process = Process.Start(start)!;
        var output = Task.WhenAll(process.StandardOutput.ReadToEndAsync(), process.StandardError.ReadToEndAsync());
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, $"dotnet new {template}: {string.Join("\n", await output)}");
    }

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
