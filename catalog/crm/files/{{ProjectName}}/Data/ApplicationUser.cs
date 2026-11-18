using Microsoft.AspNetCore.Identity;

namespace {{RootNamespace}}.Data;

// Add profile data for application users by adding properties to the ApplicationUser class
public class ApplicationUser : IdentityUser
{
    /// <summary>The name shown in the app; the email when there's none.</summary>
    public string? Name { get; set; }

    /// <summary>When the account was made, or the invite sent, in UTC.</summary>
    public DateTime Created { get; set; } = DateTime.UtcNow;

    /// <summary>The last time they signed in, in UTC; null if they never have (an invite not taken up).</summary>
    public DateTime? LastSignIn { get; set; }
}
