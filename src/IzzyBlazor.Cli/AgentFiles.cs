using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace IzzyBlazor.Cli;

/// <summary>
/// What <c>izzy agent</c> writes for coding agents: the IzzyBlazor skill, in the two folders agents read
/// skills from, and the docs server and <c>izzy mcp</c> in the three MCP settings files. The skill is ours and
/// is replaced by a newer one; a settings file of the user's keeps everything it has but our two servers.
/// </summary>
static class AgentFiles
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    static readonly string[] SkillPaths = [".claude/skills/izzyblazor/SKILL.md", ".agents/skills/izzyblazor/SKILL.md"];

    // The file, the key its servers are under, and whether the client wants a type on an http server.
    static readonly (string Path, string Key, bool HttpType)[] Configs =
    [
        (".mcp.json", "mcpServers", true),
        (".vscode/mcp.json", "servers", true),
        (".cursor/mcp.json", "mcpServers", false),
    ];

    /// <summary>The skill's text, as the CLI carries it.</summary>
    public static string Skill
    {
        get
        {
            using var stream = typeof(AgentFiles).Assembly.GetManifestResourceStream("SKILL.md")!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().ReplaceLineEndings("\n");
        }
    }

    /// <summary>Writes the files under <paramref name="directory"/>, or says what it would write.</summary>
    public static void Write(string directory, bool dryRun, TextWriter output, TextWriter error)
    {
        var changed = 0;
        void Put(string path, string text)
        {
            var full = Path.Combine(directory, path);
            var exists = File.Exists(full);
            if (exists && File.ReadAllText(full).ReplaceLineEndings("\n") == text) return;
            changed++;
            output.WriteLine($"{(dryRun ? (exists ? "Would change" : "Would create") : exists ? "Changed" : "Created")} {path}");
            if (dryRun) return;
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, text);
        }

        foreach (var path in SkillPaths) Put(path, Skill);
        foreach (var (path, key, httpType) in Configs)
        {
            var full = Path.Combine(directory, path);
            JsonObject root;
            try
            {
                root = File.Exists(full) ? JsonNode.Parse(File.ReadAllText(full)) as JsonObject ?? throw new JsonException() : [];
            }
            catch (JsonException)
            {
                // Comments or a slip in a file of the user's: theirs to edit.
                error.WriteLine($"warning: {path} isn't plain JSON, so it was kept. Add two servers under \"{key}\": \"izzyblazor\", {Docs(httpType).ToJsonString()}, and \"izzy\", {Izzy().ToJsonString()}.");
                continue;
            }
            if (root[key] is not JsonObject servers) root[key] = servers = [];
            servers["izzyblazor"] = Docs(httpType);
            servers["izzy"] = Izzy();
            Put(path, root.ToJsonString(Json).ReplaceLineEndings("\n") + "\n");
        }
        if (changed == 0) output.WriteLine("Nothing to change: the agent files are up to date.");
    }

    /// <summary>What to add by hand for the agents whose settings izzy doesn't write.</summary>
    public const string Others = """
        Claude Code, Copilot, Cursor and Visual Studio read the files above. Codex, Gemini CLI, Junie and Windsurf
        read the skill from .agents/skills; add the two servers to their own MCP settings: a stdio server named
        izzy that runs "izzy mcp", and an http server named izzyblazor at https://izzyblazor.com/mcp.
        """;

    static JsonObject Docs(bool type) => type
        ? new() { ["type"] = "http", ["url"] = "https://izzyblazor.com/mcp" }
        : new() { ["url"] = "https://izzyblazor.com/mcp" };

    static JsonObject Izzy() => new() { ["type"] = "stdio", ["command"] = "izzy", ["args"] = new JsonArray("mcp") };
}
