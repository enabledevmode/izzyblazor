#if (IndividualLocalAuth)
#if (UseServer)
using Microsoft.AspNetCore.Components.Authorization;
#endif
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
#endif
using {{RootNamespace}}.Components;
#if (IndividualLocalAuth)
using {{RootNamespace}}.Components.Account;
using {{RootNamespace}}.Data;
#endif
#if (UseProgramMain)

namespace {{RootNamespace}};

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddIzzyBlazor();
#if (UseServer && UseWebAssembly && IndividualLocalAuth)
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents()
            .AddAuthenticationStateSerialization();
#elif (UseServer && UseWebAssembly)
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents();
#elif (UseServer)
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();
#elif (UseWebAssembly && IndividualLocalAuth)
        builder.Services.AddRazorComponents()
            .AddInteractiveWebAssemblyComponents()
            .AddAuthenticationStateSerialization();
#elif (UseWebAssembly)
        builder.Services.AddRazorComponents()
            .AddInteractiveWebAssemblyComponents();
#else
        builder.Services.AddRazorComponents();
#endif
#if (IndividualLocalAuth)

        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddScoped<IdentityRedirectManager>();
#if (UseServer)
        builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
#endif

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();
#if (!UseServer)
        builder.Services.AddAuthorization();
#endif

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
#if (UseLocalDB)
            options.UseSqlServer(connectionString));
#else
            options.UseSqlite(connectionString));
#endif
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        builder.Services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = true;
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();
#endif

        var app = builder.Build();

        // Configure the HTTP request pipeline.
#if (UseWebAssembly || IndividualLocalAuth)
        if (app.Environment.IsDevelopment())
        {
#if (UseWebAssembly)
            app.UseWebAssemblyDebugging();
#endif
#if (IndividualLocalAuth)
            app.UseMigrationsEndPoint();
#endif
        }
        else
#else
        if (!app.Environment.IsDevelopment())
#endif
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
#if (HasHttpsProfile)
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
#endif
        }
        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
#if (HasHttpsProfile)
        app.UseHttpsRedirection();

#endif
        app.UseAntiforgery();

        app.MapStaticAssets();
#if (UseServer && UseWebAssembly)
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof({{RootNamespace}}.Client._Imports).Assembly);
#elif (UseServer)
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();
#elif (UseWebAssembly)
        app.MapRazorComponents<App>()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof({{RootNamespace}}.Client._Imports).Assembly);
#else
        app.MapRazorComponents<App>();
#endif
#if (IndividualLocalAuth)

        // Add additional endpoints required by the Identity /Account Razor components.
        app.MapAdditionalIdentityEndpoints();
#endif

        app.Run();
    }
}
#else

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddIzzyBlazor();
#if (UseServer && UseWebAssembly && IndividualLocalAuth)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents()
    .AddAuthenticationStateSerialization();
#elif (UseServer && UseWebAssembly)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();
#elif (UseServer)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
#elif (UseWebAssembly && IndividualLocalAuth)
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents()
    .AddAuthenticationStateSerialization();
#elif (UseWebAssembly)
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();
#else
builder.Services.AddRazorComponents();
#endif
#if (IndividualLocalAuth)

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
#if (UseServer)
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
#endif

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();
#if (!UseServer)
builder.Services.AddAuthorization();
#endif

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
#if (UseLocalDB)
    options.UseSqlServer(connectionString));
#else
    options.UseSqlite(connectionString));
#endif
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();
#endif

var app = builder.Build();

// Configure the HTTP request pipeline.
#if (UseWebAssembly || IndividualLocalAuth)
if (app.Environment.IsDevelopment())
{
#if (UseWebAssembly)
    app.UseWebAssemblyDebugging();
#endif
#if (IndividualLocalAuth)
    app.UseMigrationsEndPoint();
#endif
}
else
#else
if (!app.Environment.IsDevelopment())
#endif
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
#if (HasHttpsProfile)
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
#endif
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
#if (HasHttpsProfile)
app.UseHttpsRedirection();

#endif
app.UseAntiforgery();

app.MapStaticAssets();
#if (UseServer && UseWebAssembly)
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof({{RootNamespace}}.Client._Imports).Assembly);
#elif (UseServer)
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
#elif (UseWebAssembly)
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof({{RootNamespace}}.Client._Imports).Assembly);
#else
app.MapRazorComponents<App>();
#endif
#if (IndividualLocalAuth)

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();
#endif

app.Run();
#endif
