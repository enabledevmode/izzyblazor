using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using {{RootNamespace}}.Admin;
using {{RootNamespace}}.Auditing;
using {{RootNamespace}}.Components;
using {{RootNamespace}}.Components.Account;
using {{RootNamespace}}.Data;
using {{RootNamespace}}.Security;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddIzzyBlazor();
builder.Services.AddRazorComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

// Who can do what: the roles and their policies, in Security/Policies.cs.
builder.Services.AddAuthorizationBuilder().AddAppPolicies();

// Every change to an audited record is written to the audit log as it's saved (Auditing/).
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<AuditInterceptor>();
builder.Services.AddScoped<Audit>();

// What admins do to accounts: invite, roles, deactivate, delete (Admin/UserAdmin.cs).
builder.Services.AddScoped<UserAdmin>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>((services, options) =>
    options.UseSqlite(connectionString).AddInterceptors(services.GetRequiredService<AuditInterceptor>()));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddRoles<IdentityRole>()
    .AddClaimsPrincipalFactory<NameClaimsPrincipalFactory>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager<AuditingSignInManager>()
    .AddDefaultTokenProviders();

// Sends no mail: it logs each link. Swap in your own IEmailSender<ApplicationUser> to send invites and resets.
builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

// The keys that protect sign-in cookies and form tokens, kept in the database, so every instance of the app
// reads the others' and no request has to go back to the one that served the last.
builder.Services.AddDataProtection().PersistKeysToDbContext<ApplicationDbContext>();

var app = builder.Build();

// Creates the database on first run and fills it with sample data (Data/SeedData.cs).
await app.SeedAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>();

// A company's logo, from the database, for people who may see records. Its address changes with the logo
// (Company.LogoSrc), so the browser may keep the picture for a year.
app.MapGet("/company-logos/{id:int}", async (int id, ApplicationDbContext db, HttpContext http) =>
{
    if (await db.CompanyLogos.AsNoTracking().FirstOrDefaultAsync(l => l.CompanyId == id) is not { } logo) return Results.NotFound();
    http.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
    http.Response.Headers.XContentTypeOptions = "nosniff";
    return Results.File(logo.Data, logo.ContentType);
}).RequireAuthorization(Policies.ViewRecords);

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();
