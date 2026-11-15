using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using IzzyBlazor.Generation;

namespace IzzyBlazor.Cli;

/// <summary>
/// The izzy commands. Exit codes: 0 done, 1 an error, 2 files exist with other content (nothing written), 3 a Pro
/// item or choice.
/// </summary>
/// <param name="catalogRoot">The free catalogue folder.</param>
/// <param name="workingDirectory">Where relative paths start and the project is looked for.</param>
/// <param name="output">Results.</param>
/// <param name="error">Errors and warnings.</param>
/// <param name="proRoot">The Pro catalogue, for working on it (IZZY_PRO_CATALOG); null for the free catalogue only.</param>
sealed class Cli(string catalogRoot, string workingDirectory, TextWriter output, TextWriter error, string? proRoot = null)
{
    const string Help = """
        izzy: IzzyBlazor apps, blocks and pages from IzzyBlazor's catalogue.

        Usage:
          izzy list [--kind starter|page|block|section|recipe] [--json]
          izzy describe <item> [--json]
          izzy add <item> [--<setting> <value> ...] [--at <file>:<line>] [--project <path>] [--dry-run] [--diff] [--json] [--overwrite]
          izzy add crud <Model> [--name <Plural>] [--singular <words>] [--plural <words>] [--route <path>] [--mode Grid|Pages] [--store Ef|Memory] [--context <DbContext>] [--authorize]
          izzy new <starter> [-n <name>] [-o <folder>] [--<setting> <value> ...] [--dry-run] [--json] [--overwrite]
          izzy init [--preset <preset>] [--brand <color>] [--interactivity None|Server] [--keep-css] [--mcp] [--project <path>] [--no-build] [--dry-run] [--diff] [--json]

        izzy add crud makes a page whose grid adds, edits and deletes a model's records (list, detail and
        form pages are --mode pages, Pro), for a model class in your app read from its source: no build needed.
        izzy init sets up IzzyBlazor in the app you have: a Blazor Web App, or an MVC or Razor Pages app, which
        gets a Blazor root beside its pages. It builds the app afterwards and says how to fix a name clash.
        Settings are the item's, as izzy describe <item> lists them, such as --layout Split.
        --dry-run writes nothing; --diff shows the changes as a unified diff; --json prints them as data.
        Files that exist with other content are never replaced without --overwrite.
        """;

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public int Run(string[] arguments)
    {
        try
        {
            var args = Args.Parse(arguments);
            if (args.Words.Count == 0)
            {
                if (args.Has("version")) output.WriteLine(Generator.IzzyBlazorVersion);
                else output.Write(Help.ReplaceLineEndings("\n") + "\n");
                return 0;
            }
            if (args.Switch("help"))
            {
                output.Write(Help.ReplaceLineEndings("\n") + "\n");
                return 0;
            }
            return args.Words[0] switch
            {
                "list" => List(args),
                "describe" => Describe(args),
                "add" => Add(args),
                "new" => New(args),
                "init" => Init(args),
                var other => throw new UsageException($"Unknown command \"{other}\". Commands: list, describe, add, new, init."),
            };
        }
        catch (ProRequiredException e)
        {
            error.WriteLine($"{e.Message} Pro items arrive with IzzyBlazor Pro.");
            return 3;
        }
        catch (Exception e) when (e is GenerationException or UsageException)
        {
            error.WriteLine(e.Message);
            return 1;
        }
    }

    Catalog Catalog => field ??= Catalog.Load(catalogRoot, proRoot);

