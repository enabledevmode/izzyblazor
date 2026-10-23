using System.Text;
using System.Text.RegularExpressions;

namespace IzzyBlazor.Site.Services;

/// <summary>
/// In an Off example frame, sends links to other pages to the whole window. With script, site.js
/// does this; an Off frame runs none, so the server marks those links <c>target="_top"</c> (the
/// frame's sandbox allows top navigation on a click). Links within the example (its own path,
/// such as a pager's <c>?page=2</c>, an <c>#anchor</c>, or IzzyBlazor's <c>/_izzy/</c> endpoints) stay
/// in the frame, written relative or absolute (<c>NavigationManager.GetUriWithQueryParameter</c>
/// gives the request's own scheme and host).
/// </summary>
public static partial class OffFrameLinks
{
    public static IApplicationBuilder UseOffFrameLinks(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        if (!context.Request.Path.StartsWithSegments("/frame") || context.Request.Query["mode"].ToString() != "off")
        {
            await next();
            return;
        }

        var original = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await next();
        }
        finally
        {
            context.Response.Body = original;
        }

        buffer.Position = 0;
        if (context.Response.ContentType?.StartsWith("text/html", StringComparison.Ordinal) != true)
        {
            await buffer.CopyToAsync(original);
            return;
        }

        var html = await new StreamReader(buffer, Encoding.UTF8).ReadToEndAsync();
        var rewritten = Rewrite(html, context.Request.Path, context.Request.Host.Value ?? "site");
        context.Response.ContentLength = null;
        await original.WriteAsync(Encoding.UTF8.GetBytes(rewritten));
    });

    /// <summary>
    /// Adds <c>target="_top"</c> to links that leave <paramref name="path"/> on <paramref name="host"/>,
    /// resolved against the base href "/".
    /// </summary>
    public static string Rewrite(string html, string path, string host = "site") => Links().Replace(html, match =>
    {
        var tag = match.Value;
        var href = System.Net.WebUtility.HtmlDecode(match.Groups["href"].Value);
        if (tag.Contains(" target=", StringComparison.Ordinal) || href.StartsWith('#') || !Uri.TryCreate(new Uri($"http://{host}/"), href, out var url))
        {
            return tag;
        }

        // IzzyBlazor's own endpoints (/_izzy/scheme) redirect back to the frame, so they stay in it.
        var leaves = !string.Equals(url.Authority, new Uri($"http://{host}/").Authority, StringComparison.OrdinalIgnoreCase)
            || !(string.Equals(url.AbsolutePath, path, StringComparison.Ordinal) || url.AbsolutePath.StartsWith("/_izzy/", StringComparison.Ordinal));
        return leaves && url.Scheme is "http" or "https" ? tag[..^1].TrimEnd('/').TrimEnd() + " target=\"_top\"" + (tag.EndsWith("/>", StringComparison.Ordinal) ? " />" : ">") : tag;
    });

    [GeneratedRegex("<a\\b[^>]*?\\bhref=\"(?<href>[^\"]*)\"[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex Links();
}
