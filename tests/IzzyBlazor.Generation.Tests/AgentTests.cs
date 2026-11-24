using System.IO.Pipelines;
using System.Text.Json.Nodes;
using IzzyBlazor.Cli;
using IzzyBlazor.Generation;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace IzzyBlazor.Generation.Tests;

/// <summary>
/// izzy for a coding agent: izzy mcp's tools called as an agent calls them, in a new app, and the files izzy
/// agent writes beside settings the developer already has.
/// </summary>
[Collection(ProjectBuild.Collection)]
public sealed class AgentTests : IDisposable
{
    readonly string _work = Directory.CreateTempSubdirectory("izzy-agent-").FullName;

    static string PublicCatalog => Path.Combine(ProjectBuild.RepoRoot, "catalog");

    [AccountFact]
    public async Task An_agent_adds_a_block_through_izzy_mcp()
    {
        var cli = new Cli.Cli(PublicCatalog, _work, new StringWriter(), new StringWriter(), ProjectBuild.PrivateCatalog, ItemTier.Account);
        await using var session = await Session.Start(cli);
        var client = session.Client;

        Assert.Equal(["add_crud", "add_item", "describe_item", "init_app", "list_items", "new_app"],
            (await client.ListToolsAsync()).Select(t => t.Name).Order(StringComparer.Ordinal));

        var created = await client.CallToolAsync("new_app", new Dictionary<string, object?>
        {
            ["kind"] = "starter", ["item"] = "blazor", ["name"] = "Acme", ["settings"] = new Dictionary<string, object?> { ["no-restore"] = true },
        });
        Assert.NotEqual(true, created.IsError);

        var app = Path.Combine(_work, "Acme");
        var hero = new Dictionary<string, object?> { ["item"] = "hero", ["settings"] = new Dictionary<string, object?> { ["Layout"] = "Split" }, ["project"] = app };
        var preview = await client.CallToolAsync("add_item", new Dictionary<string, object?>(hero) { ["dryRun"] = true });
        Assert.Contains("+++ b/Components/Blocks/Hero.razor", Text(preview));
        Assert.False(File.Exists(Path.Combine(app, "Components", "Blocks", "Hero.razor")));

        Assert.Contains("Created Components/Blocks/Hero.razor", Text(await client.CallToolAsync("add_item", hero)));
        Assert.Contains("<Grid Columns=", File.ReadAllText(Path.Combine(app, "Components", "Blocks", "Hero.razor")));

        // Another layout over the file that's there: refused, and the answer names the tool's own arguments.
        hero["settings"] = new Dictionary<string, object?> { ["Layout"] = "Centered" };
        var refused = await client.CallToolAsync("add_item", hero);
        Assert.True(refused.IsError);
        Assert.Contains("replace them with overwrite", Text(refused));
    }

    [Fact]
    public async Task Signed_out_every_tool_says_to_run_izzy_login()
    {
        var account = new AccountSession(Path.Combine(_work, "home"), new Uri("https://account.test/"), new Dictionary<string, string>(), "9.9.9");
        var cli = new Cli.Cli(PublicCatalog, _work, new StringWriter(), new StringWriter(), account: account);
        await using var session = await Session.Start(cli);

        var answer = await session.Client.CallToolAsync("list_items", new Dictionary<string, object?>());
        Assert.True(answer.IsError);
        Assert.Contains("izzy login", Text(answer));
    }

    [Fact]
    public void Izzy_agent_writes_the_skill_and_keeps_the_servers_a_settings_file_has()
    {
        (int Exit, string Out) Izzy(string directory, params string[] args)
        {
            var output = new StringWriter();
            var exit = new Cli.Cli(PublicCatalog, directory, output, new StringWriter()).Run(args);
            return (exit, output.ToString());
        }
        Assert.Equal(0, Izzy(_work, "new", "starter", "blazor", "-n", "Acme", "--no-restore").Exit);
        var app = Path.Combine(_work, "Acme");
        File.WriteAllText(Path.Combine(app, ".mcp.json"), """{ "mcpServers": { "tickets": { "command": "tickets-mcp" } } }""");

        Assert.Equal(0, Izzy(app, "agent").Exit);

        var skill = File.ReadAllText(Path.Combine(app, ".claude", "skills", "izzyblazor", "SKILL.md"));
        Assert.StartsWith("---\nname: izzyblazor\ndescription: ", skill);
        Assert.Equal(skill, File.ReadAllText(Path.Combine(app, ".agents", "skills", "izzyblazor", "SKILL.md")));

        var servers = JsonNode.Parse(File.ReadAllText(Path.Combine(app, ".mcp.json")))!["mcpServers"]!;
        Assert.Equal("tickets-mcp", (string)servers["tickets"]!["command"]!);
        Assert.Equal("https://izzyblazor.com/mcp", (string)servers["izzyblazor"]!["url"]!);
        // The app pins izzy as a tool of its own, so the server is the app's izzy: nothing on the PATH is needed.
        Assert.Contains("\"izzyblazor.cli\"", File.ReadAllText(Path.Combine(app, ".config", "dotnet-tools.json")));
        Assert.Equal(["dotnet", "izzy", "mcp"], [(string)servers["izzy"]!["command"]!, .. servers["izzy"]!["args"]!.AsArray().Select(a => (string)a!)]);
        var code = JsonNode.Parse(File.ReadAllText(Path.Combine(app, ".vscode", "mcp.json")))!["servers"]!["izzy"]!;
        Assert.Equal(("stdio", "${workspaceFolder}"), ((string)code["type"]!, (string)code["cwd"]!));
        // And the commands izzy names there are the app's.
        Assert.Contains("dotnet izzy describe block <id>", Izzy(app, "list").Out);

        // An app without the manifest gets the izzy on the PATH.
        Assert.Equal(0, Izzy(_work, "new", "starter", "blazor", "-n", "Plain", "--no-restore").Exit);
        var plain = Path.Combine(_work, "Plain");
        Directory.Delete(Path.Combine(plain, ".config"), recursive: true);
        Assert.Equal(0, Izzy(plain, "agent").Exit);
        Assert.Equal("izzy", (string)JsonNode.Parse(File.ReadAllText(Path.Combine(plain, ".mcp.json")))!["mcpServers"]!["izzy"]!["command"]!);

        Assert.Contains("Nothing to change", Izzy(app, "agent").Out);
    }

    static string Text(CallToolResult result) => string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text));

    public void Dispose() => Directory.Delete(_work, recursive: true);

    /// <summary>izzy mcp's server and a client, joined by two pipes in place of the process's input and output.</summary>
    sealed class Session(McpServer server, Task running, McpClient client, CancellationTokenSource stop) : IAsyncDisposable
    {
        public McpClient Client => client;

        public static async Task<Session> Start(Cli.Cli cli)
        {
            var toServer = new Pipe();
            var toClient = new Pipe();
            var stop = new CancellationTokenSource();
            var server = cli.BuildTools().Server(new StreamServerTransport(toServer.Reader.AsStream(), toClient.Writer.AsStream(), "izzy"), "9.9.9");
            var running = server.RunAsync(stop.Token);
            var client = await McpClient.CreateAsync(new StreamClientTransport(toServer.Writer.AsStream(), toClient.Reader.AsStream()));
            return new Session(server, running, client, stop);
        }

        public async ValueTask DisposeAsync()
        {
            await client.DisposeAsync();
            stop.Cancel();
            try { await running; } catch (OperationCanceledException) { }
            await server.DisposeAsync();
            stop.Dispose();
        }
    }
}
