using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using IzzyBlazor.Cli;
using IzzyBlazor.Generation;

namespace IzzyBlazor.Generation.Tests;

/// <summary>
/// The izzy CLI as a developer uses it: a new app, a block shown first as JSON, then added once, recorded in
/// .izzy/items.json; and the packed tool, installed from a feed, carrying the catalog with it.
/// </summary>
[Collection(ProjectBuild.Collection)]
public sealed class CliTests : IDisposable
{
    readonly string _work = Directory.CreateTempSubdirectory("izzy-cli-").FullName;

    (int Exit, string Out, string Error) Izzy(string directory, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exit = new Cli.Cli(Path.Combine(ProjectBuild.RepoRoot, "catalog"), directory, output, error, ProjectBuild.PrivateCatalog, ItemTier.Account).Run(args);
        return (exit, output.ToString(), error.ToString());
    }

    [AccountFact]
    public void A_new_app_takes_a_hero_once_and_records_both()
    {
        // An editor previews, then writes what it showed: the ports izzy picked given back, and the preview's hash.
        var app = Path.Combine(_work, "Acme");
        string[] starter = ["new", "starter", "Blazor", "-n", "Acme", "--no-restore"];
        var made = JsonNode.Parse(Izzy(_work, [.. starter, "--dry-run", "--json"]).Out)!;
        string[] generated = [.. made["generated"]!.AsObject().SelectMany(g => new[] { "--" + g.Key, (string)g.Value! })];
        Assert.Contains("--KestrelHttpPort", generated);
        Assert.Equal(0, Izzy(_work, [.. starter, .. generated, "--expect", (string)made["hash"]!]).Exit);

        // Ids are found in any case, and recorded as the catalog writes them.
        var preview = Izzy(app, "add", "block", "Hero", "--layout", "Split", "--dry-run", "--json");
        Assert.Equal(0, preview.Exit);
        var json = JsonNode.Parse(preview.Out)!;
        Assert.False((bool)json["written"]!);
        Assert.Equal(["Components/Blocks/Hero.razor", "wwwroot/images/hero-screenshot.svg"], json["creates"]!.AsArray().Select(c => (string)c!["path"]!));
        Assert.False(File.Exists(Path.Combine(app, "Components", "Blocks", "Hero.razor")));

        // A page changed after the preview: nothing is written, and the editor previews again.
        var homePath = Path.Combine(app, "Components", "Pages", "Home.razor");
        var before = File.ReadAllText(homePath);
        File.AppendAllText(homePath, "<p>Edited</p>\n");
        var stale = Izzy(app, "add", "block", "hero", "--layout", "Split", "--expect", (string)json["hash"]!);
        Assert.Equal(6, stale.Exit);
        Assert.Contains("Preview them again", stale.Error);
        Assert.False(File.Exists(Path.Combine(app, "Components", "Blocks", "Hero.razor")));
        File.WriteAllText(homePath, before);

        Assert.Equal(0, Izzy(app, "add", "block", "hero", "--layout", "Split", "--expect", (string)json["hash"]!).Exit);
        Assert.Contains("<Grid Columns=", File.ReadAllText(Path.Combine(app, "Components", "Blocks", "Hero.razor")));
        Assert.Contains("Nothing to change", Izzy(Path.Combine(app, "Components"), "add", "block", "hero", "--layout", "Split").Out);

        var other = Izzy(app, "add", "block", "hero", "--layout", "Centered");
        Assert.Equal(2, other.Exit);
        Assert.Contains("Components/Blocks/Hero.razor", other.Error);

        // A section goes below what the page already has, so a landing page builds top to bottom.
        Assert.Equal(0, Izzy(app, "add", "block", "features").Exit);
        var home = File.ReadAllText(Path.Combine(app, "Components", "Pages", "Home.razor")).TrimEnd();
        Assert.EndsWith("<Features />", home);
        Assert.True(home.IndexOf("<Hero />", StringComparison.Ordinal) < home.IndexOf("<Features />", StringComparison.Ordinal));
        Assert.Equal(3, Izzy(app, "add", "block", "features", "--layout", "Rows").Exit);

        var items = JsonNode.Parse(File.ReadAllText(Path.Combine(app, ".izzy", "items.json")))!["items"]!.AsArray();
        Assert.Equal(["blazor", "hero", "features"], items.Select(i => (string)i!["id"]!));
        Assert.Equal("Split", (string)items[1]!["settings"]!["Layout"]!);
    }

    // An editor's cursor: --at puts the hero on that line, and the page it names is the one checked for an h1.
    [AccountFact]
    public void At_a_line_the_hero_goes_there_instead_of_Home()
    {
        Assert.Equal(0, Izzy(_work, "new", "starter", "blazor", "-n", "Acme", "--no-restore").Exit);
        var app = Path.Combine(_work, "Acme");
        var home = File.ReadAllText(Path.Combine(app, "Components", "Pages", "Home.razor"));

        var added = Izzy(app, "add", "block", "hero", "--at", "Components/Pages/Counter.razor:3");
        Assert.Equal(0, added.Exit);
        Assert.Equal("<Hero />", File.ReadAllLines(Path.Combine(app, "Components", "Pages", "Counter.razor"))[2]);
        Assert.Equal(home, File.ReadAllText(Path.Combine(app, "Components", "Pages", "Home.razor")));
        Assert.Contains("Components/Pages/Counter.razor already has a level-1 heading", added.Error);
    }

