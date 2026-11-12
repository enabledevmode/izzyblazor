using System.Diagnostics;
using System.Text;
using IzzyBlazor.Generation;

namespace IzzyBlazor.Generation.Tests;

/// <summary>
/// The phase 1 gate: every starter, exported as a dotnet new template and run by the real dotnet new (in a
/// private template cache), makes exactly the files the engine makes, for the defaults, each other choice of each
/// choice setting, each switch flipped, and a project name that needs a safe namespace.
/// </summary>
public sealed class DotnetNewTests : IDisposable
{
    readonly string _work = Directory.CreateTempSubdirectory("izzy-dotnet-new-").FullName;

    public static TheoryData<string> Starters()
    {
        var data = new TheoryData<string>();
        var roots = new[] { Path.Combine(AppContext.BaseDirectory, "Items"), Path.Combine(RepoRoot(), "catalog") };
        foreach (var root in roots.Where(Directory.Exists))
            foreach (var dir in Directory.GetDirectories(root).Order(StringComparer.Ordinal))
                if (File.Exists(Path.Combine(dir, "izzy.json")) && ItemDefinition.FromDirectory(dir).Manifest.Kind == ItemKind.Starter)
                    data.Add(dir);
        return data;
    }

    [Theory]
    [MemberData(nameof(Starters))]
    public void Dotnet_new_makes_what_the_engine_makes(string itemDirectory)
    {
        var item = ItemDefinition.FromDirectory(itemDirectory);
        var id = item.Manifest.Id;
        var template = Path.Combine(_work, "template");
        var hive = Path.Combine(_work, "hive");
        TemplateExport.WriteTemplateFolder(item, template);
        Dotnet("new", "install", template, "--debug:custom-hive", hive);

        var failures = new List<string>();
        var run = 0;
        foreach (var (name, settings) in Cases(item.Manifest))
        {
            var output = Path.Combine(_work, $"out{run++}");
            var args = new List<string> { "new", id, "-o", output, "-n", name, "--debug:custom-hive", hive };
            foreach (var (key, value) in settings) args.AddRange([$"--{item.Manifest.Parameters.Single(p => p.Name == key).Option}", value]);
            Dotnet([.. args]);

            var label = $"{id} -n \"{name}\" {string.Join(" ", settings.Select(s => $"{s.Key}={s.Value}"))}";
            var expected = Generator.Generate(item, settings, ProjectInfo.ForNewProject(name)).Creates.ToDictionary(c => c.Path);
            var actual = Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories)
                .ToDictionary(f => Path.GetRelativePath(output, f).Replace('\\', '/'), File.ReadAllBytes);

            foreach (var path in expected.Keys.Except(actual.Keys)) failures.Add($"{label}: dotnet new didn't make {path}");
            foreach (var path in actual.Keys.Except(expected.Keys)) failures.Add($"{label}: dotnet new made {path}, the engine didn't");
            foreach (var path in expected.Keys.Intersect(actual.Keys))
            {
                var want = expected[path].Bytes ?? Encoding.UTF8.GetBytes(expected[path].Text!);
                if (!want.AsSpan().SequenceEqual(actual[path]))
                    failures.Add($"{label}: {path} differs.\n--- engine\n{Encoding.UTF8.GetString(want)}\n--- dotnet new\n{Encoding.UTF8.GetString(actual[path])}");
            }
        }
        Assert.True(failures.Count == 0, string.Join("\n\n", failures));
    }

    /// <summary>
    /// The defaults; each other choice of each choice setting; each switch flipped; a name that needs a safe
    /// namespace. Ports and GUIDs are given (a picked one is random) and restore is skipped (it changes no files).
    /// </summary>
    static IEnumerable<(string Name, Dictionary<string, string> Settings)> Cases(ItemManifest manifest)
    {
        Dictionary<string, string> With(params (string Key, string Value)[] extra)
        {
            var settings = new Dictionary<string, string>();
            var port = 5100;
            foreach (var p in manifest.Parameters.Where(p => p.Port is not null)) settings[p.Name] = (port++).ToString();
            foreach (var p in manifest.Parameters.Where(p => p.Guid)) settings[p.Name] = "0f8fad5b-d9cb-469f-a165-70867728950e";
            foreach (var p in manifest.Parameters.Where(p => p.Name is "SkipRestore" or "NoRestore")) settings[p.Name] = "true";
            foreach (var (key, value) in extra) settings[key] = value;
            return settings;
        }

        var restoreSettings = new[] { "SkipRestore", "NoRestore" };
        var choices = manifest.Parameters.Where(p => p.Type == ParameterType.Choice)
            .SelectMany(p => p.Choices.Where(c => c != p.Default).Select(c => (p.Name, Value: c))).ToList();
        var flips = manifest.Parameters.Where(p => p.Type == ParameterType.Bool && !restoreSettings.Contains(p.Name))
            .Select(p => (p.Name, Value: p.Default == "true" ? "false" : "true")).ToList();

        yield return ("MyApp", With());
        foreach (var choice in choices) yield return ("MyApp", With(choice));
        foreach (var flip in flips) yield return ("MyApp", With(flip));
        // Each other choice with each switch flipped: the settings that change a starter's layout together.
        foreach (var choice in choices)
            foreach (var flip in flips) yield return ("MyApp", With(choice, flip));
        yield return ("My-App.2", With());
    }

    static void Dotnet(params string[] args)
    {
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(TimeSpan.FromMinutes(2))) { process.Kill(); throw new TimeoutException($"dotnet {string.Join(" ", args)}"); }
        if (process.ExitCode != 0) throw new InvalidOperationException($"dotnet {string.Join(" ", args)} failed ({process.ExitCode}):\n{stdout.Result}\n{stderr}");
    }

    static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "izzyblazor.sln"))) return dir.FullName;
        throw new InvalidOperationException("No izzyblazor.sln above the test folder.");
    }

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); } catch (IOException) { }
    }
}
