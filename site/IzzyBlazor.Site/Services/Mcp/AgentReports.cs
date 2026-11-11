using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace IzzyBlazor.Site.Services.Mcp;

/// <summary>What an agent reports: a gap it found in IzzyBlazor and worked around.</summary>
public enum GapKind
{
    /// <summary>The library does something wrong.</summary>
    Bug,

    /// <summary>The task needed something the library doesn't have.</summary>
    MissingFeature,

    /// <summary>The docs were wrong, missing or misleading.</summary>
    UnclearDocs,

    /// <summary>It works, but only with a workaround that shouldn't be needed.</summary>
    Workaround,
}

/// <summary>
/// One report from <c>report_gap</c>. Agents fill it from what they hit while building; nothing in
/// it comes from the user's own code or data (the tool's description asks for sample names only,
/// and <see cref="AgentReportRules"/> turns away what looks like secrets or personal addresses).
/// </summary>
public sealed record GapReport(
    GapKind Kind,
    string Title,
    string? Component,
    string Expected,
    string Actual,
    string? Repro,
    string? Workaround,
    string? Suggestion,
    string? RenderMode,
    string? Browser,
    string? LibraryVersion);

/// <summary>An open report or issue that may already cover a problem.</summary>
public sealed record KnownReport(string Title, string Url, string Kind);

/// <summary>Where a report went.</summary>
public sealed record ReportReceipt(string Url);

/// <summary>
/// Where reports go and where open ones are found. The site posts them as GitHub Discussions in the
/// "Agent reports" category (<see cref="GitHubDiscussionsReportSink"/>), where they're triaged and
/// promoted to issues; without a token it's <see cref="DisabledReportSink"/>.
/// </summary>
public interface IAgentReportSink
{
    /// <summary>Whether reports can be posted.</summary>
    bool Enabled { get; }

    /// <summary>Open issues and agent reports matching a few words; empty when disabled.</summary>
    Task<IReadOnlyList<KnownReport>> FindAsync(string query, CancellationToken cancellationToken);

    /// <summary>Posts a report whose body is already written; throws <see cref="AgentReportException"/> when it can't.</summary>
    Task<ReportReceipt> SubmitAsync(string title, string body, CancellationToken cancellationToken);
}

/// <summary>A report that couldn't be taken, with the reason the agent is told.</summary>
public sealed class AgentReportException(string message) : Exception(message);

/// <summary>Settings for agent reports, from the <c>AgentReports</c> section of configuration.</summary>
public sealed class AgentReportOptions
{
    /// <summary>
    /// A token that can read issues and create discussions in the repository (a GitHub App
    /// installation token or a fine-grained token). No token, no posting: reports are switched off.
    /// </summary>
    public string? Token { get; set; }

    public string Owner { get; set; } = "enabledevmode";

    public string Repository { get; set; } = "izzyblazor";

    /// <summary>The Discussions category reports go to. It must exist in the repository.</summary>
    public string Category { get; set; } = "Agent reports";

    /// <summary>Reports one client may post per hour.</summary>
    public int PerClientPerHour { get; set; } = 5;
}

/// <summary>Reports switched off: nothing is found or posted.</summary>
public sealed class DisabledReportSink : IAgentReportSink
{
    public bool Enabled => false;

    public Task<IReadOnlyList<KnownReport>> FindAsync(string query, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<KnownReport>>([]);

    public Task<ReportReceipt> SubmitAsync(string title, string body, CancellationToken cancellationToken) =>
        throw new AgentReportException("Reports aren't switched on for this server yet. Tell the user what you worked around; they can open an issue at https://github.com/enabledevmode/izzyblazor/issues.");
}

/// <summary>
/// Posts reports as GitHub Discussions and searches open issues and discussions, through GitHub's
/// GraphQL API with the configured token.
/// </summary>
public sealed class GitHubDiscussionsReportSink(IHttpClientFactory clients, IOptions<AgentReportOptions> options, ILogger<GitHubDiscussionsReportSink> log) : IAgentReportSink
{
    public const string ClientName = "github";

    private readonly AgentReportOptions _options = options.Value;

    // The repository's id and the category's, looked up once.
    private (string Repository, string Category)? _ids;

    public bool Enabled => true;

