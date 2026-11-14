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
sealed class Cli(string catalogRoot, string workingDirectory, TextWriter output, TextWriter error)
{
    const string Help = """
        izzy: IzzyBlazor apps, blocks and pages from IzzyBlazor's catalogue.

        Usage:
          izzy list [--kind starter|page|block|section|recipe] [--json]
          izzy describe <item> [--json]
          izzy add <item> [--<setting> <value> ...] [--at <file>:<line>] [--project <path>] [--dry-run] [--diff] [--json] [--overwrite]
          izzy new <starter> [-n <name>] [-o <folder>] [--<setting> <value> ...] [--dry-run] [--json] [--overwrite]

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
                var other => throw new UsageException($"Unknown command \"{other}\". Commands: list, describe, add, new."),
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

    Catalog Catalog => field ??= Catalog.Load(catalogRoot);

    int List(Args args)
    {
        Words(args, 1, "izzy list");
        var kind = args.Take("kind");
        var json = args.Switch("json");
        NoSettings(args);
        var items = Catalog.Items.Values.Select(i => i.Manifest)
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
        var item = Item(args, "izzy add <item>");
        if (item.Manifest.Kind == ItemKind.Starter)
            throw new UsageException($"{item.Manifest.Id} makes a new project: izzy new {item.Manifest.Id} -n <name>.");
        var (directory, csproj) = FindProject(args.Take("project"));
        if (!ReferencesIzzyBlazor(File.ReadAllText(csproj)))
            throw new UsageException($"{Path.GetFileName(csproj)} doesn't reference IzzyBlazor. Add the IzzyBlazor package first.");
        var at = Placement(args.Take("at"), directory);
        var flags = Flags(args);
        var project = ProjectInfo.FromDirectory(directory);
        var changes = Generator.Generate(item, Settings(item.Manifest, args), project, at);
        return Finish(item, changes, project, directory, csproj, flags);
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
        return Finish(item, changes, project, directory, csproj: null, flags);
    }

    // Shows or writes a changeset, then adds packages, restores and records the item.
    int Finish(ItemDefinition item, Changeset changes, ProjectInfo project, string directory, string? csproj, (bool Json, bool DryRun, bool Diff, bool Overwrite) flags)
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
            if (!flags.Json) output.WriteLine($"Nothing to change: {item.Manifest.Id} is already there with these settings.");
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
        if (csproj is not null)
        {
            var text = File.ReadAllText(csproj);
            foreach (var package in changes.Packages.Where(p => !Regex.IsMatch(text, $"Include=\"{Regex.Escape(p.Id)}\"", RegexOptions.IgnoreCase)))
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

    // The project: --project (a folder or .csproj), else the nearest folder up from here with one .csproj.
    (string Directory, string Csproj) FindProject(string? given)
    {
        if (given is not null)
        {
            var full = Path.GetFullPath(given, workingDirectory);
            if (full.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) && File.Exists(full)) return (Path.GetDirectoryName(full)!, full);
            if (!Directory.Exists(full)) throw new UsageException($"No project at {given}.");
            return (full, OneProject(full) ?? throw new UsageException($"No .csproj file in {given}."));
        }
        for (var dir = new DirectoryInfo(workingDirectory); dir is not null; dir = dir.Parent)
            if (OneProject(dir.FullName) is { } csproj) return (dir.FullName, csproj);
        throw new UsageException("No .csproj file here or in a folder above. Run izzy in your project, or give --project <path>.");
    }

    static string? OneProject(string directory)
    {
        var projects = Directory.GetFiles(directory, "*.csproj");
        return projects.Length switch
        {
            0 => null,
            1 => projects[0],
            _ => throw new UsageException($"More than one .csproj file in {directory}; pick one with --project."),
        };
    }

    // --at <file>:<line>, the file relative to here (or absolute, as an editor gives it), inside the project.
    Placement? Placement(string? at, string projectDirectory)
    {
        if (at is null) return null;
        var colon = at.LastIndexOf(':');
        if (colon <= 0 || !int.TryParse(at[(colon + 1)..], out var line) || line < 1)
            throw new UsageException($"--at is <file>:<line>, such as Components/Pages/Home.razor:12, not \"{at}\".");
        var path = Path.GetRelativePath(projectDirectory, Path.GetFullPath(at[..colon], workingDirectory)).Replace('\\', '/');
        if (path.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(path)) throw new UsageException($"{at[..colon]} isn't in the project.");
        return new Placement(path, line);
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
            ["path"] = i.Path, ["offset"] = i.Offset, ["text"] = i.Text, ["originalHash"] = i.OriginalHash,
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
