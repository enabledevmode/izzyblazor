using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.StaticAssets;
using Microsoft.Extensions.Primitives;

namespace {{RootNamespace}}
{
    /// <summary>
    /// Maintenance mode, from izzy add maintenance: appsettings.json's "Maintenance" section. Turn it
    /// on with <see cref="Enabled"/>, in the file (it takes effect without a restart) or with an
    /// environment variable such as <c>Maintenance__Enabled=true</c>.
    /// </summary>
    public sealed class MaintenanceOptions
    {
        /// <summary>While true, every request gets the maintenance page with status 503.</summary>
        public bool Enabled { get; set; }

        /// <summary>When it ends, such as "2026-11-16T03:00:00Z": the page says when, and Retry-After counts down to it.</summary>
        public DateTimeOffset? Until { get; set; }

        /// <summary>Your status page's address, for the page's button. Empty leaves the button out.</summary>
        public string? StatusPage { get; set; }

        /// <summary>
        /// Lets you in while it's on: open /{{Route}}?key=<em>secret</em> and that browser uses the app as
        /// usual for 12 hours, or until you change the secret. Empty lets nobody in.
        /// </summary>
        public string? Secret { get; set; }

        /// <summary>Paths that stay open, such as health checks, so the host doesn't restart the app. Ones in appsettings.json are added.</summary>
        public List<string> Allow { get; set; } = ["/health", "/alive"];
    }
}

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>Maintenance mode's middleware, from izzy add maintenance: in this namespace so Program.cs needs no using.</summary>
    public static class MaintenanceExtensions
    {
        private static readonly PathString PagePath = "/" + "{{Route}}".Trim('/');
        private const string BypassCookie = "maintenance-bypass";

        // The settings as last read, with the Allow paths ready to compare.
        private sealed record Settings({{RootNamespace}}.MaintenanceOptions Options, PathString[] Allow);

        /// <summary>
        /// While maintenance is on, answers every request with the maintenance page and status 503, with
        /// Retry-After and no caching; files (styles, scripts, images), the Allow paths and a browser with the
        /// bypass cookie get through. A request that doesn't ask for a page (fetch, an API call, Blazor's
        /// connection) gets the 503 with no body. The page is always at /{{Route}}, so you can check it.
        /// Circuits already open keep running until the app stops.
        /// </summary>
        public static IApplicationBuilder UseMaintenance(this IApplicationBuilder app)
        {
            var services = app.ApplicationServices;
            var section = services.GetRequiredService<IConfiguration>().GetSection("Maintenance");
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Maintenance");
            var bypass = services.GetRequiredService<IDataProtectionProvider>().CreateProtector("Maintenance.Bypass").ToTimeLimitedDataProtector();
            var settings = Read(section, logger);
            ChangeToken.OnChange(section.GetReloadToken, () => settings = Read(section, logger));

            return app.Use(async (context, next) =>
            {
                var current = settings;
                var options = current.Options;
                if (context.Request.Path.Equals(PagePath, StringComparison.OrdinalIgnoreCase))
                {
                    if (options.Enabled && Matches(options.Secret, context.Request.Query["key"]))
                    {
                        context.Response.Cookies.Append(BypassCookie, bypass.Protect(Hash(options.Secret!), TimeSpan.FromHours(12)), new CookieOptions
                        {
                            Expires = DateTimeOffset.UtcNow.AddHours(12),
                            Path = "/",
                            HttpOnly = true,
                            SameSite = SameSiteMode.Lax,
                            Secure = context.Request.IsHttps,
                            IsEssential = true,
                        });
                        context.Response.Redirect(context.Request.PathBase + "/");
                        return;
                    }
                    await RespondAsync(context, options);
                    return;
                }
                if (!options.Enabled || IsOpen(context, current, bypass))
                {
                    await next(context);
                    return;
                }
                await RespondAsync(context, options);
            });
        }

        // The section, read again whenever the file changes. A value that doesn't parse (an Until of
        // "Monday 3am") is logged and left out, and the rest still applies, so maintenance still turns on.
        private static Settings Read(IConfigurationSection section, ILogger logger)
        {
            {{RootNamespace}}.MaintenanceOptions options;
            try
            {
                options = section.Get<{{RootNamespace}}.MaintenanceOptions>() ?? new();
            }
            catch (InvalidOperationException exception)
            {
                logger.LogError(exception, "A Maintenance setting isn't valid, so it's left out until it's fixed.");
                options = new()
                {
                    Enabled = bool.TryParse(section["Enabled"], out var enabled) && enabled,
                    Until = DateTimeOffset.TryParse(section["Until"], out var until) ? until : null,
                    StatusPage = section["StatusPage"],
                    Secret = section["Secret"],
                };
                options.Allow.AddRange(section.GetSection("Allow").GetChildren().Select(child => child.Value ?? ""));
            }
            // "health", "/health/" and "/health" are the same path; an empty one is no path.
            var allow = options.Allow.Select(path => path.Trim().Trim('/')).Where(path => path.Length > 0)
                .Select(path => new PathString("/" + path)).ToArray();
            return new Settings(options, allow);
        }

        private static bool IsOpen(HttpContext context, Settings settings, ITimeLimitedDataProtector bypass)
        {
            // Files the page needs. A page's address can have a dot too (/users/jane.doe), so a file is a
            // static asset, or a name with an extension that no page or endpoint matches.
            var endpoint = context.GetEndpoint();
            if (endpoint?.Metadata.GetMetadata<StaticAssetDescriptor>() is not null || (endpoint is null && Path.HasExtension(context.Request.Path.Value)))
            {
                return true;
            }
            if (settings.Allow.Any(path => context.Request.Path.StartsWithSegments(path, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
            // The Not found page for a request already let through, such as a missing file.
            if (context.Features.Get<IStatusCodeReExecuteFeature>() is not null)
            {
                return true;
            }
            return !string.IsNullOrEmpty(settings.Options.Secret) && context.Request.Cookies[BypassCookie] is { } cookie
                && Bypasses(bypass, cookie, settings.Options.Secret);
        }

        private static Task RespondAsync(HttpContext context, {{RootNamespace}}.MaintenanceOptions options)
        {
            var on = options.Enabled;
            if (on)
            {
                // Seconds until it ends, or five minutes when nobody said.
                var left = options.Until - DateTimeOffset.UtcNow;
                context.Response.Headers.RetryAfter = (left > TimeSpan.Zero ? (int)Math.Ceiling(left.Value.TotalSeconds) : 300).ToString();
                // Not kept by a CDN or the browser, so the app is back the moment it's off.
                context.Response.Headers.CacheControl = "no-store";
            }
            if (on && !context.Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase))
            {
                // No body, and not the starter's Not found page either.
                if (context.Features.Get<IStatusCodePagesFeature>() is { } statusPages)
                {
                    statusPages.Enabled = false;
                }
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return Task.CompletedTask;
            }
            // The page as a document of its own, outside the router: no layout, no circuit.
            context.SetEndpoint(null);
            var page = new RazorComponentResult<{{RootNamespace}}.Components.Pages.{{Name}}>(new { options.Until, options.StatusPage })
            {
                StatusCode = on ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status200OK,
            };
            return page.ExecuteAsync(context);
        }

        private static bool Matches(string? secret, string? key) =>
            !string.IsNullOrEmpty(secret) && key is not null
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(secret));

        // The cookie holds the secret's hash, encrypted with the time it ends: it can't be read or made up,
        // it stops working after 12 hours whatever the browser keeps, and a new secret ends it at once.
        private static bool Bypasses(ITimeLimitedDataProtector bypass, string cookie, string secret)
        {
            try
            {
                var hash = bypass.Unprotect(cookie, out _);
                return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(hash), Encoding.UTF8.GetBytes(Hash(secret)));
            }
            catch (CryptographicException)
            {
                return false;
            }
        }

        private static string Hash(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
    }
}
