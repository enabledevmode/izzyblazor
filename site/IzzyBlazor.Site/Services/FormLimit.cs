using System.Collections.Concurrent;

namespace IzzyBlazor.Site.Services;

/// <summary>
/// Holds each visitor to a few posts of the site's forms (the newsletter, the founding price) every ten minutes.
/// The account service sees this site as the caller for every visitor, so it can't tell them apart: one visitor
/// flooding a form would use up everyone's turns there.
/// </summary>
public sealed class FormLimit(TimeProvider time)
{
    private const int Posts = 5;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, (DateTimeOffset Start, int Count)> _callers = new();

    /// <summary>Counts a post from this request's caller. False once it has had its posts for the window.</summary>
    public bool Allow(HttpContext? context)
    {
        var now = time.GetUtcNow();
        // Old windows go as new posts come, so the list stays as long as the last ten minutes' callers.
        if (_callers.Count > 10_000)
        {
            foreach (var (key, value) in _callers)
            {
                if (now - value.Start >= Window) _callers.TryRemove(key, out _);
            }
        }

        var counted = _callers.AddOrUpdate(Caller(context), _ => (now, 1),
            (_, seen) => now - seen.Start >= Window ? (now, 1) : (seen.Start, seen.Count + 1));
        return counted.Count <= Posts;
    }

    // Fly's proxy gives its caller in Fly-Client-IP (it overwrites one a caller sends); without Fly, the
    // connection's own address.
    private static string Caller(HttpContext? context) =>
        context?.Request.Headers["Fly-Client-IP"].FirstOrDefault() is { Length: > 0 } client ? client
        : context?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
