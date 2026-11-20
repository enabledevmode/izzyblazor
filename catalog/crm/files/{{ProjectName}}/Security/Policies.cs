using Microsoft.AspNetCore.Authorization;

namespace {{RootNamespace}}.Security;

/// <summary>
/// The app's roles. They're set here, in code, not edited in the app: a new role is a constant here, a line in
/// the seeder (Data/SeedData.cs), its place in <see cref="Policies"/> and a member of the team list's UserRole.
/// </summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Viewer = "Viewer";

    public static IReadOnlyList<string> All { get; } = [Admin, Manager, Viewer];

    /// <summary>A role in a line, as the member form puts it under each choice (Components/Settings/MemberDialog.razor).</summary>
    public static string Summary(string role) => role switch
    {
        Admin => "Manage customer records, team access, and workspace settings.",
        Manager => "Add and update contacts, companies, deals, and activities.",
        Viewer => "View customer records and reports without making changes.",
        _ => "",
    };
}

/// <summary>
/// What pages and actions ask for: <c>@attribute [Authorize(Policy = Policies.EditRecords)]</c> on a page, or
/// <c>&lt;AuthorizeView Policy="@Policies.EditRecords"&gt;</c> around a button. Change who has a policy in
/// <see cref="All"/>: the app enforces it from that line.
/// </summary>
public static class Policies
{
    /// <summary>Seeing contacts, companies, deals, activities and reports.</summary>
    public const string ViewRecords = nameof(ViewRecords);

    /// <summary>Creating, editing and deleting contacts, companies, deals, activities and notes.</summary>
    public const string EditRecords = nameof(EditRecords);

    /// <summary>Settings &amp; team: the members, the workspace and its settings.</summary>
    public const string ManageUsers = nameof(ManageUsers);

    /// <summary>The audit log.</summary>
    public const string ViewAudit = nameof(ViewAudit);

    /// <summary>Each policy: what it lets people do, and the roles that have it.</summary>
    public static IReadOnlyList<(string Name, string Allows, string[] Roles)> All { get; } =
    [
        (ViewRecords, "View contacts, companies, deals and activities", [Security.Roles.Admin, Security.Roles.Manager, Security.Roles.Viewer]),
        (EditRecords, "Create, edit and delete contacts, companies, deals and activities", [Security.Roles.Admin, Security.Roles.Manager]),
        (ManageUsers, "Manage users and their roles", [Security.Roles.Admin]),
        (ViewAudit, "See the audit log", [Security.Roles.Admin]),
    ];

    public static AuthorizationBuilder AddAppPolicies(this AuthorizationBuilder builder)
    {
        foreach (var (name, _, roles) in All)
            builder.AddPolicy(name, p => p.RequireRole(roles));
        return builder;
    }
}
