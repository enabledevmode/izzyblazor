using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
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
        /// usual for 12 hours. Empty lets nobody in.
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
        private const string PagePath = "/{{Route}}";
        private const string BypassCookie = "maintenance-bypass";

        /// <summary>
        /// While maintenance is on, answers every request with the maintenance page and status 503, with
        /// Retry-After and no caching; files (styles, scripts, images), the Allow paths and a browser with the
        /// bypass cookie get through. A request that doesn't ask for a page (fetch, an API call, Blazor's
        /// connection) gets the 503 with no body. The page is always at /{{Route}}, so you can check it.
        /// Circuits already open keep running until the app stops.
        /// </summary>
        public static IApplicationBuilder UseMaintenance(this IApplicationBuilder app)
        {
            var section = app.ApplicationServices.GetRequiredService<IConfiguration>().GetSection("Maintenance");
            var options = section.Get<{{RootNamespace}}.MaintenanceOptions>() ?? new();
            ChangeToken.OnChange(section.GetReloadToken, () => options = section.Get<{{RootNamespace}}.MaintenanceOptions>() ?? new());

            return app.Use(async (context, next) =>
            {
                var current = options;
                if (context.Request.Path.Equals(PagePath, StringComparison.OrdinalIgnoreCase))
                {
                    if (current.Enabled && Matches(current.Secret, context.Request.Query["key"]))
                    {
                        context.Response.Cookies.Append(BypassCookie, Hash(current.Secret!), new CookieOptions
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
                    await RespondAsync(context, current);
                    return;
                }
                if (!current.Enabled || IsOpen(context, current))
                {
                    await next(context);
                    return;
                }
                await RespondAsync(context, current);
            });
        }

        private static bool IsOpen(HttpContext context, {{RootNamespace}}.MaintenanceOptions options) =>
            Path.HasExtension(context.Request.Path.Value)
            || options.Allow.Any(path => context.Request.Path.StartsWithSegments(path, StringComparison.OrdinalIgnoreCase))
            || (context.Request.Cookies[BypassCookie] is { } cookie && !string.IsNullOrEmpty(options.Secret)
                && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(cookie), Encoding.UTF8.GetBytes(Hash(options.Secret))));

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

        // The cookie holds a hash, so the secret itself never sits in a browser.
        private static string Hash(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
    }
}