    int List(Args args)
    {
        Words(args, 1, "izzy list");
        var kind = args.Take("kind");
        var json = args.Switch("json");
        NoSettings(args);
        var items = Catalog.Items.Values.Select(i => i.Manifest)
            .Where(m => m.Kind != ItemKind.Setup)
            .Where(m => kind is null || m.Kind.ToString().Equals(kind, StringComparison.OrdinalIgnoreCase)).ToList();
        if (json)
        {
            Print(new JsonArray(items.Select(m => (JsonNode)new JsonObject
            {
                ["id"] = m.Id, ["name"] = m.Name, ["kind"] = Lower(m.Kind), ["category"] = m.Category,
                ["tier"] = Lower(m.Tier), ["summary"] = m.Summary,
                ["proChoices"] = ProChoices(m),
            }).ToArray()));
            return 0;
        }
        var width = items.Count == 0 ? 0 : items.Max(m => m.Id.Length);
        foreach (var m in items)
        {
            var pro = m.Parameters.Where(p => p.ProChoices.Count > 0).Select(p => $"{p.Name} {string.Join(", ", p.ProChoices)}").ToList();
            output.WriteLine($"{m.Id.PadRight(width)}  {Lower(m.Kind),-7}  {m.Summary}{(pro.Count > 0 ? $" (Pro: {string.Join("; ", pro)})" : "")}");
        }
        return 0;
    }

