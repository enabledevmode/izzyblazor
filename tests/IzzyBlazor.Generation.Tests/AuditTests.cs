using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using IzzyBlazor.Cli;
using IzzyBlazor.Generation;

namespace IzzyBlazor.Generation.Tests;

/// <summary>A test that drives a browser: skipped on a machine with no Chrome, Edge or Chromium.</summary>
public sealed class BrowserFactAttribute : FactAttribute
{
    public BrowserFactAttribute()
    {
        if (Chromium.Find(null) is null) Skip = "No Chrome, Edge or Chromium on this machine.";
    }
}

/// <summary>izzy audit: a page with a failure is reported with its rule and element, as a report and as data.</summary>
// With the tests that build apps, one at a time: a browser started while four builds run can take a minute.
[Collection(ProjectBuild.Collection)]
public sealed class AuditTests : IDisposable
{
    readonly string _work = Directory.CreateTempSubdirectory("izzy-audit-").FullName;

    [BrowserFact]
    public void A_page_is_audited_and_its_finding_is_in_the_report_and_the_data()
    {
        // A page with a picture that has no text alternative, and nothing else wrong.
        const string html = "<!doctype html><html lang=\"en\"><head><title>Orders</title></head><body><main><h1>Orders</h1><img src=\"data:image/gif;base64,R0lGODlhAQABAAAAACw=\"></main></body></html>";
        using var site = new Site(() => html);

        var output = new StringWriter();
        var error = new StringWriter();
        var exit = new Cli.Cli(Path.Combine(ProjectBuild.RepoRoot, "catalog"), _work, output, error, access: ItemTier.Free)
            .Run(["audit", site.Url, "--sizes", "1440", "--schemes", "light", "--out", "audit", "--fail-on", "any"]);

        Assert.True(exit == 5, error.ToString());
        Assert.Contains("1 finding in 1 rule", output.ToString());
        var results = JsonNode.Parse(File.ReadAllText(Path.Combine(_work, "audit", "results.json")))!;
        var rule = Assert.Single(results["pages"]![0]!["violations"]!.AsArray());
        Assert.Equal("image-alt", (string)rule!["id"]!);
        Assert.StartsWith("<img", (string)rule["nodes"]![0]!["html"]!);
        var report = File.ReadAllText(Path.Combine(_work, "audit", "report.md"));
        Assert.Contains("(`image-alt`)", report);
        Assert.Contains("WCAG 1.1.1 (level A)", report);
    }

