using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using {{RootNamespace}}.Data;

namespace {{RootNamespace}}.Auditing;

/// <summary>
/// Identity's sign-in, with each password, two-factor and recovery-code sign-in written to the audit log, and the
/// user's last sign-in kept. The account pages call SignInManager as they always do; Program.cs registers this one.
/// </summary>
public sealed class AuditingSignInManager(
    UserManager<ApplicationUser> users,
    IHttpContextAccessor http,
    IUserClaimsPrincipalFactory<ApplicationUser> claims,
    IOptions<IdentityOptions> options,
    ILogger<SignInManager<ApplicationUser>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<ApplicationUser> confirmation,
    Audit audit)
    : SignInManager<ApplicationUser>(users, http, claims, options, logger, schemes, confirmation)
{
    public override async Task<SignInResult> PasswordSignInAsync(string userName, string password, bool isPersistent, bool lockoutOnFailure)
    {
        if (await UserManager.FindByNameAsync(userName) is { } user)
            return await PasswordSignInAsync(user, password, isPersistent, lockoutOnFailure);
        await audit.RecordAsync(AuditActions.SignInFailed, note: $"No account for {userName}.");
        return SignInResult.Failed;
    }

    public override async Task<SignInResult> PasswordSignInAsync(ApplicationUser user, string password, bool isPersistent, bool lockoutOnFailure) =>
        await RecordAsync(user, await base.PasswordSignInAsync(user, password, isPersistent, lockoutOnFailure), "password");

    public override async Task<SignInResult> TwoFactorAuthenticatorSignInAsync(string code, bool isPersistent, bool rememberClient)
    {
        var user = await GetTwoFactorAuthenticationUserAsync();
        var result = await base.TwoFactorAuthenticatorSignInAsync(code, isPersistent, rememberClient);
        return user is null ? result : await RecordAsync(user, result, "authenticator code");
    }

    public override async Task<SignInResult> TwoFactorRecoveryCodeSignInAsync(string recoveryCode)
    {
        var user = await GetTwoFactorAuthenticationUserAsync();
        var result = await base.TwoFactorRecoveryCodeSignInAsync(recoveryCode);
        return user is null ? result : await RecordAsync(user, result, "recovery code");
    }

    // A password that needs a second step isn't recorded yet: the second step is.
    async Task<SignInResult> RecordAsync(ApplicationUser user, SignInResult result, string with)
    {
        if (result.RequiresTwoFactor) return result;
        if (result.Succeeded)
        {
            user.LastSignIn = DateTime.UtcNow;
            await UserManager.UpdateAsync(user);
            await audit.RecordAsync(AuditActions.SignedIn, "User", user.Id, user.Name ?? user.Email, note: $"With a {with}.", actor: user);
        }
        else
        {
            var why = result.IsLockedOut ? "The account is locked." : result.IsNotAllowed ? "The account can't sign in yet (email not confirmed)." : $"Wrong {with}.";
            await audit.RecordAsync(AuditActions.SignInFailed, "User", user.Id, user.Name ?? user.Email, note: why, actor: user);
        }
        return result;
    }
}
