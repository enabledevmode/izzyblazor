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
        var body = $"{picture}<p class=\"quiet\" style=\"color:#aaa\">Prices exclude tax</p><div class=\"sample\"><span style=\"color:#bbb\">Unavailable</span></div>";
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
        Assert.Contains("3 findings: 1 real, 1 accepted, 1 false positive.", first.Output);
        var verdicts = JsonNode.Parse(File.ReadAllText(Path.Combine(_work, "first", "results.json")))!["pages"]![0]!["violations"]!.AsArray()
            .SelectMany(rule => rule!["nodes"]!.AsArray().Select(node => $"{(string)rule["id"]!}: {(string)node!["verdict"]!["is"]!}")).Order().ToList();
        Assert.Equal(["color-contrast: accepted", "color-contrast: false positive", "image-alt: real"], verdicts);
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
