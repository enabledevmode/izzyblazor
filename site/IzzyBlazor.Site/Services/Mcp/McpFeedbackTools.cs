using System.ComponentModel;
using System.Text;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace IzzyBlazor.Site.Services.Mcp;

/// <summary>
/// The tools at <c>/mcp</c> for what the docs don't cover: find_known_issues looks a problem up
/// first, and report_gap posts what an agent had to work around (with the user's agreement) as a
/// GitHub discussion in "Agent reports", to be triaged into issues. The instructions in
/// <see cref="McpTools.Instructions"/> say when to use them.
/// </summary>
[McpServerToolType]
public sealed class McpFeedbackTools(McpDocs docs, ApiMetadata api, IAgentReportSink sink, AgentReportLimiter limiter, IHttpContextAccessor http)
{
    [McpServerTool(Name = "find_known_issues", Title = "Find known IzzyBlazor issues", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Before working around something in IzzyBlazor, or reporting it: the troubleshooting articles about the problem or component, the component's mode limits, and open issues and agent reports on GitHub. Often gives the fix. Describe the symptom in a few words and name the component if there is one.")]
    public async Task<string> FindKnownIssues(
        [Description("The symptom in a few words (\"OnClick does nothing\", \"select loses value after post\").")] string problem,
        [Description("The component involved, as written in Razor (Button). Optional.")] string? component = null,
        CancellationToken cancellationToken = default)
    {
        string text;
        try
        {
            text = docs.KnownIssues(problem, component);
        }
        catch (McpDocsException e)
        {
            throw new McpException(e.Message);
        }

        var open = await sink.FindAsync($"{component} {problem}", cancellationToken);
        if (open.Count == 0)
        {
            return sink.Enabled ? text + "\nNo open issues or agent reports match.\n" : text;
        }

        var answer = new StringBuilder(text).Append("\n## Open on GitHub\n\n");
        foreach (var known in open)
        {
            answer.Append($"- [{known.Title}]({known.Url}) ({known.Kind})\n");
        }

        return answer.ToString();
    }

    [McpServerTool(Name = "report_gap", Title = "Report an IzzyBlazor gap", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = true)]
    [Description("Posts a public report to IzzyBlazor's GitHub when the library couldn't do what the task needed and you worked around it: a bug, a missing feature, unclear docs, or a workaround that shouldn't be needed. Call find_known_issues first. Only after the user agrees to report it: tell them what you worked around and that the report is public. Use sample names in the repro, never the user's code, names or data.")]
    public async Task<string> ReportGap(
        [Description("bug, missing_feature, unclear_docs or workaround.")] string kind,
        [Description("One line naming the component and the problem (\"Select: Placeholder ignored when Required on a static page\").")] string title,
        [Description("What you expected to happen.")] string expected,
        [Description("What happened instead, with any error message.")] string actual,
        [Description("True only when the user has agreed to post this report publicly.")] bool userApproved,
        [Description("The smallest .razor markup and code that shows it, with sample names. Required for bug and workaround.")] string? repro = null,
        [Description("The component involved, as written in Razor. Optional.")] string? component = null,
        [Description("How you worked around it. Optional, but it helps.")] string? workaround = null,
        [Description("What would fix it in the library: a parameter, a behavior, a docs change. Optional.")] string? suggestion = null,
        [Description("Static, InteractiveServer, InteractiveWebAssembly or InteractiveAuto. Optional.")] string? renderMode = null,
        [Description("The browser, when it matters. Optional.")] string? browser = null,
        [Description("The IzzyBlazor package version in the project. Optional.")] string? izzyBlazorVersion = null,
        CancellationToken cancellationToken = default)
    {
        if (!userApproved)
        {
            throw new McpException("Ask the user first: say what you worked around and that the report will be public on GitHub. Call again with userApproved true if they agree.");
        }

        var parsed = AgentReportRules.ParseKind(kind)
            ?? throw new McpException("kind is one of bug, missing_feature, unclear_docs or workaround.");
        var report = new GapReport(parsed, title ?? "", component, expected ?? "", actual ?? "", repro, workaround, suggestion, renderMode, browser, izzyBlazorVersion);
        var problems = AgentReportRules.Problems(report);
        if (problems.Count > 0)
        {
            throw new McpException("Not posted. " + string.Join(" ", problems));
        }

        if (!sink.Enabled)
        {
            throw new McpException("Reports aren't switched on for this server yet. Tell the user what you worked around; they can open an issue at https://github.com/enabledevmode/izzyblazor/issues.");
        }

        if (!limiter.TryTake(http.HttpContext is { } context ? SiteApp.ClientKey(context) : ""))
        {
            throw new McpException("Not posted: this client has sent its reports for the hour. Tell the user what you worked around instead.");
        }

        try
        {
            var related = await sink.FindAsync($"{component} {title}", cancellationToken);
            var body = AgentReportRules.Body(report, api.Version, related);
            var receipt = await sink.SubmitAsync($"[{AgentReportRules.KindName(parsed)}] {report.Title.Trim()}", body, cancellationToken);
            return $"Posted for triage: {receipt.Url}\nTell the user the link. Keep your workaround in place until a release fixes it."
                + (related.Count > 0 ? $"\nPossibly related, linked in the report: {string.Join(", ", related.Select(r => r.Url))}" : "");
        }
        catch (AgentReportException e)
        {
            throw new McpException("Not posted. " + e.Message);
        }
    }
}
