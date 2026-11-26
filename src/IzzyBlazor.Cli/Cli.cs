using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using IzzyBlazor.Generation;

namespace IzzyBlazor.Cli;

/// <summary>
/// The izzy commands. Exit codes: 0 done, 1 an error, 2 files exist with other content (nothing written), 3 a Pro
/// item or choice, 4 an item that comes with a free account, 5 an audit with findings under <c>--fail-on</c>.
/// </summary>
/// <param name="catalogRoot">The free catalog folder.</param>
/// <param name="workingDirectory">Where relative paths start and the project is looked for.</param>
/// <param name="output">Results.</param>
/// <param name="error">Errors and warnings.</param>
/// <param name="proRoot">The private catalog, with the account items' files and the Pro items (IZZY_PRO_CATALOG); null for the public catalog only.</param>
/// <param name="access">What the private catalog holds: all of it, or an account's copy.</param>
/// <param name="releases">The releases on nuget.org, for izzy update.</param>
/// <param name="account">This machine's IzzyBlazor account, whose copy of the catalog is used when <paramref name="proRoot"/> is null.</param>
sealed class Cli(string catalogRoot, string workingDirectory, TextWriter output, TextWriter error, string? proRoot = null, ItemTier access = ItemTier.Pro, AccountSession? account = null, TextReader? input = null, Releases? releases = null)
{
    const string Help = """
        izzy: IzzyBlazor apps, blocks and pages from IzzyBlazor's catalog.

        Usage:
          izzy list [starter|kit|block|component] [--json]
          izzy describe starter|kit|block <id> [--json]
          izzy describe component <Name> [--json]
          izzy describe component --all --json
          izzy describe crud|init [--json]
          izzy add block <id> [--<setting> <value> ...] [--at <file>:<line>] [--project <path>] [--dry-run] [--diff] [--json] [--overwrite]
          izzy add page --name <Name> [--from list|detail|form] [--route <path>] [--folder <folder>] [--mode Static|Server|WebAssembly|Auto] [--layout <Layout>] [--nav] [--project <path>] [--dry-run] [--diff] [--json]
          izzy add razor --name <Name> [--folder <folder>] [--project <path>] [--dry-run] [--diff] [--json]
          izzy add component <Name> [--example <name> | --tag] --at <file>:<line>[:<col>] [--stdin] [--project <path>] [--dry-run] [--diff] [--json]
          izzy inspect <file>:<line>:<col> [--stdin] [--project <path>] --json
          izzy set <file>:<line>:<col> --<option> <value> ... [--unset <option>] [--stdin] [--project <path>] [--dry-run] [--diff] [--json]
          izzy wrap <file>:<line>:<col>[-<line>:<col>] --with <component> [--<option> <value> ...] [--stdin] [--project <path>] [--dry-run] [--diff] [--json]
          izzy wrap <file>:<line>:<col>[-<line>:<col>] --suggest --json
          izzy unwrap <file>:<line>:<col> [--stdin] [--project <path>] [--dry-run] [--diff] [--json]
          izzy extract <file>:<line>:<col>[-<line>:<col>] --name <Name> [--folder <folder>] [--stdin] [--project <path>] [--dry-run] [--diff] [--json]
          izzy add crud <Model> [--name <Plural>] [--singular <words>] [--plural <words>] [--route <path>] [--mode Grid|Pages] [--store Ef|Memory|Http] [--context <DbContext>] [--authorize] [--project <path>] [--dry-run] [--diff] [--json] [--overwrite]
          izzy new starter|kit <id> [-n <name>] [-o <folder>] [--<setting> <value> ...] [--dry-run] [--diff] [--json] [--overwrite]
          izzy init [--preset <preset>] [--brand <color>] [--interactivity None|Server] [--keep-css] [--mcp] [--project <path>] [--no-build] [--dry-run] [--diff] [--json] [--overwrite]
          izzy update [--to <version>] [--global] [--project <path>] [--no-build] [--dry-run] [--diff] [--json]
          izzy upgrade [--apply] [--from <version>] [--to <version>] [--project <path>] [--diff] [--json]
          izzy audit <url> [--sizes 1440,375] [--schemes light,dark] [--cookie <name=value>] [--out <folder>] [--format md,json,tasks,html,sarif] [--fail-on any|real|new] [--baseline <results.json>] [--browser <path>] [--json]
          izzy audit --site <url> [--max-pages 500], or izzy audit --urls <file>
          izzy audit [<path> ...] --run [--site] [--project <path>]
          izzy audit --recheck <finding id> [--run] [--project <path>]
          izzy agent [--project <path>] [--dry-run]
          izzy mcp
          izzy doctor [--build] [--project <path>] [--json]
          izzy login [--no-browser]
          izzy logout
          izzy account [--json]
          izzy feedback ["<what happened>"] [--about "<command>"] [--yes] [--json]

        izzy add component puts a component's usage example from the docs on a page: its markup at the line,
        its @using lines at the top and its @code members in the page's @code. --example takes another of its
        examples (izzy describe component <Name> lists them); --tag puts the tag alone, at the column too.
        izzy inspect answers with the component at a place on a page and what each of its attributes says, for
        an editor's side panel (a free account). izzy set changes or removes its options there (IzzyBlazor Pro).
        izzy wrap puts a component or an HTML element around the element at a place, or around the elements from
        it to a second place in the same parent; --suggest lists the wrappers that fit there. izzy unwrap takes an
        element away and keeps what it holds. izzy extract moves markup to a component of its own and passes it
        what it read from the page's @code as parameters (all three: IzzyBlazor Pro).
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
        izzy doctor checks what izzy needs on this machine and in this app: the .NET SDK, the app's IzzyBlazor
        package and its own izzy against this izzy, your account and the browser for the audit. --build builds
        the app too. It changes nothing.
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

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public int Run(string[] arguments)
    {
        // In an app that pins izzy as a local tool, the commands izzy names are written as the app runs them.
        _data = output;
        if (ToolManifest.Lists(workingDirectory)) (output, error) = (new LocalToolWriter(output), new LocalToolWriter(error));
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
            // Only a command that generates may call the account service (to renew the sign-in, or for the
            // templates after an update).
            _generates = args.Words[0] is "add" or "new";
            return args.Words[0] switch
            {
                "list" => List(args),
                "describe" => Describe(args),
                "add" => Add(args),
                "inspect" => Inspect(args),
                "set" => Set(args),
                "wrap" => Wrap(args),
                "unwrap" => Unwrap(args),
                "extract" => Extract(args),
                "doctor" => Doctor(args),
                "new" => New(args),
                "init" => Init(args),
                "update" => Update(args),
                "upgrade" => Upgrade(args),
                "audit" => AuditPages(args),
                "agent" => Agent(args),
                "mcp" => Mcp(args),
                "bundle" => Bundle(args),
                "login" => Login(args),
                "logout" => Account(args, "izzy logout").Logout(output),
                "account" => Account(args, "izzy account [--json]", "json").Status(output, args.Switch("json")),
                "feedback" => Feedback(args),
                var other => throw new UsageException($"Unknown command \"{other}\". Commands: list, describe, add, inspect, set, wrap, unwrap, extract, new, init, update, upgrade, audit, agent, mcp, doctor, login, logout, account, feedback."),
            };
        }
        catch (ProRequiredException e)
        {
            error.WriteLine($"{e.Message} Pro items arrive with IzzyBlazor Pro.");
            return 3;
        }
        catch (AccountRequiredException e)
        {
            error.WriteLine(account is { SignedIn: false } ? $"{e.Message} Run izzy login." : e.Message);
            return 4;
        }
        catch (Exception e) when (e is GenerationException or UsageException)
        {
            error.WriteLine(e.Message);
            return 1;
        }
        catch (Exception e) when (e is System.Xml.XmlException or JsonException)
        {
            // A project file or a settings file of the app's that doesn't parse: say which problem, not a stack trace.
            error.WriteLine($"A file izzy reads in this app isn't valid: {e.Message}");
            return 1;
        }
    }

    bool _generates;

    // Where data goes (JSON, a file's text): the output as it was given, with nothing renamed in it.
    TextWriter _data = TextWriter.Null;

    Catalog Catalog => field ??= proRoot is null && account?.Catalog(error, _generates) is { } copy
        ? Catalog.Load(catalogRoot, copy, account.Plan == ItemTier.Pro ? ItemTier.Pro : ItemTier.Account)
        : Catalog.Load(catalogRoot, proRoot, access);

    // izzy bundle <file>: an account's copy of the private catalog as a zip, for the release workflow to send to
    // the account service. Not in the help: it needs the private catalog, which only IzzyBlazor's own builds have.
    int Bundle(Args args)
    {
        Words(args, 2, "izzy bundle <file> [--pro]");
        var pro = args.Switch("pro");
        NoSettings(args);
        if (proRoot is null) throw new UsageException("izzy bundle needs the private catalog (IZZY_PRO_CATALOG).");
        var file = Path.GetFullPath(args.Words[1], workingDirectory);
        File.WriteAllBytes(file, AccountBundle.Export(catalogRoot, proRoot, pro ? ItemTier.Pro : ItemTier.Account));
        output.WriteLine($"Wrote {file}.");
        return 0;
    }

    // izzy agent: the skill and the MCP settings, in the app's folder (the solution's, as izzy init --mcp).
    int Agent(Args args)
    {
        Words(args, 1, "izzy agent [--project <path>] [--dry-run]");
        var project = ProjectInfo.FromProject(FindProject(args.Take("project")));
        var dryRun = args.Switch("dry-run");
        NoSettings(args);
        AgentFiles.Write(project.Directory!, dryRun, output, error);
        output.Write(AgentFiles.Others.ReplaceLineEndings("\n") + "\n");
        return 0;
    }

    // izzy mcp: the commands as tools over this process's input and output, until the client closes it.
    // Each call is a new Cli, so a sign-in made while the server runs is seen by the next call.
    int Mcp(Args args)
    {
        Words(args, 1, "izzy mcp");
        NoSettings(args);
        var tools = BuildTools();
        var transport = new ModelContextProtocol.Server.StdioServerTransport("izzy");
        var server = tools.Server(transport, Generator.IzzyBlazorVersion);
        try
        {
            server.RunAsync().GetAwaiter().GetResult();
        }
        finally
        {
            // The client closed the input: a command still writing finishes first.
            tools.Finish();
            server.DisposeAsync().AsTask().GetAwaiter().GetResult();
            transport.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        return 0;
    }

    /// <summary>The tools of izzy mcp over this catalog, folder and account.</summary>
    internal BuildTools BuildTools() => new(
        arguments =>
        {
            var o = new StringWriter();
            var e = new StringWriter();
            var exit = arguments is ["mcp", ..] ? 1 : new Cli(catalogRoot, workingDirectory, o, e, proRoot, access, account).Run(arguments);
            return (exit, o.ToString(), e.ToString());
        },
        // The private catalog on this machine (IzzyBlazor's own builds) stands for a sign-in.
        () => proRoot is not null || account is null || account.SignedIn);

    // izzy audit https://localhost:5001/orders: the page in a browser on this machine, checked with axe-core.
    int AuditPages(Args args)
    {
        const string usage = "izzy audit <url> [--site] [--urls <file>] [--max-pages 500] [--cookie <name=value>] [--sizes 1440,375] [--schemes light,dark] [--out <folder>] [--format md,json,tasks,html,sarif] [--fail-on any|real|new] [--baseline <results.json>] [--browser <path>] [--json], or izzy audit [<path> ...] --run [--site] [--project <path>], or izzy audit --recheck <finding id> [--run]";
        var run = args.Switch("run");
        // --site with an address, or alone after one (or with --run, for the app's root).
        var site = args.Has("site");
        var siteStart = site ? args.Options.First(o => o.Name == "site").Value : null;
        args.Options.RemoveAll(o => o.Name == "site");
        var listFile = args.Take("urls");
        var maxPages = args.Take("max-pages") is { } most ? int.TryParse(most, out var limit) && limit >= 1 ? limit : throw new UsageException($"--max-pages takes a number of pages: \"{most}\" isn't one.") : 500;
        var cookies = new List<KeyValuePair<string, string>>();
        while (args.Take("cookie") is { } cookie)
            cookies.Add(cookie.Split('=', 2) is [{ Length: > 0 } name, var value] ? KeyValuePair.Create(name.Trim(), value) : throw new UsageException($"--cookie takes name=value: \"{cookie}\" has no =."));
        var json = args.Switch("json");
        var folder = args.Take("project");
        var given = args.Take("browser");
        var outFolder = args.Take("out");
        var failOn = args.Take("fail-on") ?? "none";
        var baselineFile = args.Take("baseline");
        var recheck = args.Take("recheck");
        var (sizes, colors) = (args.Take("sizes"), args.Take("schemes"));
        var formats = (args.Take("format") ?? "md,json").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var widths = (sizes ?? "1440,375").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(w => int.TryParse(w, out var width) && width is >= 200 and <= 7680 ? width : throw new UsageException($"--sizes takes widths in pixels, such as 1440,375: \"{w}\" isn't one.")).ToList();
        var schemes = (colors ?? "light,dark").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s is "light" or "dark" ? s : throw new UsageException($"--schemes takes light, dark or both: \"{s}\" is neither.")).ToList();
        NoSettings(args);
        if (failOn is not ("any" or "real" or "new" or "none")) throw new UsageException("--fail-on takes any, real, new or none.");
        if (formats.FirstOrDefault(f => f is not ("md" or "json" or "tasks" or "html" or "sarif")) is { } format) throw new UsageException($"--format takes md, json, tasks, html and sarif: \"{format}\" is none of them.");
        if (failOn == "new" && baselineFile is null) throw new UsageException("--fail-on new counts the findings a past run lacks: say which with --baseline <results.json>.");
        JsonObject? baseline = null;
        if (baselineFile is not null)
        {
            var file = Path.GetFullPath(baselineFile, workingDirectory);
            if (!File.Exists(file)) throw new UsageException($"--baseline: there is no {baselineFile}.");
            baseline = JsonNode.Parse(File.ReadAllText(file)) as JsonObject ?? throw new UsageException($"{baselineFile} isn't a results.json from izzy audit.");
        }
        if (widths.Count == 0 || schemes.Count == 0 || formats.Length == 0) throw new UsageException($"Usage: {usage}");

        // Reports go beside the project when there is one, else here.
        string? csproj = null;
        try { csproj = FindProject(folder); }
        catch (UsageException) when (!run && folder is null) { }
        var home = csproj is null ? workingDirectory : Path.GetDirectoryName(csproj)!;

        var pages = args.Words.Skip(1).ToList();
        // --recheck: the page a past run found the finding on, at that run's widths and schemes.
        JsonObject? past = null;
        if (recheck is not null)
        {
            if (site || listFile is not null || pages.Count > 0) throw new UsageException("--recheck audits the page its finding is on: give the finding's id alone, with --run for this project's app.");
            past = baseline is not null && Audit.Has(baseline, recheck) ? baseline : baseline is null ? Audit.RunWith(Path.Combine(home, ".izzy", "audit"), recheck) : null;
            if (past is null) throw new UsageException(baseline is not null ? $"{baselineFile} has no finding {recheck}." : $"No run in .izzy/audit has a finding {recheck}. Audit the page first, or name a run's results with --baseline <results.json>.");
            var address = (string)Audit.AllFindings(past).First(f => f.Id == recheck).Page["url"]!;
            // With --run the app has a new port: the page is its path.
            pages.Add(run && Uri.TryCreate(address, UriKind.Absolute, out var before) ? before.PathAndQuery : address);
            try
            {
                if (sizes is null && past["widths"] is JsonArray { Count: > 0 } pastWidths) widths = [.. pastWidths.Select(w => (int)w!)];
                if (colors is null && past["schemes"] is JsonArray { Count: > 0 } pastSchemes) schemes = [.. pastSchemes.Select(c => (string)c!)];
            }
            catch (Exception e) when (e is InvalidOperationException or FormatException) { throw new UsageException("The earlier run's widths and schemes can't be read: say them with --sizes and --schemes."); }
            if (widths.Any(w => w is < 200 or > 7680) || schemes.Any(c => c is not ("light" or "dark"))) throw new UsageException("The earlier run's widths and schemes can't be read: say them with --sizes and --schemes.");
        }
        // The site's start: the address given with --site, else the first page named (not one of a list's), else the app's root.
        if (site && siteStart is null && pages.Count > 0) (siteStart, pages) = (pages[0], pages[1..]);
        if (listFile is not null)
        {
            var file = Path.GetFullPath(listFile, workingDirectory);
            if (!File.Exists(file)) throw new UsageException($"--urls: there is no {listFile}.");
            pages.AddRange(File.ReadLines(file).Select(line => line.Trim()).Where(line => line.Length > 0 && line[0] != '#'));
            if (pages.Count == 0) throw new UsageException($"{listFile} lists no addresses: one a line.");
        }
        if (site)
        {
            siteStart ??= run ? "/" : throw new UsageException("--site needs the site's address: izzy audit --site https://localhost:5001/, or izzy audit --run --site for this project's app.");
            if (!HasPro)
            {
                error.WriteLine("Auditing a whole site comes with IzzyBlazor Pro. One page is free, and a free account adds verdicts and the other reports: izzy audit <url>.");
                return 3;
            }
        }
        if (!run && !site && pages.Count == 0) throw new UsageException($"Say which page: izzy audit https://localhost:5001/, or izzy audit --run to start this project's app. Usage: {usage}");
        if (run && !site && pages.Count == 0) pages.Add("/");
        if (pages.Count > 1 && !HasPro)
        {
            error.WriteLine($"Auditing several pages in one run comes with IzzyBlazor Pro: this run audits {pages[0]}.");
            pages.RemoveRange(1, pages.Count - 1);
        }
        // Verdicts come from the pack in an account's copy of the catalog and the app's own .izzy/audit.json.
        var verdicts = failOn is "real" or "new" || baselineFile is not null || formats.Contains("tasks") || formats.Contains("html") || formats.Contains("sarif");
        if (verdicts && Plan == ItemTier.Free)
        {
            error.WriteLine("A verdict for each finding, tasks.md, report.html, results.sarif, --baseline and --fail-on real or new come with a free IzzyBlazor account. Run izzy login. Without one, --fail-on any fails on any finding.");
            return 4;
        }
        var browser = Chromium.Find(given) ?? throw new UsageException("izzy audit needs Chrome, Edge or Chromium, and found none on this machine. Install one, or say where one is with --browser <path>.");

        var pack = (Plan != ItemTier.Free ? proRoot ?? account?.Catalog(error, online: true) : null) is { } catalog && File.Exists(Path.Combine(catalog, ".audit", "pack.json"))
            ? Path.Combine(catalog, ".audit", "pack.json") : null;
        if (verdicts && pack is null) throw new GenerationException("This machine's copy of the catalog has no audit pack, so findings get no verdicts. Run izzy login to download it again.");
        // Several pages are Pro's: said once when Pro is here as a preview.
        if (!json && (site || pages.Count > 1)) PreviewNote();
        var decisions = Path.Combine(home, ".izzy", "audit.json");
        var judge = pack is null ? null : new Audit.Judge(pack, File.Exists(decisions) ? decisions : null);
        // The trace goes into the source of the app that is audited: this project's with --run, or the one --project names.
        var sources = new List<string>();
        if (judge is not null && csproj is not null && (run || folder is not null))
        {
            sources.Add(home);
            try
            {
                var project = ProjectInfo.FromProject(csproj);
                sources.AddRange(((string?[])[project.Host, project.Client]).Where(part => part is not null).Select(part => Path.GetFullPath(Path.Combine(project.Directory!, part!)).TrimEnd(Path.DirectorySeparatorChar)).Where(full => !sources.Contains(full)));
            }
            catch (Exception e) when (e is GenerationException or IOException or System.Xml.XmlException) { }
        }
        if (formats.Contains("sarif") && sources.Count == 0) throw new UsageException("--format sarif puts each finding on its line in the app's source: say where that is with --project <path>, or audit this project's app with --run.");
        _projectFolder = folder;
        var tracer = judge is null ? null : new Audit.Tracer(Library.HasMarks ? Library : Components.Load(Path.Combine(AppContext.BaseDirectory, "components.json")), judge.Fixes, sources);

        var saying = new object();
        void Progress(string line)
        {
            if (json) return;
            lock (saying) error.WriteLine(line);
        }
        using var app = run ? Audit.App.StartAsync(csproj!, Progress).GetAwaiter().GetResult() : null;
        string Resolve(string page)
        {
            if (app is not null && page.StartsWith('/')) return app.Url + page;
            var address = page.Contains("://", StringComparison.Ordinal) ? page : "http://" + page;
            return Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? uri.ToString()
                : throw new UsageException(run ? $"\"{page}\" isn't a path in the app (/orders) or an address." : $"\"{page}\" isn't an address: izzy audit https://localhost:5001/orders.");
        }
        var urls = pages.Select(Resolve).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (site)
        {
            var start = Resolve(siteStart!);
            Progress($"Looking for the pages of {start}");
            var found = Audit.Discover(new Uri(start), maxPages, cookies).GetAwaiter().GetResult();
            Progress(found.FromSitemap ? $"sitemap.xml lists {found.Found} page{(found.Found == 1 ? "" : "s")}." : $"No sitemap.xml: {found.Pages.Count} page{(found.Pages.Count == 1 ? "" : "s")} found by following links.");
            if (found.Found > found.Pages.Count)
                error.WriteLine(found.FromSitemap ? $"This run audits the first {found.Pages.Count} of them. For more, raise --max-pages." : $"The search stopped at {found.Pages.Count} pages, and the site has more. For more, raise --max-pages.");
            urls = [.. urls.Concat(found.Pages).Distinct(StringComparer.OrdinalIgnoreCase)];
        }

        // A long run is shared between up to three browsers.
        var loads = urls.Count * (widths.Count * schemes.Count + 1);
        var browsers = loads >= 24 ? Math.Clamp(Environment.ProcessorCount / 2, 1, 3) : 1;
        // report.html has a picture of each element it shows: taken now, while its page is open.
        var pictures = formats.Contains("html") && !json ? new Audit.Pictures() : null;
        var results = Audit.Run(browser, urls, widths, schemes, Generator.IzzyBlazorVersion, Progress, judge, cookies, browsers, tracer, pictures, sweeps: true,
            // One page names the elements axe couldn't decide, up to 20 of a rule; a site's run would carry tens of thousands.
            undecided: urls.Count == 1 ? 20 : 0).GetAwaiter().GetResult();
        if (baseline is not null) Audit.Compare(results, baseline, baselineFile!.Replace('\\', '/'));
        if (past is not null) results["recheck"] = Audit.Recheck(results, past, recheck!);
        var failing = failOn switch
        {
            "any" => Audit.Findings(results),
            "real" => Audit.Count(results, Audit.Real),
            "new" => Audit.Count(results, Audit.Real, newOnly: true),
            _ => 0,
        };
        var exit = failing > 0 ? 5 : 0;
        if (results["pages"]!.AsArray().All(p => p!["error"] is not null))
        {
            error.WriteLine($"{urls[0]} couldn't be opened: {(string)results["pages"]![0]!["error"]!}");
            return 1;
        }
        // A build that fails on findings can't pass on a page that wasn't checked: the report is still written.
        if (failOn != "none" && exit == 0 && Audit.Unchecked(results) is { Count: > 0 } missed)
        {
            error.WriteLine($"{missed.Count} of {results["pages"]!.AsArray().Count} page loads weren't checked (they didn't open, or answered with an error), so this run can't pass --fail-on {failOn}.");
            exit = 1;
        }
        // A finding looked for again: still there is 5, as a finding under --fail-on; not looked at is an error.
        if (results["recheck"] is { } again) exit = (string)again["state"]! switch { Audit.StillThere => 5, Audit.Fixed or Audit.Closed => exit, _ => exit == 0 ? 1 : exit };
        // --json prints the results and writes no files, unless --out says where: an editor reads the answer and
        // keeps the reports. The printed results then name the folder and its files.
        if (json && outFolder is null)
        {
            Print(results);
            return exit;
        }
        var target = outFolder is not null ? Path.GetFullPath(outFolder, workingDirectory) : Path.Combine(home, ".izzy", "audit", DateTime.Now.ToString("yyyy-MM-dd-HHmmss", System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(target);
        // The pictures as files too, named in the data: report.html holds its own copies, so it stays one file.
        pictures?.Save(target, results);
        if (formats.Contains("json")) File.WriteAllText(Path.Combine(target, "results.json"), results.ToJsonString(Json).ReplaceLineEndings("\n") + "\n");
        if (formats.Contains("md")) File.WriteAllText(Path.Combine(target, "report.md"), Audit.Markdown(results));
        if (formats.Contains("tasks")) File.WriteAllText(Path.Combine(target, "tasks.md"), Audit.Tasks(results));
        if (formats.Contains("html")) File.WriteAllText(Path.Combine(target, "report.html"), Audit.Html(results, pictures));
        if (formats.Contains("sarif"))
        {
            // Code scanning reads paths from the repository's root.
            var repository = home;
            for (var dir = new DirectoryInfo(home); dir is not null; dir = dir.Parent)
                if (Directory.Exists(Path.Combine(dir.FullName, ".git")) || File.Exists(Path.Combine(dir.FullName, ".git"))) { repository = dir.FullName; break; }
            var prefix = Path.GetRelativePath(repository, home).Replace('\\', '/') is var from && from != "." ? from + "/" : "";
            File.WriteAllText(Path.Combine(target, "results.sarif"), Audit.Sarif(results, prefix).ToJsonString(Json).ReplaceLineEndings("\n") + "\n");
        }
        if (json)
        {
            var printed = results.DeepClone().AsObject();
            printed["written"] = new JsonObject
            {
                ["folder"] = target.Replace('\\', '/'),
                ["files"] = new JsonArray([.. new[] { ("md", "report.md"), ("json", "results.json"), ("tasks", "tasks.md"), ("html", "report.html"), ("sarif", "results.sarif") }.Where(f => formats.Contains(f.Item1)).Select(f => (JsonNode)f.Item2)]),
            };
            Print(printed);
            return exit;
        }
        if (results["recheck"] is JsonObject looked) output.Write(Audit.Rechecked(looked));
        output.Write(Audit.Summary(results));
        // Beside the project it is a short path from here; somewhere else it is the whole path.
        var shown = (Path.GetRelativePath(workingDirectory, target) is var near && !near.StartsWith("..", StringComparison.Ordinal) ? near : target).Replace('\\', '/');
        output.WriteLine(formats.Contains("md") ? $"Report: {shown}/report.md" : formats.Contains("html") ? $"Report: {shown}/report.html" : formats.Contains("tasks") ? $"Tasks: {shown}/tasks.md" : formats.Contains("sarif") ? $"Results: {shown}/results.sarif" : $"Results: {shown}/results.json");
        if (formats.Contains("md") && formats.Contains("html")) output.WriteLine($"For a client: {shown}/report.html");
        return exit;
    }

    int Login(Args args)
    {
        var session = Account(args, "izzy login [--no-browser]", "no-browser");
        return session.Login(output, error, browser: !args.Switch("no-browser"));
    }

    // izzy feedback "The task list repeats a finding" --about "izzy audit --format tasks": a post in the
    // Pro preview feedback discussions. Without the words it asks for them; either way it shows the post
    // and sends it on a yes. An editor gives the words, its own name and --yes, having asked itself.
    int Feedback(Args args)
    {
        const string usage = "izzy feedback [\"<what happened>\"] [--about \"<command>\"] [--yes] [--json]";
        if (args.Words.Count > 2) throw new UsageException($"Put what happened in quotes. Usage: {usage}");
        var (about, from, yes, json) = (args.Take("about"), args.Take("from"), args.Switch("yes"), args.Switch("json"));
        if (args.Options.FirstOrDefault() is { Name: { } unknown }) throw new UsageException($"Unknown option --{unknown}. Usage: {usage}");
        var text = args.Words.Count == 2 ? args.Words[1] : null;
        if (json && (text is null || !yes)) throw new UsageException("--json takes what happened as a word and --yes: izzy feedback \"<what happened>\" --yes --json.");
        var session = account ?? throw new UsageException("This izzy has no account service.");
        if (session.NoFeedback() is var (refused, why))
        {
            error.WriteLine(why);
            return refused;
        }

        var reader = input ?? Console.In;
        if (text is null)
        {
            output.WriteLine("What happened? Write as much as you need, then end with an empty line.");
            var lines = new List<string>();
            while (reader.ReadLine() is { } line && (line.Trim().Length > 0 || lines.Count == 0)) lines.Add(line);
            text = string.Join('\n', lines);
            if (about is null)
            {
                output.WriteLine("Which izzy command was it about? Press Enter to skip.");
                about = reader.ReadLine();
            }
        }
        text = text.Trim();
        about = string.IsNullOrWhiteSpace(about) ? null : about.Trim();
        if (FeedbackText.Problem(text, about) is { } problem)
        {
            error.WriteLine(problem);
            return 1;
        }

        if (!yes)
        {
            output.WriteLine("This goes to IzzyBlazor's public discussions on GitHub, with your plan and without your name, email or account:");
            // As written: the words are the person's, not a command of izzy's to show as this app runs it.
            _data.Write($"\n{string.Join('\n', text.Split('\n').Select(line => "  " + line.TrimEnd()))}\n\n{(about is null ? "" : $"  Command: {about}\n")}  izzy {session.Where}\n  Plan: {(session.Preview ? "Pro preview" : "Pro")}\n{(from is null ? "" : $"  Sent from {from}\n")}\n");
            output.WriteLine("For anything private, write to support@izzyblazor.com instead.");
            output.Write("Post it? [y/N] ");
            var answer = reader.ReadLine();
            if (answer?.Trim().ToLowerInvariant() is not ("y" or "yes"))
            {
                // Nobody there to ask (a script, an agent): it says how to post without the question.
                output.WriteLine(answer is null ? "\nNot posted. To post without being asked, add --yes." : "Not posted.");
                return answer is null ? 1 : 0;
            }
        }

        var (exit, message) = session.SendFeedback(text, about, from);
        if (exit != 0)
        {
            error.WriteLine(message);
            return exit;
        }
        if (json) Print(new JsonObject { ["url"] = message });
        else output.WriteLine($"Posted: {message}");
        return 0;
    }

    AccountSession Account(Args args, string usage, string? allowed = null)
    {
        Words(args, 1, usage);
        if (args.Options.FirstOrDefault(o => o.Name != allowed) is { Name: { } unknown }) throw new UsageException($"Unknown option --{unknown}. Usage: {usage}");
        return account ?? throw new UsageException("This izzy has no account service.");
    }

    int List(Args args)
    {
        // izzy list block: the kind is a word, as it is in izzy add block hero.
        const string usage = "izzy list [starter|kit|block|component] [--json]";
        if (args.Words.Count > 2) throw new UsageException($"Usage: {usage}");
        if (args.Take("kind") is { } option) throw new UsageException($"The kind is a word: izzy list {option.ToLowerInvariant()}.");
        var kind = args.Words.Count == 2 ? args.Words[1] : null;
        var components = kind is null || kind.Equals("component", StringComparison.OrdinalIgnoreCase);
        if (kind is not null && !components && !Enum.GetValues<ItemKind>().Any(k => k != ItemKind.Setup && k.ToString().Equals(kind, StringComparison.OrdinalIgnoreCase)))
            throw new UsageException($"\"{kind}\" isn't a kind of item. Usage: {usage}");
        var json = args.Switch("json");
        NoSettings(args);
        // A tool with no component data still lists the catalog.
        if (kind is null && !File.Exists(Path.Combine(AppContext.BaseDirectory, "components.json")))
        {
            components = false;
            error.WriteLine("This izzy has no component data (components.json is missing beside it), so the components aren't listed. Reinstall the tool.");
        }
        var items = Catalog.Items.Values.Select(i => i.Manifest)
            .Where(m => m.Kind != ItemKind.Setup)
            .Where(m => kind is null || m.Kind.ToString().Equals(kind, StringComparison.OrdinalIgnoreCase)).ToList();
        if (json)
        {
            Print(new JsonArray(items.Select(m => (JsonNode)new JsonObject
            {
                ["id"] = m.Id, ["name"] = m.Name, ["kind"] = Lower(m.Kind), ["category"] = m.Category,
                ["tier"] = Lower(m.Tier), ["summary"] = m.Summary,
                ["docs"] = Pictures.Docs(m), ["picture"] = Pictures.Card(m),
                ["proChoices"] = ProChoices(m),
            }).Concat(components ? Library.Documented.Select(c => (JsonNode)Components.ListEntry(c)) : []).ToArray()));
            return 0;
        }
        if (kind is not null && components)
        {
            // izzy list component: a row each, in the docs' groups.
            var named = Library.Documented.ToList();
            var wide = named.Max(c => ((string)c["name"]!).Length);
            foreach (var c in named) output.WriteLine($"{((string)c["name"]!).PadRight(wide)}  {(string?)c["category"],-17}  {(string?)c["summary"]}");
            return 0;
        }
        // A heading a kind when everything is listed (starters, kits, blocks, then the components); one kind is its rows alone.
        var width = items.Count == 0 ? 0 : items.Max(m => m.Id.Length);
        foreach (var group in items.GroupBy(m => m.Kind).OrderBy(g => g.Key switch { ItemKind.Starter => 0, ItemKind.Kit => 1, ItemKind.Block => 2, _ => 3 }))
        {
            if (kind is null)
            {
                // crud is described by its id alone: izzy describe crud.
                var describe = group.Key is ItemKind.Starter or ItemKind.Kit or ItemKind.Block ? $"each described by izzy describe {Lower(group.Key)} <id>" : $"described by izzy describe {string.Join("|", group.Select(m => m.Id))}";
                output.WriteLine($"{group.Key}s ({group.Count()}), {describe}:");
            }
            foreach (var m in group)
            {
                var pro = m.Parameters.Where(p => p.ProChoices.Count > 0).Select(p => $"{p.Name} {string.Join(", ", p.ProChoices)}").ToList();
                output.WriteLine($"{(kind is null ? "  " : "")}{m.Id.PadRight(width)}  {m.Summary}{(m.Tier == ItemTier.Account ? " (free account)" : "")}{(pro.Count > 0 ? $" (Pro: {string.Join("; ", pro)})" : "")}");
            }
            if (kind is null)
            {
                output.WriteLine();
            }
        }
        if (components)
        {
            // With everything else, the components are names only, a line a group: izzy list component has their rows.
            var groups = Library.Documented.GroupBy(c => (string)c["category"]!).ToList();
            output.WriteLine($"Components ({groups.Sum(g => g.Count())}), each described by izzy describe component <Name>:");
            foreach (var g in groups) output.WriteLine($"  {g.Key}: {string.Join(", ", g.Select(c => (string)c["name"]!))}");
        }
        return 0;
    }

    int Describe(Args args)
    {
        if (args.Words.Count > 1 && args.Words[1].Equals("component", StringComparison.OrdinalIgnoreCase)) return DescribeComponent(args);
        var item = Item(args, "izzy describe", "izzy describe starter|kit|block <id> [--json], or izzy describe crud|init");
        var json = args.Switch("json");
        NoSettings(args);
        var m = item.Manifest;
        if (json)
        {
            // The schema as written, with each setting's option and a title, for forms and MCP tool inputs.
            var schema = m.Schema.DeepClone().AsObject();
            foreach (var p in m.Parameters)
            {
                var property = schema["properties"]![p.Name]!.AsObject();
                property["x-option"] = p.Option;
                // A label for a form: the manifest's own, else the name in words.
                if (property["title"] is null) property["title"] = Titles.For(p.Name);
            }
            Print(new JsonObject
            {
                ["id"] = m.Id, ["name"] = m.Name, ["kind"] = Lower(m.Kind), ["category"] = m.Category, ["tier"] = Lower(m.Tier),
                ["summary"] = m.Summary,
                ["docs"] = Pictures.Docs(m), ["picture"] = Pictures.Card(m), ["screens"] = Pictures.Screens(m),
                ["components"] = new JsonArray(m.Components.Select(c => (JsonNode)c).ToArray()),
                ["modes"] = new JsonObject(m.Modes.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value))),
                ["accessibility"] = new JsonArray(m.Accessibility.Select(a => (JsonNode)a).ToArray()),
                ["parameters"] = schema,
            });
            return 0;
        }
        output.WriteLine($"{m.Name} ({m.Id}), {Lower(m.Kind)}{m.Tier switch { ItemTier.Pro => ", Pro", ItemTier.Account => ", free account", _ => "" }}");
        if (m.Summary is not null) output.WriteLine(m.Summary);
        output.WriteLine();
        output.WriteLine(m.Kind.MakesProject() ? $"{m.NewCommand()} -n <name> [settings]" : m.Kind == ItemKind.Setup ? "izzy init [settings]" : $"{m.AddCommand()} [settings]");
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
        if (args.Words.Count > 1 && args.Words[1].Equals("component", StringComparison.OrdinalIgnoreCase)) return AddComponent(args);
        // izzy add block hero: the kind, then the id. izzy add crud <Model>: the model after it.
        // izzy add page --name Orders, izzy add razor --name OrderSummary: a new page, or a component with no route.
        var made = args.Words.Count == 2 && args.Words[1].ToLowerInvariant() is "page" or "razor" ? args.Words[1].ToLowerInvariant() : null;
        // izzy add page --from list: a starter page (list, detail or form) in place of the blank one, with a free account.
        var from = made == "page" ? args.Take("from") : null;
        if (from is not null && from.ToLowerInvariant() is not ("list" or "detail" or "form"))
            throw new UsageException($"--from is list, detail or form, not \"{from}\".");
        var isCrud = args.Words.Count > 1 && args.Words[1].Equals("crud", StringComparison.OrdinalIgnoreCase);
        var crud = args.Words.Count == 3 && isCrud;
        if (args.Words.Count == 2 && isCrud) throw new UsageException("Usage: izzy add crud <Model>, such as izzy add crud Customer.");
        var item = crud ? Catalog["crud"] : made is not null ? Catalog[from is null ? made : from.ToLowerInvariant() + "-page"] : Item(args, "izzy add", "izzy add block <id>");
        if (made is null && item.Manifest.Id is "list-page" or "detail-page" or "form-page") made = "page";
        if (item.Manifest.Kind == ItemKind.Setup) throw new UsageException("IzzyBlazor's setup runs with izzy init.");
        if (item.Manifest.Kind.MakesProject())
            throw new UsageException($"{item.Manifest.Id} makes a new project: {item.Manifest.NewCommand()} -n <name>.");
        if (!crud && item.Manifest.Id == "crud") throw new UsageException("Usage: izzy add crud <Model>, such as izzy add crud Customer.");
        var project = ProjectInfo.FromProject(FindProject(args.Take("project")));
        var host = Path.Combine(project.Directory!, project.Host, project.Name + ".csproj");
        if (!ReferencesIzzyBlazor(File.ReadAllText(host)))
            throw new UsageException($"{Path.GetFileName(host)} doesn't reference IzzyBlazor. Run izzy init first.");
        var at = Placement(args.Take("at"), project);
        var flags = Flags(args);
        var settings = Settings(item.Manifest, args);
        var notes = new List<string>();
        var written = new List<string>();
        if (crud) project = Crud.Prepare(project, args.Words[2], settings, notes, written);
        if (made is not null) project = Pages.Prepare(project, made == "page", settings, notes);
        var changes = Generator.Generate(item, settings, project, at);
        // What was made is said by the run that makes it, not by one that changes nothing.
        if (changes.Creates.Count > 0 || changes.Inserts.Count > 0 || changes.Packages.Count > 0) notes.InsertRange(0, written);
        if (notes.Count > 0)
            changes = new Changeset
            {
                Creates = changes.Creates, Inserts = changes.Inserts, Packages = changes.Packages, Conflicts = changes.Conflicts,
                Warnings = [.. notes, .. changes.Warnings], Settings = changes.Settings, Restore = changes.Restore,
            };
        return Finish(item, changes, project, project.Directory!, existing: true, flags);
    }

    // izzy describe component Button: its options, content and events. --all --json is every component and
    // part in one answer, for an editor's inspector to keep.
    int DescribeComponent(Args args)
    {
        const string usage = "izzy describe component <Name> [--json], or izzy describe component --all --json";
        var json = args.Switch("json");
        if (args.Switch("all"))
        {
            Words(args, 2, usage);
            if (!json) throw new UsageException("--all answers as data: izzy describe component --all --json.");
            var folder = _projectFolder = args.Take("project");
            NoSettings(args);
            Print(new JsonObject
            {
                ["version"] = Library.Version,
                // "package": the app's own IzzyBlazor.Core; "tool": this izzy's copy.
                ["source"] = Library.Source,
                ["projectVersion"] = ProjectVersion(folder),
                ["components"] = new JsonArray(Library.All.Select(c => (JsonNode)Library.Describe(c)).ToArray()),
            });
            return 0;
        }
        Words(args, 3, usage);
        NoSettings(args);
        var component = Library.Find(args.Words[2]);
        var described = Library.Describe(component);
        if (json)
        {
            Print(described);
            return 0;
        }
        var name = (string)component["name"]!;
        output.WriteLine($"{(string)component["displayName"]!}, component{((string?)component["partOf"] is { } parent ? $", part of {(string)Library.Find(parent)["name"]!}" : "")}");
        if ((string?)component["summary"] is { } summary) output.WriteLine(summary);
        if ((string?)component["docs"] is { } docs) output.WriteLine(docs);
        output.WriteLine();
        output.WriteLine("Options:");
        var properties = described["parameters"]!["properties"]!.AsObject();
        var needed = described["parameters"]!["required"]!.AsArray().Select(r => (string)r!).ToHashSet();
        foreach (var (option, value) in properties.Where(p => p.Value!["x-group"] is null))
        {
            var p = value!.AsObject();
            var values = p["enum"] is JsonArray choices ? string.Join(" | ", choices.Select(v => (string)v!)) : (string)p["x-type"]!;
            var start = p["default"] is { } d ? $"  (default {d})" : (string?)p["x-default"] is { } written ? $"  (default {written})" : "";
            output.WriteLine($"  {option}  {values}{(needed.Contains(option) ? "  (required)" : "")}{start}");
            if ((string?)p["description"] is { } description) output.WriteLine($"      {description}");
        }
        foreach (var shared in (component["bases"]?.AsArray() ?? []).Select(b => (string)b!))
            if (Library.BaseOptions(shared).Where(o => properties[o]?["x-group"] is not null).ToList() is { Count: > 0 } options)
                output.WriteLine($"  Shared ({shared}): {string.Join(", ", options)}");
        void Names(string title, string key)
        {
            if (described[key]!.AsArray() is { Count: > 0 } members) output.WriteLine($"{title}: {string.Join(", ", members.Select(m => (string)m!["name"]!))}");
        }
        Names("Content", "slots");
        Names("Events", "events");
        if (described["parts"]!.AsArray() is { Count: > 0 } parts) output.WriteLine($"Parts: {string.Join(", ", parts.Select(p => (string)p!))}");
        if (described["examples"]!.AsArray() is { Count: > 0 } examples)
        {
            output.WriteLine();
            output.WriteLine("Examples:");
            var wide = examples.Max(e => ((string)e!["name"]!).Length);
            foreach (var e in examples) output.WriteLine($"  {((string)e!["name"]!).PadRight(wide)}  {(string)e["title"]!}{((bool)e["interactiveOnly"]! ? " (interactive pages)" : "")}");
        }
        return 0;
    }

    // izzy add component Select --at Components/Pages/Home.razor:12: the docs' usage example, in three parts
    // (ExampleInsert), or with --tag the tag alone. With --stdin the page's text is read from standard input
    // (an editor's buffer, perhaps unsaved) and nothing is written: the changes are the answer.
    int AddComponent(Args args)
    {
        const string usage = "izzy add component <Name> [--example <name> | --tag] --at <file>:<line>[:<col>]";
        Words(args, 3, usage);
        var component = Library.Find(args.Words[2]);
        var name = (string)component["name"]!;
        var tag = args.Switch("tag");
        var wanted = args.Take("example");
        if (tag && wanted is not null) throw new UsageException("--tag adds the tag alone and --example an example: give one of them.");
        var stdin = args.Switch("stdin");
        var at = args.Take("at");
        var folder = _projectFolder = args.Take("project");
        var flags = Flags(args);
        NoSettings(args);
        if (stdin && !flags.Json) throw new UsageException("--stdin answers with the changes as data: add --json.");
        if (flags.Overwrite) throw new UsageException("izzy add component adds to a page and never replaces a file: leave --overwrite out.");

        JsonNode? listed = null;
        string text;
        if (tag)
        {
            var required = Library.Parameters(component).Where(p => (bool?)p["required"] == true).Select(p => $" {(string)p["name"]!}=\"\"");
            var content = (component["slots"]?.AsArray() ?? []).Any(s => (string)s!["name"]! == "ChildContent");
            text = $"<{name}{string.Concat(required)}{(content ? $"></{name}>" : " />")}";
        }
        else
        {
            var examples = component["examples"]?.AsArray() ?? [];
            listed = examples.FirstOrDefault(e => ((string)e!["name"]!).Equals(wanted ?? "usage", StringComparison.OrdinalIgnoreCase) || ((string)e["id"]!).Equals(wanted, StringComparison.OrdinalIgnoreCase));
            if (listed is null)
                throw new UsageException(wanted is null
                    ? $"{name} has no usage example. izzy describe component {name} lists its examples, and --tag adds the tag alone."
                    : $"{name} has no example named \"{wanted}\". izzy describe component {name} lists them.");
            text = (string)Library.Example((string)listed["id"]!)!["source"]!;
        }
        if (at is null)
        {
            if (flags.Json || stdin) throw new UsageException("--json and --stdin give a page's changes: say where with --at <file>:<line>.");
            // Nowhere to put it: shown, as a block with no page is.
            _data.Write(text.Replace("__PageNamespace__", "MyApp.Components.Pages", StringComparison.Ordinal).TrimEnd('\n') + "\n");
            error.WriteLine($"{name} isn't placed on a page: add this where it goes, or place it with --at <file>:<line>.");
            var files = listed is null ? [] : (Library.Example((string)listed["id"]!)!["files"]?.AsArray() ?? []).Select(f => (string)f!["name"]!).ToList();
            if (files.Count > 0)
                error.WriteLine($"It uses {string.Join(" and ", files)}, which --at writes beside the page; MyApp.Components.Pages stands for the page's namespace.");
            return 0;
        }

        var project = ProjectInfo.FromProject(FindProject(folder));
        var (path, line, column) = Position(at, project);
        RazorFile(path);
        if (column is not null && !tag) throw new UsageException("A column places a tag: add --tag, or give the line alone for the example.");
        var page = stdin ? (input ?? Console.In).ReadToEnd() : project.ReadFile(path) ?? throw new UsageException($"{path} isn't in the project.");
        var inserts = new List<TextInsert>();
        var creates = new List<FileCreate>();
        var warnings = new List<string>();
        if (tag)
        {
            // At a column the tag goes into the line as it is; at a line it takes a line of its own.
            if (column is { } col) inserts.Add(new TextInsert(path, RazorPage.Offset(page, path, line, col), text, Inserts.Hash(page)));
            else
            {
                var (offset, body) = Inserts.AtLine(page, path, line, text);
                inserts.Add(new TextInsert(path, offset, body, Inserts.Hash(page)));
            }
        }
        else
        {
            var imports = new StringBuilder();
            for (var directory = Path.GetDirectoryName(path); directory is not null; directory = Path.GetDirectoryName(directory))
            {
                imports.Append(project.ReadFile((directory.Length > 0 ? directory.Replace('\\', '/') + "/" : "") + "_Imports.razor")).Append('\n');
                if (directory.Length == 0) break;
            }
            var (offset, _) = Inserts.AtLine(page, path, line, "");
            var where = Path.GetDirectoryName(path)!.Replace('\\', '/');
            // The page's namespace: its files' own, where the example's sample-data files go too. It's the
            // namespace of the project the page is in, then the folders inside that project, as Razor names them.
            var (root, inside) = project.Client is { } client && path.StartsWith(client, StringComparison.Ordinal) ? (project.ClientNamespace ?? project.RootNamespace, where[Math.Min(client.Length, where.Length)..])
                : project.Host.Length > 0 && path.StartsWith(project.Host, StringComparison.Ordinal) ? (project.RootNamespace, where[Math.Min(project.Host.Length, where.Length)..])
                : (project.RootNamespace, where);
            var space = string.Join('.', new[] { root }.Concat(inside.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(NamespacePart)));
            var codeBehind = project.ReadFile(path + ".cs");
            var taken = ExampleInsert.Names(page, imports.ToString(), codeBehind);
            var files = Library.Example((string)listed!["id"]!)!["files"]?.AsArray() ?? [];
            // A member of the page with a type's name hides the type inside the page (a Home with an Orders list,
            // and the example's Orders class): there the example reaches the type by a @using name of its own.
            var hidden = files.SelectMany(file => Regex.Matches((string)file!["source"]!, @"\b(?:class|struct|enum|interface|record)\s+(?!class\b|struct\b)(\w+)").Select(m => m.Groups[1].Value))
                .Where(taken.Contains).Distinct().ToDictionary(type => type, type => $"{space}.{type}", StringComparer.Ordinal);
            (inserts, warnings) = ExampleInsert.Place(page, path, offset, text.Replace("__PageNamespace__", space, StringComparison.Ordinal), imports.ToString(), Library, codeBehind, hidden);
            foreach (var file in files)
            {
                // A model the example keeps in a file of its own goes beside the page, in the page's namespace.
                var target = (where.Length > 0 ? where + "/" : "") + (string)file!["name"]!;
                var written = Regex.Replace((string)file["source"]!, @"^namespace [\w.]+;", $"namespace {space};", RegexOptions.Multiline);
                var existing = project.ReadFile(target);
                if (existing is null) creates.Add(new FileCreate(target, written, null));
                else if (existing.ReplaceLineEndings("\n") != written) warnings.Add($"{target} exists with other content, so the example's {(string)file["name"]!} wasn't written: it holds the types the example uses.");
            }
            if ((bool?)listed["interactiveOnly"] == true && !page.Contains("@rendermode", StringComparison.Ordinal)
                && project.ReadFile(project.Host + "Components/App.razor")?.Contains("@rendermode", StringComparison.Ordinal) != true)
                warnings.Add($"This example needs an interactive page, and {path} has no @rendermode.");
        }

        var changes = new Changeset { Creates = creates, Inserts = inserts, Packages = [], Conflicts = [], Warnings = warnings };
        var write = !flags.DryRun && !stdin;
        if (flags.Diff && !stdin && !flags.Json) output.Write(ChangesetWriter.Diff(changes, project));
        if (flags.Json) Print(ChangesetJson(changes, write));
        foreach (var warning in warnings) error.WriteLine($"warning: {warning}");
        if (!write)
        {
            if (!flags.Json && !flags.Diff) Summary(changes, "Would create", "Would change", "Would add package");
            return 0;
        }
        ChangesetWriter.Apply(changes, project.Directory!);
        if (!flags.Json) Summary(changes, "Created", "Changed", "Added package");
        return 0;
    }

    // What this run may do: the private catalog on this machine (IzzyBlazor's own builds) and a run with no
    // account service stand for the access they were given; otherwise it's the sign-in's.
    ItemTier Plan => proRoot is not null || account is null ? access : account.Plan;

    // Whether this run has Pro. A sign-in that doesn't say so is renewed once first: the account may have
    // reserved the founding price, or bought Pro, since its key was signed.
    bool HasPro => Plan == ItemTier.Pro || (proRoot is null && account?.Upgrade(error) == true);

    // Said once by a command that is Pro's, when Pro is here as a preview.
    void PreviewNote()
    {
        if (proRoot is null && account is { Preview: true }) error.WriteLine(AccountSession.PreviewNote);
    }

    // izzy inspect Components/Pages/Home.razor:12:8 --json: the component there, as data for an inspector.
    int Inspect(Args args)
    {
        const string usage = "izzy inspect <file>:<line>:<col> [--stdin] --json";
        Words(args, 2, usage);
        var stdin = args.Switch("stdin");
        var folder = _projectFolder = args.Take("project");
        if (!args.Switch("json")) throw new UsageException($"izzy inspect answers as data. Usage: {usage}");
        NoSettings(args);
        if (Plan == ItemTier.Free)
        {
            error.WriteLine("Inspecting a component comes with a free IzzyBlazor account. Run izzy login.");
            return 4;
        }
        var (page, path, tag) = TagAt(args.Words[1], folder, stdin, usage);
        Print(TagEdit.Inspect(page, path, tag, Library));
        return 0;
    }

    // izzy set Components/Pages/Home.razor:12:8 --variant Outline --unset size: the edit, as a changeset.
    int Set(Args args)
    {
        const string usage = "izzy set <file>:<line>:<col> --<option> <value> ... [--unset <option>]";
        Words(args, 2, usage);
        var stdin = args.Switch("stdin");
        var folder = _projectFolder = args.Take("project");
        var flags = Flags(args);
        var changes = new List<(string, string?)>();
        while (args.Take("unset") is { } removed) changes.Add((removed, null));
        // What's left are the component's options. One with no value is a switch turned on.
        changes.AddRange(args.Options.Select(o => (o.Name, (string?)(o.Value ?? TagEdit.Switch))));
        if (changes.Count == 0) throw new UsageException($"Nothing to set. Usage: {usage}");
        if (stdin && !flags.Json) throw new UsageException("--stdin answers with the changes as data: add --json.");
        if (!HasPro)
        {
            error.WriteLine("Changing a component's options with izzy set comes with IzzyBlazor Pro.");
            return 3;
        }
        if (!flags.Json) PreviewNote();
        var (page, path, tag) = TagAt(args.Words[1], folder, stdin, usage);
        var project = ProjectInfo.FromProject(FindProject(folder));
        var edits = TagEdit.Set(page, path, tag, Library, changes);
        var changeset = new Changeset { Creates = [], Inserts = edits, Packages = [], Conflicts = [], Warnings = [] };
        var write = !flags.DryRun && !stdin;
        if (flags.Diff && !stdin && !flags.Json) output.Write(ChangesetWriter.Diff(changeset, project));
        if (flags.Json) Print(ChangesetJson(changeset, write));
        if (edits.Count == 0)
        {
            if (!flags.Json) output.WriteLine("Nothing to change.");
            return 0;
        }
        if (!write)
        {
            if (!flags.Json && !flags.Diff) Summary(changeset, "Would create", "Would change", "Would add package");
            return 0;
        }
        ChangesetWriter.Apply(changeset, project.Directory!);
        if (!flags.Json) Summary(changeset, "Created", "Changed", "Added package");
        return 0;
    }

    // izzy wrap Components/Pages/Home.razor:12:5 --with Card: a wrapper around the element there, or around
    // the siblings from it to a second position (…:12:5-18:9). --suggest lists the wrappers that fit.
    int Wrap(Args args)
    {
        const string usage = "izzy wrap <file>:<line>:<col>[-<line>:<col>] --with <component> [--<option> <value> ...], or --suggest --json";
        Words(args, 2, usage);
        var stdin = args.Switch("stdin");
        var suggest = args.Switch("suggest");
        var folder = _projectFolder = args.Take("project");
        var with = suggest ? null : args.Take("with") ?? throw new UsageException($"Say what to wrap it in. Usage: {usage}");
        var flags = Flags(args);
        if ((stdin || suggest) && !flags.Json) throw new UsageException($"{(suggest ? "--suggest" : "--stdin")} answers as data: add --json.");
        if (suggest) NoSettings(args);
        if (!HasPro)
        {
            error.WriteLine("Wrapping markup with izzy wrap comes with IzzyBlazor Pro.");
            return 3;
        }
        if (!flags.Json) PreviewNote();
        var (page, path, start, end, project) = RangeAt(args.Words[1], folder, stdin, usage);
        var (from, to, run, parent) = Restructure.Range(page, path, start, end);
        if (suggest)
        {
            Print(new JsonObject { ["path"] = path, ["start"] = from, ["end"] = to, ["suggestions"] = Restructure.Suggest(run, parent, Library) });
            return 0;
        }
        var options = args.Options.Select(o => (o.Name, (string?)(o.Value ?? TagEdit.Switch))).ToList();
        var name = Restructure.WrapperName(with!, Library);
        var edits = Restructure.Wrap(page, path, from, to, name, Restructure.OpenTag(name, Library, options), Imports(project, path), Library);
        return Edited(new Changeset { Creates = [], Inserts = edits, Packages = [], Conflicts = [], Warnings = [] }, project, flags, stdin);
    }

    // izzy unwrap Components/Pages/Home.razor:12:5: the element there goes, and what it holds moves out a level.
    int Unwrap(Args args)
    {
        const string usage = "izzy unwrap <file>:<line>:<col>";
        Words(args, 2, usage);
        var stdin = args.Switch("stdin");
        var folder = _projectFolder = args.Take("project");
        var flags = Flags(args);
        NoSettings(args);
        if (stdin && !flags.Json) throw new UsageException("--stdin answers with the changes as data: add --json.");
        if (!HasPro)
        {
            error.WriteLine("Unwrapping markup with izzy unwrap comes with IzzyBlazor Pro.");
            return 3;
        }
        if (!flags.Json) PreviewNote();
        var (page, path, start, end, project) = RangeAt(args.Words[1], folder, stdin, usage);
        if (end is not null) throw new UsageException($"izzy unwrap takes one position. Usage: {usage}");
        var element = Restructure.At(Restructure.Elements(page), start) ?? throw new UsageException($"No element at that place in {path}: put the position on a tag.");
        var edits = Restructure.Unwrap(page, path, element, Library);
        return Edited(new Changeset { Creates = [], Inserts = edits, Packages = [], Conflicts = [], Warnings = [] }, project, flags, stdin);
    }

    // izzy extract Components/Pages/Home.razor:12:5-30:9 --name OrderSummary: the markup there becomes a component
    // of its own, in the page's folder or the one --folder names, and its tag takes the markup's place.
    int Extract(Args args)
    {
        const string usage = "izzy extract <file>:<line>:<col>[-<line>:<col>] --name <Name> [--folder <folder>]";
        Words(args, 2, usage);
        var stdin = args.Switch("stdin");
        var folder = _projectFolder = args.Take("project");
        var name = args.Take("name") ?? throw new UsageException($"Give the new component a name. Usage: {usage}");
        var into = args.Take("folder");
        var flags = Flags(args);
        NoSettings(args);
        if (stdin && !flags.Json) throw new UsageException("--stdin answers with the changes as data: add --json.");
        if (!Regex.IsMatch(name, "^[A-Z][A-Za-z0-9_]*$"))
            throw new UsageException($"--name is a component's name, such as OrderSummary, not \"{name}\": it starts with a capital letter and has no spaces.");
        if (Library.Options(name) is not null) throw new UsageException($"{name} is an IzzyBlazor component's name. Pick another --name.");
        if (!HasPro)
        {
            error.WriteLine("Moving markup to a component with izzy extract comes with IzzyBlazor Pro.");
            return 3;
        }
        if (!flags.Json) PreviewNote();
        var (page, path, start, end, project) = RangeAt(args.Words[1], folder, stdin, usage);
        var (from, to, _, _) = Restructure.Range(page, path, start, end);
        var own = Path.GetDirectoryName(path)!.Replace('\\', '/');
        var target = (into ?? own).Replace('\\', '/').Trim('/');
        if (target.Split('/').Any(part => part is "." or "..") || Path.IsPathRooted(target))
            throw new UsageException($"--folder is a folder in the project, such as Components/Shared, not \"{into}\".");
        var file = (target.Length > 0 ? target + "/" : "") + name + ".razor";
        if (project.ReadFile(file) is not null) throw new UsageException($"{file} is already there. Pick another --name or --folder.");
        var (edits, create, warnings) = Restructure.Extract(page, path, from, to, name, file, Library);
        // A component in another folder is in another namespace: the page names it, unless an _Imports.razor does.
        if (!target.Equals(own, StringComparison.OrdinalIgnoreCase))
        {
            var inClient = project.Client is { } client && file.StartsWith(client, StringComparison.Ordinal);
            var root = inClient ? project.Client! : project.Host;
            if (!file.StartsWith(root, StringComparison.Ordinal)) throw new UsageException($"--folder is a folder of the project the page is in, under {root.TrimEnd('/')}.");
            var inside = target.Length > root.TrimEnd('/').Length ? target[root.Length..].Trim('/') : "";
            var ns = (inClient ? project.ClientNamespace ?? project.RootNamespace + ".Client" : project.RootNamespace)
                + (inside.Length > 0 ? "." + string.Join('.', inside.Split('/').Select(NamespacePart)) : "");
            if (Restructure.Using(page, path, Imports(project, path), ns, Inserts.Hash(page)) is { } added) edits.Insert(0, added);
        }
        return Edited(new Changeset { Creates = [create], Inserts = edits, Packages = [], Conflicts = [], Warnings = warnings }, project, flags, stdin);
    }

    // izzy doctor: whether what izzy needs is in place on this machine and in this app, each check with what it
    // found and how to put it right. It changes nothing; --build also builds the app.
    int Doctor(Args args)
    {
        const string usage = "izzy doctor [--build] [--project <path>] [--json]";
        Words(args, 1, usage);
        var folder = _projectFolder = args.Take("project");
        var json = args.Switch("json");
        var build = args.Switch("build");
        var browser = args.Take("browser");
        NoSettings(args);
        var checks = new JsonArray();
        void Check(string id, string title, string level, string found, string? detail = null, string? fix = null) =>
            checks.Add(new JsonObject { ["id"] = id, ["title"] = title, ["level"] = level, ["found"] = found, ["detail"] = detail, ["fix"] = fix });
        var izzy = Generator.IzzyBlazorVersion;

        // The app, when this is one.
        ProjectInfo? project = null;
        string? none = null;
        try { project = ProjectInfo.FromProject(FindProject(folder)); }
        catch (Exception e) when (e is UsageException or GenerationException) { none = e.Message; }
        var directory = project?.Directory ?? (folder is not null && Directory.Exists(Path.GetFullPath(folder, workingDirectory)) ? Path.GetFullPath(folder, workingDirectory) : workingDirectory);

        // The SDK dotnet picks in that folder: a global.json there can name another.
        string? sdk = null;
        try
        {
            using var process = DotnetHost.Start(directory, ["--version"], "so the SDK wasn't checked.");
            sdk = process.StandardOutput.ReadToEnd().Trim().Split('\n')[^1].Trim();
            process.WaitForExit();
            if (process.ExitCode != 0) sdk = null;
        }
        catch (GenerationException) { }
        if (sdk is null || !int.TryParse(sdk.Split('.')[0], out var major))
            Check("sdk", ".NET SDK", "fail", "Not found", "IzzyBlazor needs the .NET 10 SDK or later. A global.json in this folder may name one that isn't installed.", "Install the .NET SDK from https://dotnet.microsoft.com/download");
        else if (major < 10)
            Check("sdk", ".NET SDK", "fail", sdk, "IzzyBlazor needs the .NET 10 SDK or later. A global.json in this folder may hold it at an older one.", "Install the .NET SDK from https://dotnet.microsoft.com/download");
        else Check("sdk", ".NET SDK", "ok", sdk);

        Check("izzy", "izzy", "ok", izzy);

        if (project is null)
            Check("app", "IzzyBlazor app", "info", "None in this folder", none, "izzy new starter blazor -n <name>, or izzy init in a Blazor app");
        else
        {
            var package = ProjectVersion(folder);
            if (package is null)
            {
                var csproj = File.ReadAllText(Path.Combine(project.Directory!, project.Host, project.Name + ".csproj"));
                if (ReferencesIzzyBlazor(csproj)) Check("app", "IzzyBlazor app", "ok", $"{project.Name}: IzzyBlazor, version set outside the project file");
                else Check("app", "IzzyBlazor app", "info", $"{project.Name} doesn't reference IzzyBlazor", "izzy can set it up: the package, the stylesheet, the theme and the services.", "izzy init");
            }
            else if (package == izzy) Check("app", "IzzyBlazor app", "ok", $"{project.Name}: IzzyBlazor {package}");
            else
                Check("app", "IzzyBlazor app", "warn", $"{project.Name}: IzzyBlazor {package}", $"izzy is {izzy} and the app uses {package}. What izzy adds is written for its own version, so keep the two the same.",
                    ToolManifest.Pinned(directory) is null ? "izzy update" : "dotnet tool restore, then dotnet izzy update");

            if (ToolManifest.Pinned(directory) is not { } pinned)
                Check("tool", "The app's own izzy", "info", "Not in a tool manifest", "The izzy on this machine is used. An app that lists izzy in .config/dotnet-tools.json gives everyone who works on it the same version.", "izzy update");
            else if (package is not null && pinned.Version != package)
                Check("tool", "The app's own izzy", "warn", $"{pinned.Version} in {Path.GetRelativePath(directory, pinned.File).Replace('\\', '/')}", $"The app's package is {package}: its izzy should be the same version.", "izzy update");
            else Check("tool", "The app's own izzy", "ok", $"{pinned.Version} in {Path.GetRelativePath(directory, pinned.File).Replace('\\', '/')}");

            if (build)
            {
                string text;
                int exit;
                try
                {
                    using var process = DotnetHost.Start(project.Directory!, ["build", Path.Combine(project.Host, project.Name + ".csproj"), "--nologo", "-v", "q"], "so the app wasn't built.");
                    text = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    exit = process.ExitCode;
                }
                catch (GenerationException e) { (text, exit) = (e.Message, 1); }
                var errors = text.Split('\n').Select(l => l.Trim()).Where(l => Regex.IsMatch(l, @"\berror\b")).Distinct().ToList();
                if (exit == 0) Check("build", "Build", "ok", "The app builds");
                else Check("build", "Build", "fail", errors.Count > 0 ? $"{errors.Count} error{(errors.Count == 1 ? "" : "s")}" : "The build failed", errors.FirstOrDefault() ?? text.Trim(), "dotnet build");
            }
        }

        if (account is null) Check("account", "Account", "info", "This izzy has no account service");
        else if (!account.SignedIn) Check("account", "Account", "info", "Not signed in", "Everything open works without an account. A free account adds app starters, blocks, Izzy CRM and the audit's verdicts.", "izzy login");
        else Check("account", "Account", "ok", account.Preview ? "Signed in: Pro preview" : account.Plan == ItemTier.Pro ? "Signed in: Pro" : "Signed in: Free");

        string? chrome = null;
        string? wrong = null;
        try { chrome = Chromium.Find(browser); }
        catch (UsageException e) { wrong = e.Message; }
        if (chrome is not null) Check("browser", "Browser for the audit", "ok", chrome);
        else Check("browser", "Browser for the audit", "warn", wrong ?? "Chrome, Edge or Chromium not found", "izzy audit opens pages in one of them. Everything else works without.", "Install Chrome or Edge, or name one: izzy audit --browser <path> (or IZZY_BROWSER)");

        var failed = checks.Count(c => (string)c!["level"]! == "fail");
        if (json)
        {
            Print(new JsonObject { ["ok"] = failed == 0, ["izzy"] = izzy, ["checks"] = checks });
            return 0;
        }
        foreach (var check in checks)
        {
            var level = (string)check!["level"]!;
            output.WriteLine($"{level switch { "ok" => "ok  ", "warn" => "warn", "fail" => "FAIL", _ => "info" }}  {(string)check["title"]!}: {(string)check["found"]!}");
            if (level != "ok" && (string?)check["detail"] is { } detail) output.WriteLine($"      {detail}");
            if (level != "ok" && (string?)check["fix"] is { } fix) output.WriteLine($"      Fix: {fix}");
        }
        return failed == 0 ? 0 : 1;
    }

    // A changeset printed, shown or written, as izzy set does: an editor's buffer (--stdin) is never written.
    int Edited(Changeset changeset, ProjectInfo project, (bool Json, bool DryRun, bool Diff, bool Overwrite) flags, bool stdin)
    {
        var write = !flags.DryRun && !stdin;
        if (flags.Diff && !stdin && !flags.Json) output.Write(ChangesetWriter.Diff(changeset, project));
        if (flags.Json) Print(ChangesetJson(changeset, write));
        foreach (var warning in changeset.Warnings) error.WriteLine($"warning: {warning}");
        if (!write)
        {
            if (!flags.Json && !flags.Diff) Summary(changeset, "Would create", "Would change", "Would add package");
            return 0;
        }
        ChangesetWriter.Apply(changeset, project.Directory!);
        if (!flags.Json) Summary(changeset, "Created", "Changed", "Added package");
        return 0;
    }

    // The page and the offsets of <file>:<line>:<col>, or of a range of two: <file>:<line>:<col>-<line>:<col>.
    (string Page, string Path, int Start, int? End, ProjectInfo Project) RangeAt(string at, string? folder, bool stdin, string usage)
    {
        var project = ProjectInfo.FromProject(FindProject(folder));
        var range = Regex.Match(at, @"^(?<first>.+:\d+:\d+)-(?:.+:)?(?<line>\d+):(?<col>\d+)$");
        var (path, line, column) = Position(range.Success ? range.Groups["first"].Value : at, project);
        RazorFile(path);
        if (column is not { } col) throw new UsageException($"Give the column too. Usage: {usage}");
        var page = stdin ? (input ?? Console.In).ReadToEnd() : project.ReadFile(path) ?? throw new UsageException($"{path} isn't in the project.");
        int? end = range.Success ? RazorPage.Offset(page, path, int.Parse(range.Groups["line"].Value), int.Parse(range.Groups["col"].Value)) : null;
        return (page, path, RazorPage.Offset(page, path, line, col), end, project);
    }

    // The text of the _Imports.razor files that apply to a file.
    static string Imports(ProjectInfo project, string path)
    {
        var imports = new StringBuilder();
        for (var directory = Path.GetDirectoryName(path); directory is not null; directory = Path.GetDirectoryName(directory))
        {
            imports.Append(project.ReadFile((directory.Length > 0 ? directory.Replace('\\', '/') + "/" : "") + "_Imports.razor")).Append('\n');
            if (directory.Length == 0) break;
        }
        return imports.ToString();
    }

    // The page (from standard input for an editor's buffer) and the component's tag at <file>:<line>:<col>.
    (string Page, string Path, RazorTag Tag) TagAt(string at, string? folder, bool stdin, string usage)
    {
        var project = ProjectInfo.FromProject(FindProject(folder));
        var (path, line, column) = Position(at, project);
        RazorFile(path);
        if (column is not { } col) throw new UsageException($"Give the column too. Usage: {usage}");
        var page = stdin ? (input ?? Console.In).ReadToEnd() : project.ReadFile(path) ?? throw new UsageException($"{path} isn't in the project.");
        var tag = TagEdit.Find(page, RazorPage.Offset(page, path, line, col), Library)
            ?? throw new UsageException($"No IzzyBlazor component at {path}:{line}:{col}.");
        return (page, path, tag);
    }

    static void RazorFile(string path)
    {
        if (!path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)) throw new UsageException($"{path} isn't a .razor file: components go on a page or in a component.");
    }

    // A folder's name as Razor writes it in a namespace: my-area is my_area, 2024 is _2024.
    internal static string NamespacePart(string folder)
    {
        var part = new string(folder.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
        return part.Length > 0 && char.IsDigit(part[0]) ? "_" + part : part;
    }

    // <file>:<line> or <file>:<line>:<col>, the file relative to here (or absolute), inside the project.
    (string Path, int Line, int? Column) Position(string at, ProjectInfo project)
    {
        var parts = at.Split(':');
        int? column = null;
        var count = parts.Length;
        // The last one or two parts are numbers; what's before them is the file (C:\app\Home.razor has a colon too).
        if (count >= 3 && int.TryParse(parts[^1], out var second) && int.TryParse(parts[^2], out var first) && first >= 1 && second >= 1)
        {
            column = second;
            count -= 2;
            return Named(first);
        }
        if (count >= 2 && int.TryParse(parts[^1], out var line) && line >= 1)
        {
            count -= 1;
            return Named(line);
        }
        throw new UsageException($"--at is <file>:<line> or <file>:<line>:<col>, such as Components/Pages/Home.razor:12, not \"{at}\".");

        (string, int, int?) Named(int line)
        {
            var file = string.Join(':', parts[..count]);
            try { return (project.Relative(Path.GetFullPath(file, workingDirectory)), line, column); }
            catch (GenerationException) { throw new UsageException($"{file} isn't in the project."); }
        }
    }

    // The IzzyBlazor version the app in this folder references, for a front end to compare with the data's.
    string? ProjectVersion(string? folder)
    {
        try
        {
            var project = ProjectInfo.FromProject(FindProject(folder));
            var csproj = File.ReadAllText(Path.Combine(project.Directory!, project.Host, project.Name + ".csproj"));
            return Regex.Match(csproj, """<PackageReference\s[^>]*?Include="IzzyBlazor(?:\.Core)?"[^>]*>""", RegexOptions.IgnoreCase) is { Success: true } reference
                && Regex.Match(reference.Value, """Version="([^"]+)" """.TrimEnd(), RegexOptions.IgnoreCase) is { Success: true } m ? m.Groups[1].Value : null;
        }
        catch (Exception e) when (e is UsageException or GenerationException or IOException)
        {
            return null;
        }
    }

    Components? _library;

    // The app's own version of the components where its IzzyBlazor.Core package holds the data (alpha.26 on);
    // else this tool's, with a line saying so when the app is on another version.
    Components Library => _library ??= LoadLibrary();

    // The --project a command was given, for finding the app's package.
    string? _projectFolder;

    Components LoadLibrary()
    {
        if (PackageData() is { } path && Components.FromPackage(path) is { } packaged) return packaged;
        var own = Components.Load(Path.Combine(AppContext.BaseDirectory, "components.json"));
        if (ProjectVersion(_projectFolder) is { } app && app != own.Version)
            error.WriteLine($"Using this izzy's component data ({own.Version}); the app is on {app}.");
        return own;
    }

    // tools/izzy/components.json in the IzzyBlazor.Core package the app restored, from its obj/project.assets.json.
    string? PackageData()
    {
        try
        {
            var project = ProjectInfo.FromProject(FindProject(_projectFolder));
            var assets = Path.Combine(project.Directory!, project.Host, "obj", "project.assets.json");
            if (!File.Exists(assets) || JsonNode.Parse(File.ReadAllText(assets)) is not JsonObject restored) return null;
            var library = restored["libraries"]?.AsObject().FirstOrDefault(l => l.Key.StartsWith("IzzyBlazor.Core/", StringComparison.OrdinalIgnoreCase)).Value;
            if ((string?)library?["type"] != "package" || (string?)library["path"] is not { } relative) return null;
            return (restored["packageFolders"]?.AsObject() ?? []).Select(f => Path.Combine(f.Key, relative, "tools", "izzy", "components.json")).FirstOrDefault(File.Exists);
        }
        catch (Exception e) when (e is UsageException or GenerationException or IOException or System.Text.Json.JsonException or InvalidOperationException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    int New(Args args)
    {
        // izzy new starter sidebar, izzy new kit crm: the kind, then the id.
        const string usage = "izzy new starter|kit <id> [-n <name>]";
        if (args.Words.Count == 2 && Catalog.Items.TryGetValue(args.Words[1], out var named) && named.Manifest.Kind.MakesProject())
            throw new UsageException($"Say what {named.Manifest.Id} is: {named.Manifest.NewCommand()} -n <name>.");
        Words(args, 3, usage);
        if (args.Words[1] is not ("starter" or "kit")) throw new UsageException($"Usage: {usage}");
        var item = Catalog[args.Words[2]];
        if (!item.Manifest.Kind.MakesProject())
            throw new UsageException($"{item.Manifest.Id} goes into a project you have: {item.Manifest.AddCommand()}.");
        if (!item.Manifest.Kind.ToString().Equals(args.Words[1], StringComparison.OrdinalIgnoreCase))
            throw new UsageException($"{item.Manifest.Id} is a {item.Manifest.Kind.ToString().ToLowerInvariant()}: {item.Manifest.NewCommand()} -n <name>.");
        var name = args.Take("name");
        if (name is not null && (name.IndexOfAny(['/', '\\']) >= 0 || name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
            throw new UsageException($"\"{name}\" isn't a project name. Give the folder with -o.");
        var folder = args.Take("output");
        var directory = Path.GetFullPath(folder ?? name ?? ".", workingDirectory);
        name ??= Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var flags = Flags(args);
        var project = ProjectInfo.ForNewProject(name) with { ReadFile = path => ReadFile(directory, path) };
        var changes = Generator.Generate(item, Settings(item.Manifest, args), project);
        // --mcp: the agent files are izzy agent's to write, with izzy's own server and the skill beside the
        // docs server the starter's files name.
        var agent = changes.Settings.GetValueOrDefault("Mcp") == "true";
        if (agent)
            changes = new Changeset
            {
                Creates = [.. changes.Creates.Where(c => !c.Path.EndsWith("mcp.json", StringComparison.Ordinal))], Inserts = changes.Inserts,
                Packages = changes.Packages, Conflicts = [.. changes.Conflicts.Where(c => !c.EndsWith("mcp.json", StringComparison.Ordinal))],
                Warnings = changes.Warnings, Settings = changes.Settings, Restore = changes.Restore,
            };
        var exit = Finish(item, changes, project, directory, existing: false, flags);
        if (exit == 0 && agent) AgentFiles.Write(directory, flags.DryRun, flags.Json ? TextWriter.Null : output, error);
        return exit;
    }

    // izzy update: the app's IzzyBlazor packages and the izzy in its tool manifest to one version, then a build;
    // outside an app, or with --global, the izzy on this machine.
    int Update(Args args)
    {
        Words(args, 1, "izzy update [--to <version>] [--global] [--project <path>] [--no-build] [--dry-run] [--diff] [--json]");
        var to = args.Take("to");
        var global = args.Switch("global");
        var folder = args.Take("project");
        var build = !args.Switch("no-build");
        var flags = Flags(args);
        NoSettings(args);
        if (to is not null && !ToolManifest.Exact(to)) throw new UsageException($"--to takes a version, such as 0.1.0: \"{to}\" isn't one.");
        if (global && folder is not null) throw new UsageException("--global updates the izzy on this machine, so it takes no --project.");
        string? csproj = null;
        if (!global)
        {
            try { csproj = FindProject(folder); }
            catch (UsageException) when (folder is null) { }
        }
        var target = to ?? (releases ?? Releases.FromEnvironment()).Latest(TimeSpan.FromSeconds(10))
            ?? throw new GenerationException("izzy couldn't reach nuget.org to see the newest release. Try again, or name one: izzy update --to <version>.");
        return csproj is null ? UpdateTool(target, flags.Json, flags.DryRun) : UpdateApp(csproj, target, build, flags);
    }

    int UpdateApp(string csproj, string target, bool build, (bool Json, bool DryRun, bool Diff, bool Overwrite) flags)
    {
        var project = ProjectInfo.FromProject(csproj);
        var root = project.Directory!;
        var inserts = new List<TextInsert>();
        var warnings = new List<string>();
        var unversioned = new List<string>();
        string? from = null;
        var referenced = false;
        var central = false;
        foreach (var path in VersionFiles(root))
        {
            var text = File.ReadAllText(Path.Combine(root, path));
            foreach (Match reference in PackageElements(text))
            {
                var isReference = reference.Groups[1].Value.Equals("PackageReference", StringComparison.OrdinalIgnoreCase);
                referenced |= isReference;
                central |= !isReference;
                if (Regex.Match(reference.Value, """\sVersion="([^"]*)" """.TrimEnd(), RegexOptions.IgnoreCase) is not { Success: true } version)
                {
                    if (isReference) unversioned.Add(path);
                    continue;
                }
                var at = version.Groups[1];
                if (!ToolManifest.Exact(at.Value))
                {
                    warnings.Add($"{path} sets {reference.Groups[2].Value}'s version as {at.Value}, which izzy leaves as it is: change it by hand.");
                    continue;
                }
                if (from is null || Releases.Compare(at.Value, from) < 0) from = at.Value;
                if (at.Value != target) inserts.Add(new TextInsert(path, reference.Index + at.Index, target, Generation.Inserts.Hash(text)) { Length = at.Length });
            }
        }
        if (!referenced)
            throw new UsageException($"{project.Name} doesn't reference the IzzyBlazor package, so there is nothing to update in it. izzy init sets IzzyBlazor up in an app; izzy update --global updates the izzy on this machine.");
        if (unversioned.Count > 0 && !central)
            warnings.Add($"{string.Join(", ", unversioned.Distinct())} references IzzyBlazor with its version set somewhere izzy didn't find: set it to {target} there.");
        // The app's own izzy moves with its library. An app with no manifest, or one without izzy, gets none here.
        if (ToolManifest.Change(project.ReadFile, target) is { Insert: { Length: > 0 } tool }) inserts.Add(tool);

        var changes = new Changeset { Creates = [], Inserts = inserts, Packages = [], Conflicts = [], Warnings = warnings };
        var write = !flags.DryRun && inserts.Count > 0;
        // What the releases in between removed or renamed. An izzy without the record still updates the app.
        var between = from is not null && File.Exists(ReleaseChanges.Beside) ? Changes.Between(from, target) : [];
        if (flags.Diff && !flags.Json) _data.Write(ChangesetWriter.Diff(changes, project));
        if (flags.Json)
        {
            var json = ChangesetJson(changes, write);
            json["from"] = from;
            json["to"] = target;
            json["changes"] = new JsonArray(between.Select(c => (JsonNode)c.Json()).ToArray());
            Print(json);
        }
        foreach (var warning in warnings) error.WriteLine($"warning: {warning}");
        if (inserts.Count == 0)
        {
            if (flags.Json) return 0;
            if (warnings.Count == 0) output.WriteLine($"Nothing to change: {project.Name} is on IzzyBlazor {target}.");
            if (Releases.Compare(Generator.IzzyBlazorVersion, target) < 0)
                output.WriteLine($"The izzy that ran this is {Generator.IzzyBlazorVersion}. To update the one on this machine: izzy update --global");
            return 0;
        }
        var move = from is null ? $"to IzzyBlazor {target}" : $"from IzzyBlazor {from} to {target}";
        if (!write)
        {
            if (!flags.Json && !flags.Diff)
            {
                Summary(changes, "Would create", "Would change", "Would add package");
                output.WriteLine($"Would update {project.Name} {move}");
                Changed(between, from, target);
            }
            return 0;
        }
        ChangesetWriter.Apply(changes, root);
        if (between.Count > 0) RecordFrom(root, from!);
        if (!flags.Json)
        {
            Summary(changes, "Created", "Changed", "Added package");
            output.WriteLine($"Updated {project.Name} {move}");
            Changed(between, from, target);
        }
        if (inserts.Any(i => ToolManifest.Is(i.Path))) Dotnet(root, flags.Json, "tool", "restore");
        var exit = build ? Build(project, flags.Json) : 0;
        if (!flags.Json) output.WriteLine($"What changed in each release: {Releases.Notes}");
        return exit;
    }

    ReleaseChanges? _changes;

    ReleaseChanges Changes => _changes ??= ReleaseChanges.Load(ReleaseChanges.Beside);

    // What the releases after one, up to another, removed or renamed: a line each, and one line for a
    // parameter that went from several components the same way.
    void Changed(List<ReleaseChange> between, string? from, string to)
    {
        if (between.Count == 0) return;
        output.WriteLine($"The releases after {from}, up to {to}, removed or renamed {between.Count} {(between.Count == 1 ? "name" : "names")}:");
        foreach (var release in between.GroupBy(c => c.Release))
        {
            output.WriteLine($"  {release.Key}");
            foreach (var same in release.GroupBy(c => c is { Of: "component", Member: not null } ? (c.Member, c.To, c.Note) : (c.Subject, null, null)))
            {
                var first = same.First();
                output.WriteLine(same.Count() == 1 ? $"    {first.Line}" : $"    {first.Line.Replace(first.Subject, $"{first.Member} on {string.Join(", ", same.Select(c => c.Name))}")}");
            }
        }
        output.WriteLine("Razor takes an old parameter name without a warning, and the page fails when it's opened.");
        output.WriteLine("izzy upgrade shows where this app uses these, and izzy upgrade --apply renames what has a new name (IzzyBlazor Pro).");
        output.WriteLine($"These are names in C# and Razor. CSS variables and classes a release renamed are in its notes: {Releases.Notes}");
    }

    // .izzy/upgrade.json: the release the app came from, for izzy upgrade. An update before izzy upgrade has
    // run keeps the older one.
    static string UpgradeRecord(string root) => Path.Combine(root, ".izzy", "upgrade.json");

    static string? RecordedFrom(string root)
    {
        try
        {
            return File.Exists(UpgradeRecord(root)) && (string?)JsonNode.Parse(File.ReadAllText(UpgradeRecord(root)))?["from"] is { } from && ToolManifest.Exact(from) ? from : null;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or IOException)
        {
            return null;
        }
    }

    static void RecordFrom(string root, string from)
    {
        if (RecordedFrom(root) is { } earlier && Releases.Compare(earlier, from) <= 0) return;
        Directory.CreateDirectory(Path.GetDirectoryName(UpgradeRecord(root))!);
        File.WriteAllText(UpgradeRecord(root), new JsonObject { ["from"] = from }.ToJsonString(Json) + "\n");
    }

    // The elements that reference IzzyBlazor in a project file, but for those commented out.
    static IEnumerable<Match> PackageElements(string text)
    {
        var comments = Regex.Matches(text, "<!--.*?-->", RegexOptions.Singleline);
        return Regex.Matches(text, """<(PackageReference|PackageVersion)\s[^>]*?(?:Include|Update)="(IzzyBlazor(?:\.Core)?)"[^>]*>""", RegexOptions.IgnoreCase)
            .Where(m => !comments.Any(c => m.Index > c.Index && m.Index < c.Index + c.Length));
    }

    // The oldest IzzyBlazor version the app's project files name.
    static string? AppVersion(string root) =>
        VersionFiles(root)
            .SelectMany(path => PackageElements(File.ReadAllText(Path.Combine(root, path))))
            .Select(m => Regex.Match(m.Value, """\sVersion="([^"]*)" """.TrimEnd(), RegexOptions.IgnoreCase).Groups[1].Value).Where(ToolManifest.Exact)
            .Order(Comparer<string>.Create(Releases.Compare)).FirstOrDefault();

    // izzy upgrade: where the app uses what the releases between two versions removed or renamed. It is a plan,
    // and writes nothing, until --apply says the plan is approved. What has a new name is
    // renamed, as a changeset; the rest is listed by file and line with what to do.
    int Upgrade(Args args)
    {
        const string usage = "izzy upgrade [--apply] [--from <version>] [--to <version>] [--project <path>] [--diff] [--json]";
        Words(args, 1, usage);
        var from = args.Take("from");
        var to = args.Take("to");
        var folder = args.Take("project");
        var apply = args.Switch("apply");
        var flags = Flags(args);
        NoSettings(args);
        foreach (var (name, version) in new[] { ("from", from), ("to", to) })
        {
            if (version is not null && !ToolManifest.Exact(version)) throw new UsageException($"--{name} takes a version, such as 0.1.0: \"{version}\" isn't one.");
        }
        if (!HasPro)
        {
            error.WriteLine("izzy upgrade comes with IzzyBlazor Pro. izzy update lists what each release removed or renamed. If your account has Pro, run izzy login.");
            return 3;
        }
        var project = ProjectInfo.FromProject(FindProject(folder));
        var root = project.Directory!;
        var recorded = RecordedFrom(root);
        // The whole of the move izzy update recorded: only such a run may end it.
        var whole = to is null && recorded is not null && (from is null || Releases.Compare(from, recorded) <= 0);
        from ??= recorded
            ?? throw new UsageException($"Say which release the app came from: izzy upgrade --from <version>. izzy update records it when it moves an app. Usage: {usage}");
        to ??= AppVersion(root) ?? Generator.IzzyBlazorVersion;
        if (Releases.Compare(from, to) >= 0)
            throw new UsageException($"The app is on {to}, which isn't after {from}. Move it first with izzy update, or name the release it came from: izzy upgrade --from <version>.");
        if (!flags.Json) PreviewNote();
        var warnings = new List<string>();
        if (Releases.Compare(to, Generator.IzzyBlazorVersion) > 0)
            warnings.Add($"This izzy is {Generator.IzzyBlazorVersion}, and doesn't know what the releases after it changed. To update it: izzy update --global");

        var between = Changes.Between(from, to);
        var uses = new List<ChangeUse>();
        foreach (var path in between.Count == 0 ? [] : ReleaseChanges.Files(root))
        {
            try
            {
                uses.AddRange(ReleaseChanges.Find(path, File.ReadAllText(Path.Combine(root, path)), between, Library));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                warnings.Add($"{path} couldn't be read, so it wasn't looked in.");
            }
        }
        var fixes = uses.Where(u => u.Fix is not null).Select(u => u.Fix!).DistinctBy(f => (f.Path, f.Offset)).ToList();
        var found = uses.Where(u => !u.Closing).ToList();
        var byHand = found.Count(u => u.Fix is null);
        var changeset = new Changeset { Creates = [], Inserts = fixes, Packages = [], Conflicts = [], Warnings = warnings };
        var write = apply && !flags.DryRun && fixes.Count > 0;
        // Written first: a file that can't be written ends the command before it says anything was.
        if (write) ChangesetWriter.Apply(changeset, root);
        if (flags.Diff && !flags.Json) output.Write(ChangesetWriter.Diff(changeset, project));
        if (flags.Json)
        {
            var json = ChangesetJson(changeset, write);
            json["from"] = from;
            json["to"] = to;
            json["uses"] = new JsonArray(found.Select(u =>
            {
                var use = u.Change.Json();
                use["path"] = u.Path;
                use["line"] = u.Line;
                use["column"] = u.Column;
                use["renamed"] = u.Fix is not null;
                return (JsonNode)use;
            }).ToArray());
            Print(json);
        }
        foreach (var warning in warnings) error.WriteLine($"warning: {warning}");
        // Nothing is left of the move once every use has its new name.
        if (whole && !flags.DryRun && (apply || fixes.Count == 0) && byHand == 0 && warnings.Count == 0 && File.Exists(UpgradeRecord(root))) File.Delete(UpgradeRecord(root));
        if (flags.Json) return 0;
        if (found.Count == 0)
        {
            output.WriteLine($"Nothing to change: {project.Name} uses nothing that the releases after {from}, up to {to}, removed or renamed.");
            return 0;
        }
        // With --diff the renames are in the diff: what's left is listed.
        foreach (var use in found.Where(u => !flags.Diff || u.Fix is null)) output.WriteLine($"{use.Path}:{use.Line}:{use.Column}  {use.Change.Line}");
        var renamed = found.Count - byHand;
        if (renamed > 0) output.WriteLine($"{(write ? "Renamed" : "Would rename")} {renamed} in {fixes.Select(f => f.Path).Distinct().Count()} {(fixes.Select(f => f.Path).Distinct().Count() == 1 ? "file" : "files")}.");
        if (byHand > 0) output.WriteLine($"{byHand} to change by hand: {(byHand == 1 ? "it has" : "they have")} no plain new name.");
        if (renamed > 0 && !write) output.WriteLine($"Nothing was written. To make the {(renamed == 1 ? "rename" : "renames")}: izzy upgrade --apply");
        return 0;
    }

    // The project files that may hold IzzyBlazor's version, from the app's folder.
    static IEnumerable<string> VersionFiles(string root) =>
        Directory.EnumerateFiles(root, "*", new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 4, IgnoreInaccessible = true })
            .Where(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(f) is "Directory.Packages.props" or "Directory.Build.props")
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Where(p => !p.Split('/').Any(part => part is "bin" or "obj" or "node_modules" || part.StartsWith('.')))
            .Order(StringComparer.Ordinal);

    // The izzy on this machine, with dotnet tool update. A tool can't replace its own files while it runs on
    // Windows, so there the global izzy starts the update and ends; .NET does it a moment later.
    int UpdateTool(string target, bool json, bool dryRun)
    {
        var mine = Generator.IzzyBlazorVersion;
        var self = AppContext.BaseDirectory.Contains(Path.Combine(".store", "izzyblazor.cli"), StringComparison.OrdinalIgnoreCase);
        List<string> arguments = ["tool", "update", "-g", "IzzyBlazor.Cli", "--version", target];
        if (Releases.Compare(target, mine) < 0) arguments.Add("--allow-downgrade");
        var command = $"dotnet {string.Join(' ', arguments)}";
        void Answer(bool written)
        {
            if (json) Print(new JsonObject { ["written"] = written, ["from"] = self ? mine : null, ["to"] = target, ["command"] = command });
        }
        if (self && target == mine)
        {
            Answer(false);
            if (!json) output.WriteLine($"Nothing to change: izzy is on {mine}.");
            return 0;
        }
        if (dryRun)
        {
            Answer(false);
            if (!json) output.WriteLine($"Would run {command}");
            return 0;
        }
        if (self && OperatingSystem.IsWindows())
        {
            var start = new ProcessStartInfo("cmd.exe")
            {
                Arguments = $"/c \"ping -n 3 127.0.0.1 >nul & \"{DotnetHost.Path}\" {string.Join(' ', arguments)}{(json ? " >nul 2>&1" : "")}\"",
                UseShellExecute = false,
                WorkingDirectory = workingDirectory,
            };
            try { Process.Start(start)?.Dispose(); }
            catch (System.ComponentModel.Win32Exception) { throw new GenerationException($"izzy couldn't start the update. Run it yourself: {command}"); }
            Answer(true);
            if (!json) output.WriteLine($"Updating izzy to {target}. .NET replaces it once this command ends, and says so here in a moment.");
            return 0;
        }
        using var process = DotnetHost.Start(workingDirectory, arguments, $"so izzy wasn't updated. Run it yourself: {command}");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Answer(process.ExitCode == 0);
        if (process.ExitCode != 0)
        {
            error.WriteLine($"{command} failed, so izzy wasn't updated:\n{stdout.Result}{stderr.Result}".TrimEnd());
            return 1;
        }
        if (!json)
        {
            output.WriteLine($"Updated the izzy on this machine to {target}");
            output.WriteLine($"What changed in each release: {Releases.Notes}");
        }
        return 0;
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
        // --mcp: the agent files are izzy agent's to write, which keeps what a settings file of the user's has.
        var agent = generated.Settings.GetValueOrDefault("Mcp") == "true";
        var configs = generated.Creates.Select(c => c.Path).Concat(generated.Conflicts).Where(c => c.EndsWith("mcp.json", StringComparison.Ordinal)).ToList();
        // izzy as a tool of the app, at the version of the IzzyBlazor it references (this izzy's, when init adds it).
        var library = ProjectVersion(Path.Combine(project.Directory!, project.Host)) is { } referenced && ToolManifest.Exact(referenced) ? referenced : Generator.IzzyBlazorVersion;
        var tool = ToolManifest.Change(project.ReadFile, library);
        if (tool.Warning is not null) warnings.Add(tool.Warning);
        var changes = new Changeset
        {
            Creates = [.. generated.Creates.Where(c => !configs.Contains(c.Path)), .. tool.Create is null ? Array.Empty<FileCreate>() : [tool.Create]],
            Inserts = [.. generated.Inserts, .. Setup.QualifyClashes(project), .. tool.Insert is null ? Array.Empty<TextInsert>() : [tool.Insert]],
            // A package the project has already is nothing to do.
            Packages = [.. generated.Packages.Where(p => !References(Path.Combine(project.Directory!, p.Project), p.Id))],
            Conflicts = [.. generated.Conflicts.Except(configs)],
            Warnings = warnings,
            Settings = generated.Settings,
        };
        var writes = !flags.DryRun && (changes.Conflicts.Count == 0 || flags.Overwrite)
            && (changes.Creates.Count > 0 || changes.Inserts.Count > 0 || changes.Packages.Count > 0);
        var exit = Finish(item, changes, project, project.Directory!, existing: true, flags);
        if (exit == 0 && agent) AgentFiles.Write(project.Directory!, flags.DryRun, flags.Json ? TextWriter.Null : output, error);
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
        using var process = DotnetHost.Start(project.Directory!, ["build", csproj, "-nologo", "-v", "q"], "so the app wasn't built. The changes above were written: run dotnet build yourself.");
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
        if (flags.Diff) _data.Write(ChangesetWriter.Diff(changes, project));
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
        // The app's own izzy, from its tool manifest: with a new app's packages, or when init put it there.
        if ((changes.Restore || existing) && changes.Creates.Select(c => c.Path).Concat(changes.Inserts.Select(i => i.Path)).Any(ToolManifest.Is))
            Dotnet(directory, quiet, "tool", "restore");
        Record(directory, item.Manifest.Id, changes.Settings);
        // A new app says where to go next: its README has how to run it, what was made and, where it has
        // accounts, how to log in.
        if (!quiet && !existing && changes.Creates.Any(c => c.Path == "README.md"))
        {
            var folder = Path.GetRelativePath(Environment.CurrentDirectory, directory);
            output.WriteLine();
            output.WriteLine(folder == "." ? "Next: open README.md." : $"Next: cd {folder}, then open README.md.");
            output.WriteLine("It says how to run the app, what was made and where to go from here.");
        }
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
        JsonObject root;
        try
        {
            root = File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? [] : [];
        }
        catch (JsonException)
        {
            // The files are written by now. A record that doesn't parse is the user's to mend: it is kept as it is.
            throw new UsageException(".izzy/items.json isn't valid JSON, so this item wasn't recorded in it. The files above were written.");
        }
        if (root["items"] is not JsonArray items) root["items"] = items = [];
        // A model's crud is recorded once, as its last run made it: a second run replaces the first's entry.
        if (id == "crud" && settings.TryGetValue("Model", out var model))
            foreach (var earlier in items.Where(i => (string?)i?["id"] == "crud" && (string?)i?["settings"]?["Model"] == model).ToList())
                items.Remove(earlier);
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
        var command = $"dotnet {string.Join(' ', arguments)}";
        var where = Path.GetRelativePath(workingDirectory, directory) is var shown && shown == "." ? "this folder" : shown;
        using var process = DotnetHost.Start(directory, arguments, $"so {command} didn't run. The files above were written: run it yourself in {where}.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        if (process.ExitCode != 0)
            error.WriteLine(arguments[0] == "tool"
                ? $"warning: {command} failed, so dotnet izzy isn't ready in this app yet. Run it again there:\n{stdout.Result}{stderr.Result}"
                : $"warning: {command} failed:\n{stdout.Result}{stderr.Result}");
        else if (!quiet && arguments[0] == "restore") output.WriteLine("Restored packages");
        else if (!quiet && arguments[0] == "tool") output.WriteLine("Restored izzy as a tool of the app: dotnet izzy runs it here");
    }

    // The item a command names by its kind and id (izzy describe block hero). crud and init are named alone,
    // as their own commands name them. An id with no kind is answered with the command to type.
    ItemDefinition Item(Args args, string command, string usage)
    {
        if (args.Words.Count == 2)
        {
            if (!Catalog.Items.TryGetValue(args.Words[1], out var alone)) throw new UsageException($"Usage: {usage}");
            if (alone.Manifest.Kind == ItemKind.Setup || alone.Manifest.NamedAlone()) return alone;
            if (command == "izzy add" && alone.Manifest.Kind.MakesProject())
                throw new UsageException($"{alone.Manifest.Id} makes a new project: {alone.Manifest.NewCommand()} -n <name>.");
            throw new UsageException($"Say what {alone.Manifest.Id} is: {command} {alone.Manifest.Kind.ToString().ToLowerInvariant()} {alone.Manifest.Id}.");
        }
        Words(args, 3, usage);
        var item = Catalog[args.Words[2]];
        if (item.Manifest.Kind == ItemKind.Setup || item.Manifest.NamedAlone())
            throw new UsageException($"{item.Manifest.Id} is named alone: {(command == "izzy add" ? item.Manifest.Kind == ItemKind.Setup ? "izzy init" : item.Manifest.AddCommand() : $"{command} {item.Manifest.Id}")}.");
        var kind = item.Manifest.Kind.ToString().ToLowerInvariant();
        if (!kind.Equals(args.Words[1], StringComparison.OrdinalIgnoreCase))
            throw new UsageException($"{item.Manifest.Id} is a {kind}: {command} {kind} {item.Manifest.Id}.");
        return item;
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
                ?? manifest.Parameters.FirstOrDefault(p => p.Name.Equals(option, StringComparison.OrdinalIgnoreCase))
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
        if (!ProjectInfo.Inside(Path.GetFullPath(root), full)) throw new GenerationException($"{path} is outside the project.");
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

    void Print(JsonNode node) => _data.Write(node.ToJsonString(Json).ReplaceLineEndings("\n") + "\n");

    static string Lower<T>(T value) where T : struct, Enum => value.ToString().ToLowerInvariant();
}
