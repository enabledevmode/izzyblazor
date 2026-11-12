using System.Diagnostics;
using IzzyBlazor.Generation;

namespace IzzyBlazor.Generation.Tests;

/// <summary>
/// The other half of the phase 1 gate: what izzy-blazor makes builds. Three projects cover its layouts: the
/// default (Server, pages static by default), static with no samples and a Main method, and Auto with every page
/// interactive (the layout and pages in the .Client project) under a name that needs a safe namespace. The
/// IzzyBlazor packages are swapped for this repo's projects, so it builds against the code here, not a release.
/// </summary>
public sealed class StarterBuildTests : IDisposable
{
    readonly string _work = Directory.CreateTempSubdirectory("izzy-starter-build-").FullName;

    public static TheoryData<string, string> Cases() => new()
    {
        { "MyApp", "" },
        { "MyApp", "Interactivity=None;Empty=true;UseProgramMain=true" },
        { "My-App.2", "Interactivity=Auto;AllInteractive=true" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task The_starter_builds(string name, string settings)
    {
        var root = RepoRoot();
        var item = ItemDefinition.FromDirectory(Path.Combine(root, "catalog", "izzy-blazor"));
        var values = settings.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Split('=')).ToDictionary(s => s[0], s => s[1]);
        values["KestrelHttpPort"] = "5100";
        values["KestrelHttpsPort"] = "7100";
        var changes = Generator.Generate(item, values, ProjectInfo.ForNewProject(name));
        ChangesetWriter.Apply(changes, _work);

        foreach (var project in Directory.EnumerateFiles(_work, "*.csproj", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(project);
            foreach (var package in new[] { "IzzyBlazor", "IzzyBlazor.Core" })
                text = text.Replace(
                    $"<PackageReference Include=\"{package}\" Version=\"{Generator.IzzyBlazorVersion}\" />",
                    $"<ProjectReference Include=\"{Path.Combine(root, "src", package, package + ".csproj")}\" />");
            File.WriteAllText(project, text);
        }

        // No MSBuild nodes or compiler server left running: they'd inherit the redirected output and keep
        // it open after the build ends.
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = _work, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "build", "-nologo", "-v", "q", "-nodeReuse:false", "-p:UseSharedCompilation=false", "-p:SkipAssetBuild=true" })
            start.ArgumentList.Add(arg);
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        using var process = Process.Start(start)!;
        var output = Task.WhenAll(process.StandardOutput.ReadToEndAsync(), process.StandardError.ReadToEndAsync());
        var finished = Task.WhenAll(process.WaitForExitAsync(), output);
        if (await Task.WhenAny(finished, Task.Delay(TimeSpan.FromMinutes(5))) != finished)
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail($"{name} {settings}: the build didn't finish in five minutes.");
        }
        var printed = await output;
        Assert.True(process.ExitCode == 0, $"{name} {settings} didn't build:\n{printed[0]}\n{printed[1]}");
    }

    static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "izzyblazor.sln"))) return dir.FullName;
        throw new InvalidOperationException("No izzyblazor.sln above the test folder.");
    }

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
