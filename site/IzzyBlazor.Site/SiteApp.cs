using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics;
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
    private const string PagesRateLimit = "pages";
    private const string BusyPartition = "busy";

    // A page render with a query string that came through Fly's proxy: the crawlable, endless kind.
    // Not the examples' frames (a page loads many at once), the circuit, the framework's files,
    // MCP, static files (a dot in the last segment), or an error page re-executed for a request
    // already let through.
    private static bool IsQueryPage(HttpContext context)
    {
        var request = context.Request;
        var path = request.Path.Value ?? "";
        return request.QueryString.HasValue
            && request.Headers.ContainsKey("Fly-Client-IP")
            && context.Features.Get<IStatusCodeReExecuteFeature>() is null
            && context.Features.Get<IExceptionHandlerFeature>() is null
            && !request.Path.StartsWithSegments("/frame")
            && !request.Path.StartsWithSegments("/_blazor")
            && !request.Path.StartsWithSegments("/_framework")
            && !request.Path.StartsWithSegments(McpPath)
            && !path[(path.LastIndexOf('/') + 1)..].Contains('.');
    }

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
        // The Chat examples' stand-in model (canned answers, no API key).
        builder.Services.AddSingleton<Microsoft.Extensions.AI.IChatClient, Services.CannedChatClient>();

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
        // A crawler that ignores its 429s is logged once a minute, not on every request.
        var rejectionLog = PartitionedRateLimiter.Create<string, string>(client => RateLimitPartition.GetFixedWindowLimiter(
            client, _ => new FixedWindowRateLimiterOptions { PermitLimit = 1, Window = TimeSpan.FromMinutes(1) }));
        // Assistants call in bursts; this only stops a runaway client.
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, token) =>
            {
                // The fixed windows say when to come back; the global concurrency limit doesn't.
                var busy = !context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var wait);
                context.HttpContext.Response.Headers.RetryAfter = busy ? "10" : ((int)Math.Ceiling(wait.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

                // A page's 429 says so in a line of text, which also keeps the status code pages from
                // re-executing it into the not-found page.
                if (context.HttpContext.Request.Path.StartsWithSegments(McpPath))
                {
                    return ValueTask.CompletedTask;
                }

                var request = context.HttpContext.Request;
                var client = ClientKey(context.HttpContext);
                if (rejectionLog.AttemptAcquire(busy ? BusyPartition : client).IsAcquired)
                {
                    context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(SiteApp))
                        .LogWarning(busy ? "Busy, turned away {Client} ({UserAgent}): {Path}" : "Rate limited {Client} ({UserAgent}): {Path}",
                            client, request.Headers.UserAgent.ToString(), request.Path + request.QueryString);
                }

                context.HttpContext.Response.StatusCode = busy ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
                return new ValueTask(context.HttpContext.Response.WriteAsync(busy ? "Busy. Try again in a few seconds." : "Too many requests. Try again in a minute.", token));
            };
            options.AddPolicy(McpRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
                ClientKey(context), _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1) }));
            // Pages, for crawlers that ignore robots.txt: the examples' query strings (Pagination's
            // ?page=&small=...) make endless addresses, and on one small machine a crawl through them
            // slowed every page for everyone. A reader opens far fewer pages a minute. Not the
            // examples' frames (a page loads many), the circuit's hub, or requests that didn't come
            // through Fly's proxy (tests, dotnet run), or the not-found and error pages re-executed for
            // a request already counted.
            options.AddPolicy(PagesRateLimit, context => context.Request.Headers.ContainsKey("Fly-Client-IP")
                && context.Features.Get<IStatusCodeReExecuteFeature>() is null
                && context.Features.Get<IExceptionHandlerFeature>() is null
                && ClientKey(context) is var client
                && !context.Request.Path.StartsWithSegments("/frame")
                && !context.Request.Path.StartsWithSegments("/_blazor")
                && !context.Request.Path.StartsWithSegments("/_framework")
                    ? RateLimitPartition.GetFixedWindowLimiter(client, _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) })
                    : RateLimitPartition.GetNoLimiter("unlimited"));
            // Pages with a query string, from everyone at once: on Nov 10, 2026 a crawler that ignored
            // robots.txt walked Table's and Pagination's example states from many addresses, each under
            // the limit above; renders piled up on the one shared CPU until the machine ran out of
            // memory. A few render at a time and a short queue waits; the rest get a 503 to retry.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context => IsQueryPage(context)
                ? RateLimitPartition.GetConcurrencyLimiter(BusyPartition, _ => new ConcurrencyLimiterOptions { PermitLimit = 2, QueueLimit = 10, QueueProcessingOrder = QueueProcessingOrder.OldestFirst })
                : RateLimitPartition.GetNoLimiter("unlimited"));
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
        // Crawlers that ignore robots.txt, by a piece of their user agent: BlockedAgents, comma
        // separated (a Fly secret, so a block needs no build). Each client's first page with a query
        // a minute is logged with its user agent, to find them.
        var blocked = (app.Configuration["BlockedAgents"] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        // And by network, for one that passes as a browser from many addresses (Nov 10, 2026: a
        // cloud provider's 47.79.0.0/16): BlockedNetworks, comma separated CIDRs.
        var networks = (app.Configuration["BlockedNetworks"] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(n => System.Net.IPNetwork.TryParse(n, out var net) ? net : (System.Net.IPNetwork?)null)
            .OfType<System.Net.IPNetwork>()
            .ToArray();
        var queryLog = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Crawlers");
        app.Use((context, next) =>
        {
            var agent = context.Request.Headers.UserAgent.ToString();
            var from = IPAddress.TryParse(context.Request.Headers["Fly-Client-IP"].ToString(), out var fly) ? fly : null;
            if ((blocked.Length > 0 && blocked.Any(b => agent.Contains(b, StringComparison.OrdinalIgnoreCase)))
                || (from is not null && networks.Any(n => n.Contains(from.IsIPv4MappedToIPv6 ? from.MapToIPv4() : from))))
            {
                // With a body, so the status code pages don't render the not-found page for it.
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "text/plain; charset=utf-8";
                return context.Response.WriteAsync("Crawling these pages isn't allowed; see /robots.txt.");
            }

            if (IsQueryPage(context) && ClientKey(context) is var client && rejectionLog.AttemptAcquire("q " + client).IsAcquired)
            {
                queryLog.LogInformation("Query page {Client} ({UserAgent}): {Path}", client, agent, context.Request.Path + context.Request.QueryString);
            }

            return next(context);
        });
        app.UseRateLimiter();

        // A link that opens a page in a mode (?mode=noscript, the mode switch) keeps it for the next
        // pages, with JavaScript off too: an example's own links and forms drop ?mode= from the
        // address, and the page has to stay in its mode.
        app.Use((context, next) =>
        {
            if (context.Request.Query["mode"].ToString() is "noscript" or "static" or "interactive" && !context.Request.Path.StartsWithSegments("/frame"))
            {
                var mode = context.Request.Query["mode"].ToString();
                if (context.Request.Cookies[ModeSwitch.Cookie] != mode)
                {
                    context.Response.Cookies.Append(ModeSwitch.Cookie, mode, new CookieOptions
                    {
                        Path = context.Request.PathBase.HasValue ? context.Request.PathBase.Value + "/" : "/",
                        // No script is the hidden mode, for checking: kept for the visit only, so it doesn't
                        // greet the reader next time.
                        MaxAge = mode == "noscript" ? null : TimeSpan.FromDays(365),
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
        // The DataGrid export example's downloads.
        app.MapGet("/examples/orders.csv", (HttpContext context) => Results.Stream(
                stream => DataExport.WriteCsvAsync(stream, Examples.Table.Orders.All.AsQueryable(), context.Request.QueryString.Value, DataFields.Of<Examples.Table.Order>(), cancellationToken: context.RequestAborted),
                "text/csv", "orders.csv"))
            .RequireRateLimiting(PagesRateLimit);
        app.MapGet("/examples/orders.xlsx", (HttpContext context) => Results.Stream(
                stream => DataExport.WriteExcelAsync(stream, Examples.Table.Orders.All.AsQueryable(), context.Request.QueryString.Value, DataFields.Of<Examples.Table.Order>(), "Orders", cancellationToken: context.RequestAborted),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "orders.xlsx"))
            .RequireRateLimiting(PagesRateLimit);
        // A format of the site's own, for the example with DataGridExportFormat: an array of objects by caption.
        app.MapGet("/examples/orders.json", (HttpContext context) => Results.Stream(
                async stream =>
                {
                    var export = DataExport.Read(Examples.Table.Orders.All.AsQueryable(), context.Request.QueryString.Value, DataFields.Of<Examples.Table.Order>(), context.RequestAborted);
                    await using var json = new System.Text.Json.Utf8JsonWriter(stream, new() { Indented = true });
                    json.WriteStartArray();
                    await foreach (var row in export.Rows)
                    {
                        json.WriteStartObject();
                        for (var i = 0; i < row.Length; i++)
                        {
                            json.WritePropertyName(export.Columns[i].Key);
                            // To an element first: Serialize would flush the response synchronously, which Kestrel refuses.
                            System.Text.Json.JsonSerializer.SerializeToElement(row[i] is Enum e ? e.ToString() : row[i]).WriteTo(json);
                        }

                        json.WriteEndObject();
                        await json.FlushAsync(context.RequestAborted);
                    }

                    json.WriteEndArray();
                },
                "application/json", "orders.json"))
            .RequireRateLimiting(PagesRateLimit);
        // Accessibility was one article under Common features until Nov 10, 2026; it's a section now.
        app.MapGet("/docs/common/accessibility", () => Results.Redirect("/docs/accessibility/overview", permanent: true));
        // Chart types that were topics on Chart's pages until Nov 7, 2026 have pages of their own.
        foreach (var (topic, page) in new[] { ("bars", "bar-chart"), ("lines", "line-chart"), ("scatter", "scatter-chart") })
        {
            app.MapGet($"/docs/components/chart/{topic}", () => Results.Redirect($"/docs/components/{page}", permanent: true));
            app.MapGet($"/docs/components/chart/{topic}/api", () => Results.Redirect($"/docs/components/{page}/api", permanent: true));
        }

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
            .AddInteractiveServerRenderMode()
            .RequireRateLimiting(PagesRateLimit);

        return app;
    }
}
