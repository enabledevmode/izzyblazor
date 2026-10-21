using System.Globalization;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using IzzyBlazor.Site.Components;
using IzzyBlazor.Site.Services;

namespace IzzyBlazor.Site;

/// <summary>Builds the site's app, so tests can start the same one.</summary>
public static class SiteApp
{
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        // Examples format money and dates as en-US whatever the server's culture is.
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en-US");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ApplicationName = typeof(SiteApp).Assembly.GetName().Name,
        });
        configure?.Invoke(builder);

        // The library's files (_content/IzzyBlazor.Core/...) and blazor.web.js come from the build's
        // static web assets manifest, which ASP.NET Core only reads by itself in Development. Read it
        // in any environment, so `dotnet run` without the launch profile works too; published output
        // has no such manifest and serves its own copies.
        StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();
        builder.Services.AddIzzyBlazor();
        var api = ApiMetadata.Load();
        builder.Services.AddSingleton(api);
        builder.Services.AddSingleton(Registry.Load(api));
        builder.Services.AddSingleton<ExampleCatalog>();

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/error", createScopeForErrors: true);
        }

        app.UseStatusCodePagesWithReExecute("/not-found");
        app.UseAntiforgery();
        app.UseOffFrameLinks();

        // Tests run the app from their own folder, without the static assets manifest.
        var manifest = Path.Combine(AppContext.BaseDirectory, $"{builder.Environment.ApplicationName}.staticwebassets.endpoints.json");
        if (File.Exists(manifest))
        {
            app.MapStaticAssets();
        }

        app.MapGet("/izzyblazor.api.json", (ApiMetadata api) => Results.Text(api.Json, "application/json"));
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();
        app.MapIzzyBlazor();

        return app;
    }
}
