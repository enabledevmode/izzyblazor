using System.ComponentModel.DataAnnotations;
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

/// <summary>
/// Where an account stands, worked out from Identity's own fields: nothing extra is stored. Invited is an
/// account with no password yet: accepting the invitation sets one.
/// </summary>
public enum UserStatus { Active, Invited, Locked, Deactivated }

/// <summary>
/// A person's role, as the team list shows and the member form edits it. The roles nest (an Admin can do all a Manager can,
/// and a Manager all a Viewer can), so each person has one. The names are the Identity roles in Security/Policies.cs.
/// </summary>
public enum UserRole { Admin, Manager, Viewer }

/// <summary>One person, as Settings &amp; team lists them and the member form invites and edits them.</summary>
public sealed class UserRow
{
    public string Id { get; set; } = "";

    /// <summary>The name shown in the app; optional when inviting.</summary>
    [StringLength(100)] public string? Name { get; set; }

    [Required(ErrorMessage = "Enter an email address."), EmailAddress(ErrorMessage = "Enter an email address, like name@example.com."), StringLength(256)]
    public string Email { get; set; } = "";

    public UserRole Role { get; set; } = UserRole.Viewer;

    /// <summary>What they aim to win each month, in the workspace's currency; empty for someone who doesn't sell.</summary>
    [Range(0, 100_000_000, ErrorMessage = "Enter an amount of 0 or more.")]
    public decimal? MonthlyTarget { get; set; }

    public UserStatus Status { get; set; }

    /// <summary>Whether they log in with a second step, which an admin can reset.</summary>
    public bool TwoFactor { get; set; }

    /// <summary>The name, or the email for someone invited without one.</summary>
    public string Shown => string.IsNullOrWhiteSpace(Name) ? Email : Name;
}

