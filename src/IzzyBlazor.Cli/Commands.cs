using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace IzzyBlazor.Cli;

/// <summary>
/// A command as izzy capabilities and the help give it: its usage line and the plan it needs (free, account, pro,
/// or item: the item's own tier, as izzy list --json gives it), with the options of its that need Pro.
/// </summary>
sealed record Command(string Name, string Usage, string Plan, string? Note, Dictionary<string, string[]>? ProChoices)
{
    /// <summary>The options its usage line names (--json, -n), without placeholders such as --&lt;setting&gt;.</summary>
    public List<string> Options => [.. Regex.Matches(Usage, @"(?<![\w-])--?[a-z][a-z-]*\b(?!>)").Select(m => m.Value).Where(o => o != "-" ).Distinct()];

    /// <summary>Whether it takes an item's or a component's own settings as options (--&lt;setting&gt; &lt;value&gt;).</summary>
    public bool Settings => Usage.Contains("--<setting>", StringComparison.Ordinal) || Usage.Contains("--<option>", StringComparison.Ordinal);
}

/// <summary>
/// Every command this izzy has, in the help's order: the one list the help's usage lines and izzy capabilities
/// are made from, so a front end reads what this version takes instead of comparing version numbers.
/// </summary>
static class Commands
{
    /// <summary>
    /// The contract with front ends (the VS Code extension, agents): raised when a command's answer changes shape or
    /// a front end must do something new. 1 is 0.1.0-alpha.30's; <see cref="Features"/> says what each raise added.
    /// </summary>
    public const int Contract = 2;

    /// <summary>What a front end can rely on, each with the contract that brought it.</summary>
    public static readonly (string Id, int Contract, string Summary)[] Features =
    [
        ("capabilities", 2, "izzy capabilities --json: this list."),
        ("changeset.expect", 2, "Every changeset's JSON has hash and generated; --expect <hash> writes only the previewed changes, else exit code 6."),
        ("audit.files-with-json", 2, "audit --json --out writes every --format, report.html with its pictures."),
        ("describe.important", 2, "describe component marks up to 8 options x-priority: important, as the library names them."),
        ("theme.vars", 2, "izzy theme vars --json: IzzyTheme's head for the given settings, from the app's own library."),
        ("theme.sample", 2, "izzy theme sample --json: a page showing the theme on the library's components."),
        ("describe.tokens", 2, "izzy describe tokens --json: the library's CSS variables, their values per scheme, renames and token types."),
        ("token.edit", 2, "izzy add token, rename token and remove token: the app's own colors as changesets."),
        ("check", 2, "izzy check --json: where the app uses what a release renamed or removed, CSS variables too, with the replacement."),
        ("inspect.choices", 2, "inspect --json gives choices for Accent and Color options, the app's own with the library's."),
    ];

    /// <summary>The options every command that answers with a changeset takes.</summary>
    public static readonly string[] ChangesetOptions = ["--dry-run", "--diff", "--json", "--expect"];

