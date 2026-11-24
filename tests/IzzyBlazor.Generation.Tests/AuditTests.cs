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
    public async Task A_page_is_audited_and_its_finding_is_in_the_report_and_the_data()
    {
        // A page with a picture that has no text alternative, and nothing else wrong.
        const string html = "<!doctype html><html lang=\"en\"><head><title>Orders</title></head><body><main><h1>Orders</h1><img src=\"data:image/gif;base64,R0lGODlhAQABAAAAACw=\"></main></body></html>";
        using var server = new HttpListener();
        int port;
        using (var probe = new TcpListener(IPAddress.Loopback, 0))
        {
            probe.Start();
            port = ((IPEndPoint)probe.LocalEndpoint).Port;
        }
        server.Prefixes.Add($"http://127.0.0.1:{port}/");
        server.Start();
        var serving = Task.Run(async () =>
        {
            while (server.IsListening)
            {
                HttpListenerContext context;
                try { context = await server.GetContextAsync(); }
                catch (Exception e) when (e is HttpListenerException or ObjectDisposedException) { return; }
                var body = Encoding.UTF8.GetBytes(html);
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.OutputStream.WriteAsync(body);
                context.Response.Close();
            }
        });

        var output = new StringWriter();
        var error = new StringWriter();
        var exit = new Cli.Cli(Path.Combine(ProjectBuild.RepoRoot, "catalog"), _work, output, error, access: ItemTier.Free)
            .Run(["audit", $"http://127.0.0.1:{port}/", "--sizes", "1440", "--schemes", "light", "--out", "audit", "--fail-on", "any"]);
        server.Stop();
        await serving;

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

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