    [BrowserFact]
    public void With_an_account_each_finding_of_a_page_has_a_verdict_and_a_later_run_is_compared_with_the_first()
    {
        // A free account's copy of the catalog has the pack, and the app has its own decisions.
        var pro = Path.Combine(_work, "pro");
        Directory.CreateDirectory(Path.Combine(pro, ".audit"));
        File.WriteAllText(Path.Combine(pro, ".audit", "pack.json"), """
            { "version": "test", "entries": [
              { "id": "sample-part", "rule": "color-contrast", "closest": ".sample", "verdict": "false positive", "why": "A sample of a disabled look." } ] }
            """);
        Directory.CreateDirectory(Path.Combine(_work, ".izzy"));
        File.WriteAllText(Path.Combine(_work, ".izzy", "audit.json"), """
            { "accepted": [ { "rule": "color-contrast", "selector": ".quiet", "reason": "The footnote is quiet on purpose.", "by": "Ada", "on": "2026-11-24" } ] }
            """);
        const string picture = "<img src=\"data:image/gif;base64,R0lGODlhAQABAAAAACw=\">";
        var body = $"{picture}<p class=\"quiet\" style=\"color:#aaa\">Prices exclude tax</p><div class=\"sample\"><span style=\"color:#bbb\">Unavailable</span></div>"
            // Text over a gradient: axe can't measure it, and a one-page run names the element.
            + "<p class=\"total\" style=\"background:linear-gradient(#fff,#eee)\">Total</p>"
            // The same sample drawn in an iframe: the element is in the frame, what it is inside is around it.
            + "<div class=\"sample\"><iframe title=\"Preview\" srcdoc=\"<body style='background:#fff'><span style='color:#bbb'>Unavailable</span></body>\"></iframe></div>";
        using var site = new Site(() => $"<!doctype html><html lang=\"en\"><head><title>Orders</title></head><body style=\"background:#fff\"><main><h1>Orders</h1>{body}</main></body></html>");

        (int Exit, string Output, string Error) Audit(params string[] options)
        {
            var (output, error) = (new StringWriter(), new StringWriter());
            var exit = new Cli.Cli(Path.Combine(ProjectBuild.RepoRoot, "catalog"), _work, output, error, proRoot: pro, access: ItemTier.Account)
                .Run(["audit", .. options.Contains("--recheck") ? Array.Empty<string>() : [site.Url, "--sizes", "1440", "--schemes", "light"], "--format", "md,json,tasks,html", .. options]);
            return (exit, output.ToString(), error.ToString());
        }

        var first = Audit("--out", "first", "--fail-on", "real");
        Assert.True(first.Exit == 5, first.Error);
        Assert.Contains("4 findings: 1 real, 1 accepted, 2 false positive.", first.Output);
        var verdicts = JsonNode.Parse(File.ReadAllText(Path.Combine(_work, "first", "results.json")))!["pages"]![0]!["violations"]!.AsArray()
            .SelectMany(rule => rule!["nodes"]!.AsArray().Select(node => $"{(string)rule["id"]!}: {(string)node!["verdict"]!["is"]!}")).Order().ToList();
        Assert.Equal(["color-contrast: accepted", "color-contrast: false positive", "color-contrast: false positive", "image-alt: real"], verdicts);
        Assert.Contains("The footnote is quiet on purpose (Ada, 2026-11-24)", File.ReadAllText(Path.Combine(_work, "first", "report.md")));
        Assert.Contains("- [ ] **Images must have alternative text** (`image-alt`", File.ReadAllText(Path.Combine(_work, "first", "tasks.md")));

        var undecided = JsonNode.Parse(File.ReadAllText(Path.Combine(_work, "first", "results.json")))!["pages"]![0]!["incomplete"]!.AsArray().First(rule => (string)rule!["id"]! == "color-contrast")!["nodes"]![0]!;
        Assert.Equal(".total", (string)undecided["target"]!);
        Assert.Equal(12, ((string)undecided["id"]!).Length);

        // report.html is one file: each of the four elements has its picture in it, the one in the iframe too.
        var html = File.ReadAllText(Path.Combine(_work, "first", "report.html"));
        Assert.Equal(4, html.Split("<img src=\"data:image/webp;base64,").Length - 1);
        Assert.Contains("&lt;span style=&quot;color:#bbb&quot;&gt;Unavailable&lt;/span&gt;", html);
        // The data names each picture's file, which is beside it.
        var pictured = JsonNode.Parse(File.ReadAllText(Path.Combine(_work, "first", "results.json")))!["pages"]![0]!["violations"]![0]!["nodes"]![0]!["picture"]!;
        Assert.True(File.Exists(Path.Combine(_work, "first", (string)pictured["file"]!)));

        // Looked for again by its id, the picture's finding is still there, and gone once the picture has its text (below).
        var picked = (string)JsonNode.Parse(File.ReadAllText(Path.Combine(_work, "first", "results.json")))!["pages"]![0]!["violations"]!.AsArray().First(rule => (string)rule!["id"]! == "image-alt")!["nodes"]![0]!["id"]!;
        Assert.Contains($"- Id: `{picked}`", File.ReadAllText(Path.Combine(_work, "first", "report.md")));
        // An agent's tools do the same through izzy mcp: the page, then the finding, found in the newest run in .izzy/audit.
        var tools = new Cli.Cli(Path.Combine(ProjectBuild.RepoRoot, "catalog"), _work, new StringWriter(), new StringWriter(), proRoot: pro, access: ItemTier.Account).BuildTools();
        Assert.Contains("/report.md", tools.AuditPage(site.Url));
        Assert.Contains("Still there: `image-alt` on `/`", tools.RecheckFinding(picked));

        // A finding the app accepted is still on the page: the recheck says so, never "Fixed", and doesn't fail.
        var accepted = (string)JsonNode.Parse(File.ReadAllText(Path.Combine(_work, "first", "results.json")))!["pages"]![0]!["violations"]!.AsArray()
            .SelectMany(rule => rule!["nodes"]!.AsArray()).First(node => (string)node!["verdict"]!["is"]! == "accepted")!["id"]!;
        var kept = Audit("--recheck", accepted, "--baseline", "first/results.json", "--out", "kept");
        Assert.True(kept.Exit == 0, kept.Error);
        Assert.Contains("Still there, accepted: `color-contrast` on `/`", kept.Output);
        Assert.DoesNotContain("Fixed", kept.Output);
        // For an editor: --json with --out prints the results, writes the files and names them, report.html with its pictures.
        var answer = JsonNode.Parse(Audit("--recheck", accepted, "--baseline", "first/results.json", "--out", "kept", "--json").Output)!;
        Assert.Equal("closed", (string)answer["recheck"]!["state"]!);
        Assert.Contains("report.html", answer["written"]!["files"]!.AsArray().Select(f => (string)f!));
        Assert.True(File.Exists(Path.Combine((string)answer["written"]!["folder"]!, "report.md")));
        Assert.Contains("<img src=\"data:image/webp;base64,", File.ReadAllText(Path.Combine((string)answer["written"]!["folder"]!, "report.html")));

        // The picture gets its text and a button arrives with no name: one fixed, one new.
        body = body.Replace(picture, "<img alt=\"Orders by month\" src=\"data:image/gif;base64,R0lGODlhAQABAAAAACw=\"><button></button>");
        var second = Audit("--out", "second", "--baseline", "first/results.json", "--fail-on", "new");
        Assert.True(second.Exit == 5, second.Error);
        Assert.Contains("Against the baseline: 1 new, 1 fixed.", second.Output);
        var tasks = File.ReadAllText(Path.Combine(_work, "second", "tasks.md"));
        Assert.Contains("- [ ] **Buttons must have discernible text** (`button-name`", tasks);
        Assert.Contains("- [x] **Images must have alternative text** (`image-alt`", tasks);
        var gone = Audit("--recheck", picked, "--baseline", "first/results.json", "--out", "after");
        Assert.True(gone.Exit == 0, gone.Error);
        Assert.Contains("Fixed: `image-alt` on `/`", gone.Output);
    }