    public static readonly Command[] All =
    [
        new("list", "izzy list [starter|kit|block|component] [--json]", "free", null, null),
        new("describe item", "izzy describe starter|kit|block <id> [--json]", "free", null, null),
        new("describe component", "izzy describe component <Name> [--json]", "free", null, null),
        new("describe component --all", "izzy describe component --all --json", "free", null, null),
        new("describe tokens", "izzy describe tokens [--json]", "free", null, null),
        new("describe crud|init", "izzy describe crud|init [--json]", "free", null, null),
        new("add block", "izzy add block <id> [--<setting> <value> ...] [--at <file>:<line>] [--project <path>] [--dry-run] [--diff] [--json] [--overwrite]", "item", "The plan is the block's: izzy list --json gives each item's tier.", null),
        new("add page", "izzy add page --name <Name> [--route <path>] [--folder <folder>] [--mode Static|Server|WebAssembly|Auto] [--layout <Layout>] [--nav] [--project <path>] [--dry-run] [--diff] [--json]", "free", null, null),
        new("add razor", "izzy add razor --name <Name> [--folder <folder>] [--project <path>] [--dry-run] [--diff] [--json]", "free", null, null),
        new("add component", "izzy add component <Name> [--example <name> | --tag] --at <file>:<line>[:<col>] [--stdin] [--project <path>] [--dry-run] [--diff] [--json]", "free", null, null),
        new("add token", "izzy add token <name> --light <color> [--dark <color>] [--on <color>[,<dark color>]] [--types accent,color] [--project <path>] [--dry-run] [--diff] [--json]", "free", null, null),
        new("rename token", "izzy rename token <name> <new name> [--project <path>] [--dry-run] [--diff] [--json]", "free", null, null),
        new("remove token", "izzy remove token <name> [--project <path>] [--dry-run] [--diff] [--json]", "free", null, null),
        new("inspect", "izzy inspect <file>:<line>:<col> [--stdin] [--project <path>] --json", "account", null, null),
        new("set", "izzy set <file>:<line>:<col> --<option> <value> ... [--unset <option>] [--stdin] [--project <path>] [--dry-run] [--diff] [--json]", "account", null, null),
        new("wrap", "izzy wrap <file>:<line>:<col>[-<line>:<col>] --with <component> [--<option> <value> ...] [--stdin] [--project <path>] [--dry-run] [--diff] [--json]", "account", null, null),
        new("wrap --suggest", "izzy wrap <file>:<line>:<col>[-<line>:<col>] --suggest --json", "account", null, null),
        new("unwrap", "izzy unwrap <file>:<line>:<col> [--stdin] [--project <path>] [--dry-run] [--diff] [--json]", "account", null, null),
        new("extract", "izzy extract <file>:<line>:<col>[-<line>:<col>] --name <Name> [--folder <folder>] [--stdin] [--project <path>] [--dry-run] [--diff] [--json]", "account", null, null),
        new("add crud", "izzy add crud <Model> [--folder <folder>] [--layout <Layout>] [--render Server|WebAssembly|Auto] [--nav false] [--name <Plural>] [--singular <words>] [--plural <words>] [--route <path>] [--mode Grid|Pages] [--store Ef|Memory|Http] [--context <DbContext>] [--authorize] [--project <path>] [--dry-run] [--diff] [--json] [--overwrite]", "free", "Free in grid mode; --mode Pages and --store Http are Pro (proChoices).", new Dictionary<string, string[]> { ["--mode"] = ["Pages"], ["--store"] = ["Http"] }),
        new("new", "izzy new starter|kit <id> [-n <name>] [-o <folder>] [--<setting> <value> ...] [--dry-run] [--diff] [--json] [--overwrite]", "item", "The plan is the starter's or kit's: izzy list --json gives each item's tier.", null),
        new("init", "izzy init [--preset <preset>] [--brand <color>] [--interactivity None|Server] [--keep-css] [--mcp] [--project <path>] [--no-build] [--dry-run] [--diff] [--json] [--overwrite]", "free", null, null),
        new("update", "izzy update [--to <version>] [--global] [--project <path>] [--no-build] [--dry-run] [--diff] [--json]", "free", null, null),
        new("check", "izzy check [--to <version>] [--project <path>] [--json]", "free", null, null),
        new("upgrade", "izzy upgrade [--apply] [--from <version>] [--to <version>] [--project <path>] [--diff] [--json]", "pro", null, null),
        new("audit", "izzy audit <url> [--sizes 1440,375] [--schemes light,dark] [--cookie <name=value>] [--out <folder>] [--format md,json,tasks,html,sarif] [--fail-on any|real|new] [--baseline <results.json>] [--browser <path>] [--json]", "free", "One page, with everything, free; the verdicts come with a free account (signed out, the checks still run). Several pages in a run are Pro.", null),
        new("audit --site", "izzy audit --site <url> [--max-pages 500], or izzy audit --urls <file>", "pro", null, null),
        new("audit --run", "izzy audit [<path> ...] --run [--site] [--project <path>]", "free", "Free for one page; --site or several pages is Pro.", null),
        new("audit --recheck", "izzy audit --recheck <finding id> [--run] [--project <path>]", "free", null, null),
        new("agent", "izzy agent [--project <path>] [--dry-run]", "free", null, null),
        new("mcp", "izzy mcp", "account", "Its tools follow the commands' plans.", null),
        new("doctor", "izzy doctor [--build] [--browser <path>] [--project <path>] [--json]", "free", null, null),
        new("theme vars", "izzy theme vars [--preset <preset>] [--brand <color>] [--gray <color>] [--radius <radius>] [--density <density>] [--chart-palette <palette>] [--scrollbars <scrollbars>] [--color-scheme auto|light|dark] [--body-font <fonts>] [--heading-font <fonts>] [--mono-font <fonts>] [--heading-weight <weight>] [--project <path>] [--json]", "free", null, null),
        new("theme sample", "izzy theme sample [the same settings] [--base <url>] [--project <path>] [--json]", "free", null, null),
        new("login", "izzy login [--no-browser]", "free", null, null),
        new("logout", "izzy logout", "free", null, null),
        new("account", "izzy account [--json]", "free", null, null),
        new("capabilities", "izzy capabilities --json", "free", null, null),
        new("feedback", "izzy feedback [\"<what happened>\"] [--about \"<command>\"] [--yes] [--json]", "pro", "Pro and Pro preview.", null),
    ];