    public async Task<IReadOnlyList<KnownReport>> FindAsync(string query, CancellationToken cancellationToken)
    {
        var words = string.Join(' ', SearchIndex.Terms(query).Take(8));
        if (words.Length == 0)
        {
            return [];
        }

        var scope = $"repo:{_options.Owner}/{_options.Repository} is:open {words}";
        const string graphQl = """
            query($issues: String!, $discussions: String!) {
              issues: search(query: $issues, type: ISSUE, first: 5) { nodes { ... on Issue { title url } } }
              discussions: search(query: $discussions, type: DISCUSSION, first: 5) { nodes { ... on Discussion { title url category { name } } } }
            }
            """;
        try
        {
            var data = await QueryAsync(graphQl, new { issues = scope + " is:issue", discussions = scope }, cancellationToken);
            var found = new List<KnownReport>();
            foreach (var node in data["issues"]?["nodes"]?.AsArray() ?? new JsonArray())
            {
                if (node?["url"]?.GetValue<string>() is { } url)
                {
                    found.Add(new(node?["title"]?.GetValue<string>() ?? url, url, "issue"));
                }
            }

            foreach (var node in data["discussions"]?["nodes"]?.AsArray() ?? new JsonArray())
            {
                if (node?["url"]?.GetValue<string>() is { } url)
                {
                    found.Add(new(node?["title"]?.GetValue<string>() ?? url, url, node?["category"]?["name"]?.GetValue<string>() ?? "discussion"));
                }
            }

            return found;
        }
        catch (Exception e) when (e is HttpRequestException or AgentReportException or JsonException)
        {
            // A failed search shouldn't stop the docs' own answer.
            log.LogWarning(e, "Searching GitHub for known issues failed.");
            return [];
        }
    }

    public async Task<ReportReceipt> SubmitAsync(string title, string body, CancellationToken cancellationToken)
    {
        var (repository, category) = _ids ??= await LookUpIdsAsync(cancellationToken);
        const string graphQl = """
            mutation($repository: ID!, $category: ID!, $title: String!, $body: String!) {
              createDiscussion(input: { repositoryId: $repository, categoryId: $category, title: $title, body: $body }) { discussion { url } }
            }
            """;
        var data = await QueryAsync(graphQl, new { repository, category, title, body }, cancellationToken);
        var url = data["createDiscussion"]?["discussion"]?["url"]?.GetValue<string>()
            ?? throw new AgentReportException("GitHub didn't return the new discussion. Try again later.");
        log.LogInformation("Agent report posted: {Url}", url);
        return new(url);
    }

    private async Task<(string, string)> LookUpIdsAsync(CancellationToken cancellationToken)
    {
        const string graphQl = """
            query($owner: String!, $name: String!) {
              repository(owner: $owner, name: $name) { id discussionCategories(first: 50) { nodes { id name } } }
            }
            """;
        var data = await QueryAsync(graphQl, new { owner = _options.Owner, name = _options.Repository }, cancellationToken);
        var repository = data["repository"]?["id"]?.GetValue<string>()
            ?? throw new AgentReportException("The reports repository wasn't found.");
        var category = (data["repository"]?["discussionCategories"]?["nodes"]?.AsArray() ?? new JsonArray())
            .FirstOrDefault(n => string.Equals(n?["name"]?.GetValue<string>(), _options.Category, StringComparison.OrdinalIgnoreCase))?["id"]?.GetValue<string>()
            ?? throw new AgentReportException($"The \"{_options.Category}\" discussion category doesn't exist yet.");
        return (repository, category);
    }

    private async Task<JsonNode> QueryAsync(string query, object variables, CancellationToken cancellationToken)
    {
        var client = clients.CreateClient(ClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.github.com/graphql")
        {
            Content = JsonContent.Create(new { query, variables }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Token);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("izzyblazor-docs", "1"));
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            log.LogWarning("GitHub answered {Status} to an agent report call.", (int)response.StatusCode);
            throw new AgentReportException("GitHub couldn't take the report right now. Try again later.");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken);
        if (json?["errors"] is JsonArray { Count: > 0 } errors)
        {
            log.LogWarning("GitHub GraphQL errors: {Errors}", errors.ToJsonString());
            throw new AgentReportException("GitHub couldn't take the report right now. Try again later.");
        }

        return json?["data"] ?? throw new AgentReportException("GitHub sent an empty answer.");
    }
}

/// <summary>A few reports per client per hour, on each machine: agents over-report, a loop more so.</summary>
public sealed class AgentReportLimiter(IOptions<AgentReportOptions> options, TimeProvider clock)
{
    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _sent = new(StringComparer.Ordinal);

