using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace IzzyBlazor.Site.Services;

/// <summary>
/// A cookie every page gives its reader, which a request for an example's state must carry: a
/// random number and the time, signed. On Nov 24, 2026 a crawler walked the Scheduler's and the
/// store demo's states from a new address for every request, with one browser's user agent and
/// the headers a click on the site sends, so no address, agent or header told it from a reader.
/// A reader has opened a page here first and so has the pass; a crawler that asks for addresses
/// one by one has not, and one that keeps the pass is one visitor, with one visitor's share.
/// </summary>
/// <remarks>
/// Signed with a key made when the site starts, so a pass doesn't outlive a deploy: its reader
/// gets the plain page once, and a new pass with it. The number is in no log and tied to nothing.
/// </remarks>
public static class ReaderPass
{
    internal const string Cookie = "ib-reader";
    private static readonly TimeSpan Life = TimeSpan.FromHours(12);
    private static readonly byte[] Key = RandomNumberGenerator.GetBytes(32);

    /// <summary>The number of the pass a request carries; null without one, or with one that is forged or too old.</summary>
    public static string? Read(HttpRequest request)
    {
        if (request.Cookies[Cookie] is not { Length: > 0 and <= 120 } pass || pass.Split('.') is not [var id, var at, var signature]
            || !long.TryParse(at, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
        {
            return null;
        }

        var age = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(Math.Min(seconds, 253_402_300_799));
        return age >= TimeSpan.Zero && age < Life
            && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Sign(id, at)), Encoding.ASCII.GetBytes(signature))
                ? id
                : null;
    }

    /// <summary>Gives the response a new pass.</summary>
    public static void Give(HttpContext context)
    {
        var id = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(12));
        var at = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        context.Response.Cookies.Append(Cookie, $"{id}.{at}.{Sign(id, at)}", new CookieOptions
        {
            HttpOnly = true, SameSite = SameSiteMode.Lax, Secure = context.Request.IsHttps, MaxAge = Life, Path = "/", IsEssential = true,
        });
    }

    private static string Sign(string id, string at) =>
        Base64Url.EncodeToString(HMACSHA256.HashData(Key, Encoding.UTF8.GetBytes($"{id}.{at}")));
}
