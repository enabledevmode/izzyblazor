using System.Text.RegularExpressions;

namespace IzzyBlazor.Generation;

/// <summary>
/// Looks for what shouldn't be in text that is posted in public: keys, tokens, passwords, and a real
/// person's email address. An agent's gap report and <c>izzy feedback</c> are both checked with it before
/// they go anywhere.
/// </summary>
public static partial class PrivateText
{
    /// <summary>What in the text looks like a secret or a real person's address, or null.</summary>
    public static string? Find(string text)
    {
        if (PrivateKey().IsMatch(text) || AwsKey().IsMatch(text) || GitHubToken().IsMatch(text) || Jwt().IsMatch(text) || IzzyKey().IsMatch(text))
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
            if (!(domain is "example.com" or "example.org" or "example.net" or "izzyblazor.com"
                || domain.EndsWith(".example", StringComparison.Ordinal) || domain.EndsWith(".test", StringComparison.Ordinal)))
            {
                return "what looks like a real email address";
            }
        }

        return null;
    }

    [GeneratedRegex(@"-----BEGIN [A-Z ]*PRIVATE KEY-----")]
    private static partial Regex PrivateKey();

    [GeneratedRegex(@"\b(AKIA|ASIA)[0-9A-Z]{16}\b")]
    private static partial Regex AwsKey();

    [GeneratedRegex(@"\b(gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,})\b")]
    private static partial Regex GitHubToken();

    [GeneratedRegex(@"\beyJ[\w-]{10,}\.[\w-]{10,}\.[\w-]{10,}")]
    private static partial Regex Jwt();

    // An IzzyBlazor account's own: a key for CI from the account page, or a machine's sign-in.
    [GeneratedRegex(@"\b(izzyci_[A-Za-z0-9_-]{20,}|izzy1\.[\w-]{20,}\.[\w-]{20,})")]
    private static partial Regex IzzyKey();

    // A value set in quotes, or a bare one that isn't code: not a Razor expression (Password="@model.Password")
    // or a property read (password = model.Password;), which a repro of a login form is full of.
    [GeneratedRegex(@"(?i)\b(api[_-]?key|secret|client[_-]?secret|password|passwd|pwd|access[_-]?token)\b\s*[""']?\s*[:=]\s*(?:[""'](?!@)|(?!@)(?![A-Za-z_][\w.]*(?=[\s""';,)\]}>/]|$)))[^\s""';,]{8,}")]
    private static partial Regex Assigned();

    [GeneratedRegex(@"(?i)(server|data source|host)\s*=.*;\s*(password|pwd)\s*=\s*[^;\s]+")]
    private static partial Regex ConnectionPassword();

    [GeneratedRegex(@"\b[\w.+-]+@(?<domain>[\w-]+(\.[\w-]+)+)\b")]
    private static partial Regex Email();
}

/// <summary>
/// What feedback must be before it is posted in public. izzy checks it before it asks to post, and the account
/// service checks it again.
/// </summary>
public static class FeedbackText
{
    /// <summary>The fewest characters that say what happened.</summary>
    public const int Min = 20;

    /// <summary>The most a post holds.</summary>
    public const int Max = 4000;

    /// <summary>What to change before it can be posted, as a sentence for the person; null when it can.</summary>
    /// <param name="text">What the person wrote.</param>
    /// <param name="about">The izzy command it is about, if they gave one.</param>
    public static string? Problem(string? text, string? about)
    {
        text = text?.Trim() ?? "";
        if (text.Length < Min) return $"Say what happened in a sentence or two ({Min} characters or more).";
        if (text.Length > Max) return $"Shorten it to {Max:N0} characters or fewer.";
        if (about?.Trim().Length > 200) return "Shorten the command to 200 characters or fewer.";
        foreach (var value in new[] { text, about })
            if (value is not null && PrivateText.Find(value) is { } what) return $"Take out {what}: the post is public. For anything private, write to support@izzyblazor.com.";
        return null;
    }
}
