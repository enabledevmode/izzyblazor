using System.Text;
using System.Text.RegularExpressions;

namespace IzzyBlazor.Site.Services;

/// <summary>
/// On an Off page, sends an example's links and forms that load the page again to the example
/// (<c>#ex-{id}</c>), so a post or a pager link lands on it rather than at the top of the page.
/// site.js does this when script runs; with JavaScript truly off, the server writes the fragment
/// into the link's <c>href</c> or the form's <c>action</c>. Only links and forms inside an example
/// (<c>section[data-ex]</c>) to the page's own path, with no fragment of their own; an example's
/// own mode switch too (site.js drops the fragment when it runs, and keeps the example where it
/// was on the screen). Links resolve against the site's <c>&lt;base href="/"&gt;</c>, as the
/// browser resolves them. Not covered: a redirect after a post (its <c>Location</c> has no
/// fragment), and a form with an empty <c>action</c> (Blazor always writes one).
/// </summary>
public static partial class OffExampleAnchors
{
    public static IApplicationBuilder UseOffExampleAnchors(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        // An Off page: ?mode=off, or the site-mode cookie when the address has no mode.
        var query = context.Request.Query["mode"].ToString();
        var mode = query is "" ? context.Request.Cookies[IzzyBlazor.Site.Components.Parts.ModeSwitch.Cookie] : query;
        if (mode != "off" || context.Request.Path.StartsWithSegments("/frame"))
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
        if (context.Response.ContentType?.StartsWith("text/html", StringComparison.Ordinal) != true
            || context.Response.Headers.ContentEncoding.Count > 0)
        {
            await buffer.CopyToAsync(original);
            return;
        }

        var html = await new StreamReader(buffer, Encoding.UTF8).ReadToEndAsync();
        // A page that ignores the mode (an article, Home) renders Static with its script: leave it.
        var rewritten = html.Contains(NoScriptCss.Path, StringComparison.Ordinal) ? Rewrite(html, context.Request.Path, context.Request.Host.Value ?? "site") : html;
        if (ReferenceEquals(rewritten, html) || rewritten == html)
        {
            buffer.Position = 0;
            await buffer.CopyToAsync(original);
            return;
        }

        context.Response.ContentLength = null;
        await original.WriteAsync(Encoding.UTF8.GetBytes(rewritten));
    });

    /// <summary>
    /// Adds <c>#{id}</c> to links and form actions inside each <c>section[data-ex]</c> that go to
    /// <paramref name="path"/> on <paramref name="host"/> with no fragment.
    /// </summary>
    public static string Rewrite(string html, string path, string host = "site")
    {
        var output = new StringBuilder(html.Length + 256);
        var at = 0;
        foreach (Match start in Examples().Matches(html))
        {
            if (start.Index < at)
            {
                continue;
            }

            var end = SectionEnd(html, start.Index + start.Length);
            output.Append(html, at, start.Index - at);
            if (Id().Match(start.Value) is not { Success: true } idMatch)
            {
                continue;
            }

            var id = idMatch.Groups["id"].Value;
            output.Append(Targets().Replace(html[start.Index..end], tag => Anchor(tag, id, path, host)));
            at = end;
        }

        output.Append(html, at, html.Length - at);
        return output.ToString();
    }

    private static string Anchor(Match tag, string id, string path, string host)
    {
        var value = tag.Groups["url"];
        var url = System.Net.WebUtility.HtmlDecode(value.Value);
        var site = new Uri($"http://{host}/");
        if (url.StartsWith('#') || url.Contains('#') || !Uri.TryCreate(site, url, out var target)
            || !string.Equals(target.Authority, site.Authority, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(target.AbsolutePath, path, StringComparison.Ordinal))
        {
            return tag.Value;
        }

        var offset = value.Index - tag.Index + value.Length;
        return string.Concat(tag.Value.AsSpan(0, offset), "#", id, tag.Value.AsSpan(offset));
    }

    /// <summary>Where the section that starts before <paramref name="from"/> ends: after its own <c>&lt;/section&gt;</c>.</summary>
    private static int SectionEnd(string html, int from)
    {
        var depth = 1;
        foreach (Match tag in SectionTags().Matches(html, from))
        {
            depth += tag.Value[1] == '/' ? -1 : 1;
            if (depth == 0)
            {
                return tag.Index + tag.Length;
            }
        }

        return html.Length;
    }

    [GeneratedRegex("<section\\b[^>]*?\\sdata-ex=\"[^\"]*\"[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex Examples();

    [GeneratedRegex("\\sid=\"(?<id>[^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex Id();

    [GeneratedRegex("</?section\\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex SectionTags();

    [GeneratedRegex("<(?:a\\b[^>]*?\\shref|form\\b[^>]*?\\saction|(?:button|input)\\b[^>]*?\\sformaction)=\"(?<url>[^\"]*)\"[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex Targets();
}