    /// <summary>Counts a report for the client; false when it's over its hourly limit.</summary>
    public bool TryTake(string client)
    {
        var now = clock.GetUtcNow();
        var times = _sent.GetOrAdd(client, _ => new Queue<DateTimeOffset>());
        lock (times)
        {
            while (times.Count > 0 && now - times.Peek() > TimeSpan.FromHours(1))
            {
                times.Dequeue();
            }

            if (times.Count >= options.Value.PerClientPerHour)
            {
                return false;
            }

            times.Enqueue(now);
            return true;
        }
    }
}

/// <summary>
/// Checks a report before it goes anywhere public, and writes the discussion's Markdown. Rejections
/// are messages the agent can act on: fill this in, take that out.
/// </summary>
public static partial class AgentReportRules
{
    public const int MaxTitle = 120;
    public const int MaxField = 2000;
    public const int MaxRepro = 6000;

    /// <summary>What's wrong with a report, as sentences for the agent; empty when it can be posted.</summary>
    public static IReadOnlyList<string> Problems(GapReport report)
    {
        var problems = new List<string>();
        var title = report.Title?.Trim() ?? "";
        if (title.Length < 10 || title.Length > MaxTitle)
        {
            problems.Add($"Give a title of 10 to {MaxTitle} characters that names the component and the problem.");
        }

        if (string.IsNullOrWhiteSpace(report.Expected) || string.IsNullOrWhiteSpace(report.Actual))
        {
            problems.Add("Say what you expected and what happened instead.");
        }

        var needsRepro = report.Kind is GapKind.Bug or GapKind.Workaround;
        if (needsRepro && (report.Repro?.Trim().Length ?? 0) < 20)
        {
            problems.Add("Add a minimal .razor repro: the smallest markup and code that shows it, with sample names.");
        }

        foreach (var (name, value, max) in Fields(report))
        {
            if (value?.Length > max)
            {
                problems.Add($"Shorten {name} to {max} characters or fewer.");
            }

            if (value is not null && LooksPrivate(value) is { } what)
            {
                problems.Add($"Take out {what} from {name}: reports are public. Use sample names and values.");
            }
        }

        return problems;
    }

