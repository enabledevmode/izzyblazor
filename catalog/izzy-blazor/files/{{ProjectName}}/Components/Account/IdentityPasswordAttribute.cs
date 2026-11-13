using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace {{RootNamespace}}.Components.Account;

/// <summary>
/// Checks a new password against Identity's own password options before the form posts, so the field says
/// what's missing ("Add an uppercase letter and a symbol.") instead of Identity's list after the fact.
/// <see cref="Hint"/> says the same rules in one line, for the field's hint.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class IdentityPasswordAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is not string { Length: > 0 } password)
        {
            return ValidationResult.Success; // [Required] covers an empty field.
        }

        var options = Options(context);
        var missing = new List<string>();
        if (options.RequireUppercase && !password.Any(char.IsAsciiLetterUpper)) missing.Add("an uppercase letter");
        if (options.RequireLowercase && !password.Any(char.IsAsciiLetterLower)) missing.Add("a lowercase letter");
        if (options.RequireDigit && !password.Any(char.IsAsciiDigit)) missing.Add("a number");
        if (options.RequireNonAlphanumeric && password.All(char.IsAsciiLetterOrDigit)) missing.Add("a symbol");
        if (password.Length < options.RequiredLength)
        {
            var more = options.RequiredLength - password.Length;
            missing.Add(missing.Count == 0 ? $"{more} more character{(more == 1 ? "" : "s")}" : $"{more} more");
        }
        if (options.RequiredUniqueChars > 1 && password.Distinct().Count() < options.RequiredUniqueChars)
        {
            missing.Add("more different characters");
        }

        return missing.Count == 0
            ? ValidationResult.Success
            : new ValidationResult($"Add {Join(missing)}.", [context.MemberName!]);
    }

    /// <summary>Identity's password rules in one line: "At least 6 characters, with mixed case, a number and a symbol."</summary>
    public static string? Hint(PasswordOptions options)
    {
        var parts = new List<string>();
        if (options.RequireUppercase && options.RequireLowercase) parts.Add("mixed case");
        else if (options.RequireUppercase) parts.Add("an uppercase letter");
        else if (options.RequireLowercase) parts.Add("a lowercase letter");
        if (options.RequireDigit) parts.Add("a number");
        if (options.RequireNonAlphanumeric) parts.Add("a symbol");
        if (options.RequiredUniqueChars > 1) parts.Add($"{options.RequiredUniqueChars} different characters");

        return (options.RequiredLength, parts.Count) switch
        {
            ( > 0, 0) => $"At least {options.RequiredLength} characters.",
            ( > 0, _) => $"At least {options.RequiredLength} characters, with {Join(parts)}.",
            (_, > 0) => $"Use {Join(parts)}.",
            _ => null,
        };
    }

    private static PasswordOptions Options(ValidationContext context) =>
        (context.GetService(typeof(IOptions<IdentityOptions>)) as IOptions<IdentityOptions>)?.Value.Password ?? new PasswordOptions();

    private static string Join(List<string> parts) =>
        parts.Count == 1 ? parts[0] : $"{string.Join(", ", parts[..^1])} and {parts[^1]}";
}
