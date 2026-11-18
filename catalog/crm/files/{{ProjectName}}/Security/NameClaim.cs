using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using {{RootNamespace}}.Data;

namespace {{RootNamespace}}.Security;

/// <summary>
/// Puts a person's name in their sign-in, so the header's avatar and account menu show it without a database
/// call. Identity's own name claim is the user name, which is the email address.
/// </summary>
public static class NameClaim
{
    /// <summary>The claim's type.</summary>
    public const string Type = "name";

    /// <summary>The person's name, or their email address when they haven't set one.</summary>
    public static string? Shown(ClaimsPrincipal user) => user.FindFirst(Type)?.Value ?? user.Identity?.Name;
}

/// <summary>
/// Identity's claims (id, user name, roles) plus <see cref="NameClaim"/>. A sign-in picks up a new name when
/// it's refreshed: at once on your own Profile, and within half an hour when an admin changes it (Identity's
/// security stamp check).
/// </summary>
public sealed class NameClaimsPrincipalFactory(
    UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>(users, roles, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (!string.IsNullOrWhiteSpace(user.Name))
        {
            identity.AddClaim(new Claim(NameClaim.Type, user.Name));
        }
        return identity;
    }
}
