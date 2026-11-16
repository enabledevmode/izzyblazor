using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using {{RootNamespace}}.Auditing;
using {{RootNamespace}}.Data;
using {{RootNamespace}}.Security;

namespace {{RootNamespace}}.Admin;

/// <summary>Where an account stands, worked out from Identity's own fields: nothing extra is stored.</summary>
public enum UserStatus { Active, Invited, Locked, Deactivated }

/// <summary>One person, as the Users pages list them.</summary>
public sealed record UserRow(string Id, string Name, string Email, string Roles, UserStatus Status, DateTime? LastSignIn);

/// <summary>
/// What an admin does to people's accounts: the Users pages call these, and each is written to the audit log.
/// Deactivating is Identity's lockout with no end date, so a deactivated person can't sign in and nothing new is
/// stored. Two rules keep the app usable: nobody deactivates, deletes or demotes themselves, and the last
/// Admin can't be removed.
/// </summary>
public sealed class UserAdmin(UserManager<ApplicationUser> users, ApplicationDbContext db, Audit audit,
    IEmailSender<ApplicationUser> email, NavigationManager navigation)
{
    public static UserStatus StatusOf(ApplicationUser user) =>
        user.LockoutEnd == DateTimeOffset.MaxValue ? UserStatus.Deactivated
        : user.LockoutEnd > DateTimeOffset.UtcNow ? UserStatus.Locked
        : !user.EmailConfirmed && user.PasswordHash is null ? UserStatus.Invited
        : UserStatus.Active;

    /// <summary>Everyone, with their roles. An admin console has tens or hundreds of people, so they're read at once.</summary>
    public async Task<List<UserRow>> ListAsync()
    {
        var roles = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id select new { ur.UserId, r.Name })
            .ToListAsync();
        var byUser = roles.GroupBy(r => r.UserId).ToDictionary(g => g.Key, g => string.Join(", ", g.Select(r => r.Name).Order()));
        var all = await db.Users.AsNoTracking().ToListAsync();
        return all.Select(u => new UserRow(u.Id, u.Name ?? u.Email!, u.Email!, byUser.GetValueOrDefault(u.Id, ""), StatusOf(u), u.LastSignIn))
            .OrderBy(u => u.Name).ToList();
    }

    /// <summary>Emails a link to set a new password; to someone invited, the invite again.</summary>
    public async Task<string> SendPasswordResetAsync(ApplicationUser user)
    {
        var invited = StatusOf(user) == UserStatus.Invited;
        await email.SendPasswordResetLinkAsync(user, user.Email!, HtmlEncoder.Default.Encode(await SetPasswordLinkAsync(user)));
        await audit.RecordAsync(invited ? AuditActions.Invited : AuditActions.PasswordResetSent, "User", user.Id, user.Email,
            note: invited ? "Invite sent again." : null);
        return invited ? "Invite sent" : "Password reset sent";
    }

    /// <summary>The link that lets <paramref name="user"/> set a password: a reset, or for an invite their first.</summary>
    public async Task<string> SetPasswordLinkAsync(ApplicationUser user)
    {
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await users.GeneratePasswordResetTokenAsync(user)));
        return navigation.GetUriWithQueryParameters(navigation.ToAbsoluteUri("Account/ResetPassword").AbsoluteUri,
            new Dictionary<string, object?> { ["code"] = code });
    }

    public async Task<string?> DeactivateAsync(ApplicationUser user, string? me)
    {
        if (user.Id == me) return "You can't deactivate your own account.";
        if (await IsLastAdminAsync(user)) return "They're the only Admin. Give someone else the Admin role first.";
        await users.SetLockoutEnabledAsync(user, true);
        await users.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        await users.UpdateSecurityStampAsync(user);
        await audit.RecordAsync(AuditActions.Deactivated, "User", user.Id, user.Email);
        return null;
    }

    public async Task ReactivateAsync(ApplicationUser user)
    {
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        await audit.RecordAsync(AuditActions.Reactivated, "User", user.Id, user.Email);
    }

    public async Task UnlockAsync(ApplicationUser user)
    {
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        await audit.RecordAsync(AuditActions.Unlocked, "User", user.Id, user.Email);
    }

    /// <summary>Turns two-factor off and forgets their authenticator, so they set it up again at their next sign-in.</summary>
    public async Task ResetTwoFactorAsync(ApplicationUser user)
    {
        await users.SetTwoFactorEnabledAsync(user, false);
        await users.ResetAuthenticatorKeyAsync(user);
        await audit.RecordAsync(AuditActions.TwoFactorReset, "User", user.Id, user.Email);
    }

    /// <summary>Gives <paramref name="user"/> exactly <paramref name="chosen"/>; returns why not, if it can't.</summary>
    public async Task<string?> SetRolesAsync(ApplicationUser user, IReadOnlyCollection<string> chosen, string? me)
    {
        var current = await users.GetRolesAsync(user);
        if (user.Id == me && current.Contains(Roles.Admin) && !chosen.Contains(Roles.Admin))
            return "You can't remove your own Admin role.";
        if (!chosen.Contains(Roles.Admin) && await IsLastAdminAsync(user))
            return "They're the only Admin. Give someone else the Admin role first.";
        var add = chosen.Intersect(Roles.All).Except(current).ToList();
        var remove = current.Except(chosen).ToList();
        if (add.Count == 0 && remove.Count == 0) return null;
        await users.RemoveFromRolesAsync(user, remove);
        await users.AddToRolesAsync(user, add);
        await audit.RecordAsync(AuditActions.RolesChanged, "User", user.Id, user.Email,
            [new("Roles", string.Join(", ", current.Order()), string.Join(", ", chosen.Intersect(Roles.All).Order()))]);
        return null;
    }

    public async Task<string?> DeleteAsync(ApplicationUser user, string? me)
    {
        if (user.Id == me) return "You can't delete your own account.";
        if (await IsLastAdminAsync(user)) return "They're the only Admin. Give someone else the Admin role first.";
        var result = await users.DeleteAsync(user);
        if (!result.Succeeded) return string.Join(" ", result.Errors.Select(e => e.Description));
        await audit.RecordAsync(AuditActions.Deleted, "User", user.Id, user.Email);
        return null;
    }

    private async Task<bool> IsLastAdminAsync(ApplicationUser user) =>
        await users.IsInRoleAsync(user, Roles.Admin)
        && (await users.GetUsersInRoleAsync(Roles.Admin)).Count(u => u.Id != user.Id && StatusOf(u) != UserStatus.Deactivated) == 0;
}
