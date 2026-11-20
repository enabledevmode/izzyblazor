using System.Security.Claims;

namespace {{RootNamespace}}.Security;

public static class UserExtensions
{
    /// <summary>The signed-in person's id (their ApplicationUser.Id); null when signed out.</summary>
    public static string? UserId(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier);
}
