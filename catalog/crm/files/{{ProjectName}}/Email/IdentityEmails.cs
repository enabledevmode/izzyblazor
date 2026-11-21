using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using {{RootNamespace}}.Data;

namespace {{RootNamespace}}.Email;

/// <summary>
/// The account emails, in the app's words: an invitation, a password reset, and what Identity's own pages ask
/// for (the account pages send through <see cref="IEmailSender{TUser}"/>, which this is). Each returns whether
/// it was sent (<see cref="Mailer"/>).
/// </summary>
public sealed class IdentityEmails(Mailer mailer, ApplicationDbContext db) : IEmailSender<ApplicationUser>
{
    // Links to set a password work this long: Identity's token lifespan, a day unless Program.cs changes it.
    private const string Expires = "The link works for a day.";

    /// <summary>Invites <paramref name="user"/>: <paramref name="link"/> is where they choose a password.</summary>
    public async Task<bool> InviteAsync(ApplicationUser user, string link)
    {
        var workspace = await WorkspaceAsync();
        return await mailer.SendAsync(user.Email!, $"You’re invited to {workspace}", workspace,
            [Greeting(user), $"You’ve been invited to join {workspace}. Choose a password to get started."],
            "Accept the invitation", link, $"{Expires} If you weren’t expecting this, you can ignore it.");
    }

    /// <summary>Sends <paramref name="user"/> the <paramref name="link"/> that sets a new password.</summary>
    public async Task<bool> ResetAsync(ApplicationUser user, string link)
    {
        var workspace = await WorkspaceAsync();
        return await mailer.SendAsync(user.Email!, $"Reset your {workspace} password", workspace,
            [Greeting(user), "Someone asked to reset the password for your account. Choose a new one to log in again."],
            "Choose a new password", link, $"{Expires} If this wasn’t you, ignore this email: your password stays as it is.");
    }

    // The account pages' calls. Their links arrive encoded for HTML, as Identity's templates send them.
    async Task IEmailSender<ApplicationUser>.SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        // Someone invited who asks for a reset still has no password: what they need is the invitation.
        if (user.PasswordHash is null) await InviteAsync(user, WebUtility.HtmlDecode(resetLink));
        else await ResetAsync(user, WebUtility.HtmlDecode(resetLink));
    }

    async Task IEmailSender<ApplicationUser>.SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        var workspace = await WorkspaceAsync();
        await mailer.SendAsync(email, "Confirm your email address", workspace,
            [Greeting(user), $"Confirm this address to use it for your {workspace} account."],
            "Confirm the address", WebUtility.HtmlDecode(confirmationLink), "If this wasn’t you, you can ignore it.");
    }

    async Task IEmailSender<ApplicationUser>.SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        var workspace = await WorkspaceAsync();
        await mailer.SendAsync(email, $"Reset your {workspace} password", workspace,
            $"{Greeting(user)}\n\nYour password reset code is {resetCode}\n\nIf this wasn’t you, ignore this email: your password stays as it is.\n");
    }

    private async Task<string> WorkspaceAsync() =>
        (await db.Workspaces.AsNoTracking().Select(w => w.Name).FirstOrDefaultAsync()) is { Length: > 0 } name ? name : "the workspace";

    private static string Greeting(ApplicationUser user) => string.IsNullOrWhiteSpace(user.Name) ? "Hello," : $"Hi {user.Name.Split(' ')[0]},";
}