    /// <summary>The discussion's body: the report's fields under headings, the repro fenced.</summary>
    public static string Body(GapReport report, string libraryVersion, IReadOnlyList<KnownReport> related)
    {
        var text = new StringBuilder();
        text.Append($"**Kind:** {KindName(report.Kind)}  \n");
        if (!string.IsNullOrWhiteSpace(report.Component))
        {
            text.Append($"**Component:** {report.Component.Trim()}  \n");
        }

        text.Append($"**IzzyBlazor:** {(string.IsNullOrWhiteSpace(report.LibraryVersion) ? libraryVersion + " (the docs' version; the project's wasn't given)" : report.LibraryVersion.Trim())}  \n");
        if (!string.IsNullOrWhiteSpace(report.RenderMode))
        {
            text.Append($"**Render mode:** {report.RenderMode.Trim()}  \n");
        }

        if (!string.IsNullOrWhiteSpace(report.Browser))
        {
            text.Append($"**Browser:** {report.Browser.Trim()}  \n");
        }

        text.Append($"\n### Expected\n\n{report.Expected.Trim()}\n\n### What happened\n\n{report.Actual.Trim()}\n");
        if (!string.IsNullOrWhiteSpace(report.Repro))
        {
            text.Append($"\n### Repro\n\n{Fence(report.Repro.Trim(), "razor")}\n");
        }

        if (!string.IsNullOrWhiteSpace(report.Workaround))
        {
            text.Append($"\n### Workaround used\n\n{report.Workaround.Trim()}\n");
        }

        if (!string.IsNullOrWhiteSpace(report.Suggestion))
        {
            text.Append($"\n### Suggested fix\n\n{report.Suggestion.Trim()}\n");
        }

        if (related.Count > 0)
        {
            text.Append("\n### Possibly related\n\n");
            foreach (var known in related)
            {
                text.Append($"- [{known.Title}]({known.Url}) ({known.Kind})\n");
            }
        }

        return text.Append("\n---\n_Reported by an AI agent through the IzzyBlazor MCP server, with the user's agreement. Not yet triaged._\n").ToString();
    }

    public static string KindName(GapKind kind) => kind switch
    {
        GapKind.Bug => "Bug",
        GapKind.MissingFeature => "Missing feature",
        GapKind.UnclearDocs => "Unclear docs",
        _ => "Workaround needed",
    };

    /// <summary>A kind from the tool's argument: bug, missing_feature, unclear_docs or workaround.</summary>
    public static GapKind? ParseKind(string? kind) => kind?.Trim().Replace("-", "_", StringComparison.Ordinal).ToLowerInvariant() switch
    {
        "bug" => GapKind.Bug,
        "missing_feature" or "feature" => GapKind.MissingFeature,
        "unclear_docs" or "docs" => GapKind.UnclearDocs,
        "workaround" => GapKind.Workaround,
        _ => null,
    };

    private static IEnumerable<(string Name, string? Value, int Max)> Fields(GapReport report) =>
    [
        ("the title", report.Title, MaxTitle),
        ("expected", report.Expected, MaxField),
        ("what happened", report.Actual, MaxField),
        ("the repro", report.Repro, MaxRepro),
        ("the workaround", report.Workaround, MaxField),
        ("the suggestion", report.Suggestion, MaxField),
        ("the component", report.Component, 200),
        ("the render mode", report.RenderMode, 200),
        ("the browser", report.Browser, 200),
        ("the version", report.LibraryVersion, 200),
    ];

    /// <summary>What in the text looks like a secret or a real person's address, or null.</summary>
    internal static string? LooksPrivate(string text)
    {
        if (PrivateKey().IsMatch(text) || AwsKey().IsMatch(text) || GitHubToken().IsMatch(text) || Jwt().IsMatch(text))
        {
            return "what looks like a key or token";
        }

        if (Assigned().IsMatch(text) || ConnectionPassword().IsMatch(text))
        {
            return "what looks like a password or secret";
        }

        foreach (Match email in Email().Matches(text))
        {
            var domain = email.Groups["domain"].Value.ToLowerInvariant();
            if (!(domain is "example.com" or "example.org" or "example.net" || domain.EndsWith(".example", StringComparison.Ordinal) || domain.EndsWith(".test", StringComparison.Ordinal)))
            {
                return "what looks like a real email address";
            }
        }

        return null;
    }

    // Longer than any backtick run inside, so a repro holding ``` stays one block.
    private static string Fence(string code, string language)
    {
        var longest = 2;
        foreach (Match run in Backticks().Matches(code))
        {
            longest = Math.Max(longest, run.Length);
        }

        var fence = new string('`', longest + 1);
        return $"{fence}{language}\n{code}\n{fence}";
    }

    [GeneratedRegex(@"-----BEGIN [A-Z ]*PRIVATE KEY-----")]
    private static partial Regex PrivateKey();

    [GeneratedRegex(@"\b(AKIA|ASIA)[0-9A-Z]{16}\b")]
    private static partial Regex AwsKey();

    [GeneratedRegex(@"\b(gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,})\b")]
    private static partial Regex GitHubToken();

    [GeneratedRegex(@"\beyJ[\w-]{10,}\.[\w-]{10,}\.[\w-]{10,}")]
    private static partial Regex Jwt();

    // A value set in quotes, or a bare one that isn't code: not a Razor expression (Password="@model.Password")
    // or a property read (password = model.Password;), which a repro of a login form is full of.
    [GeneratedRegex(@"(?i)\b(api[_-]?key|secret|client[_-]?secret|password|passwd|pwd|access[_-]?token)\b\s*[""']?\s*[:=]\s*(?:[""'](?!@)|(?!@)(?![A-Za-z_][\w.]*(?=[\s""';,)\]}>/]|$)))[^\s""';,]{8,}")]
    private static partial Regex Assigned();

    [GeneratedRegex(@"(?i)(server|data source|host)\s*=.*;\s*(password|pwd)\s*=\s*[^;\s]+")]
    private static partial Regex ConnectionPassword();

    [GeneratedRegex(@"\b[\w.+-]+@(?<domain>[\w-]+(\.[\w-]+)+)\b")]
    private static partial Regex Email();

    [GeneratedRegex("`+")]
    private static partial Regex Backticks();
}
