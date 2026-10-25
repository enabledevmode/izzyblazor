using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.HttpResults;
using IzzyBlazor.Site.Components;
using IzzyBlazor.Site.Components.Parts;
using IzzyBlazor.Site.Services;
using IzzyBlazor.Site.Services.Mcp;

namespace IzzyBlazor.Site;

/// <summary>Builds the site's app, so tests can start the same one.</summary>
public static class SiteApp
{
    private const string McpPath = "/mcp";
    private const string McpRateLimit = "mcp";

    // The caller's address: Fly's proxy gives it in Fly-Client-IP. IPv6 by its /64, the block one
    // client usually has, so changing addresses inside it doesn't reset the limit.
    private static string ClientKey(HttpContext context)
    {
        var address = IPAddress.TryParse(context.Request.Headers["Fly-Client-IP"].ToString(), out var fly) ? fly : context.Connection.RemoteIpAddress;
        if (address is null)
        {
            return "";
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return $"{new IPAddress(bytes)}/64";
    }

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
        var registry = Registry.Load(api);
        builder.Services.AddSingleton(registry);
        builder.Services.AddSingleton(new SearchIndex(registry, api));
        builder.Services.AddSingleton<NoScriptCss>();
        builder.Services.AddSingleton<ExampleCatalog>();

        // The MCP server at /mcp (docs/get-started/ai-assistants): read-only tools over the same
        // data, stateless so any of Fly's machines can answer any request.
        builder.Services.AddSingleton<PageText>();
        builder.Services.AddSingleton<McpDocs>();
        builder.Services.AddMcpServer(options =>
            {
                options.ServerInfo = new() { Name = "izzyblazor", Title = "IzzyBlazor docs", Version = api.Version };
                options.ServerInstructions = McpTools.Instructions.ReplaceLineEndings(" ");
            })
            .WithHttpTransport(options => options.Stateless = true)
            .WithTools<McpTools>();
        // Assistants call in bursts; this only stops a runaway client.
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var wait))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(wait.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                return ValueTask.CompletedTask;
            };
            options.AddPolicy(McpRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
                ClientKey(context), _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1) }));
        });

        var app = builder.Build();

        // Fly ends TLS and forwards plain HTTP with X-Forwarded-Proto, so without this every
        // absolute address the site writes (og:image) would be http://. Fly's proxy has no fixed
        // address to trust.
        var forwarded = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedProto };
        forwarded.KnownIPNetworks.Clear();
        forwarded.KnownProxies.Clear();
        app.UseForwardedHeaders(forwarded);
        app.UseCanonicalHost();

        // The site's error pages are HTML; /mcp answers its client as it is (a 429 stays a 429).
        app.UseWhen(context => !context.Request.Path.StartsWithSegments(McpPath), site =>
        {
            if (!app.Environment.IsDevelopment())
            {
                site.UseExceptionHandler("/error", createScopeForErrors: true);
            }

            site.UseStatusCodePagesWithReExecute("/not-found");
        });
        app.UseAntiforgery();
        app.UseRateLimiter();

        // A link that opens a page in a mode (?mode=off, the mode switch) keeps it for the next
        // pages, with JavaScript off too: an example's own links and forms drop ?mode= from the
        // address, and the page has to stay in its mode.
        app.Use((context, next) =>
        {
            if (context.Request.Query["mode"].ToString() is "off" or "static" or "live" && !context.Request.Path.StartsWithSegments("/frame"))
            {
                var mode = context.Request.Query["mode"].ToString();
                if (context.Request.Cookies[ModeSwitch.Cookie] != mode)
                {
                    context.Response.Cookies.Append(ModeSwitch.Cookie, mode, new CookieOptions
                    {
                        Path = context.Request.PathBase.HasValue ? context.Request.PathBase.Value + "/" : "/",
                        MaxAge = TimeSpan.FromDays(365),
                        SameSite = SameSiteMode.Lax,
                        IsEssential = true,
                    });
                }
            }

            return next(context);
        });
        app.UseOffFrameLinks();
        app.UseOffExampleAnchors();

        // Tests run the app from their own folder, without the static assets manifest.
        var manifest = Path.Combine(AppContext.BaseDirectory, $"{builder.Environment.ApplicationName}.staticwebassets.endpoints.json");
        if (File.Exists(manifest))
        {
            app.MapStaticAssets();
        }

        app.MapMethods("/" + NoScriptCss.Path, ["GET", "HEAD"], (NoScriptCss css, HttpContext context) =>
        {
            if (css.File is not { } file)
            {
                return Results.NotFound();
            }

            if (context.Request.Query["v"] == file.Version)
            {
                context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            }

            return Results.Text(file.Css, "text/css");
        });
        // The search dialog's results as you type (site.js): the same markup the header renders.
        app.MapGet("/search/results", (string? q, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "public, max-age=300";
            return new RazorComponentResult<SearchDialogResults>(new { Q = q });
        });
        app.MapMethods("/robots.txt", ["GET", "HEAD"], () => Results.Text(Sitemap.Robots, "text/plain"));
        app.MapMethods("/sitemap.xml", ["GET", "HEAD"], (Registry registry) => Results.Text(Sitemap.Xml(registry), "application/xml"));
        app.MapMethods("/izzyblazor.api.json", ["GET", "HEAD"], (ApiMetadata api) => Results.Text(api.Json, "application/json"));
        app.MapMethods("/llms.txt", ["GET", "HEAD"], (Registry registry, ApiMetadata api) => Results.Text(LlmsTxt.Of(registry, api), "text/plain; charset=utf-8"));
        app.MapMcp(McpPath).RequireRateLimiting(McpRateLimit);
        // Stateless: no event stream to GET and no session to DELETE, which the spec answers with 405.
        app.MapMethods(McpPath, ["GET", "HEAD", "DELETE"], (HttpContext context) =>
        {
            context.Response.Headers.Allow = "POST";
            return Results.StatusCode(StatusCodes.Status405MethodNotAllowed);
        });
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();
        app.MapIzzyBlazor();

        return app;
    }
}
