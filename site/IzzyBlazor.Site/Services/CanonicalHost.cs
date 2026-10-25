namespace IzzyBlazor.Site.Services;

/// <summary>
/// Sends the site's other names (www.izzyblazor.com, izzyblazor.dev and www.izzyblazor.dev) to
/// https://izzyblazor.com with a 301, keeping the path and query. Only those names: Fly's health
/// checks, izzyblazor.fly.dev and localhost arrive with other hosts and are served as they are.
/// </summary>
public static class CanonicalHost
{
    public const string Host = "izzyblazor.com";

    private static readonly HashSet<string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "www.izzyblazor.com", "izzyblazor.dev", "www.izzyblazor.dev",
    };

    /// <summary>The address to redirect to, or null to serve the request.</summary>
    public static string? Target(string host, string pathAndQuery) =>
        Aliases.Contains(host) ? $"https://{Host}{pathAndQuery}" : null;

    public static IApplicationBuilder UseCanonicalHost(this IApplicationBuilder app) => app.Use((context, next) =>
    {
        var request = context.Request;
        if (Target(request.Host.Host, $"{request.PathBase}{request.Path}{request.QueryString}") is { } target)
        {
            context.Response.Redirect(target, permanent: true);
            return Task.CompletedTask;
        }

        return next(context);
    });
}
