using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace IzzyBlazor.Cli;

/// <summary>
/// The tools of <c>izzy mcp</c>: each runs an izzy command in the project, as a person would type it, and
/// answers with what the command printed. Nothing here generates on its own, so an agent and a person get the
/// same result. The tools come with a free account; a Pro item answers as its command does.
/// </summary>
/// <param name="izzy">Runs an izzy command line.</param>
/// <param name="signedIn">Whether this machine has an account's sign-in.</param>
sealed class BuildTools(Func<string[], (int Exit, string Out, string Error)> izzy, Func<bool> signedIn)
{
    public const string Instructions = """
        izzy builds with IzzyBlazor's catalog in this project: starters, app kits, pages and blocks made of
        IzzyBlazor components, accessible and working in every render mode. Before you write a page or section
        by hand, call list_items, then describe_item for the one that fits and add_item with dryRun to see the
        changes. These tools write files in the project; they never replace a file with other content unless
        overwrite is set, so ask the user first. For component APIs and examples use the izzyblazor docs server
        (https://izzyblazor.com/mcp). After you add or change a page, audit_page checks its accessibility and
        recheck_finding says whether a finding you fixed is gone: they report and change no code, and a report
        with no findings is not a statement of conformance. The tools come with a free IzzyBlazor account: if
        one answers that the user must run izzy login, tell them.
        """;

    readonly Lock _one = new();

    /// <summary>Waits for a command that is running to finish, before the server's process ends.</summary>
    public void Finish()
    {
        lock (_one) { }
    }

