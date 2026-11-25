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
    public void With_the_pack_each_finding_has_a_verdict_and_a_later_run_is_compared_with_the_first()
    {
        // Pro: the pack is in the private catalog, and the app has its own decisions.
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
            // The same sample drawn in an iframe: the element is in the frame, what it is inside is around it.
            + "<div class=\"sample\"><iframe title=\"Preview\" srcdoc=\"<body style='background:#fff'><span style='color:#bbb'>Unavailable</span></body>\"></iframe></div>";
        using var site = new Site(() => $"<!doctype html><html lang=\"en\"><head><title>Orders</title></head><body style=\"background:#fff\"><main><h1>Orders</h1>{body}</main></body></html>");

        (int Exit, string Output, string Error) Audit(params string[] options)
        {
            var (output, error) = (new StringWriter(), new StringWriter());
            var exit = new Cli.Cli(Path.Combine(ProjectBuild.RepoRoot, "catalog"), _work, output, error, proRoot: pro)
                .Run(["audit", site.Url, "--sizes", "1440", "--schemes", "light", "--format", "md,json,tasks", .. options]);
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

        // The picture gets its text and a button arrives with no name: one fixed, one new.
        body = body.Replace(picture, "<img alt=\"Orders by month\" src=\"data:image/gif;base64,R0lGODlhAQABAAAAACw=\"><button></button>");
        var second = Audit("--out", "second", "--baseline", "first/results.json", "--fail-on", "new");
        Assert.True(second.Exit == 5, second.Error);
        Assert.Contains("Against the baseline: 1 new, 1 fixed.", second.Output);
        var tasks = File.ReadAllText(Path.Combine(_work, "second", "tasks.md"));
        Assert.Contains("- [ ] **Buttons must have discernible text** (`button-name`", tasks);
        Assert.Contains("- [x] **Images must have alternative text** (`image-alt`", tasks);
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
        var pages = JsonNode.Parse(File.ReadAllText(Path.Combine(_work, "site", "results.json")))!["pages"]!.AsArray().Select(p => new Uri((string)p!["url"]!).AbsolutePath).ToList();
        Assert.Equal(["/", "/orders"], pages);
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
