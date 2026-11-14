using System.Diagnostics;
using System.Text.Json.Nodes;
using IzzyBlazor.Cli;
using IzzyBlazor.Generation;

namespace IzzyBlazor.Generation.Tests;

/// <summary>
/// The izzy CLI as a developer uses it: a new app, a block shown first as JSON, then added once, recorded in
/// .izzy/items.json; and the packed tool, installed from a feed, carrying the catalogue with it.
/// </summary>
[Collection(ProjectBuild.Collection)]
public sealed class CliTests : IDisposable
{
    readonly string _work = Directory.CreateTempSubdirectory("izzy-cli-").FullName;

    (int Exit, string Out, string Error) Izzy(string directory, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exit = new Cli.Cli(Path.Combine(ProjectBuild.RepoRoot, "catalog"), directory, output, error).Run(args);
        return (exit, output.ToString(), error.ToString());
    }

    [Fact]
    public void A_new_app_takes_a_hero_once_and_records_both()
    {
        Assert.Equal(0, Izzy(_work, "new", "izzy-blazor", "-n", "Acme", "--no-restore").Exit);
        var app = Path.Combine(_work, "Acme");

        var preview = Izzy(app, "add", "hero", "--layout", "Split", "--dry-run", "--json");
        Assert.Equal(0, preview.Exit);
        var json = JsonNode.Parse(preview.Out)!;
        Assert.False((bool)json["written"]!);
        Assert.Equal(["Components/Blocks/Hero.razor", "wwwroot/images/hero-screenshot.svg"], json["creates"]!.AsArray().Select(c => (string)c!["path"]!));
        Assert.False(File.Exists(Path.Combine(app, "Components", "Blocks", "Hero.razor")));

        Assert.Equal(0, Izzy(app, "add", "hero", "--layout", "Split").Exit);
        Assert.Contains("<Grid Columns=", File.ReadAllText(Path.Combine(app, "Components", "Blocks", "Hero.razor")));
        Assert.Contains("Nothing to change", Izzy(Path.Combine(app, "Components"), "add", "hero", "--layout", "Split").Out);

        var other = Izzy(app, "add", "hero", "--layout", "Centered");
        Assert.Equal(2, other.Exit);
        Assert.Contains("Components/Blocks/Hero.razor", other.Error);

        var items = JsonNode.Parse(File.ReadAllText(Path.Combine(app, ".izzy", "items.json")))!["items"]!.AsArray();
        Assert.Equal(["izzy-blazor", "hero"], items.Select(i => (string)i!["id"]!));
        Assert.Equal("Split", (string)items[1]!["settings"]!["Layout"]!);
    }

    // An editor's cursor: --at puts the hero on that line, and the page it names is the one checked for an h1.
    [Fact]
    public void At_a_line_the_hero_goes_there_instead_of_Home()
    {
        Assert.Equal(0, Izzy(_work, "new", "izzy-blazor", "-n", "Acme", "--no-restore").Exit);
        var app = Path.Combine(_work, "Acme");
        var home = File.ReadAllText(Path.Combine(app, "Components", "Pages", "Home.razor"));

        var added = Izzy(app, "add", "hero", "--at", "Components/Pages/Counter.razor:3");
        Assert.Equal(0, added.Exit);
        Assert.Equal("<Hero />", File.ReadAllLines(Path.Combine(app, "Components", "Pages", "Counter.razor"))[2]);
        Assert.Equal(home, File.ReadAllText(Path.Combine(app, "Components", "Pages", "Home.razor")));
        Assert.Contains("Components/Pages/Counter.razor already has a level-1 heading", added.Error);
    }

    [Fact]
    public async Task The_packed_tool_installs_and_carries_the_catalogue()
    {
        var feed = Path.Combine(_work, "feed");
        var tools = Path.Combine(_work, "tools");
        await Run(ProjectBuild.RepoRoot, "pack", Path.Combine("src", "IzzyBlazor.Cli"), "-p:IsPackable=true", "-c", Configuration, "--no-build", "-o", feed);
        await Run(_work, "tool", "install", "IzzyBlazor.Cli", "--tool-path", tools, "--add-source", feed, "--version", Generator.IzzyBlazorVersion);

        var izzy = Path.Combine(tools, OperatingSystem.IsWindows() ? "izzy.exe" : "izzy");
        Assert.Contains("hero", await Run(_work, izzy, "list"));
        // Dot files ship too: --mcp writes .mcp.json from the catalogue.
        await Run(_work, izzy, "new", "izzy-blazor", "-n", "App", "--mcp", "--no-restore");
        Assert.True(File.Exists(Path.Combine(_work, "App", ".mcp.json")));
    }

    // The tests' own build of the CLI (the test project references it), so packing builds nothing.
#if DEBUG
    const string Configuration = "Debug";
#else
    const string Configuration = "Release";
#endif

    static async Task<string> Run(string directory, string program, params string[] args)
    {
        if (!Path.IsPathRooted(program)) args = [program, .. args];
        var start = new ProcessStartInfo(Path.IsPathRooted(program) ? program : "dotnet") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in args) start.ArgumentList.Add(a);
        // Its own package cache, so the install takes this pack, not a copy of the same version from an earlier run.
        if (program == "tool") start.Environment["NUGET_PACKAGES"] = Path.Combine(directory, "packages");
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, $"{program} {string.Join(' ', args)} exited {process.ExitCode}:\n{await output}{await error}");
        return await output;
    }

    public void Dispose() => Directory.Delete(_work, recursive: true);
}
