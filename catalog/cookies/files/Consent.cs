namespace {{RootNamespace}}
{
    /// <summary>
    /// The visitor's cookie choices, from izzy add cookies: one cookie named "consent" holding the
    /// categories they allowed ("analytics,preferences", or "none"). Essential cookies need no
    /// consent. Load anything that isn't essential only when <see cref="Allows"/> says so.
    /// </summary>
    public static class Consent
    {
        public const string CookieName = "consent";

        /// <summary>The categories people choose, besides essential cookies, which are always on.</summary>
        public static readonly string[] Categories = ["analytics", "preferences"];

        /// <summary>Whether the visitor has made a choice: until then, the cookie notice shows.</summary>
        public static bool Chosen(HttpContext? http) => http?.Request.Cookies.ContainsKey(CookieName) == true;

        /// <summary>Whether the visitor allowed a category, such as "analytics". False until they choose.</summary>
        public static bool Allows(HttpContext? http, string category) =>
            http?.Request.Cookies[CookieName] is { } value && value.Split(',').Contains(category);

        /// <summary>
        /// The page a return address ("~/pricing" or "/pricing") names, relative to the app's base: "" for
        /// the home page, null when it isn't a page on this site ("//evil.example", "/\evil.example").
        /// </summary>
        public static string? LocalPath(string? url)
        {
            var path = url is null ? null : url.StartsWith("~/") ? url[2..] : url.StartsWith('/') ? url[1..] : null;
            if (path is null || path.StartsWith('/') || path.StartsWith('\\'))
            {
                return null;
            }
            // Still this site once resolved, so not "javascript:..." either.
            return Uri.TryCreate(new Uri("http://app/"), path, out var uri) && uri.Scheme == "http" && uri.Host == "app" ? path : null;
        }

        /// <summary>Saves the categories the visitor allowed, for a year.</summary>
        public static void Write(HttpContext http, IEnumerable<string> allowed)
        {
            var value = string.Join(',', allowed.Where(Categories.Contains));
            http.Response.Cookies.Append(CookieName, value.Length > 0 ? value : "none", new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                Path = "/",
                SameSite = SameSiteMode.Lax,
                Secure = http.Request.IsHttps,
                IsEssential = true,
            });
        }
    }
}

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>The cookie consent endpoint, from izzy add cookies: in this namespace so Program.cs needs no using.</summary>
    public static class ConsentEndpoints
    {
        /// <summary>
        /// Maps POST /consent, where the cookie notice's buttons post: "accept" allows every
        /// category, anything else allows none. It saves the choice and goes back to the page.
        /// </summary>
        public static IEndpointConventionBuilder MapConsent(this IEndpointRouteBuilder app) =>
            app.MapPost("/consent", ([Microsoft.AspNetCore.Mvc.FromForm] string? choice, [Microsoft.AspNetCore.Mvc.FromForm] string? returnUrl, HttpContext http) =>
            {
                {{RootNamespace}}.Consent.Write(http, choice == "accept" ? {{RootNamespace}}.Consent.Categories : []);
                return Results.LocalRedirect("~/" + ({{RootNamespace}}.Consent.LocalPath(returnUrl) ?? ""));
            });
    }
}