    // Words a person writes can hold quotes, an ampersand or an @: they stay text in markup and in C# strings.
    [AccountFact]
    public void A_setting_s_words_are_escaped_where_they_land()
    {
        Assert.Equal(0, Izzy(_work, "new", "starter", "blazor", "-n", "Acme", "--no-restore").Exit);
        var app = Path.Combine(_work, "Acme");

        Assert.Equal(0, Izzy(app, "add", "block", "hero", "--eyebrow", "Say \"hi\" @you", "--headline", "Fast & <simple>").Exit);
        var hero = File.ReadAllText(Path.Combine(app, "Components", "Blocks", "Hero.razor"));
        Assert.Contains("Label=\"Say &quot;hi&quot; &#64;you\"", hero);
        Assert.Contains(">Fast &amp; &lt;simple&gt;</Heading>", hero);

        Assert.Equal(0, Izzy(app, "add", "block", "articles", "--link-base", "a\"b/").Exit);
        Assert.Contains("new(\"a\\\"b/blog/", File.ReadAllText(Path.Combine(app, "Components", "Blocks", "Articles.razor")));
    }

    [Fact]
    public void A_component_is_described_with_its_options_as_a_form_draws_them()
    {
        var button = JsonNode.Parse(Izzy(_work, "describe", "component", "button", "--json").Out)!;
        var variant = button["parameters"]!["properties"]!["Variant"]!;
        Assert.Contains("Outline", variant["enum"]!.AsArray().Select(v => (string)v!));
        Assert.Equal("ButtonVariant.{value}", (string)variant["x-write"]!);
        Assert.Equal("Solid", (string)variant["default"]!);
        Assert.Contains("usage", button["examples"]!.AsArray().Select(e => (string)e!["name"]!));

        // Every component and part in one answer, for an editor's inspector.
        var all = JsonNode.Parse(Izzy(_work, "describe", "component", "--all", "--json").Out)!;
        Assert.Contains("ActionColumn", all["components"]!.AsArray().Select(c => (string)c!["name"]!));

        // What a form shows first, as the library names it ([ImportantParameters]): a field's Label and Hint
        // from FieldBase, never where an example sits on its page (Grow).
        string[] Important(string name) => [.. all["components"]!.AsArray().Single(c => (string)c!["name"]! == name)!["parameters"]!["properties"]!.AsObject()
            .Where(p => (string?)p.Value!["x-priority"] == "important").Select(p => p.Key).Order(StringComparer.Ordinal)];
        Assert.Equal(["Caption", "EditMode", "FilterRow", "ItemKey", "Items", "PageSize", "Searchable", "SelectionMode"], Important("DataGrid"));
        Assert.Contains("Label", Important("Select"));
        Assert.DoesNotContain("Grow", Important("Card"));
    }

    // What IzzyTheme writes, for a preview with no app: a name as a meta tag, a hex as its palette.
    [Fact]
    public void A_theme_preview_gets_the_tags_IzzyTheme_writes()
    {
        var theme = JsonNode.Parse(Izzy(_work, "theme", "vars", "--preset", "compact", "--brand", "#0F766E", "--json").Out)!;
        Assert.Equal("compact", (string)theme["meta"]!.AsArray().Single(m => (string)m!["name"]! == "ib-preset")!["content"]!);
        Assert.Contains("--ib-color-primary: light-dark(#0f766e", (string)theme["css"]!);
        Assert.Contains("<style data-ib-theme-style>@layer izzy.tokens", (string)theme["head"]!);

        // The theme sample: the same settings, on the library's own components, linked to its stylesheets.
        var sample = JsonNode.Parse(Izzy(_work, "theme", "sample", "--preset", "compact", "--json").Out)!;
        var html = (string)sample["html"]!;
        Assert.True(File.Exists(Path.Combine((string)sample["assets"]!, "izzyblazor.css")));
        Assert.Contains("<meta name=\"ib-preset\" content=\"compact\" />", html);
        Assert.Equal(["shell", "type", "color", "actions", "forms", "data", "navigation", "feedback"],
            Regex.Matches(html, "data-izzy-section=\"([a-z]+)\"").Select(m => m.Groups[1].Value));
        Assert.Contains("href=\"chart.css\"", html);
    }

    [Fact]
    public async Task The_packed_tool_installs_and_carries_the_catalog()
    {
        var feed = Path.Combine(_work, "feed");
        var tools = Path.Combine(_work, "tools");
        await Run(ProjectBuild.RepoRoot, "pack", Path.Combine("src", "IzzyBlazor.Cli"), "-p:IsPackable=true", "-c", Configuration, "--no-build", "-o", feed);
        await Run(_work, "tool", "install", "IzzyBlazor.Cli", "--tool-path", tools, "--add-source", feed, "--version", Generator.IzzyBlazorVersion);

        var izzy = Path.Combine(tools, OperatingSystem.IsWindows() ? "izzy.exe" : "izzy");
        // The catalog's items, and the components from the data file the build makes.
        var list = await Run(_work, izzy, "list");
        Assert.Contains("hero", list);
        Assert.Contains("DataGrid", list);
        // Dot files ship too: --mcp writes .mcp.json from the catalog.
        await Run(_work, izzy, "new", "starter", "blazor", "-n", "App", "--mcp", "--no-restore");
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
