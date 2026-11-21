using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using {{RootNamespace}}.Security;

namespace {{RootNamespace}}.Data;

/// <summary>
/// What the app needs before its first request, on every start: the database (the migrations applied), the roles
/// and the workspace's row. Then, while there are no accounts, either the sample data (Seed:SampleData on:
/// Data/SampleData.cs) or the first account, an admin, who invites everyone else:
/// <code>
/// Admin__Email=you@example.com     the first admin's email address (needed)
/// Admin__Name="Your Name"          their name; the email's first part without it
/// Admin__Password=…                their password; without it one is made up and written to the log, once
/// Workspace__Name="Acme Sales"     the workspace's name ({{ProjectName}} without it); Settings changes it later
/// </code>
/// </summary>
public static class Setup
{
    public static async Task SetupAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        // A role added to Roles.All is made on the next start.
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
            if (!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new IdentityRole(role));

        if (await db.Users.AnyAsync()) return;

        // Nothing here is a person's change: the audit log starts with the first sign-in.
        db.Auditing = false;
        var workspace = await db.Workspaces.FirstOrDefaultAsync();
        if (workspace is null)
        {
            workspace = new Workspace { Name = app.Configuration["Workspace:Name"] is { Length: > 0 } name ? name : "{{ProjectName}}" };
            db.Workspaces.Add(workspace);
        }

        if (app.Configuration.GetValue<bool>("Seed:SampleData"))
        {
            await SampleData.AddAsync(services, app.Configuration, db, workspace);
            return;
        }

        await db.SaveChangesAsync();
        db.Auditing = true;

        // The first account. There's no Register page: without it no one could get in.
        var admin = app.Configuration.GetSection("Admin");
        if (admin["Email"]?.Trim() is not { Length: > 0 } email)
            throw new InvalidOperationException(
                "No one can log in yet. Set Admin:Email (the environment variable Admin__Email) to the first admin's email address and start again, or turn Seed:SampleData on for the sample team.");

        var log = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(Setup));
        var password = admin["Password"];
        var made = string.IsNullOrEmpty(password);
        if (made) password = NewPassword();

        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email, Email = email, EmailConfirmed = true,
            Name = admin["Name"] is { Length: > 0 } given ? given : email.Split('@')[0],
        };
        var result = await users.CreateAsync(user, password!);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Couldn't make the first admin, {email}: {string.Join(" ", result.Errors.Select(e => e.Description))}");
        await users.AddToRoleAsync(user, Roles.Admin);

        if (made) log.LogWarning("Made the first admin. Log in as {Email} with {Password}, then change it: this is the only time it's shown.", email, password);
        else log.LogInformation("Made the first admin, {Email}.", email);
    }

    /// <summary>A password to hand out once: 16 letters and digits that can't be mistaken for each other, and what any rule asks for.</summary>
    public static string NewPassword() =>
        RandomNumberGenerator.GetString("ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789", 16) + "-1a";
}
