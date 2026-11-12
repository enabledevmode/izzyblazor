using {{RootNamespace}}.Components;
#if (UseProgramMain)

namespace {{RootNamespace}};

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddIzzyBlazor();
#if (UseServer && UseWebAssembly)
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents();
#elif (UseServer)
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();
#elif (UseWebAssembly)
        builder.Services.AddRazorComponents()
            .AddInteractiveWebAssemblyComponents();
#else
        builder.Services.AddRazorComponents();
#endif

        var app = builder.Build();

        // Configure the HTTP request pipeline.
#if (UseWebAssembly)
        if (app.Environment.IsDevelopment())
        {
            app.UseWebAssemblyDebugging();
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

        app.Run();
    }
}
#else

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddIzzyBlazor();
#if (UseServer && UseWebAssembly)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();
#elif (UseServer)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
#elif (UseWebAssembly)
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();
#else
builder.Services.AddRazorComponents();
#endif

var app = builder.Build();

// Configure the HTTP request pipeline.
#if (UseWebAssembly)
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
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

app.Run();
#endif