    [BrowserFact]
    public void A_finding_is_traced_to_its_component_the_fix_and_the_line_in_the_source()
    {
        var pro = Path.Combine(_work, "pro");
        Directory.CreateDirectory(Path.Combine(pro, ".audit"));
        File.WriteAllText(Path.Combine(pro, ".audit", "pack.json"), """
            { "version": "test", "entries": [], "fixes": [
              { "id": "icon-button-label", "rule": "button-name", "component": "IconButton", "set": "Label", "fix": "Set Label, the button's name", "docs": "#accessibility" } ] }
            """);
        // The app: a page with two icon buttons, one of them without its name, and a grid whose two pagers share a name.
        var app = Path.Combine(_work, "Shop");
        Directory.CreateDirectory(Path.Combine(app, "Components", "Pages"));
        File.WriteAllText(Path.Combine(app, "Shop.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>");
        File.WriteAllText(Path.Combine(app, "Components", "Pages", "Orders.razor"), """
            @page "/orders/{id:int}"

            <h1>Order</h1>
            <IconButton Icon="Lucide.Pencil" Label="Edit" id="edit" />
            <IconButton Icon="Lucide.Trash" id="remove" />
            <DataGrid Items="_lines" Caption="Lines" />
            <Pagination TotalItems="40" Label="Orders" />
            """);
        const string body = "<button class=\"ib-btn ib-btn-neutral ib-btn-icon-only\" id=\"edit\" aria-label=\"Edit\"></button><button class=\"ib-btn ib-btn-neutral ib-btn-icon-only\" id=\"remove\"></button>"
            + "<ib-table class=\"ib-table ib-datagrid\"><nav class=\"ib-pagination\" aria-label=\"Lines pages\"></nav><nav class=\"ib-pagination\" aria-label=\"Lines pages\"></nav></ib-table>";
        using var site = new Site(() => $"<!doctype html><html lang=\"en\"><head><title>Order</title></head><body><main><h1>Order</h1>{body}</main></body></html>");

        var (output, error) = (new StringWriter(), new StringWriter());
        var exit = new Cli.Cli(Path.Combine(ProjectBuild.RepoRoot, "catalog"), _work, output, error, proRoot: pro)
            .Run(["audit", site.Url + "orders/7", "--project", "Shop", "--sizes", "1440", "--schemes", "light", "--format", "md,json,sarif", "--out", "audit"]);

        Assert.True(exit == 0, error.ToString());
        var violations = JsonNode.Parse(File.ReadAllText(Path.Combine(_work, "audit", "results.json")))!["pages"]![0]!["violations"]!.AsArray();
        var node = violations.First(v => (string)v!["id"]! == "button-name")!["nodes"]![0]!;
        Assert.Equal("IconButton", (string)node["trace"]!["component"]!);
        Assert.Equal("Label", (string)node["trace"]!["fix"]!["option"]!);
        Assert.Equal("https://izzyblazor.com/docs/components/icon-button#accessibility", (string)node["trace"]!["docs"]!);
        // Two IconButtons are written: the one whose id is the element's.
        Assert.Equal("Components/Pages/Orders.razor:5", $"{(string)node["trace"]!["source"]!["file"]!}:{(int)node["trace"]!["source"]!["line"]!}");
        Assert.Contains("- Source: `Components/Pages/Orders.razor:5`", File.ReadAllText(Path.Combine(_work, "audit", "report.md")));
        // A pager the grid drew is the grid's, whatever Pagination the page has of its own.
        var pager = violations.First(v => (string)v!["id"]! == "landmark-unique")!["nodes"]![0]!["trace"]!;
        Assert.Equal("DataGrid", (string)pager["component"]!);
        Assert.Equal(6, (int)pager["source"]!["line"]!);
        var located = JsonNode.Parse(File.ReadAllText(Path.Combine(_work, "audit", "results.sarif")))!["runs"]![0]!["results"]![0]!["locations"]![0]!["physicalLocation"]!;
        Assert.Equal("Components/Pages/Orders.razor", (string)located["artifactLocation"]!["uri"]!);
        Assert.Equal(5, (int)located["region"]!["startLine"]!);
    }

    [BrowserFact]
    public void A_site_without_a_sitemap_is_audited_by_following_its_links()
    {
        // Every address answers with this page, so the site is its root and the page it links to.
        const string html = "<!doctype html><html lang=\"en\"><head><title>Shop</title><base href=\"/\"></head><body><main><h1>Shop</h1><a href=\"orders\">Orders</a> <a href=\"https://example.com/\">Elsewhere</a></main></body></html>";
        using var site = new Site(() => html);
        var pro = Path.Combine(_work, "pro");
        Directory.CreateDirectory(Path.Combine(pro, ".audit"));
        File.WriteAllText(Path.Combine(pro, ".audit", "pack.json"), """{ "version": "test", "entries": [] }""");

        var (output, error) = (new StringWriter(), new StringWriter());
        var exit = new Cli.Cli(Path.Combine(ProjectBuild.RepoRoot, "catalog"), _work, output, error, proRoot: pro)
            .Run(["audit", "--site", site.Url, "--sizes", "1440", "--schemes", "light", "--out", "site"]);

        Assert.True(exit == 0, error.ToString());
        Assert.Contains("2 pages found by following links", error.ToString());
        var pages = JsonNode.Parse(File.ReadAllText(Path.Combine(_work, "site", "results.json")))!["pages"]!.AsArray().Select(p => new Uri((string)p!["url"]!).AbsolutePath).Distinct().ToList();
        Assert.Equal(["/", "/orders"], pages);
    }

    [BrowserFact]
    public void The_sweeps_find_what_axe_does_not_and_pass_what_is_right()
    {
        var pro = Path.Combine(_work, "pro");
        Directory.CreateDirectory(Path.Combine(pro, ".audit"));
        File.WriteAllText(Path.Combine(pro, ".audit", "pack.json"), """{ "version": "test", "entries": [] }""");
        // Each fault beside the same thing done right: a header that covers what takes focus; a button whose
        // focus is a shadow, which forced colors erase, and one with a ring; a field whose border only changes
        // color; a meter's value drawn by a fill, and one with a border; two small buttons side by side.
        const string style = """
            body { margin: 0; background: #fff; font: 16px sans-serif; }
            header { position: sticky; top: 0; height: 80px; background: #eee; }
            main { padding: 24px; }
            button, input { font: inherit; }
            .big { min-width: 120px; min-height: 40px; border: 1px solid #333; background: #fff; }
            #shadow { outline: none; }
            #shadow:focus-visible { box-shadow: 0 0 0 3px #06c; }
            #ring:focus-visible { outline: 2px solid #06c; outline-offset: 2px; }
            #shade { width: 240px; height: 40px; border: 1px solid #555; outline: none; }
            #shade:focus-visible { border-color: #06c; }
            .meter { width: 240px; height: 12px; border: 1px solid #333; margin: 16px 0; }
            .meter > div { width: 40%; height: 100%; background: #06c; }
            #edged > div { box-sizing: border-box; border: 1px solid transparent; }
            .tiny { width: 16px; height: 16px; padding: 0; border: 1px solid #333; background: #fff; }
            p { margin: 0 0 400px; }
            """;
        const string body = """
            <header><strong>Shop</strong></header>
            <main><h1>Orders</h1>
            <button class="big" id="shadow">Save</button> <button class="big" id="ring">Cancel</button>
            <label>Name <input id="shade"></label>
            <div class="meter" id="filled" role="progressbar" aria-label="Packed" aria-valuenow="40"><div></div></div>
            <div class="meter" id="edged" role="progressbar" aria-label="Sent" aria-valuenow="40"><div></div></div>
            <button class="tiny" aria-label="Up"></button><button class="tiny" aria-label="Down"></button>
            <p>First</p><p><a href="/one" id="one">One</a></p><p><a href="/two" id="two">Two</a></p><p><a href="/three" id="three">Three</a></p><p>Last</p>
            </main>
            """;
        var padded = false;
        using var site = new Site(() => $"<!doctype html><html lang=\"en\"{(padded ? " style=\"scroll-padding-top: 96px\"" : "")}><head><title>Orders</title><style>{style}</style></head><body>{body}</body></html>");

        List<string> Audit(string folder)
        {
            var (output, error) = (new StringWriter(), new StringWriter());
            var exit = new Cli.Cli(Path.Combine(ProjectBuild.RepoRoot, "catalog"), _work, output, error, proRoot: pro)
                .Run(["audit", site.Url, "--sizes", "1440", "--schemes", "light", "--out", folder]);
            Assert.True(exit == 0, error.ToString());
            return [.. JsonNode.Parse(File.ReadAllText(Path.Combine(_work, folder, "results.json")))!["pages"]!.AsArray().SelectMany(page => page!["violations"]!.AsArray()
                .SelectMany(rule => rule!["nodes"]!.AsArray().Select(node => $"{(string)page["scheme"]!}: {(string)rule["id"]!} {(string)node!["target"]!}: {(string)node["verdict"]!["is"]!}"))).Order()];
        }

        Assert.Equal(
        [
            "forced colors: focus-visible-forced-colors #shade: needs a person",
            "forced colors: focus-visible-forced-colors #shadow: real",
            "forced colors: forced-colors-indicator #filled > div: needs a person",
            "light: focus-not-obscured header: real",
            "light: target-size button[aria-label=\"Down\"]: real",
            "light: target-size button[aria-label=\"Up\"]: real",
        ], Audit("first"));
        Assert.Contains("stays hidden under this bar when it takes focus", File.ReadAllText(Path.Combine(_work, "first", "report.md")));

        // The page reserves the header's room: what takes focus is scrolled out from under it.
        padded = true;
        Assert.DoesNotContain(Audit("second"), finding => finding.Contains("focus-not-obscured"));
    }

    /// <summary>A page served on this machine: whatever <paramref name="html"/> gives at the time it is asked for.</summary>
    sealed class Site : IDisposable
    {
        readonly HttpListener _server = new();
        readonly Task _serving;

        public string Url { get; }

        public Site(Func<string> html)
        {
            int port;
            using (var probe = new TcpListener(IPAddress.Loopback, 0))
            {
                probe.Start();
                port = ((IPEndPoint)probe.LocalEndpoint).Port;
            }
            Url = $"http://127.0.0.1:{port}/";
            _server.Prefixes.Add(Url);
            _server.Start();
            _serving = Task.Run(async () =>
            {
                while (_server.IsListening)
                {
                    HttpListenerContext context;
                    try { context = await _server.GetContextAsync(); }
                    catch (Exception e) when (e is HttpListenerException or ObjectDisposedException) { return; }
                    context.Response.ContentType = "text/html; charset=utf-8";
                    await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(html()));
                    context.Response.Close();
                }
            });
        }

        public void Dispose()
        {
            _server.Stop();
            _serving.Wait(TimeSpan.FromSeconds(5));
            _server.Close();
        }
    }

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
