using System.Text.Json;
using System.Text.Json.Nodes;

namespace IzzyBlazor.Cli;

/// <summary>
/// The IzzyBlazor releases on nuget.org, for the line that says a newer one is out and for <c>izzy update</c>.
/// The line's check runs beside the command, at most once a day, and is dropped when the command ends first:
/// it never holds a command up or fails one. What it learned is kept in <c>update.json</c> under
/// <paramref name="home"/> (<c>~/.izzy</c>). The request names the package and nothing else.
/// </summary>
/// <param name="home">The folder for <c>update.json</c>.</param>
/// <param name="version">This izzy's version.</param>
/// <param name="handler">The HTTP handler, for tests.</param>
/// <param name="clock">The time, for tests.</param>
sealed class Releases(string home, string version, HttpMessageHandler? handler = null, Func<DateTimeOffset>? clock = null)
{
    /// <summary>The versions of IzzyBlazor.Cli, which is released with the library under one version.</summary>
    public static readonly Uri Index = new("https://api.nuget.org/v3-flatcontainer/izzyblazor.cli/index.json");

    /// <summary>Where a release's changes are listed.</summary>
    public const string Notes = "https://github.com/enabledevmode/izzyblazor/releases";

    static readonly TimeSpan Every = TimeSpan.FromDays(1);

    // After a check that didn't get through (offline, or the command ended first), the next waits this long.
    static readonly TimeSpan Again = TimeSpan.FromHours(1);

    Task<string?>? _check;

    /// <summary>The check under way, for tests to wait on.</summary>
    internal Task? Check => _check;

    string StatePath => Path.Combine(home, "update.json");
    DateTimeOffset Now => clock?.Invoke() ?? DateTimeOffset.UtcNow;

