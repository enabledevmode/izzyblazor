using System.Security.Cryptography;

namespace IzzyBlazor;

/// <summary>
/// Generates element ids for wiring ARIA and popover attributes
/// (<c>aria-controls</c>, <c>for</c>, <c>popovertarget</c>).
/// </summary>
/// <remarks>
/// Components generate an id once per instance and keep it, so it is stable across
/// re-renders. A static page rendered on the server counts its ids (<c>ib-tabs-1</c>,
/// <c>ib-tabs-2</c>), so the same page gets the same ids on every request: an enhanced
/// navigation that patches the page then leaves them as they were. Elsewhere (interactive
/// renders, a part of a page fetched by script) ids are random, since what they join may not be
/// the page that counted. A prerendered page and its interactive render are different instances,
/// so ids may change when a page becomes interactive; the whole subtree re-renders together, so
/// references stay consistent.
/// </remarks>
public static class IdGenerator
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>
    /// The counter for the static render under way, if any. Set by the server package, which can
    /// see the request; null in a browser.
    /// </summary>
    internal static Func<IdCounter?>? Current { get; set; }

    /// <summary>Returns a new id such as <c>ib-tabs-1</c>, or <c>ib-tabs-k3x9q2</c> outside a static render.</summary>
    public static string Next(string prefix = "ib")
    {
        if (Current?.Invoke() is { } counter)
        {
            return counter.Next(prefix);
        }

        Span<char> suffix = stackalloc char[6];
        for (var i = 0; i < suffix.Length; i++)
        {
            suffix[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return $"{prefix}-{suffix}";
    }
}

/// <summary>
/// One static render's ids, counted for each prefix, so adding a component of one kind leaves the
/// ids of the others as they were.
/// </summary>
internal sealed class IdCounter
{
    private readonly Dictionary<string, int> _counts = [];

    public string Next(string prefix)
    {
        lock (_counts)
        {
            var n = _counts.GetValueOrDefault(prefix) + 1;
            _counts[prefix] = n;
            return $"{prefix}-{n}";
        }
    }
}
