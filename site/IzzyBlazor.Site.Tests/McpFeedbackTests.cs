using IzzyBlazor.Site.Services.Mcp;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace IzzyBlazor.Site.Tests;

// find_known_issues and report_gap: known problems come from the docs, and nothing is posted
// without the user's agreement, a repro, or with what looks like a secret. The test site has no
// token, so reports are switched off and say so.
public class McpFeedbackTests(SiteFixture site) : IClassFixture<SiteFixture>
{
    private async Task<McpClient> Connect() => await McpClient.CreateAsync(new HttpClientTransport(
        new HttpClientTransportOptions { Endpoint = new Uri(site.Client.BaseAddress!, "mcp") }));

    private static string TextOf(CallToolResult result) => string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text));

    private static Dictionary<string, object?> Report(bool approved = true, string? repro = "<Button OnClick=\"Save\">Save</Button>\n@code { void Save() { } }") => new()
    {
        ["kind"] = "bug",
        ["title"] = "Button: OnClick never runs on a static page",
        ["expected"] = "Save runs when the button is clicked.",
        ["actual"] = "Nothing happens.",
        ["repro"] = repro,
        ["userApproved"] = approved,
    };

    [Fact]
    public async Task Known_issues_come_from_the_troubleshooting_articles()
    {
        await using var client = await Connect();
        var result = await client.CallToolAsync("find_known_issues", new Dictionary<string, object?> { ["problem"] = "OnClick does nothing", ["component"] = "Button" });
        Assert.NotEqual(true, result.IsError);
        Assert.Contains("docs/troubleshooting/onclick", TextOf(result));

        var empty = await client.CallToolAsync("find_known_issues", new Dictionary<string, object?> { ["problem"] = "" });
        Assert.True(empty.IsError);
    }

    [Fact]
    public async Task A_report_needs_the_users_agreement_first()
    {
        await using var client = await Connect();
        var result = await client.CallToolAsync("report_gap", Report(approved: false));
        Assert.True(result.IsError);
        Assert.Contains("Ask the user first", TextOf(result));
    }

    [Fact]
    public async Task A_bug_needs_a_repro_and_no_secrets()
    {
        await using var client = await Connect();
        var noRepro = await client.CallToolAsync("report_gap", Report(repro: null));
        Assert.True(noRepro.IsError);
        Assert.Contains("minimal .razor repro", TextOf(noRepro));

        var secret = await client.CallToolAsync("report_gap", Report(repro: "<Button>Save</Button>\n@code { string apiKey = \"sk_live_1234567890abcdef\"; }"));
        Assert.True(secret.IsError);
        Assert.Contains("password or secret", TextOf(secret));
    }

    [Fact]
    public async Task Without_a_token_reports_are_switched_off()
    {
        await using var client = await Connect();
        var result = await client.CallToolAsync("report_gap", Report());
        Assert.True(result.IsError);
        Assert.Contains("aren't switched on", TextOf(result));
    }

    [Theory]
    [InlineData("-----BEGIN RSA PRIVATE KEY-----", true)]
    [InlineData("var key = \"AKIAABCDEFGHIJKLMNOP\";", true)]
    [InlineData("ghp_abcdefghijklmnopqrstuvwxyz0123456789", true)]
    [InlineData("Server=db;Database=x;Password=hunter2", true)]
    [InlineData("contact jane.doe@contoso.com", true)]
    [InlineData("<TextField Label=\"Email\" Value=\"person@example.com\" />", false)]
    [InlineData("<PasswordField Label=\"Password\" @bind-Value=\"model.Password\" />", false)]
    [InlineData("<LoginForm Password=\"@model.Password\" />", false)]
    [InlineData("@code { string password = model.Password; }", false)]
    [InlineData("secret=s3cr3t!value", true)]
    public void Secrets_and_real_addresses_are_turned_away(string text, bool rejected)
    {
        if (rejected)
        {
            Assert.NotNull(AgentReportRules.LooksPrivate(text));
        }
        else
        {
            Assert.Null(AgentReportRules.LooksPrivate(text));
        }
    }

    [Fact]
    public void The_body_fences_a_repro_that_holds_backticks()
    {
        var report = new GapReport(GapKind.Workaround, "Code: a block inside a block", "Code", "One block.", "Two blocks.", "<Code>```razor</Code>", "Wrapped it.", null, "Static", null, null);
        var body = AgentReportRules.Body(report, "0.1.0", []);
        Assert.Contains("````razor\n<Code>```razor</Code>\n````", body);
        Assert.Contains("the docs' version", body);
    }

    [Fact]
    public void A_client_gets_a_few_reports_an_hour()
    {
        var clock = new ManualClock();
        var limiter = new AgentReportLimiter(Options.Create(new AgentReportOptions { PerClientPerHour = 2 }), clock);
        Assert.True(limiter.TryTake("a"));
        Assert.True(limiter.TryTake("a"));
        Assert.False(limiter.TryTake("a"));
        Assert.True(limiter.TryTake("b"));
        clock.Now += TimeSpan.FromMinutes(61);
        Assert.True(limiter.TryTake("a"));
    }

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