    /// <summary>The releases from the environment: <c>~/.izzy</c> (or <c>IZZY_HOME</c>) and this izzy's version.</summary>
    public static Releases FromEnvironment() => new(
        Environment.GetEnvironmentVariable("IZZY_HOME") is { Length: > 0 } given ? given : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".izzy"),
        Generation.Generator.IzzyBlazorVersion);

    /// <summary>
    /// Whether this run may say a newer release is out: a person at a terminal, not a script, an editor or CI
    /// reading the output; not a command whose output is data; and not turned off with <c>IZZY_NO_UPDATE_CHECK</c>.
    /// </summary>
    public static bool Wanted(string[] arguments, bool terminal) =>
        terminal
        && arguments.FirstOrDefault() is not ("update" or "mcp" or "bundle")
        && !arguments.Contains("--json")
        && Environment.GetEnvironmentVariable("IZZY_NO_UPDATE_CHECK") is not { Length: > 0 }
        && Environment.GetEnvironmentVariable("CI") is not { Length: > 0 };

    /// <summary>Starts the day's check beside the command, when one is due.</summary>
    public void Begin()
    {
        var state = Read();
        if (Time(state, "checked") is { } last && Now - last < Every) return;
        if (Time(state, "tried") is { } tried && Now - tried < Again) return;
        state["tried"] = Now.ToString("O");
        Write(state);
        _check = Task.Run(() => Latest(TimeSpan.FromSeconds(10)));
    }

    /// <summary>
    /// The line to end the command with, or null: a newer release is known (from today's check if it is done,
    /// else from the last one) and the line wasn't said in the past day. <paramref name="izzy"/> is how this
    /// app runs izzy: <c>izzy</c> or <c>dotnet izzy</c>.
    /// </summary>
    public string? Notice(string izzy = "izzy")
    {
        try
        {
            var state = Read();
            if (_check is { IsCompletedSuccessfully: true, Result: { } found })
            {
                state["checked"] = Now.ToString("O");
                state["latest"] = found;
                state.Remove("tried");
                Write(state);
            }
            if ((string?)state["latest"] is not { } latest || Newest([latest], version) is null) return null;
            if (Time(state, "told") is { } told && Now - told < Every) return null;
            state["told"] = Now.ToString("O");
            Write(state);
            return $"IzzyBlazor {latest} is out (this is {version}). To update: {izzy} update";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// The newest release this izzy would move to, or this izzy's own version when none is newer; null when
    /// nuget.org can't be reached within <paramref name="limit"/>.
    /// </summary>
    public string? Latest(TimeSpan limit)
    {
        try
        {
            using var http = new HttpClient(handler ?? new HttpClientHandler(), disposeHandler: handler is null) { Timeout = limit };
            var versions = (JsonNode.Parse(http.GetStringAsync(Index).GetAwaiter().GetResult())?["versions"] as JsonArray)?.Select(v => (string?)v).OfType<string>() ?? [];
            return Newest(versions, version) ?? version;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// The newest of <paramref name="versions"/> that is newer than <paramref name="current"/>, or null. A
    /// stable izzy is offered stable releases only; a prerelease one, whichever is newest.
    /// </summary>
    public static string? Newest(IEnumerable<string> versions, string current)
    {
        if (Parse(current) is not { } mine) return null;
        string? best = null;
        var top = mine;
        foreach (var candidate in versions)
        {
            if (Parse(candidate) is not { } parsed || (mine.Pre.Length == 0 && parsed.Pre.Length > 0)) continue;
            if (Compare(parsed, top) > 0) (best, top) = (candidate, parsed);
        }
        return best;
    }

    /// <summary>Which of two versions is newer, by SemVer's order: 1.0.0-alpha.9, 1.0.0-alpha.10, 1.0.0.</summary>
    public static int Compare(string a, string b) =>
        Parse(a) is { } left && Parse(b) is { } right ? Compare(left, right) : string.CompareOrdinal(a, b);

    static int Compare((int[] Core, string[] Pre) a, (int[] Core, string[] Pre) b)
    {
        for (var i = 0; i < 3; i++)
            if (a.Core[i] != b.Core[i]) return a.Core[i].CompareTo(b.Core[i]);
        // A release is newer than its prereleases.
        if (a.Pre.Length == 0 || b.Pre.Length == 0) return b.Pre.Length.CompareTo(a.Pre.Length);
        for (var i = 0; i < Math.Min(a.Pre.Length, b.Pre.Length); i++)
        {
            var (x, y) = (a.Pre[i], b.Pre[i]);
            var (xn, yn) = (int.TryParse(x, out var xv), int.TryParse(y, out var yv));
            var order = xn && yn ? xv.CompareTo(yv) : xn ? -1 : yn ? 1 : string.CompareOrdinal(x, y);
            if (order != 0) return order;
        }
        return a.Pre.Length.CompareTo(b.Pre.Length);
    }

    static (int[] Core, string[] Pre)? Parse(string text)
    {
        var parts = text.Split('+')[0].Split('-', 2);
        var core = parts[0].Split('.');
        if (core.Length != 3 || core.Any(n => !int.TryParse(n, out _))) return null;
        return (core.Select(int.Parse).ToArray(), parts.Length == 2 ? parts[1].Split('.') : []);
    }

    static DateTimeOffset? Time(JsonObject state, string name) =>
        DateTimeOffset.TryParse((string?)state[name], null, System.Globalization.DateTimeStyles.RoundtripKind, out var at) ? at : null;

    JsonObject Read()
    {
        try
        {
            return File.Exists(StatePath) && JsonNode.Parse(File.ReadAllText(StatePath)) is JsonObject state ? state : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    // Whole or not at all: the process may end while another izzy reads the file.
    void Write(JsonObject state)
    {
        try
        {
            Directory.CreateDirectory(home);
            var temp = StatePath + "." + Environment.ProcessId;
            File.WriteAllText(temp, state.ToJsonString() + "\n");
            File.Move(temp, StatePath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A home that can't be written: the check runs again next time, and nothing is said twice a command.
        }
    }
}