    int Describe(Args args)
    {
        var item = Item(args, "izzy describe <item>");
        var json = args.Switch("json");
        NoSettings(args);
        var m = item.Manifest;
        if (json)
        {
            // The schema as written, with each setting's option, for forms and MCP tool inputs.
            var schema = m.Schema.DeepClone().AsObject();
            foreach (var p in m.Parameters) schema["properties"]![p.Name]!["x-option"] = p.Option;
            Print(new JsonObject
            {
                ["id"] = m.Id, ["name"] = m.Name, ["kind"] = Lower(m.Kind), ["category"] = m.Category, ["tier"] = Lower(m.Tier),
                ["summary"] = m.Summary,
                ["components"] = new JsonArray(m.Components.Select(c => (JsonNode)c).ToArray()),
                ["modes"] = new JsonObject(m.Modes.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value))),
                ["accessibility"] = new JsonArray(m.Accessibility.Select(a => (JsonNode)a).ToArray()),
                ["parameters"] = schema,
            });
            return 0;
        }
        output.WriteLine($"{m.Name} ({m.Id}), {Lower(m.Kind)}{(m.Tier == ItemTier.Pro ? ", Pro" : "")}");
        if (m.Summary is not null) output.WriteLine(m.Summary);
        output.WriteLine();
        output.WriteLine(m.Kind == ItemKind.Starter ? $"izzy new {m.Id} -n <name> [settings]" : $"izzy add {m.Id} [settings]");
        output.WriteLine();
        output.WriteLine("Settings:");
        foreach (var p in m.Parameters.Where(p => !p.Hidden))
        {
            var values = p.Type switch
            {
                ParameterType.Choice => string.Join(" | ", p.Choices.Select(c => p.ProChoices.Contains(c) ? $"{c} (Pro)" : c)),
                ParameterType.Bool => "true | false",
                ParameterType.Int => "<number>",
                _ => "<text>",
            };
            output.WriteLine($"  --{p.Option} {values}{(p.Default.Length > 0 ? $"  (default {p.Default})" : "")}");
            if (p.Description is not null) output.WriteLine($"      {p.Description}");
        }
        return 0;
    }

    int Add(Args args)
    {
        // izzy add crud <Model>: the one item with a word after its id.
        var crud = args.Words.Count == 3 && args.Words[1] == "crud";
        if (args.Words is [_, "crud"]) throw new UsageException("Usage: izzy add crud <Model>, such as izzy add crud Customer.");
        var item = crud ? Catalog["crud"] : Item(args, "izzy add <item>");
        if (item.Manifest.Kind == ItemKind.Setup) throw new UsageException("IzzyBlazor's setup runs with izzy init.");
        if (item.Manifest.Kind == ItemKind.Starter)
            throw new UsageException($"{item.Manifest.Id} makes a new project: izzy new {item.Manifest.Id} -n <name>.");
        var project = ProjectInfo.FromProject(FindProject(args.Take("project")));
        var host = Path.Combine(project.Directory!, project.Host, project.Name + ".csproj");
        if (!ReferencesIzzyBlazor(File.ReadAllText(host)))
            throw new UsageException($"{Path.GetFileName(host)} doesn't reference IzzyBlazor. Run izzy init first.");
        var at = Placement(args.Take("at"), project);
        var flags = Flags(args);
        var settings = Settings(item.Manifest, args);
        var notes = new List<string>();
        if (crud) project = Crud.Prepare(project, args.Words[2], settings, notes);
        var changes = Generator.Generate(item, settings, project, at);
        if (notes.Count > 0)
            changes = new Changeset
            {
                Creates = changes.Creates, Inserts = changes.Inserts, Packages = changes.Packages, Conflicts = changes.Conflicts,
                Warnings = [.. notes, .. changes.Warnings], Settings = changes.Settings, Restore = changes.Restore,
            };
        return Finish(item, changes, project, project.Directory!, existing: true, flags);
    }

    int New(Args args)
    {
        var item = Item(args, "izzy new <starter>");
        if (item.Manifest.Kind != ItemKind.Starter)
            throw new UsageException($"{item.Manifest.Id} goes into a project you have: izzy add {item.Manifest.Id}.");
        var name = args.Take("name");
        var folder = args.Take("output");
        var directory = Path.GetFullPath(folder ?? name ?? ".", workingDirectory);
        name ??= Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var flags = Flags(args);
        var project = ProjectInfo.ForNewProject(name) with { ReadFile = path => ReadFile(directory, path) };
        var changes = Generator.Generate(item, Settings(item.Manifest, args), project);
        return Finish(item, changes, project, directory, existing: false, flags);
    }

    // IzzyBlazor into the app here: the init item over what Setup reads from the app, the full names of the
    // app's components named like ours, then a build, so a clash init couldn't fix is reported with its fix.
    int Init(Args args)
    {
        Words(args, 1, "izzy init");
        var build = !args.Switch("no-build");
        var project = Setup.Inspect(ProjectInfo.FromProject(FindProject(args.Take("project"))));
        var flags = Flags(args);
        var item = Catalog["init"];
        var settings = Settings(item.Manifest, args);
        var blazor = (bool)project.Facts["Blazor"];
        var warnings = new List<string>();
        var appRazor = project.ReadFile(project.Host + "Components/App.razor") ?? "";
        if (blazor && settings.Remove("Interactivity", out var interactivity))
        {
            // Init's own Blazor root, from an earlier run: the same command again is fine, a new mode is by hand.
            if (!appRazor.Contains("from izzy init", StringComparison.Ordinal))
                throw new UsageException($"{project.Name} is a Blazor Web App, which keeps its own render modes: --interactivity is for MVC and Razor Pages apps.");
            var program = project.ReadFile(project.Host + "Program.cs") ?? "";
            if (interactivity.Equals("Server", StringComparison.OrdinalIgnoreCase) && !program.Contains("AddInteractiveServerComponents", StringComparison.Ordinal))
                warnings.Add("IzzyBlazor is set up here already, with every page static. For pages interactive over a circuit, add .AddInteractiveServerComponents() after AddRazorComponents() and .AddInteractiveServerRenderMode() after MapRazorComponents<App>() in Program.cs.");
        }
        if ((bool)project.Facts["HasTheme"] && (settings.ContainsKey("Preset") || settings.ContainsKey("Brand")))
            warnings.Add("App.razor has an IzzyTheme already, so it wasn't changed: set Preset and Brand on it there.");

        var generated = Generator.Generate(item, settings, project);
        warnings.InsertRange(0, generated.Warnings);
        if (blazor && !(bool)project.Facts["HasLayout"])
            warnings.Add("No layout found from the Router's DefaultLayout, so no ToastRegion was added: put <ToastRegion /> in your layout to show toasts.");
        if (blazor && Setup.UnlayeredElementRules(project) is { Count: > 0 } rules)
            warnings.Add($"{project.Host}wwwroot/app.css styles elements outside a cascade layer, so they win over IzzyBlazor's components: {string.Join("; ", rules)}. "
                // Bootstrap in a layer now, or from this run.
                + (project.ReadFile(project.Host + "wwwroot/app.css")!.Contains("layer(bootstrap)", StringComparison.Ordinal)
                    || (bool)project.Facts["BootstrapLinked"] && (bool)project.Facts["AppCss"] && generated.Settings["KeepCss"] == "false"
                    ? "Move them into @layer bootstrap { } in app.css to keep them for your pages"
                    : "Remove them, or keep them only for the pages that need them")
                + ": https://izzyblazor.com/docs/get-started/existing-app#keep-bootstrap");
        // An AI assistant's config of the user's own is theirs: kept, with the server to add named.
        var configs = generated.Conflicts.Where(c => c.EndsWith("mcp.json", StringComparison.Ordinal)).ToList();
        foreach (var config in configs)
            warnings.Add($"{config} is there already, so it was kept: add IzzyBlazor's docs server to it, \"izzyblazor\": {{ \"type\": \"http\", \"url\": \"https://izzyblazor.com/mcp\" }}.");
        var changes = new Changeset
        {
            Creates = [.. generated.Creates.Where(c => !configs.Contains(c.Path))],
            Inserts = [.. generated.Inserts, .. Setup.QualifyClashes(project)],
            // A package the project has already is nothing to do.
            Packages = [.. generated.Packages.Where(p => !References(Path.Combine(project.Directory!, p.Project), p.Id))],
            Conflicts = [.. generated.Conflicts.Except(configs)],
            Warnings = warnings,
            Settings = generated.Settings,
        };
        var writes = !flags.DryRun && (changes.Conflicts.Count == 0 || flags.Overwrite)
            && (changes.Creates.Count > 0 || changes.Inserts.Count > 0 || changes.Packages.Count > 0);
        var exit = Finish(item, changes, project, project.Directory!, existing: true, flags);
        return exit == 0 && writes && build ? Build(project, flags.Json) : exit;
    }

    static bool References(string folder, string package) =>
        Directory.GetFiles(folder, "*.csproj").FirstOrDefault() is { } csproj
        && Regex.IsMatch(File.ReadAllText(csproj), $"Include=\"{Regex.Escape(package)}\"", RegexOptions.IgnoreCase);

    // dotnet build of the server project (and the .Client one it references); a failure prints its errors,
    // and the fix for a name clash.
    int Build(ProjectInfo project, bool quiet)
    {
        var csproj = Path.Combine(project.Directory!, project.Host, project.Name + ".csproj");
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = project.Directory!, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in new[] { "build", csproj, "-nologo", "-v", "q" }) start.ArgumentList.Add(a);
        using var process = Process.Start(start) ?? throw new GenerationException("Couldn't start dotnet.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        if (process.ExitCode == 0)
        {
            if (!quiet) output.WriteLine($"Built {project.Name}");
            return 0;
        }
        // Each error once, its file from the app's folder, without the project it came from.
        var root = Path.GetFullPath(project.Directory!).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var errors = (stdout.Result + stderr.Result).Split('\n').Select(l => l.Trim())
            .Where(l => l.Contains(": error ", StringComparison.Ordinal))
            .Select(l => Regex.Replace(l.Replace(root, "", StringComparison.OrdinalIgnoreCase), @"\s*\[[^\]]+\.csproj\]$", ""))
            .Distinct().ToList();
        error.WriteLine($"{project.Name} doesn't build now:");
        foreach (var line in errors.Take(10)) error.WriteLine("  " + line);
        if (errors.Count == 0) error.WriteLine(stdout.Result + stderr.Result);
        if (errors.Any(e => e.Contains("RZ9985", StringComparison.Ordinal)))
            error.WriteLine("A component of yours has the name of one of IzzyBlazor's. Rename yours, or write its full name where you use it (<MyApp.Components.Layout.NavMenu />): https://izzyblazor.com/docs/get-started/existing-app#name-clashes");
        if (errors.Any(e => e.Contains("CS0104", StringComparison.Ordinal)))
            error.WriteLine("A type of yours has the name of one of IzzyBlazor's, such as Status. Qualify one where it's used (IzzyBlazor.Status.Success), or add an alias to _Imports.razor (@using Status = IzzyBlazor.Status): https://izzyblazor.com/docs/get-started/existing-app#name-clashes");
        return 1;
    }

    // Shows or writes a changeset, then adds packages, restores and records the item.
    int Finish(ItemDefinition item, Changeset changes, ProjectInfo project, string directory, bool existing, (bool Json, bool DryRun, bool Diff, bool Overwrite) flags)
    {
        var blocked = changes.Conflicts.Count > 0 && !flags.Overwrite;
        var write = !flags.DryRun && !blocked;
        if (flags.Diff) output.Write(ChangesetWriter.Diff(changes, project));
        if (flags.Json) Print(ChangesetJson(changes, write));
        foreach (var warning in changes.Warnings) error.WriteLine($"warning: {warning}");
        if (blocked)
        {
            error.WriteLine($"These files exist with other content, so nothing was written: {string.Join(", ", changes.Conflicts)}. Compare them with --diff, or replace them with --overwrite.");
            return 2;
        }
        if (changes.Creates.Count == 0 && changes.Inserts.Count == 0 && changes.Packages.Count == 0)
        {
            // An item with no file of its own and no page named (a recipe without --at) was printed above.
            if (!flags.Json && !changes.Warnings.Any(w => w.Contains("isn't placed on a page", StringComparison.Ordinal)))
                output.WriteLine(item.Manifest.Kind == ItemKind.Setup
                    ? "Nothing to change: IzzyBlazor is set up here."
                    : $"Nothing to change: {item.Manifest.Id} is already there with these settings.");
            return 0;
        }
        if (!write)
        {
            if (!flags.Json && !flags.Diff) Summary(changes, "Would create", "Would change", "Would add package");
            return 0;
        }

        ChangesetWriter.Apply(changes, directory, flags.Overwrite);
        var quiet = flags.Json;
        if (!quiet) Summary(changes, "Created", "Changed", "Added package");
        // A new starter's packages are in the project files it wrote; an existing app's go in the project each names.
        if (existing)
            foreach (var package in changes.Packages)
            {
                var folder = Path.Combine(directory, package.Project);
                var csproj = Directory.GetFiles(folder, "*.csproj").FirstOrDefault()
                    ?? throw new UsageException($"No .csproj file in {package.Project} for {package.Id}.");
                if (!Regex.IsMatch(File.ReadAllText(csproj), $"Include=\"{Regex.Escape(package.Id)}\"", RegexOptions.IgnoreCase))
                    Dotnet(directory, quiet, "add", csproj, "package", package.Id, "--version", package.Version);
            }
        if (changes.Restore) Dotnet(directory, quiet, "restore");
        Record(directory, item.Manifest.Id, changes.Settings);
        return 0;
    }

    void Summary(Changeset changes, string create, string change, string package)
    {
        foreach (var c in changes.Creates) output.WriteLine($"{create} {c.Path}");
        foreach (var path in changes.Inserts.Select(i => i.Path).Distinct()) output.WriteLine($"{change} {path}");
        foreach (var p in changes.Packages) output.WriteLine($"{package} {p.Id} {p.Version}");
    }

    // .izzy/items.json: each item made here, with its version and settings, for izzy upgrade to start from.
    static void Record(string directory, string id, IReadOnlyDictionary<string, string> settings)
    {
        var path = Path.Combine(directory, ".izzy", "items.json");
        var root = File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? [] : [];
        if (root["items"] is not JsonArray items) root["items"] = items = [];
        items.Add(new JsonObject
        {
            ["id"] = id,
            ["version"] = Generator.IzzyBlazorVersion,
            ["settings"] = new JsonObject(settings.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value))),
        });
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, root.ToJsonString(Json) + "\n");
    }

    void Dotnet(string directory, bool quiet, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in arguments) start.ArgumentList.Add(a);
        using var process = Process.Start(start) ?? throw new GenerationException("Couldn't start dotnet.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        if (process.ExitCode != 0)
            error.WriteLine($"warning: dotnet {string.Join(' ', arguments)} failed:\n{stdout.Result}{stderr.Result}");
        else if (!quiet && arguments[0] == "restore") output.WriteLine("Restored packages");
    }

    ItemDefinition Item(Args args, string usage)
    {
        Words(args, 2, usage);
        return Catalog[args.Words[1]];
    }

    static void Words(Args args, int count, string usage)
    {
        if (args.Words.Count != count) throw new UsageException($"Usage: {usage}");
    }

    static (bool Json, bool DryRun, bool Diff, bool Overwrite) Flags(Args args) =>
        (args.Switch("json"), args.Switch("dry-run"), args.Switch("diff"), args.Switch("overwrite"));

    static void NoSettings(Args args)
    {
        if (args.Options.Count > 0) throw new UsageException($"Unknown option --{args.Options[0].Name}.");
    }

    // What's left of the options are the item's settings, by option name.
    static Dictionary<string, string> Settings(ItemManifest manifest, Args args)
    {
        var settings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (option, value) in args.Options)
        {
            var p = manifest.Parameters.FirstOrDefault(p => p.Option.Equals(option, StringComparison.OrdinalIgnoreCase))
                ?? throw new UsageException($"{manifest.Id} has no setting --{option}. Settings: {string.Join(", ", manifest.Parameters.Where(p => !p.Hidden).Select(p => "--" + p.Option))}.");
            settings[p.Name] = value ?? (p.Type == ParameterType.Bool ? "true"
                : throw new UsageException($"--{option} needs a value{(p.Choices.Count > 0 ? ": " + string.Join(", ", p.Choices) : "")}."));
        }
        return settings;
    }

    // The project: --project (a folder or .csproj), else the nearest folder up from here with one .csproj, or
    // with a solution whose one web project is it. Either project of an app with a .Client project will do.
    string FindProject(string? given)
    {
        if (given is not null)
        {
            var full = Path.GetFullPath(given, workingDirectory);
            if (full.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) && File.Exists(full)) return full;
            if (!Directory.Exists(full)) throw new UsageException($"No project at {given}.");
            return OneProject(full) ?? throw new UsageException($"No .csproj file in {given}.");
        }
        for (var dir = new DirectoryInfo(workingDirectory); dir is not null; dir = dir.Parent)
            if (OneProject(dir.FullName) is { } csproj) return csproj;
        throw new UsageException("No .csproj file here or in a folder above. Run izzy in your project, or give --project <path>.");
    }

    static string? OneProject(string directory)
    {
        var projects = Directory.GetFiles(directory, "*.csproj");
        if (projects.Length > 1) throw new UsageException($"More than one .csproj file in {directory}; pick one with --project.");
        if (projects.Length == 1) return projects[0];
        // A solution folder: the web project it lists (MyApp, not MyApp.Client).
        var solutions = Directory.GetFiles(directory, "*.sln").Concat(Directory.GetFiles(directory, "*.slnx")).ToList();
        if (solutions.Count != 1) return null;
        var web = Regex.Matches(File.ReadAllText(solutions[0]), @"[\w.\-\\/ ]+\.csproj")
            .Select(m => Path.GetFullPath(Path.Combine(directory, m.Value.Trim().Replace('\\', Path.DirectorySeparatorChar))))
            .Where(File.Exists).Distinct()
            .Where(p => File.ReadAllText(p).Contains("Sdk=\"Microsoft.NET.Sdk.Web\"", StringComparison.OrdinalIgnoreCase))
            .ToList();
        // Two web projects (the app and an API): the one with a .Client project, else ask.
        if (web.Count > 1 && web.Where(p => Regex.IsMatch(File.ReadAllText(p), @"ProjectReference\s+Include=""[^""]*\.Client\.csproj""", RegexOptions.IgnoreCase)).ToList() is [var app]) return app;
        if (web.Count > 1)
            throw new UsageException($"{Path.GetFileName(solutions[0])} has more than one web project ({string.Join(", ", web.Select(Path.GetFileNameWithoutExtension))}); pick one with --project.");
        return web.Count == 1 ? web[0] : null;
    }

    // --at <file>:<line>, the file relative to here (or absolute, as an editor gives it), inside the project.
    Placement? Placement(string? at, ProjectInfo project)
    {
        if (at is null) return null;
        var colon = at.LastIndexOf(':');
        if (colon <= 0 || !int.TryParse(at[(colon + 1)..], out var line) || line < 1)
            throw new UsageException($"--at is <file>:<line>, such as Components/Pages/Home.razor:12, not \"{at}\".");
        try { return new Placement(project.Relative(Path.GetFullPath(at[..colon], workingDirectory)), line); }
        catch (GenerationException) { throw new UsageException($"{at[..colon]} isn't in the project."); }
    }

    static bool ReferencesIzzyBlazor(string csproj) =>
        Regex.IsMatch(csproj, """PackageReference\s+Include="IzzyBlazor(\.Core)?"|ProjectReference\s+Include="[^"]*IzzyBlazor(\.Core)?\.csproj" """.TrimEnd(), RegexOptions.IgnoreCase);

    static string? ReadFile(string root, string path)
    {
        var full = Path.GetFullPath(Path.Combine(root, path));
        if (!full.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase)) throw new GenerationException($"{path} is outside the project.");
        return File.Exists(full) ? File.ReadAllText(full) : null;
    }

    static JsonObject ChangesetJson(Changeset c, bool written) => new()
    {
        ["written"] = written,
        ["creates"] = new JsonArray(c.Creates.Select(f => (JsonNode)(f.Text is not null
            ? new JsonObject { ["path"] = f.Path, ["text"] = f.Text }
            : new JsonObject { ["path"] = f.Path, ["base64"] = Convert.ToBase64String(f.Bytes!) })).ToArray()),
        ["inserts"] = new JsonArray(c.Inserts.Select(i => (JsonNode)new JsonObject
        {
            ["path"] = i.Path, ["offset"] = i.Offset, ["length"] = i.Length, ["text"] = i.Text, ["originalHash"] = i.OriginalHash,
        }).ToArray()),
        ["packages"] = new JsonArray(c.Packages.Select(p => (JsonNode)new JsonObject { ["id"] = p.Id, ["version"] = p.Version }).ToArray()),
        ["conflicts"] = new JsonArray(c.Conflicts.Select(p => (JsonNode)p).ToArray()),
        ["warnings"] = new JsonArray(c.Warnings.Select(w => (JsonNode)w).ToArray()),
        ["settings"] = new JsonObject(c.Settings.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value))),
    };

    static JsonObject ProChoices(ItemManifest m) =>
        new(m.Parameters.Where(p => p.ProChoices.Count > 0)
            .Select(p => KeyValuePair.Create(p.Name, (JsonNode?)new JsonArray(p.ProChoices.Select(c => (JsonNode)c).ToArray()))));

    void Print(JsonNode node) => output.Write(node.ToJsonString(Json).ReplaceLineEndings("\n") + "\n");

    static string Lower<T>(T value) where T : struct, Enum => value.ToString().ToLowerInvariant();
}