    /// <summary>A server over <paramref name="transport"/> with these tools.</summary>
    public McpServer Server(ITransport transport, string version)
    {
        var options = new McpServerOptions
        {
            ServerInfo = new() { Name = "izzy", Title = "IzzyBlazor CLI", Version = version },
            ServerInstructions = Instructions.ReplaceLineEndings(" "),
            ToolCollection = [],
        };
        foreach (var method in typeof(BuildTools).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                     .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null))
            options.ToolCollection.Add(McpServerTool.Create(method, this));
        return McpServer.Create(transport, options);
    }

    [McpServerTool(Name = "list_items", Title = "List IzzyBlazor catalog items", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("The catalog as JSON: each item's id, kind (starter, kit, page, block, component), category, summary and tier (free, account for a free account, pro), and any setting choices that are Pro. Call it before writing a page or section by hand. The components are the library's own: describe_item gives one's options, and add_item puts its usage example on a page.")]
    public string ListItems(
        [Description("Only this kind: starter, kit, page, block or component. Leave out for all.")] string? kind = null) =>
        Run(["list", .. Word(kind), "--json"], data: true);

    [McpServerTool(Name = "describe_item", Title = "Describe an IzzyBlazor catalog item", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("An item as JSON: its summary, the components it uses, render mode support, accessibility notes and its settings as a JSON schema (each with its choices, default and description). The settings' names are what add_item, new_app and init_app take. For a component (kind component, by its name): its options as a JSON schema, its content, events, parts and examples.")]
    public string DescribeItem(
        [Description("The item's id, from list_items.")] string item,
        [Description("The item's kind, from list_items: starter, kit, block or component. Leave out only for crud and init.")] string? kind = null) =>
        Run(["describe", .. Word(kind), item, "--json"], data: true);

    [McpServerTool(Name = "add_item", Title = "Add a catalog item to the app", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Adds a block to the app, or with kind component a component's usage example from the docs to a page (give at): its markup at the line, its @using lines at the page's top and its @code members in the page's @code. For a block it creates its files, places it on a page and adds the packages it needs. Answers with the files created and changed and any warnings. With dryRun it writes nothing and answers with a diff. Adding the same item again with the same settings changes nothing.")]
    public string AddItem(
        [Description("The item's id, from list_items.")] string item,
        [Description("The item's kind, from list_items: block or component.")] string kind = "block",
        [Description("For a component: another of its examples by name, as describe_item lists them. Leave out for its usage example.")] string? example = null,
        [Description("The item's settings by name, as describe_item lists them, such as {\"Layout\": \"Split\"}. Leave out for the defaults.")] Dictionary<string, JsonElement>? settings = null,
        [Description("Where to place it, as <file>:<line>, such as Components/Pages/Home.razor:12. Leave out for the item's own place.")] string? at = null,
        [Description("The app's folder or .csproj file, when it isn't where the server runs.")] string? project = null,
        [Description("Writes nothing and answers with the changes as a diff.")] bool dryRun = false,
        [Description("Replaces files that exist with other content. Ask the user first.")] bool overwrite = false) =>
        Run(["add", .. Word(kind), item, .. Settings(settings), .. Option("example", example), .. Option("at", at), .. Option("project", project), .. Flags(dryRun, overwrite)]);

    [McpServerTool(Name = "add_crud", Title = "Add a page that edits a model's records", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Adds a page whose grid lists, adds, edits and deletes the records of a model class in the app, read from its source (no build needed), with a store for it. describe_item crud lists the settings (Name, Route, Store, Context, Authorize and more). With dryRun it writes nothing and answers with a diff.")]
    public string AddCrud(
        [Description("The model class's name, such as Customer.")] string model,
        [Description("The crud item's settings by name, as describe_item crud lists them.")] Dictionary<string, JsonElement>? settings = null,
        [Description("The app's folder or .csproj file, when it isn't where the server runs.")] string? project = null,
        [Description("Writes nothing and answers with the changes as a diff.")] bool dryRun = false,
        [Description("Replaces files that exist with other content. Ask the user first.")] bool overwrite = false) =>
        Run(["add", "crud", model, .. Settings(settings), .. Option("project", project), .. Flags(dryRun, overwrite)]);

    [McpServerTool(Name = "new_app", Title = "Create a new IzzyBlazor app", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Creates a new app from a starter or an app kit in a folder of its own. Answers with the files created. With dryRun it writes nothing and lists them.")]
    public string NewApp(
        [Description("starter or kit: the item's kind, from list_items.")] string kind,
        [Description("The item's id, from list_items.")] string item,
        [Description("The app's name, which is also its namespace, such as Acme.")] string name,
        [Description("The folder to create it in. Leave out for a folder named after the app, where the server runs.")] string? output = null,
        [Description("The item's settings by name, as describe_item lists them.")] Dictionary<string, JsonElement>? settings = null,
        [Description("Writes nothing and lists the files it would create.")] bool dryRun = false,
        [Description("Replaces files that exist with other content. Ask the user first.")] bool overwrite = false) =>
        Run(["new", kind, item, .. Option("name", name), .. Option("output", output), .. Settings(settings, "name", "output"), .. dryRun ? ["--dry-run"] : Array.Empty<string>(), .. overwrite ? ["--overwrite"] : Array.Empty<string>()]);

    [McpServerTool(Name = "init_app", Title = "Set up IzzyBlazor in the app", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Sets up IzzyBlazor in an app that doesn't have it: a Blazor Web App, or an MVC or Razor Pages app, which gets a Blazor root beside its pages. It adds the package, the stylesheet, the theme and the services, then builds the app and says how to fix a name clash. describe_item init lists the settings (Preset, Brand, Interactivity, KeepCss, Mcp). With dryRun it writes nothing and answers with a diff.")]
    public string InitApp(
        [Description("The setup's settings by name, as describe_item init lists them.")] Dictionary<string, JsonElement>? settings = null,
        [Description("The app's folder or .csproj file, when it isn't where the server runs.")] string? project = null,
        [Description("Writes nothing and answers with the changes as a diff.")] bool dryRun = false) =>
        Run(["init", .. Settings(settings), .. Option("project", project), .. Flags(dryRun, overwrite: false)]);

    [McpServerTool(Name = "audit_page", Title = "Audit a page's accessibility", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Checks one page's accessibility in the Chrome or Edge on this machine: axe-core and izzy's own checks (target size, focus under a bar that stays in place, focus and indicators in forced colors), at two widths, in light and dark, and once in forced colors. Give the page's address in the running app, or set run and give a path (/orders): izzy then starts the app in this project for the audit and stops it after. Answers with the findings counted by verdict (real, needs a person, accepted, demo page, false positive) and where report.md is: it has each finding with its id, verdict and reason, the element, the component that drew it, the fix when izzy knows one, and with run or project its file and line (ten elements a group; results.json beside it has them all). tasks.md, beside it, is the list to work through. It changes no code: you or the user make the fix, then recheck_finding. It doesn't open closed dialogs or menus, and doesn't check keyboard order or what a screen reader says, so no findings is not a statement of conformance: say so when you report. One page a call; a whole site is izzy audit --site in a terminal (IzzyBlazor Pro).")]
    public string AuditPage(
        [Description("The page: its address (https://localhost:5001/orders), or with run a path in the app (/orders).")] string page,
        [Description("Starts this project's app for the audit and stops it after. It builds first, which can take a minute. Leave out when the app is already running, and give its address.")] bool run = false,
        [Description("The app's folder or .csproj file, when it isn't where the server runs. Without run, set it when the page is this app's, for each finding's file and line.")] string? project = null) =>
        Run(["audit", Plain(page, "page"), .. run ? ["--run"] : Array.Empty<string>(), .. Option("project", project), "--format=md,json,tasks"], findings: true);

    [McpServerTool(Name = "recheck_finding", Title = "Check a finding again after a fix", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Audits again the page a finding is on, after a fix, and says whether the finding is fixed, still there, or couldn't be checked. \"Still there, accepted\" (or false positive, or demo page) means the element still fails the rule and the finding doesn't count: don't call that fixed. Take the id from report.md of an audit_page run in this project (Id, twelve characters). An edit that moves the element gives its finding a new id: the answer then says how many findings of the same rule the earlier run lacks and names one, so look at that one before you call a finding fixed. Set run as audit_page was called; a running app must have the fix built in.")]
    public string RecheckFinding(
        [Description("The finding's id, from report.md.")] string finding,
        [Description("Starts this project's app for the audit and stops it after, as for audit_page.")] bool run = false,
        [Description("The app's folder or .csproj file, when it isn't where the server runs.")] string? project = null) =>
        Run(["audit", .. Option("recheck", Plain(finding, "finding")), .. run ? ["--run"] : Array.Empty<string>(), .. Option("project", project), "--format=md,json,tasks"], findings: true);

    // A page or an id, never an option of the command.
    static string Plain(string value, string name) =>
        string.IsNullOrWhiteSpace(value) || value.TrimStart().StartsWith('-') ? throw new McpException($"\"{value}\" isn't a {name}.") : value.Trim();

    // One command at a time: two would write the same project. An error is the agent's to act on, so the
    // command's own message goes back as the tool's error.
    string Run(string[] arguments, bool data = false, bool findings = false)
    {
        if (!signedIn())
            throw new McpException("izzy's tools come with a free IzzyBlazor account, and this machine isn't signed in. Ask the user to run `izzy login` in a terminal, then call the tool again.");
        (int Exit, string Out, string Error) result;
        lock (_one) result = izzy(arguments);
        // JSON goes back as it is: a warning after it would break it.
        var text = data && result.Exit == 0 ? result.Out.Trim()
            : string.Join("\n", new[] { result.Out.Trim(), result.Error.Trim() }.Where(t => t.Length > 0));
        return result.Exit switch
        {
            0 => text.Length > 0 ? text : "Done.",
            // An audit that ends with a finding still there has answered: that is its result, not a failure.
            5 when findings => text,
            2 => throw new McpException(text.Replace("Compare them with --diff, or replace them with --overwrite.", "Compare them with dryRun, or replace them with overwrite after asking the user.", StringComparison.Ordinal)),
            _ => throw new McpException(text),
        };
    }

    // A kind, as the word a command takes before the id; crud and init are named alone.
    static string[] Word(string? kind) =>
        string.IsNullOrWhiteSpace(kind) ? [] : kind.Trim().All(char.IsLetter) ? [kind.Trim().ToLowerInvariant()] : throw new McpException($"\"{kind}\" isn't a kind of item.");

    static string[] Option(string name, string? value) => string.IsNullOrWhiteSpace(value) ? [] : [$"--{name}={value}"];

    static string[] Flags(bool dryRun, bool overwrite) =>
        [.. dryRun ? ["--dry-run", "--diff"] : Array.Empty<string>(), .. overwrite ? ["--overwrite"] : Array.Empty<string>()];

    static IEnumerable<string> Settings(Dictionary<string, JsonElement>? settings, params string[] own)
    {
        foreach (var (key, value) in settings ?? [])
        {
            var name = key.TrimStart('-');
            if (name.Length == 0 || name.Any(c => c == '=' || char.IsWhiteSpace(c))) throw new McpException($"\"{key}\" isn't a setting's name.");
            // The CLI's own options are the tool's own arguments, never settings.
            if (Args.Reserved(name.ToLowerInvariant()) || own.Contains(name, StringComparer.OrdinalIgnoreCase)) throw new McpException($"{key} isn't a setting: it is one of the tool's own arguments, where the tool has it.");
            yield return $"--{name}=" + value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Number => value.GetRawText(),
                _ => throw new McpException($"The setting {key} takes text, a number or true or false."),
            };
        }
    }
}