/// <summary>
/// What an admin does to people's accounts: Settings &amp; team calls these, and each is written to the audit log.
/// Deactivating is Identity's lockout with no end date, so a deactivated person can't log in and nothing new is
/// stored. Two rules keep the app usable: nobody deactivates, deletes or demotes themselves, and the last
/// Admin can't be removed.
/// </summary>
public sealed class UserAdmin(UserManager<ApplicationUser> users, ApplicationDbContext db, Audit audit,
    IEmailSender<ApplicationUser> email, NavigationManager navigation)
{
    private const string OnlyAdmin = "They're the only Admin. Give someone else the Admin role first.";

    public static UserStatus StatusOf(ApplicationUser user) =>
        user.LockoutEnd == DateTimeOffset.MaxValue ? UserStatus.Deactivated
        : user.LockoutEnd > DateTimeOffset.UtcNow ? UserStatus.Locked
        : user.PasswordHash is null ? UserStatus.Invited
        : UserStatus.Active;

    /// <summary>Everyone, with their role. A team has tens or hundreds of people, so they're read at once.</summary>
    public async Task<List<UserRow>> ListAsync()
    {
        var roles = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id select new { ur.UserId, r.Name })
            .ToListAsync();
        // Someone given several roles outside this page shows as the one that allows the most.
        var byUser = roles.GroupBy(r => r.UserId).ToDictionary(g => g.Key,
            g => g.Select(r => Enum.TryParse<UserRole>(r.Name, out var role) ? role : UserRole.Viewer).Min());
        var all = await db.Users.AsNoTracking().ToListAsync();
        return all.Select(u => new UserRow
        {
            Id = u.Id, Name = u.Name, Email = u.Email!, Role = byUser.GetValueOrDefault(u.Id, UserRole.Viewer), MonthlyTarget = Whole(u.MonthlyTarget),
            Status = StatusOf(u), TwoFactor = u.TwoFactorEnabled,
        }).OrderBy(u => u.Shown).ToList();
    }

    /// <summary>
    /// Makes an account with no password and emails a link to set one. Returns the link, which in development
    /// (where no mail is sent) the page shows, or why it couldn't.
    /// </summary>
    public async Task<(string? Link, string? Error)> InviteAsync(UserRow row)
    {
        var address = row.Email.Trim();
        if (await users.FindByEmailAsync(address) is { } existing)
            return (null, $"{existing.Name ?? existing.Email} already has an account.");
        var user = new ApplicationUser { UserName = address, Email = address, Name = Blank(row.Name) };
        var made = await users.CreateAsync(user);
        if (!made.Succeeded) return (null, string.Join(" ", made.Errors.Select(e => e.Description)));
        await users.AddToRoleAsync(user, row.Role.ToString());
        var link = await SetPasswordLinkAsync(user);
        await email.SendPasswordResetLinkAsync(user, address, HtmlEncoder.Default.Encode(link));
        await audit.RecordAsync(AuditActions.Invited, "User", user.Id, user.Name ?? address, [new("Role", null, row.Role.ToString())]);
        return (link, null);
    }

    /// <summary>
    /// Saves an edit of <paramref name="row"/>'s name, email, role and monthly target; when it can't, returns why
    /// and the field that's about.
    /// </summary>
    public async Task<(string Field, string Message)?> UpdateAsync(ApplicationUser user, UserRow row, string? me)
    {
        var current = await RoleOfAsync(user);
        if (row.Role != current)
        {
            if (user.Id == me && current == UserRole.Admin) return (nameof(row.Role), "You can't change your own role: ask another Admin.");
            if (current == UserRole.Admin && await IsLastAdminAsync(user)) return (nameof(row.Role), OnlyAdmin);
        }

        var address = row.Email.Trim();
        var changes = new List<AuditChange>();
        if (!string.Equals(address, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            if (await users.FindByEmailAsync(address) is { } other && other.Id != user.Id)
                return (nameof(row.Email), $"{other.Name ?? other.Email} already has that email.");
            changes.Add(new("Email", user.Email, address));
            // An admin vouches for the address they type, so it stays confirmed.
            user.Email = user.UserName = address;
            user.NormalizedEmail = user.NormalizedUserName = users.NormalizeEmail(address);
        }
        if (Blank(row.Name) != user.Name)
        {
            changes.Add(new("Name", user.Name, Blank(row.Name)));
            user.Name = Blank(row.Name);
        }
        if (row.MonthlyTarget != user.MonthlyTarget)
        {
            changes.Add(new("MonthlyTarget", Amount(user.MonthlyTarget), Amount(row.MonthlyTarget)));
            user.MonthlyTarget = row.MonthlyTarget;
        }
        if (changes.Count > 0)
        {
            var saved = await users.UpdateAsync(user);
            if (!saved.Succeeded) return (nameof(row.Email), string.Join(" ", saved.Errors.Select(e => e.Description)));
        }
        if (row.Role != current)
        {
            await users.RemoveFromRolesAsync(user, await users.GetRolesAsync(user));
            await users.AddToRoleAsync(user, row.Role.ToString());
            changes.Add(new("Role", current.ToString(), row.Role.ToString()));
        }
        // Another address or role ends what the old one allowed: the links sent to the old address stop
        // working (send the invitation again), and their open sessions log in afresh. Not your own, which
        // would sign you out as you save.
        if (user.Id != me && changes.Exists(c => c.Field is "Email" or "Role")) await users.UpdateSecurityStampAsync(user);
        if (changes.Count > 0)
            await audit.RecordAsync(changes.Exists(c => c.Field == "Role") ? AuditActions.RolesChanged : AuditActions.Updated,
                "User", user.Id, user.Name ?? user.Email, changes);
        return null;
    }

    /// <summary>Emails a link to set a new password; to someone invited, the invite again. Returns the link.</summary>
    public async Task<string> SendPasswordResetAsync(ApplicationUser user)
    {
        var invited = StatusOf(user) == UserStatus.Invited;
        var link = await SetPasswordLinkAsync(user);
        await email.SendPasswordResetLinkAsync(user, user.Email!, HtmlEncoder.Default.Encode(link));
        await audit.RecordAsync(invited ? AuditActions.Invited : AuditActions.PasswordResetSent, "User", user.Id, user.Name ?? user.Email,
            note: invited ? "Invite sent again." : null);
        return link;
    }

    /// <summary>
    /// The link that lets <paramref name="user"/> set a password: for an invite, the page that accepts it
    /// (Components/Account/Pages/AcceptInvitation.razor); otherwise the reset page. Both name the account and
    /// carry its reset code.
    /// </summary>
    public async Task<string> SetPasswordLinkAsync(ApplicationUser user)
    {
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await users.GeneratePasswordResetTokenAsync(user)));
        var page = StatusOf(user) == UserStatus.Invited ? "Account/AcceptInvitation" : "Account/ResetPassword";
        return navigation.GetUriWithQueryParameters(navigation.ToAbsoluteUri(page).AbsoluteUri,
            new Dictionary<string, object?> { ["userId"] = user.Id, ["code"] = code });
    }

    public async Task<string?> DeactivateAsync(ApplicationUser user, string? me)
    {
        if (user.Id == me) return "You can't deactivate your own account.";
        if (await IsLastAdminAsync(user)) return OnlyAdmin;
        await users.SetLockoutEnabledAsync(user, true);
        await users.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        await users.UpdateSecurityStampAsync(user);
        await audit.RecordAsync(AuditActions.Deactivated, "User", user.Id, user.Name ?? user.Email);
        return null;
    }

    public async Task ReactivateAsync(ApplicationUser user)
    {
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        await audit.RecordAsync(AuditActions.Reactivated, "User", user.Id, user.Name ?? user.Email);
    }

    public async Task UnlockAsync(ApplicationUser user)
    {
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        await audit.RecordAsync(AuditActions.Unlocked, "User", user.Id, user.Name ?? user.Email);
    }

    /// <summary>Turns two-factor off and forgets their authenticator, so they set it up again at their next login.</summary>
    public async Task ResetTwoFactorAsync(ApplicationUser user)
    {
        await users.SetTwoFactorEnabledAsync(user, false);
        await users.ResetAuthenticatorKeyAsync(user);
        await audit.RecordAsync(AuditActions.TwoFactorReset, "User", user.Id, user.Name ?? user.Email);
    }

    public async Task<string?> DeleteAsync(ApplicationUser user, string? me)
    {
        if (user.Id == me) return "You can't delete your own account.";
        if (await IsLastAdminAsync(user)) return OnlyAdmin;
        var result = await users.DeleteAsync(user);
        if (!result.Succeeded) return string.Join(" ", result.Errors.Select(e => e.Description));
        await audit.RecordAsync(AuditActions.Deleted, "User", user.Id, user.Name ?? user.Email);
        return null;
    }

    private async Task<UserRole> RoleOfAsync(ApplicationUser user) =>
        (await users.GetRolesAsync(user)).Select(r => Enum.TryParse<UserRole>(r, out var role) ? role : UserRole.Viewer)
            .DefaultIfEmpty(UserRole.Viewer).Min();

    private async Task<bool> IsLastAdminAsync(ApplicationUser user) =>
        await users.IsInRoleAsync(user, Roles.Admin)
        && (await users.GetUsersInRoleAsync(Roles.Admin)).Count(u => u.Id != user.Id && StatusOf(u) != UserStatus.Deactivated) == 0;

    // 20000, not the 20000.0 the database hands back: it's what the form's field shows.
    private static decimal? Whole(decimal? amount) => amount is { } a && decimal.Truncate(a) == a ? decimal.Truncate(a) : amount;

    private static string? Amount(decimal? amount) => amount?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
