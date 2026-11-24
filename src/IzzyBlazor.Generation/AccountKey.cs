using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace IzzyBlazor.Generation;

/// <summary>What reading an <see cref="AccountKey"/> found.</summary>
public enum AccountKeyState
{
    /// <summary>Signed by IzzyBlazor and in date.</summary>
    Valid,

    /// <summary>Signed by IzzyBlazor, past its last day: renewed the next time the machine is online.</summary>
    Expired,

    /// <summary>Not a key, changed since it was signed, signed by an unknown key, or issued in the future.</summary>
    Invalid,
}

/// <summary>
/// A signed-in machine's key: who the account is and what it includes, signed by the account service
/// (ECDSA P-256 over SHA-256) and checked here with a public key, so no network is needed. As text:
/// <c>izzy1.&lt;payload&gt;.&lt;signature&gt;</c>, both parts base64url, the payload JSON.
/// </summary>
/// <param name="Account">The account's id.</param>
/// <param name="Tier"><see cref="ItemTier.Account"/> or <see cref="ItemTier.Pro"/>.</param>
/// <param name="Machine">The id the service gave this sign-in, listed on the account page.</param>
/// <param name="KeyId">Which of the service's signing keys signed it.</param>
/// <param name="Issued">When it was signed.</param>
/// <param name="Expires">Its last moment; the service signs a new one before then.</param>
/// <param name="Preview">Pro as a preview, for an account that reserved the founding price before Pro went on sale.</param>
public sealed record AccountKey(string Account, ItemTier Tier, string Machine, string KeyId, DateTimeOffset Issued, DateTimeOffset Expires, bool Preview = false)
{
    const string Prefix = "izzy1";

    /// <summary>How far ahead of this machine's clock a key's issue time may be.</summary>
    static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(10);

    /// <summary>The key as text, signed with the service's private key.</summary>
    public string Sign(ECDsa privateKey)
    {
        var claims = new JsonObject
        {
            ["account"] = Account,
            ["tier"] = Tier == ItemTier.Pro ? "pro" : "account",
            ["machine"] = Machine,
            ["kid"] = KeyId,
            ["issued"] = Issued.ToUnixTimeSeconds(),
            ["expires"] = Expires.ToUnixTimeSeconds(),
        };
        // Written only when set: a key without it reads the same in every izzy.
        if (Preview) claims["preview"] = true;
        var payload = Encoding.UTF8.GetBytes(claims.ToJsonString());
        return $"{Prefix}.{Base64Url(payload)}.{Base64Url(privateKey.SignData(payload, HashAlgorithmName.SHA256))}";
    }

    /// <summary>
    /// Reads and checks a key. The key is returned when it is <see cref="AccountKeyState.Valid"/> or
    /// <see cref="AccountKeyState.Expired"/>, and null otherwise.
    /// </summary>
    /// <param name="text">The key as <see cref="Sign"/> wrote it.</param>
    /// <param name="publicKeys">The service's public keys by key id, each a base64 SubjectPublicKeyInfo.</param>
    /// <param name="now">The time to check its dates against.</param>
    /// <param name="state">What was found.</param>
    public static AccountKey? Read(string? text, IReadOnlyDictionary<string, string> publicKeys, DateTimeOffset now, out AccountKeyState state)
    {
        state = AccountKeyState.Invalid;
        var parts = (text ?? "").Trim().Split('.');
        if (parts.Length != 3 || parts[0] != Prefix) return null;
        try
        {
            var payload = FromBase64Url(parts[1]);
            var json = JsonNode.Parse(payload)!;
            var key = new AccountKey(
                (string)json["account"]!,
                (string)json["tier"]! switch { "pro" => ItemTier.Pro, "account" => ItemTier.Account, _ => throw new FormatException() },
                (string)json["machine"]!,
                (string)json["kid"]!,
                DateTimeOffset.FromUnixTimeSeconds((long)json["issued"]!),
                DateTimeOffset.FromUnixTimeSeconds((long)json["expires"]!),
                (bool?)json["preview"] == true);
            if (!Verify(payload, FromBase64Url(parts[2]), key.KeyId, publicKeys)) return null;
            // A clock set back to before the key was made would keep an old key in date for ever.
            if (key.Issued > now + ClockSkew) return null;
            state = key.Expires < now ? AccountKeyState.Expired : AccountKeyState.Valid;
            return key;
        }
        catch (Exception e) when (e is FormatException or JsonException or InvalidOperationException or NullReferenceException or CryptographicException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Whether <paramref name="signature"/> is the named key's over <paramref name="data"/>.</summary>
    internal static bool Verify(byte[] data, byte[] signature, string keyId, IReadOnlyDictionary<string, string> publicKeys)
    {
        if (!publicKeys.TryGetValue(keyId, out var spki)) return false;
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(spki), out _);
        return ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256);
    }

    internal static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    internal static byte[] FromBase64Url(string text)
    {
        var padded = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '='));
    }
}

/// <summary>The account service's public signing keys, by key id: compiled in, so a key is checked with no network.</summary>
public static class AccountKeys
{
    /// <summary>
    /// Each a base64 SubjectPublicKeyInfo (ECDSA P-256). A new signing key is added here a release before the
    /// service starts signing with it; an old one stays until the keys it signed have expired.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Public { get; } = new Dictionary<string, string>
    {
        ["k1"] = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEOIUI841NBErd/nha5mx1pQ5OySNmPZmOWW9ZUmiNSXVmPvT0hM/lbUJE5i1sm+A/5ifbjxku7vrArx7LnkdO1w==",
    };
}
