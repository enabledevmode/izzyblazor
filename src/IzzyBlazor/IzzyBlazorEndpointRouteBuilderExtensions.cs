using IzzyBlazor;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.AspNetCore.Builder;

/// <summary>Maps the endpoints IzzyBlazor's components use when JavaScript is off.</summary>
public static class IzzyBlazorEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps IzzyBlazor's endpoints under <c>/_izzy</c>. Today that's <c>/_izzy/scheme</c>, which
    /// ColorSchemeSwitch links to: it saves the chosen color scheme in the <c>ib-scheme</c> cookie
    /// and redirects back to the page. With JavaScript the switch never calls it.
    /// </summary>
    /// <example>
    /// <code>
    /// app.MapRazorComponents&lt;App&gt;();
    /// app.MapIzzyBlazor();
    /// </code>
    /// </example>
    public static IEndpointConventionBuilder MapIzzyBlazor(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup("/_izzy").ExcludeFromDescription();
        group.MapGet("/scheme", (HttpContext context, string? value, string? @return) => SetScheme(context, value, @return));
        return group;
    }

    internal static IResult SetScheme(HttpContext context, string? value, string? returnPath)
    {
        var scheme = ColorSchemes.Parse(value) ?? ColorScheme.System;

        // The same cookie the script writes: the app's base path, a year, readable by script.
        context.Response.Cookies.Append(ColorSchemes.CookieName, ColorSchemes.Name(scheme), new CookieOptions
        {
            Path = context.Request.PathBase.HasValue ? context.Request.PathBase.Value : "/",
            MaxAge = TimeSpan.FromDays(365),
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps,
            HttpOnly = false,
            IsEssential = true,
        });

        return Results.LocalRedirect(ReturnUrl(context.Request.PathBase, returnPath));
    }

    /// <summary>
    /// Where to go back to: a path relative to the app's base (as the switch sends it), put after
    /// the base path, so it can't leave the site. A value starting with a slash or holding a
    /// backslash (which could make <c>//host</c> or <c>/\host</c>) goes to the home page instead,
    /// as does one holding a control character, which <c>LocalRedirect</c> would refuse with an error.
    /// </summary>
    internal static string ReturnUrl(PathString pathBase, string? relative)
    {
        var home = pathBase.Value + "/";
        return string.IsNullOrEmpty(relative) || relative[0] == '/' || relative.Contains('\\') || relative.Any(char.IsControl)
            ? home
            : home + relative;
    }
}
