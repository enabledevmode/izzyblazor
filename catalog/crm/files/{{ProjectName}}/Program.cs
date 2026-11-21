using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using {{RootNamespace}}.Admin;
using {{RootNamespace}}.Auditing;
using {{RootNamespace}}.Components;
using {{RootNamespace}}.Components.Account;
using {{RootNamespace}}.Data;
using {{RootNamespace}}.Email;
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

// One upload of a deal's files at their largest (Data/DealAttachment.cs), with room for the form around them:
// more than the server's usual 30 MB a request.
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = DealAttachment.MaxBytes * DealAttachment.MaxFiles + 1_000_000);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
    options.MultipartBodyLengthLimit = DealAttachment.MaxBytes * DealAttachment.MaxFiles + 1_000_000);

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
        // A long password and no rules about what's in it (NIST SP 800-63B). The password fields' hint and
        // their checks follow these (Components/Account/IdentityPasswordAttribute.cs).
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = options.Password.RequireNonAlphanumeric = false;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddRoles<IdentityRole>()
    .AddClaimsPrincipalFactory<NameClaimsPrincipalFactory>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager<AuditingSignInManager>()
    .AddDefaultTokenProviders();

// A cookie is checked against its account on every request, not Identity's every 30 minutes: someone
// deactivated, deleted or given another role is held to it on their next page.
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);

// Invitations and password resets go by email once Email:Host and Email:From name an SMTP server (Email/Mailer.cs).
// Until then nothing is sent, and Settings & team hands the admin each link to pass on.
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddSingleton<Mailer>();
builder.Services.AddScoped<IdentityEmails>();
builder.Services.AddScoped<IEmailSender<ApplicationUser>>(services => services.GetRequiredService<IdentityEmails>());

// The keys that protect login cookies and form tokens, kept in the database, so every instance of the app
// reads the others' and no request has to go back to the one that served the last.
builder.Services.AddDataProtection().PersistKeysToDbContext<ApplicationDbContext>();

var app = builder.Build();

// Makes the database, the roles and the workspace, and on first run the first admin or, with Seed:SampleData on
// (as it is in development), the sample team and its data (Data/Setup.cs).
await app.SetupAsync();

if (!app.Environment.IsDevelopment() && !app.Services.GetRequiredService<Mailer>().Sends)
    app.Logger.LogWarning("No mail is set up, so invitations and password resets aren't emailed: set Email:Host and Email:From (Email/Mailer.cs).");

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

// A file attached to a deal, from the database, for people who may see records: always a download, whatever
// it is, so nothing uploaded opens as a page of this site.
app.MapGet("/deal-attachments/{id:int}", async (int id, ApplicationDbContext db, HttpContext http) =>
{
    if (await db.DealAttachments.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id) is not { } file) return Results.NotFound();
    http.Response.Headers.XContentTypeOptions = "nosniff";
    return Results.File(file.Data, file.ContentType, file.FileName);
}).RequireAuthorization(Policies.ViewRecords);

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();
