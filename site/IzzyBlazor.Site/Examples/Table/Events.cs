using System.Text;

namespace IzzyBlazor.Site.Examples.Table;

/// <summary>
/// A stand-in for an API that pages by cursor, as many logs and audit trails do: newest first, an
/// opaque "next" token, and no total. The cursor is the id to continue below, base64-encoded so
/// nothing reads it but this API.
/// </summary>
public static class Events
{
    private static readonly string[] Actions = ["Signed in", "Changed a rule", "Exported a report", "Invited a member", "Rotated a key", "Signed out"];
    private static readonly string[] People = ["ada", "grace", "alan", "katherine", "margaret"];

    private static readonly IReadOnlyList<AuditEvent> All = Enumerable.Range(1, 64)
        .Select(i => new AuditEvent(i, new DateTimeOffset(2026, 10, 27, 9, 0, 0, TimeSpan.Zero).AddMinutes(-i * 37), People[i * 3 % People.Length], Actions[i * 7 % Actions.Length]))
        .ToList();

    /// <summary>
    /// One page of events, newest first, after <paramref name="cursor"/> (null for the newest): those whose person and
    /// action contain <paramref name="who"/> and <paramref name="what"/>, when given.
    /// </summary>
    public static async Task<(IReadOnlyList<AuditEvent> Items, string? Next)> ListAsync(string? cursor, int limit, string? who = null, string? what = null)
    {
        await Task.Delay(300); // a network call
        var below = Decode(cursor); // a cursor this API didn't make reads as the newest
        var page = All
            .Where(e => e.Id > below)
            .Where(e => string.IsNullOrWhiteSpace(who) || e.Who.Contains(who.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(e => string.IsNullOrWhiteSpace(what) || e.What.Contains(what.Trim(), StringComparison.OrdinalIgnoreCase))
            .Take(limit + 1).ToList();
        var next = page.Count > limit ? Convert.ToBase64String(Encoding.UTF8.GetBytes(page[limit - 1].Id.ToString(System.Globalization.CultureInfo.InvariantCulture))) : null;
        return (page.Take(limit).ToList(), next);
    }

    private static int Decode(string? cursor)
    {
        Span<byte> bytes = stackalloc byte[32];
        return cursor is not null && Convert.TryFromBase64String(cursor, bytes, out var length)
            && int.TryParse(bytes[..length], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id) ? id : 0;
    }
}

public sealed record AuditEvent(int Id, DateTimeOffset At, string Who, string What);
