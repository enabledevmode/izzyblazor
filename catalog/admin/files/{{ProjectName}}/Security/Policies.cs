using Microsoft.AspNetCore.Authorization;

namespace {{RootNamespace}}.Security;

/// <summary>
/// The app's roles. They're set here, in code, not edited in the app: a new role is a constant here, a line in
/// the seeder (Data/SeedData.cs), and its place in the policies below.
/// </summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Viewer = "Viewer";

    public static IReadOnlyList<string> All { get; } = [Admin, Manager, Viewer];

    /// <summary>What each role can do, as the Roles page lists it.</summary>
    public static IReadOnlyList<string> Allows(string role) => role switch
    {
        Admin => ["View customers and invoices", "Create, edit and delete them", "Manage users and their roles", "See the audit log"],
        Manager => ["View customers and invoices", "Create, edit and delete them"],
        Viewer => ["View customers and invoices"],
        _ => [],
    };
}

/// <summary>
/// What pages and actions ask for: <c>@attribute [Authorize(Policy = Policies.EditSales)]</c> on a page, or
/// <c>&lt;AuthorizeView Policy="@Policies.EditSales"&gt;</c> around a button. Change who has a policy here.
/// </summary>
public static class Policies
{
    /// <summary>Users, Roles and the Audit log.</summary>
    public const string ManageUsers = nameof(ManageUsers);

    /// <summary>Creating, editing and deleting customers and invoices.</summary>
    public const string EditSales = nameof(EditSales);

    /// <summary>Seeing customers and invoices.</summary>
    public const string ViewSales = nameof(ViewSales);

    public static AuthorizationBuilder AddAppPolicies(this AuthorizationBuilder builder) => builder
        .AddPolicy(ManageUsers, p => p.RequireRole(Roles.Admin))
        .AddPolicy(EditSales, p => p.RequireRole(Roles.Admin, Roles.Manager))
        .AddPolicy(ViewSales, p => p.RequireRole(Roles.Admin, Roles.Manager, Roles.Viewer));
}
