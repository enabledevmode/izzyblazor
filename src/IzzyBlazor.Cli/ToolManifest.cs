using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using IzzyBlazor.Generation;

namespace IzzyBlazor.Cli;

/// <summary>
/// The app's .NET tool manifest (<c>.config/dotnet-tools.json</c>) with izzy in it, at the version of the
/// IzzyBlazor package the app references. With it, <c>dotnet tool restore</c> and then <c>dotnet izzy</c> work
/// in the app's folder with nothing on the PATH, and the app's izzy matches its library.
/// </summary>
static partial class ToolManifest
{
    const string Tool = "izzyblazor.cli";

    // Where the SDK looks, in its order.
    static readonly string[] Paths = [".config/dotnet-tools.json", "dotnet-tools.json"];

    /// <summary>Whether a path a changeset names is a tool manifest.</summary>
    public static bool Is(string path) => Paths.Contains(path, StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="directory"/> or a folder above it has a manifest that lists izzy, as <c>dotnet izzy</c> finds it.</summary>
    public static bool Lists(string directory)
    {
        try
        {
            for (var folder = new DirectoryInfo(directory); folder is not null; folder = folder.Parent)
                foreach (var path in Paths)
                    if (Path.Combine(folder.FullName, path) is var full && File.Exists(full) && Version(File.ReadAllText(full)) is not null) return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        return false;
    }

    /// <summary>A new manifest with izzy at <paramref name="version"/>, as <c>dotnet new tool-manifest</c> and <c>dotnet tool install</c> write it.</summary>
    public static string New(string version) =>
        $$"""
        {
          "version": 1,
          "isRoot": true,
          "tools": {
            "{{Tool}}": {
              "version": "{{version}}",
              "commands": [
                "izzy"
              ],
              "rollForward": false
            }
          }
        }

        """.ReplaceLineEndings("\n");

    /// <summary>
    /// What puts izzy at <paramref name="version"/> in the app's manifest: a new file, izzy added to the
    /// manifest the app has (its other tools stay), or its version there changed. Nothing when it's there
    /// already; a warning for a manifest that can't be read.
    /// </summary>
    public static (FileCreate? Create, TextInsert? Insert, string? Warning) Change(Func<string, string?> read, string version)
    {
        var path = Paths.FirstOrDefault(p => read(p) is not null);
        if (path is null) return (new FileCreate(Paths[0], New(version), null), null, null);
        var text = read(path)!;
        var hash = Inserts.Hash(text);
        var eol = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        JsonObject? tools;
        try { tools = (JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject)?["tools"] as JsonObject; }
        catch (JsonException) { tools = null; }
        var by = $"add it by hand: dotnet tool install IzzyBlazor.Cli --version {version}";
        if (tools is null) return (null, null, $"{path} isn't a tool manifest izzy can read, so izzy wasn't added to it; {by}.");

        if (Entry().Match(text) is { Success: true } entry)
        {
            var at = entry.Groups[1];
            return at.Value == version ? (null, null, null) : (null, new TextInsert(path, at.Index, version, hash) { Length = at.Length }, null);
        }
        if (tools.Any(t => t.Key.Equals(Tool, StringComparison.OrdinalIgnoreCase)) || ToolsOpen().Match(text) is not { Success: true } open)
            return (null, null, $"{path} is written in a way izzy can't add to; {by}.");
        var body = $"{eol}    \"{Tool}\": {{{eol}      \"version\": \"{version}\",{eol}      \"commands\": [{eol}        \"izzy\"{eol}      ],{eol}      \"rollForward\": false{eol}    }}";
        return (null, new TextInsert(path, open.Index + open.Length, body + (tools.Count > 0 ? "," : eol + "  "), hash), null);
    }

    // The version a manifest has izzy at; null when it doesn't list it.
    static string? Version(string text) => Entry().Match(text) is { Success: true } m ? m.Groups[1].Value : null;

    /// <summary>A version as a manifest takes one: exact, not a range, a wildcard or a property.</summary>
    public static bool Exact(string version) => ExactVersion().IsMatch(version);

    [GeneratedRegex("""
        "izzyblazor\.cli"\s*:\s*\{[^{}]*?"version"\s*:\s*"([^"]*)"
        """, RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex Entry();

    [GeneratedRegex("""
        "tools"\s*:\s*\{
        """, RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex ToolsOpen();

    [GeneratedRegex(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$")]
    private static partial Regex ExactVersion();
}

/// <summary>The <c>dotnet</c> izzy starts for a restore, a build or a run.</summary>
static class DotnetHost
{
    /// <summary>
    /// Where dotnet is: the one that runs izzy when it says so, then <c>DOTNET_ROOT</c>, then the folder of the
    /// runtime izzy runs on; <c>dotnet</c> on the PATH when none of those has it.
    /// </summary>
    public static string Path => field ??= Find();

    static string Find()
    {
        var name = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        if (Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host && File.Exists(host)) return host;
        if (Environment.ProcessPath is { } own && System.IO.Path.GetFileName(own).Equals(name, StringComparison.OrdinalIgnoreCase)) return own;
        if (Environment.GetEnvironmentVariable("DOTNET_ROOT") is { Length: > 0 } root && System.IO.Path.Combine(root, name) is var inRoot && File.Exists(inRoot)) return inRoot;
        // <dotnet>/shared/Microsoft.NETCore.App/<version>/ is where the runtime is.
        var runtime = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
        if (System.IO.Path.GetFullPath(System.IO.Path.Combine(runtime, "..", "..", "..", name)) is var beside && File.Exists(beside)) return beside;
        return "dotnet";
    }

    /// <summary>
    /// Starts dotnet with its output captured. When it can't be started, says so in a sentence that ends with
    /// <paramref name="left"/>: what wasn't done, and what to run by hand.
    /// </summary>
    public static System.Diagnostics.Process Start(string directory, IEnumerable<string> arguments, string left)
    {
        var start = new System.Diagnostics.ProcessStartInfo(Path) { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        try
        {
            return System.Diagnostics.Process.Start(start) ?? throw new GenerationException($"izzy couldn't start dotnet, {left}");
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or FileNotFoundException or DirectoryNotFoundException)
        {
            throw new GenerationException($"izzy couldn't start dotnet (it isn't on the PATH, and DOTNET_ROOT doesn't point at it), {left}");
        }
    }
}

/// <summary>
/// A writer that names izzy as the app runs it: in an app whose tool manifest lists izzy, a command izzy tells
/// the person to run (<c>izzy login</c>, <c>izzy add block hero</c>) is written <c>dotnet izzy …</c>, which works
/// there with nothing on the PATH.
/// </summary>
sealed partial class LocalToolWriter(TextWriter inner) : TextWriter
{
    public override System.Text.Encoding Encoding => inner.Encoding;

    public override void Write(char value) => inner.Write(value);

    public override void Write(string? value) => inner.Write(value is null ? null : Command().Replace(value, "dotnet izzy $1"));

    public override void Flush() => inner.Flush();

    [GeneratedRegex(@"(?<![\w./-]|dotnet )izzy (list|describe|add|inspect|set|new|init|audit|agent|mcp|login|logout|account|--version|--help)\b")]
    private static partial Regex Command();
}
