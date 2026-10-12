using System.Security.Cryptography;

namespace IzzyBlazor;

/// <summary>
/// Generates element ids for wiring ARIA and popover attributes
/// (<c>aria-controls</c>, <c>for</c>, <c>popovertarget</c>).
/// </summary>
/// <remarks>
/// Components generate an id once per instance and keep it, so it is stable across
/// re-renders. A prerendered page and its interactive render are different instances,
/// so ids may change when a page becomes interactive; the whole subtree re-renders
/// together, so references stay consistent.
/// </remarks>
public static class IdGenerator
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>Returns a new id such as <c>ib-tabs-k3x9q2</c>.</summary>
    public static string Next(string prefix = "ib")
    {
        Span<char> suffix = stackalloc char[6];
        for (var i = 0; i < suffix.Length; i++)
        {
            suffix[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return $"{prefix}-{suffix}";
    }
}
