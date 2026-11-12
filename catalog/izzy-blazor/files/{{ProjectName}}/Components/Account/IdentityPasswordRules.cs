using IzzyBlazor;
using Microsoft.AspNetCore.Identity;

namespace {{RootNamespace}}.Components.Account;

/// <summary>
/// Identity's password options as a PasswordField's Requirements: the field lists them under a new password and
/// checks them off as it's typed, and since they're Identity's own, the list matches what the server accepts.
/// </summary>
internal static class IdentityPasswordRules
{
    public static PasswordRules From(PasswordOptions options) => new()
    {
        RequiredLength = options.RequiredLength,
        RequireDigit = options.RequireDigit,
        RequireLowercase = options.RequireLowercase,
        RequireUppercase = options.RequireUppercase,
        RequireNonAlphanumeric = options.RequireNonAlphanumeric,
        RequiredUniqueChars = options.RequiredUniqueChars,
    };
}
