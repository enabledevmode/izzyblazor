using System.Text;

namespace IzzyBlazor;

/// <summary>
/// What a new password must have, for <see cref="TextField.Requirements"/>: shown as a list under the
/// field, checked off as it's typed, and checked on the server. The properties are named and checked as
/// ASP.NET Core Identity's <c>PasswordOptions</c>, so an app passes its own rules across. The defaults
/// differ: here every rule is off until set, where Identity's are on (a length of 6, a digit, both
/// cases and a symbol), so copy each value, not only the ones you changed.
/// </summary>
/// <example>
/// <code>
/// &lt;TextField @bind-Value="Model.Password" Type="TextFieldType.Password" Requirements="new PasswordRules { RequiredLength = 12 }" /&gt;
///
/// @* From Identity's options: *@
/// Requirements="new PasswordRules { RequiredLength = o.RequiredLength, RequireDigit = o.RequireDigit, ... }"
/// </code>
/// </example>
public sealed record PasswordRules
{
    /// <summary>The fewest characters, counted as <c>string.Length</c> is (as Identity counts them); only spaces don't count.</summary>
    public int RequiredLength { get; init; }

    /// <summary>At least one digit, 0 to 9.</summary>
    public bool RequireDigit { get; init; }

    /// <summary>At least one lowercase letter, a to z.</summary>
    public bool RequireLowercase { get; init; }

    /// <summary>At least one uppercase letter, A to Z.</summary>
    public bool RequireUppercase { get; init; }

    /// <summary>At least one character that isn't an ASCII letter or digit, as Identity counts it: <c>!</c>, a space, or <c>é</c>.</summary>
    public bool RequireNonAlphanumeric { get; init; }

    /// <summary>The fewest different characters. 1 or less asks nothing.</summary>
    public int RequiredUniqueChars { get; init; } = 1;

    /// <summary>The rules that ask something, in the order they're listed: a key for the script, and whether a password meets it.</summary>
    internal IEnumerable<(string Key, int Count, Func<string, bool> Met)> Each()
    {
        if (RequiredLength > 0)
        {
            yield return ("length", RequiredLength, p => p.Length >= RequiredLength && !string.IsNullOrWhiteSpace(p));
        }

        if (RequireLowercase)
        {
            yield return ("lower", 0, p => p.Any(char.IsAsciiLetterLower));
        }

        if (RequireUppercase)
        {
            yield return ("upper", 0, p => p.Any(char.IsAsciiLetterUpper));
        }

        if (RequireDigit)
        {
            yield return ("digit", 0, p => p.Any(char.IsAsciiDigit));
        }

        if (RequireNonAlphanumeric)
        {
            yield return ("symbol", 0, p => p.Any(c => !char.IsAsciiLetterOrDigit(c)));
        }

        if (RequiredUniqueChars > 1)
        {
            yield return ("unique", RequiredUniqueChars, p => p.Distinct().Count() >= RequiredUniqueChars);
        }
    }

    /// <summary>Whether <paramref name="password"/> meets every rule.</summary>
    public bool IsMetBy(string? password) => Each().All(rule => rule.Met(password ?? ""));

    /// <summary>The rules in Safari's <c>passwordrules</c> syntax, so the passwords it suggests pass them.</summary>
    internal string? AppleRules()
    {
        var rules = new StringBuilder();
        if (RequiredLength > 0)
        {
            rules.Append(System.Globalization.CultureInfo.InvariantCulture, $"minlength: {RequiredLength}; ");
        }

        if (RequireLowercase) rules.Append("required: lower; ");
        if (RequireUppercase) rules.Append("required: upper; ");
        if (RequireDigit) rules.Append("required: digit; ");
        if (RequireNonAlphanumeric) rules.Append("required: special; ");
        return rules.Length == 0 ? null : rules.ToString().TrimEnd();
    }
}
