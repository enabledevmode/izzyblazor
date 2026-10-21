using System.Text;
using System.Text.RegularExpressions;

namespace IzzyBlazor.Site.Services;

/// <summary>
/// In an Off example frame, sends links to other pages to the whole window. With script, site.js
/// does this; an Off frame runs none, so the server marks those links <c>target="_top"</c> (the
/// frame's sandbox allows top navigation on a click). Links within the example (its own path,
/// such as a pager's <c>?page=2</c>, or an <c>#anchor</c>) stay in the frame.
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
        var rewritten = Rewrite(html, context.Request.Path);
        context.Response.ContentLength = null;
        await original.WriteAsync(Encoding.UTF8.GetBytes(rewritten));
    });

    /// <summary>Adds <c>target="_top"</c> to links that leave <paramref name="path"/>, resolved against the base href "/".</summary>
    public static string Rewrite(string html, string path) => Links().Replace(html, match =>
    {
        var tag = match.Value;
        var href = System.Net.WebUtility.HtmlDecode(match.Groups["href"].Value);
        if (tag.Contains(" target=", StringComparison.Ordinal) || href.StartsWith('#') || !Uri.TryCreate(new Uri("http://site/"), href, out var url))
        {
            return tag;
        }

        var leaves = url.Host != "site" || !string.Equals(url.AbsolutePath, path, StringComparison.Ordinal);
        return leaves && url.Scheme is "http" or "https" ? tag[..^1].TrimEnd('/').TrimEnd() + " target=\"_top\"" + (tag.EndsWith("/>", StringComparison.Ordinal) ? " />" : ">") : tag;
    });

    [GeneratedRegex("<a\\b[^>]*?\\bhref=\"(?<href>[^\"]*)\"[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex Links();
}
