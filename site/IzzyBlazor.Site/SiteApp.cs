using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Memory;
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
    private const string ReadersPartition = "readers";
    private const string VisitorsPartition = "visitors";

    // Whether the browser says the request came from one of the site's own pages: Sec-Fetch-Site
    // (every current browser), else a Referer on this host (older Safari).
    private static bool FromThisSite(HttpRequest request)
    {
        var site = request.Headers["Sec-Fetch-Site"].ToString();
        if (site.Length > 0)
        {
            return site == "same-origin";
        }

        return Uri.TryCreate(request.Headers.Referer.ToString(), UriKind.Absolute, out var referer)
            && string.Equals(referer.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase);
    }

    // What an example's frame is asked for with by the page that shows it: its render mode and
    // whether it draws a scrollbar. Anything else in its query is a state of the example.
    private static bool IsFrameSetting(string name) => name is "mode" or "scrollbar";

    /// <summary>
    /// An example's frame asked for in one of its states (a page of its table, a day of its
    /// calendar): as endless as a page's query string, and walked the same way (Nov 24, 2026).
    /// </summary>
    internal static bool IsStateFrame(HttpRequest request) =>
        request.Path.StartsWithSegments("/frame") && request.Query.Keys.Any(name => !IsFrameSetting(name));

    // A page render with a query string that came through Fly's proxy: the crawlable, endless kind.
    // An example's frame only in one of its states (a page loads many frames at once, each with
    // its mode). Not the circuit, the framework's files, MCP, static files (a dot in the last
    // segment), or an error page re-executed for a request already let through.
    private static bool IsQueryPage(HttpContext context)
    {
        var request = context.Request;
        var path = request.Path.Value ?? "";
        return request.QueryString.HasValue
            && request.Headers.ContainsKey("Fly-Client-IP")
            && context.Features.Get<IStatusCodeReExecuteFeature>() is null
            && context.Features.Get<IExceptionHandlerFeature>() is null
            && (!request.Path.StartsWithSegments("/frame") || IsStateFrame(request))
            && !request.Path.StartsWithSegments("/_blazor")
            && !request.Path.StartsWithSegments("/_framework")
            && !request.Path.StartsWithSegments("/search/results")
            && !request.Path.StartsWithSegments(McpPath)
            && !IsNewsletterAnswer(request)
            && !path[(path.LastIndexOf('/') + 1)..].Contains('.');
    }

    // Where a newsletter post lands (?answer=sent, with the short-lived cookie the post set): a reader who just
    // subscribed, not a crawl of query strings, so it doesn't wait in the crawlers' queue.
    private static bool IsNewsletterAnswer(HttpRequest request) =>
        request.Query.Count == 1 && request.Query.ContainsKey("answer") && request.Cookies.ContainsKey(Components.Parts.Newsletter.AddressCookie);

    // The site's antiforgery cookie, which every page it renders sets: a reader who has been here.
    private static bool HasAntiforgeryCookie(HttpRequest request) =>
        request.Cookies.Keys.Any(k => k.StartsWith(".AspNetCore.Antiforgery.", StringComparison.Ordinal));

    // Any page render (frames too) that came through Fly's proxy: not the circuit, the framework's
    // files, MCP, static files or the search box's results. An error or not-found page re-executed
    // for a request counts only when asked (the render limiter: its slot was given back by then;
    // not FirstVisitCache, which would keep the not-found page as the path's copy).
    internal static bool IsPageRender(HttpContext context, bool reExecuted = false)
    {
        var request = context.Request;
        var path = request.Path.Value ?? "";
        return request.Headers.ContainsKey("Fly-Client-IP")
            && (reExecuted || (context.Features.Get<IStatusCodeReExecuteFeature>() is null
                && context.Features.Get<IExceptionHandlerFeature>() is null))
            && !request.Path.StartsWithSegments("/_blazor")
            && !request.Path.StartsWithSegments("/_framework")
            && !request.Path.StartsWithSegments("/search/results")
            && !request.Path.StartsWithSegments(McpPath)
            && !path[(path.LastIndexOf('/') + 1)..].Contains('.');
    }

    // A path on this site to send a post back to: plain characters only, so nothing in it can be read as
    // another site's address or refused by the redirect.
    private static bool IsPlainPath(string path) =>
        path.Length is > 0 and <= 200 && path[0] == '/' && !path.Contains("//", StringComparison.Ordinal)
        && path.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '/' or '-' or '_' or '.');

    // Cloudflare's addresses (cloudflare.com/ips-v4 and ips-v6, Nov 14, 2026): a request from one of
    // them came through Cloudflare's proxy, which gives the visitor in CF-Connecting-IP.
    private static readonly System.Net.IPNetwork[] Cloudflare = [.. new[]
    {
        "173.245.48.0/20", "103.21.244.0/22", "103.22.200.0/22", "103.31.4.0/22", "141.101.64.0/18",
        "108.162.192.0/18", "190.93.240.0/20", "188.114.96.0/20", "197.234.240.0/22", "198.41.128.0/17",
        "162.158.0.0/15", "104.16.0.0/13", "104.24.0.0/14", "172.64.0.0/13", "131.0.72.0/22",
        "2400:cb00::/32", "2606:4700::/32", "2803:f800::/32", "2405:b500::/32", "2405:8100::/32",
        "2a06:98c0::/29", "2c0f:f248::/32",
    }.Select(System.Net.IPNetwork.Parse)];

    // The visitor's address: Fly's proxy gives its caller in Fly-Client-IP; when that caller is
    // Cloudflare, the visitor is in CF-Connecting-IP (trusted only from Cloudflare's addresses, as
    // anyone can send the header). Without Fly, the connection's own address.
    internal static IPAddress? ClientAddress(HttpContext context)
    {
        if (!IPAddress.TryParse(context.Request.Headers["Fly-Client-IP"].ToString(), out var caller))
        {
            return Unmapped(context.Connection.RemoteIpAddress);
        }

        caller = Unmapped(caller)!;
        return Cloudflare.Any(n => n.Contains(caller))
            && IPAddress.TryParse(context.Request.Headers["CF-Connecting-IP"].ToString(), out var visitor)
            ? Unmapped(visitor) : caller;
    }

    // Whether Fly's caller is Cloudflare: the request came through the proxy, not straight to Fly.
    internal static bool ViaCloudflare(HttpContext context) =>
        IPAddress.TryParse(context.Request.Headers["Fly-Client-IP"].ToString(), out var caller)
        && Cloudflare.Any(n => n.Contains(Unmapped(caller)!));

    private static IPAddress? Unmapped(IPAddress? address) =>
        address is { IsIPv4MappedToIPv6: true } ? address.MapToIPv4() : address;

    // The caller's key for limits: its address, IPv6 by its /64, the block one client usually has,
    // so changing addresses inside it doesn't reset the limit.
    private const long McpMaxBody = 256 * 1024;

    internal static string ClientKey(HttpContext context)
    {
        var address = ClientAddress(context);
        if (address is null)
        {
            return "";
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
        // The blocks' manifests and templates, copied beside the site from catalog/ (see the .csproj).
        builder.Services.AddSingleton(new BlockCatalog(Path.Combine(AppContext.BaseDirectory, "catalog")));
        builder.Services.AddSingleton(new TemplateCatalog(Path.Combine(AppContext.BaseDirectory, "catalog")));
        // The Chat examples' stand-in model (canned answers, no API key).
        builder.Services.AddSingleton<Microsoft.Extensions.AI.IChatClient, Services.CannedChatClient>();

        // The MCP server at /mcp (docs/tools/ai-assistants): read-only tools over the same
        // data, stateless so any of Fly's machines can answer any request.
        builder.Services.AddSingleton<PageText>();
        builder.Services.AddSingleton<McpDocs>();
        // Agent reports (report_gap): posted as GitHub discussions when AgentReports:Token is set,
        // switched off without it, so a deploy with no token is safe.
        builder.Services.AddHttpContextAccessor();
        builder.Services.Configure<AgentReportOptions>(builder.Configuration.GetSection("AgentReports"));
        builder.Services.AddHttpClient(GitHubDiscussionsReportSink.ClientName, client => client.Timeout = TimeSpan.FromSeconds(15));
        builder.Services.AddSingleton<FormLimit>();
        builder.Services.AddHttpClient<AccountService>(client =>
        {
            client.BaseAddress = new Uri((builder.Configuration["Account:Service"] ?? AccountService.Address).TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(10);
            // The token this site and the account service share (the Fly secret Account__SiteToken on both):
            // the service takes a visitor's address from this site only.
            if (builder.Configuration["Account:SiteToken"] is { Length: > 0 } token)
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        });
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<AgentReportLimiter>();
        builder.Services.AddSingleton<GitHubDiscussionsReportSink>();
        builder.Services.AddSingleton<IAgentReportSink>(services =>
            string.IsNullOrWhiteSpace(services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AgentReportOptions>>().Value.Token)
                ? new DisabledReportSink()
                : services.GetRequiredService<GitHubDiscussionsReportSink>());
        builder.Services.AddMcpServer(options =>
            {
                options.ServerInfo = new() { Name = "izzyblazor", Title = "IzzyBlazor docs", Version = api.Version };
                options.ServerInstructions = McpTools.Instructions.ReplaceLineEndings(" ");
            })
            .WithHttpTransport(options => options.Stateless = true)
            .WithTools<McpTools>()
            .WithTools<McpFeedbackTools>();
        // A crawler that ignores its 429s is logged once a minute, not on every request.
        var rejectionLog = PartitionedRateLimiter.Create<string, string>(client => RateLimitPartition.GetFixedWindowLimiter(
            client, _ => new FixedWindowRateLimiterOptions { PermitLimit = 1, Window = TimeSpan.FromMinutes(1) }));
        // Who was just sent from a page with a query string to the page without it (the redirect
        // below), by client and page, for two minutes. Their render of the plain page waits in the
        // same short queue as query pages: on Nov 14, 2026 a crawler from many addresses followed
        // each redirect to DataGrid's and Table's pages, seconds of render each, and those renders,
        // unlimited, starved the machine (75-second waits, the home page timing out).
        var redirected = new MemoryCache(new MemoryCacheOptions { SizeLimit = 50_000 });
        bool WasRedirected(HttpContext context) =>
            context.Request.Headers.ContainsKey("Fly-Client-IP")
            && redirected.TryGetValue(ClientKey(context) + " " + context.Request.Path, out _);
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
            // Every other page render waits for a slot too, so no crawl, whatever it asks for, can
            // swamp the one shared CPU: a first visit from outside (no reader's pass or antiforgery
            // cookie of the site's, and not answered by FirstVisitCache) has 2 slots,
            // which a crawler from many addresses fills first; readers already on the site have 6 of
            // their own. The queues are short, so a long wait becomes a quick 503 with Retry-After.
            // (A crawler can claim to be a reader; the slots still bound the CPU: 2 + 2 + 6.)
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                IsQueryPage(context) || WasRedirected(context)
                    ? RateLimitPartition.GetConcurrencyLimiter(BusyPartition, _ => new ConcurrencyLimiterOptions { PermitLimit = 2, QueueLimit = 10, QueueProcessingOrder = QueueProcessingOrder.OldestFirst })
                : !IsPageRender(context, reExecuted: true)
                    ? RateLimitPartition.GetNoLimiter("unlimited")
                : ReaderPass.Read(context.Request) is not null || HasAntiforgeryCookie(context.Request)
                    ? RateLimitPartition.GetConcurrencyLimiter(ReadersPartition, _ => new ConcurrencyLimiterOptions { PermitLimit = 6, QueueLimit = 40, QueueProcessingOrder = QueueProcessingOrder.OldestFirst })
                    : RateLimitPartition.GetConcurrencyLimiter(VisitorsPartition, _ => new ConcurrencyLimiterOptions { PermitLimit = 2, QueueLimit = 20, QueueProcessingOrder = QueueProcessingOrder.OldestFirst }));
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
            // A missing file (a dot in the last segment: scanners' /wp-login.php, /.env) gets a bare
            // 404, not the not-found page, whose re-executed render no limiter sees.
            site.Use((context, next) =>
            {
                var path = context.Request.Path.Value ?? "";
                if (path[(path.LastIndexOf('/') + 1)..].Contains('.') && context.Features.Get<IStatusCodePagesFeature>() is { } pages)
                {
                    pages.Enabled = false;
                }

                return next(context);
            });
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
        // How many states of the examples one reader's pass opens a minute: a click every second.
        var states = PartitionedRateLimiter.Create<string, string>(pass => RateLimitPartition.GetFixedWindowLimiter(
            pass, _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) }));
        // With RequireCloudflare (a Fly secret, "true"), the site's own name answers only through
        // Cloudflare: a crawler that calls Fly's address straight, skipping Cloudflare's rules, gets
        // a short 403 (Nov 14, 2026). izzyblazor.fly.dev and Fly's health checks still go straight.
        var requireCloudflare = string.Equals(app.Configuration["RequireCloudflare"], "true", StringComparison.OrdinalIgnoreCase);
        app.Use((context, next) =>
        {
            var agent = context.Request.Headers.UserAgent.ToString();
            if (requireCloudflare && context.Request.Headers.ContainsKey("Fly-Client-IP") && !ViaCloudflare(context)
                && (string.Equals(context.Request.Host.Host, CanonicalHost.Host, StringComparison.OrdinalIgnoreCase)
                    || CanonicalHost.Target(context.Request.Host.Host, "/") is not null))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "text/plain; charset=utf-8";
                return context.Response.WriteAsync("Reach izzyblazor.com by its name.");
            }

            var from = context.Request.Headers.ContainsKey("Fly-Client-IP") ? ClientAddress(context) : null;
            if ((blocked.Length > 0 && blocked.Any(b => agent.Contains(b, StringComparison.OrdinalIgnoreCase)))
                || (from is not null && networks.Any(n => n.Contains(from))))
            {
                // With a body, so the status code pages don't render the not-found page for it.
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "text/plain; charset=utf-8";
                return context.Response.WriteAsync("Crawling these pages isn't allowed; see /robots.txt.");
            }

            var state = IsQueryPage(context);
            var reading = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method);
            var pass = ReaderPass.Read(context.Request);
            if (state && ClientKey(context) is var client && rejectionLog.AttemptAcquire("q " + client).IsAcquired)
            {
                // With what it showed for itself, to see how the next crawler passes.
                queryLog.LogInformation("Query page {Client} {Via} ({UserAgent}; {Signs}): {Path}", client, ViaCloudflare(context) ? "via Cloudflare" : "direct", agent,
                    $"{(FromThisSite(context.Request) ? "from the site" : "from outside")}, {(pass is null ? "no pass" : "pass")}", context.Request.Path + context.Request.QueryString);
            }

            // Every page gives its reader a pass (ReaderPass), which the examples' states ask for below.
            if (!state && reading && pass is null && IsPageRender(context) && !context.Request.Path.StartsWithSegments("/frame"))
            {
                ReaderPass.Give(context);
            }

            // A page with a query string opened from outside the site goes to the page without it.
            // Readers reach the examples' states by clicking on the site, which the browser marks
            // (Sec-Fetch-Site, or a Referer from here); a crawler asks for them straight, and each
            // is a fresh render of an endless set (Nov 13, 2026: DataGrid's states, 13 to 15 seconds
            // a page on the shared CPU). A link with a state pasted from elsewhere opens the plain page.
            // The mark is a header anyone can send (Nov 24, 2026: a crawler did, from a new address
            // for every request), so a state also asks for the pass a page of the site gave its
            // reader. A frame goes to itself as its page shows it.
            if (state && reading && (pass is null || !FromThisSite(context.Request)))
            {
                redirected.Set(ClientKey(context) + " " + context.Request.Path, true,
                    new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2) });
                var settings = context.Request.Path.StartsWithSegments("/frame")
                    ? QueryString.Create(context.Request.Query.Where(q => IsFrameSetting(q.Key)))
                    : QueryString.Empty;
                // Absolute, on this host: a path such as //evil.example/ would otherwise be read as
                // another site's address.
                context.Response.Redirect(UriHelper.BuildAbsolute(context.Request.Scheme, context.Request.Host, context.Request.PathBase, context.Request.Path, settings));
                return Task.CompletedTask;
            }

            // One pass opens the states one reader would: a crawler that keeps its pass is one
            // visitor, whatever addresses it comes from.
            if (state && reading && pass is not null && !states.AttemptAcquire(pass).IsAcquired)
            {
                if (rejectionLog.AttemptAcquire("p " + pass).IsAcquired)
                {
                    queryLog.LogWarning("One pass, too many states {Client} ({UserAgent}): {Path}", ClientKey(context), agent, context.Request.Path + context.Request.QueryString);
                }

                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.Headers.RetryAfter = "60";
                context.Response.ContentType = "text/plain; charset=utf-8";
                return context.Response.WriteAsync("Too many requests. Try again in a minute.");
            }

            return next(context);
        });
        // Before the limiter, so a copy served takes no render slot.
        app.UseMiddleware<FirstVisitCache>();
        // An MCP call is a few hundred bytes of JSON. Kestrel's own limit is 30 MB a request, and a handful of
        // those at once would fill the machine's memory.
        app.Use((context, next) =>
        {
            if (context.Request.Path.StartsWithSegments(McpPath))
            {
                if (context.Request.ContentLength > McpMaxBody)
                {
                    context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                    return Task.CompletedTask;
                }
                if (context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } size)
                    size.MaxRequestBodySize = McpMaxBody;
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
        // The components sidebar's order (DocsNav's button): kept in a cookie the sidebar reads as it
        // renders, so it arrives in the reader's order. A post, not a link, so crawlers don't follow it.
        app.MapPost("/docs/components/order", async (HttpContext context) =>
        {
            if (!context.Request.HasFormContentType) return Results.BadRequest();
            var form = await context.Request.ReadFormAsync();
            var options = new CookieOptions
            {
                Path = context.Request.PathBase.HasValue ? context.Request.PathBase.Value + "/" : "/",
                MaxAge = TimeSpan.FromDays(365),
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
            };
            if (form["az"].Contains("true")) context.Response.Cookies.Append(Components.Layout.DocsNav.OrderCookie, "az", options);
            else context.Response.Cookies.Delete(Components.Layout.DocsNav.OrderCookie, options);

            // Back to the page it was posted from, when that's a path on this site.
            var back = form["return"].ToString();
            return Results.LocalRedirect(IsPlainPath(back) ? back : "/docs/components");
        }).DisableAntiforgery();

        // The newsletter's forms (Newsletter.razor): the address goes to the account service, which sends the link
        // that confirms it, and the answer shows where the form was. No antiforgery token, so the pages that
        // hold the form stay in FirstVisitCache; each visitor gets a few posts every ten minutes (FormLimit).
        app.MapPost("/newsletter/subscribe", async (HttpContext context, AccountService account, FormLimit limit) =>
        {
            if (!context.Request.HasFormContentType) return Results.BadRequest();
            var form = await context.Request.ReadFormAsync();
            var email = form["email"].ToString().Trim();
            string answer;
            // An address needs a dot after its @ ("ada@example" isn't one), which EmailAddressAttribute doesn't ask for.
            if (email.Length is 0 or > 254 || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email)
                || email.IndexOf('.', email.LastIndexOf('@') + 1) < 0 || email.EndsWith('.')) answer = "invalid";
            else if (form["website"].ToString().Length > 0) answer = "sent";
            else if (!limit.Allow(context)) answer = "failed";
            else
            {
                var source = form["source"].ToString() is "home" or "footer" or "page" ? form["source"].ToString() : "site";
                answer = await account.SubscribeAsync(email, source, context.RequestAborted) switch
                {
                    Subscription.Sent => "sent",
                    Subscription.Already => "subscribed",
                    _ => "failed",
                };
            }

            // Back to the page that sent it (its own path only), with the address in a short-lived cookie so the
            // answer can name it without putting it in the address bar.
            var back = form["back"].ToString();
            if (!IsPlainPath(back)) back = "/newsletter";
            if (email.Length is > 0 and <= 254)
            {
                context.Response.Cookies.Append(Components.Parts.Newsletter.AddressCookie, email, new CookieOptions
                {
                    MaxAge = TimeSpan.FromMinutes(2), HttpOnly = true, Secure = context.Request.IsHttps, SameSite = SameSiteMode.Lax, Path = "/",
                });
            }
            return Results.LocalRedirect($"{back}?answer={answer}#newsletter");
        }).DisableAntiforgery();

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
        // The CRUD pages demo's Export (Frame/CrudDemoFramePage.razor), as izzy add crud's CustomerExport.cs.
        app.MapGet("/frame/cli/crud/export.xlsx", (HttpContext context) => Results.Stream(
                stream => DataExport.WriteExcelAsync(stream, Frame.CrudDemoData.Customers().AsQueryable(), context.Request.QueryString.Value, DataFields.Of<Frame.DemoCustomer>(), "Customers", cancellationToken: context.RequestAborted),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "customers.xlsx"))
            .RequireRateLimiting(PagesRateLimit);
        // The TreeList totals example's downloads: the estimate as an outline.
        app.MapGet("/examples/estimate.csv", (HttpContext context) => Results.Stream(
                stream => DataExport.WriteCsvAsync(stream, Examples.TreeList.Estimate.All.AsQueryable(), context.Request.QueryString.Value, DataFields.Of<Examples.TreeList.WorkItem>(), cancellationToken: context.RequestAborted,
                    tree: DataExportTree<Examples.TreeList.WorkItem>.Of(w => w.Id, w => w.ParentId).RollUp("hours", DataAggregate.Sum)),
                "text/csv", "estimate.csv"))
            .RequireRateLimiting(PagesRateLimit);
        app.MapGet("/examples/estimate.xlsx", (HttpContext context) => Results.Stream(
                stream => DataExport.WriteExcelAsync(stream, Examples.TreeList.Estimate.All.AsQueryable(), context.Request.QueryString.Value, DataFields.Of<Examples.TreeList.WorkItem>(), "Estimate", cancellationToken: context.RequestAborted,
                    tree: DataExportTree<Examples.TreeList.WorkItem>.Of(w => w.Id, w => w.ParentId).RollUp("hours", DataAggregate.Sum)),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "estimate.xlsx"))
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
        // A section that opens at one of its articles (Get started, since Nov 23, 2026) has no index page.
        foreach (var section in Articles.Sections.Where(s => s.OpensAt is not null))
        {
            var target = $"/{section.Href}/{section.OpensAt}";
            app.MapGet($"/{section.Href}", () => Results.Redirect(target, permanent: true));
        }
        // Accessibility was one article under Common features until Nov 10, 2026; it's a section now.
        app.MapGet("/docs/common/accessibility", () => Results.Redirect("/docs/accessibility/overview", permanent: true));
        // "The izzy CLI" became Developer Tools on Nov 26, 2026: its overview is the CLI's page there, AI assistants
        // moved in from Get started, and the audit's article became pages of the Accessibility section.
        app.MapGet("/docs/cli", () => Results.Redirect("/docs/tools", permanent: true));
        app.MapGet("/docs/cli/{slug}", (string slug) => Results.Redirect(slug switch
        {
            "overview" => "/docs/tools/cli",
            "audit" => "/docs/accessibility/audit",
            _ => $"/docs/tools/{Uri.EscapeDataString(slug)}",
        }, permanent: true));
        app.MapGet("/docs/get-started/ai-assistants", () => Results.Redirect("/docs/tools/ai-assistants", permanent: true));
        // Chart types that were topics on Chart's pages until Nov 7, 2026 have pages of their own.
        foreach (var (topic, page) in new[] { ("bars", "bar-chart"), ("lines", "line-chart"), ("scatter", "scatter-chart") })
        {
            app.MapGet($"/docs/components/chart/{topic}", () => Results.Redirect($"/docs/components/{page}", permanent: true));
            app.MapGet($"/docs/components/chart/{topic}/api", () => Results.Redirect($"/docs/components/{page}/api", permanent: true));
        }

        app.MapMethods("/robots.txt", ["GET", "HEAD"], () => Results.Text(Sitemap.Robots, "text/plain"));
        // Microsoft reads this to show izzyblazor.com as the publisher of "Continue with Microsoft" on the account
        // service's sign-in (the Entra app registration's publisher domain). An endpoint, not a file: the build
        // leaves out folders whose name starts with a dot. The content type must be application/json alone,
        // with no charset, or Microsoft's check fails.
        app.MapMethods("/.well-known/microsoft-identity-association.json", ["GET", "HEAD"], () => Results.Bytes(
            """{"associatedApplications":[{"applicationId":"41c2a094-b47a-42c1-a38b-6b1b11e7075b"}]}"""u8.ToArray(), "application/json"));
        app.MapMethods("/sitemap.xml", ["GET", "HEAD"], (Registry registry, BlockCatalog blocks, TemplateCatalog templates) => Results.Text(Sitemap.Xml(registry, blocks, templates), "application/xml"));
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
