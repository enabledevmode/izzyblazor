using System.Diagnostics;
using IzzyBlazor.Generation;

namespace IzzyBlazor.Generation.Tests;

/// <summary>Builds a generated project against this repo's IzzyBlazor projects, not a released package.</summary>
internal static class ProjectBuild
{
    /// <summary>Tests that build this repo's projects share it, so they run one at a time: two builds at once lock each other's output.</summary>
    public const string Collection = "Builds against the repo";

    public static string RepoRoot { get; } = FindRepoRoot();

    /// <summary>
    /// The private catalog, which holds the account items' files: IZZY_PRO_CATALOG, else izzyblazor-pro-catalog
    /// beside this repo. Null where there is none (a fork, a clone without it): tests of account items skip.
    /// </summary>
    public static string? PrivateCatalog { get; } = FindPrivateCatalog();

    static string? FindPrivateCatalog()
    {
        var folder = Environment.GetEnvironmentVariable("IZZY_PRO_CATALOG") is { Length: > 0 } given
            ? given : Path.Combine(RepoRoot, "..", "izzyblazor-pro-catalog");
        return Directory.Exists(Path.Combine(folder, "crm")) ? Path.GetFullPath(folder) : null;
    }

    /// <summary>
    /// A catalog item from /catalog, through the catalog so a kit is over its base, with an account's access
    /// where the private catalog is.
    /// </summary>
    public static ItemDefinition Item(string id) => Catalog.Load(Path.Combine(RepoRoot, "catalog"), PrivateCatalog, ItemTier.Account)[id];

    /// <summary>The settings for blazor with its ports and GUID given, so nothing is random.</summary>
    public static Dictionary<string, string> StarterSettings(string settings = "")
    {
        var values = settings.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Split('=')).ToDictionary(s => s[0], s => s[1]);
        values["KestrelHttpPort"] = "5100";
        values["KestrelHttpsPort"] = "7100";
        values["UserSecretsGuid"] = "0f8fad5b-d9cb-469f-a165-70867728950e";
        return values;
    }

    /// <summary>Swaps the IzzyBlazor package references in <paramref name="directory"/> for this repo's projects.</summary>
    public static void PointAtRepo(string directory)
    {
        foreach (var project in Directory.EnumerateFiles(directory, "*.csproj", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(project);
            foreach (var package in new[] { "IzzyBlazor", "IzzyBlazor.Core" })
                text = text.Replace(
                    $"<PackageReference Include=\"{package}\" Version=\"{Generator.IzzyBlazorVersion}\" />",
                    $"<ProjectReference Include=\"{Path.Combine(RepoRoot, "src", package, package + ".csproj")}\" />");
            File.WriteAllText(project, text);
        }
    }

    /// <summary>Runs dotnet build in <paramref name="directory"/>; fails the test with the output when it doesn't build.</summary>
    public static async Task BuildAsync(string directory, string label)
    {
        // No MSBuild nodes or compiler server left running: they'd inherit the redirected output and keep
        // it open after the build ends.
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
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
            Assert.Fail($"{label}: the build didn't finish in five minutes.");
        }
        var printed = await output;
        Assert.True(process.ExitCode == 0, $"{label} didn't build:\n{printed[0]}\n{printed[1]}");
    }

    static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "izzyblazor.sln"))) return dir.FullName;
        throw new InvalidOperationException("No izzyblazor.sln above the test folder.");
    }
}