    /// <summary>The help: the usage lines, then what the commands do.</summary>
    public static string Help => $"""
        izzy: IzzyBlazor apps, blocks and pages from IzzyBlazor's catalog.

        Usage:
        {string.Join("\n", All.Select(c => "  " + c.Usage))}

        {Prose}
        """;

    const string Prose = """
        izzy add component puts a component's usage example from the docs on a page: its markup at the line,
        its @using lines at the top and its @code members in the page's @code. --example takes another of its
        examples (izzy describe component <Name> lists them); --tag puts the tag alone, at the column too.
        izzy inspect answers with the component at a place on a page and what each of its attributes says, for
        an editor's side panel. izzy set changes or removes its options there (both: a free account).
        izzy wrap puts a component or an HTML element around the element at a place, or around the elements from
        it to a second place in the same parent; --suggest lists the wrappers that fit there. izzy unwrap takes an
        element away and keeps what it holds. izzy extract moves markup to a component of its own and passes it
        what it read from the page's @code as parameters (all three: a free account).
        izzy add crud makes a page whose grid adds, edits and deletes a model's records (list, detail and
        form pages are --mode pages, Pro), for a model class in your app read from its source: no build needed.
        izzy init sets up IzzyBlazor in the app you have: a Blazor Web App, or an MVC or Razor Pages app, which
        gets a Blazor root beside its pages. It builds the app afterwards and says how to fix a name clash.
        izzy update moves the app in this folder to the newest IzzyBlazor: the package its projects reference
        and the izzy in its tool manifest, then it builds the app. Outside an app, or with --global, it updates
        the izzy on this machine. --to takes a version other than the newest. It ends with what the releases
        in between removed or renamed. izzy upgrade finds where the app uses those, in its .razor and .cs
        files (IzzyBlazor Pro). It prints the plan and writes nothing: each rename it would make, and what is
        left to change by hand, by file and line. izzy upgrade --apply makes the renames. It starts from the
        release izzy update moved the app from; --from names it for an app updated by hand.
        Settings are the item's, as izzy describe lists them (izzy describe block hero), such as --layout Split.
        --dry-run writes nothing; --diff shows the changes as a unified diff; --json prints them as data.
        Files that exist with other content are never replaced without --overwrite.
        To write what a preview showed, run the command again without --dry-run, with each of the preview's
        "generated" settings and --expect <its "hash">: if the changes differ, nothing is written (exit code 6).
        izzy doctor checks what izzy needs on this machine and in this app: the .NET SDK, the app's IzzyBlazor
        package and its own izzy against this izzy, your account and the browser for the audit. --build builds
        the app too. It changes nothing.
        izzy theme vars prints the tags IzzyTheme writes into <head> for these settings, which take IzzyTheme's
        values (a hex brand or gray, or a built-in name): meta tags for what the theme CSS chooses by name, and
        a style block for a hex color's palette, the charts' brand colors and the fonts. A preview with
        izzyblazor.css puts them in its <head> to show a theme with no app running. It renders the IzzyTheme
        of the app's own IzzyBlazor package once restored, so its options are that version's; else this izzy's.
        izzy theme sample takes the same settings and prints a page that shows the theme on the library's own
        components (a project tracker's shell, type, colors, buttons, a form, data, navigation and overlays),
        drawn with no script. Its stylesheets and fonts are linked from the library's folder (--base sets the
        address they're served from). Each section has an id and data-izzy-section, to scroll to.
        izzy audit checks a page's accessibility with axe-core and izzy's own checks (target size, focus
        hidden under a header that stays in place, and focus and indicators in forced colors), in the Chrome
        or Edge on this machine, at two widths, in light and dark and once more in forced colors. It writes
        a report to read (report.md) and the same as data (results.json) in .izzy/audit/. --run starts the
        app in this project for the audit and stops it after; --fail-on any ends with exit code 5 when
        there are findings, for CI. It reports and changes no code. With a free account each finding has a
        verdict (real, needs a person, accepted, demo page, false positive), your own decisions are read
        from .izzy/audit.json, --format tasks writes tasks.md, and --baseline <results.json> with --fail-on
        new fails on real findings the past run lacks. Each finding also names the IzzyBlazor component
        that drew the element and the fix; with --run or --project it has the file and line in your source,
        and --format sarif writes results.sarif for code scanning. --format html writes report.html, the
        report as one file with a picture of each element, to hand to a client. --recheck audits again the
        page a finding of a past run in .izzy/audit/ is on, by the finding's id from report.md, and says
        whether it is fixed (exit code 5 when it is still there and open). One page a run is free; the verdicts and
        the reports beyond the first two need the account. With IzzyBlazor
        Pro, --site audits every page of a site: those its sitemap.xml lists, else those its links lead to;
        izzy audit --run --site is the whole app. --urls <file> audits a list, one address a line, and
        several addresses can be given at once. --cookie sends a cookie with every request, for pages
        behind a sign-in. Closed dialogs, keyboard order and screen reader output are not checked: a report
        with no findings is not a statement of conformance.
        izzy agent writes what a coding agent reads in your project: the IzzyBlazor skill, and the docs server
        and izzy mcp in the MCP settings of Claude Code, Copilot, Cursor and Visual Studio. izzy mcp is the
        server those settings start: izzy's commands as tools, for an agent, with a free account. Two of them,
        audit_page and recheck_finding, check one page's accessibility. izzy agent
        replaces the skill with this izzy's, and keeps the other servers a settings file has.
        izzy login signs this machine in to your free IzzyBlazor account, which the app starters, the blocks
        and the kits come with. After that they are created on this machine, online or not. In CI, set IZZY_KEY
        to a key from your account page instead.
        izzy feedback tells us what works and what doesn't (IzzyBlazor Pro and its preview). It asks what
        happened, shows the post and asks before sending it to IzzyBlazor's public discussions on GitHub,
        without your name or email. For anything private, write to support@izzyblazor.com.
        """;

    /// <summary>izzy capabilities --json.</summary>
    public static JsonObject Capabilities(string version) => new()
    {
        ["version"] = version,
        ["contract"] = Contract,
        ["plans"] = new JsonArray("free", "account", "pro", "item"),
        ["changesetOptions"] = new JsonArray(ChangesetOptions.Select(o => (JsonNode)o).ToArray()),
        ["commands"] = new JsonArray(All.Select(c => (JsonNode)new JsonObject
        {
            ["name"] = c.Name, ["usage"] = c.Usage, ["plan"] = c.Plan,
            ["options"] = new JsonArray(c.Options.Select(o => (JsonNode)o).ToArray()),
            ["settings"] = c.Settings,
            ["proChoices"] = c.ProChoices is null ? new JsonObject() : new JsonObject(c.ProChoices.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)new JsonArray(kv.Value.Select(v => (JsonNode)v).ToArray())))),
            ["note"] = c.Note,
        }).ToArray()),
        ["features"] = new JsonArray(Features.Select(f => (JsonNode)new JsonObject { ["id"] = f.Id, ["contract"] = f.Contract, ["summary"] = f.Summary }).ToArray()),
    };
}
