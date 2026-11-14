using System.Collections.Concurrent;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Caching.Memory;

namespace IzzyBlazor.Site.Services;

/// <summary>
/// Serves a first visit from outside the site from a copy of the page rendered a few minutes ago,
/// so a crawl costs a render per page every few minutes, not one per request: on Nov 10, 13 and
/// 14, 2026 crawlers from many addresses at once kept the one shared CPU rendering the same docs
/// pages (seconds each) until the site stopped answering.
/// </summary>
/// <remarks>
/// Only a request that could be anyone's: a GET or HEAD for a page with no query string and no
/// cookie, which isn't enhanced navigation. Such a request sees the page as everyone does: the
/// default render mode and the system's color scheme. Each copy served gets this visitor's own
/// antiforgery cookie, with its token in place of the one rendered into the copy, so forms post
/// as from a fresh render. A page with server components keeps for 2 minutes, well inside the 5
/// their markers stay valid for; any other for 10. While a page renders, other requests for it
/// wait for that render rather than starting their own.
/// </remarks>
public sealed class FirstVisitCache(RequestDelegate next, IAntiforgery antiforgery, ILogger<FirstVisitCache> log)
{
    private const string TokenMark = "\u001fib-antiforgery\u001f";
    private static readonly TimeSpan WithServerComponents = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan Otherwise = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan LongestWait = TimeSpan.FromSeconds(30);

    // Headers that belong to one response: the antiforgery call writes its own cookie and
    // caching headers for each visitor, and the server sets the rest.
    private static readonly HashSet<string> OwnHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Set-Cookie", "Cache-Control", "Pragma", "Expires", "X-Frame-Options", "Date", "Content-Length", "Transfer-Encoding", "Server",
    };

    private readonly MemoryCache _pages = new(new MemoryCacheOptions { SizeLimit = 128L * 1024 * 1024 });
    private readonly ConcurrentDictionary<string, Task<Page?>> _rendering = new();

    private sealed record Page(string Html, bool HasToken, KeyValuePair<string, string[]>[] Headers);

    public async Task InvokeAsync(HttpContext context)
    {
        if (!Cacheable(context))
        {
            await next(context);
            return;
        }

        var key = context.Request.Path.Value!;
        if (_pages.TryGetValue(key, out Page? page) && page is not null)
        {
            await ServeAsync(context, page);
            return;
        }

        // One render per page at a time: the first request renders, the rest wait for its copy.
        var rendering = new TaskCompletionSource<Page?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var current = _rendering.GetOrAdd(key, rendering.Task);
        if (current != rendering.Task)
        {
            var finished = await Task.WhenAny(current, Task.Delay(LongestWait, context.RequestAborted));
            if (finished == current && await current is { } rendered)
            {
                await ServeAsync(context, rendered);
                return;
            }

            if (context.RequestAborted.IsCancellationRequested)
            {
                return;
            }

            await next(context);
            return;
        }

        Page? result = null;
        try
        {
            result = await RenderAsync(context, key);
        }
        finally
        {
            _rendering.TryRemove(key, out _);
            rendering.TrySetResult(result);
        }
    }

    private static bool Cacheable(HttpContext context)
    {
        var request = context.Request;
        return (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
            && !request.QueryString.HasValue
            && request.Headers.Cookie.Count == 0
            && !request.Headers.ContainsKey("blazor-enhanced-nav")
            && SiteApp.IsPageRender(context);
    }

    // Renders the page for this request into a buffer, sends it, and keeps a copy when it's a whole
    // HTML page.
    private async Task<Page?> RenderAsync(HttpContext context, string key)
    {
        var response = context.Response;
        var body = response.Body;
        await using var buffer = new MemoryStream();
        response.Body = buffer;
        try
        {
            await next(context);
        }
        finally
        {
            response.Body = body;
        }

        buffer.Position = 0;
        await buffer.CopyToAsync(body, context.RequestAborted);

        if (response.StatusCode != StatusCodes.Status200OK
            || response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) != true
            || HttpMethods.IsHead(context.Request.Method)
            || buffer.Length == 0)
        {
            return null;
        }

        var html = Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
        // The tokens this render used (generated once a request, so the same ones): the copy keeps
        // a mark in their place.
        var token = antiforgery.GetTokens(context).RequestToken;
        var hasToken = !string.IsNullOrEmpty(token) && html.Contains(token, StringComparison.Ordinal);
        if (hasToken)
        {
            html = html.Replace(token!, TokenMark, StringComparison.Ordinal);
        }

        var headers = response.Headers
            .Where(h => !OwnHeaders.Contains(h.Key))
            .Select(h => KeyValuePair.Create(h.Key, h.Value.Select(v => v ?? "").ToArray()))
            .ToArray();
        var page = new Page(html, hasToken, headers);
        var serverComponents = html.Contains("<!--Blazor:{\"type\":\"server\"", StringComparison.Ordinal);
        _pages.Set(key, page, new MemoryCacheEntryOptions
        {
            Size = html.Length * 2L,
            AbsoluteExpirationRelativeToNow = serverComponents ? WithServerComponents : Otherwise,
        });
        log.LogInformation("Cached {Path} ({Kilobytes} KB)", key, html.Length / 1024);
        return page;
    }

    private async Task ServeAsync(HttpContext context, Page page)
    {
        var response = context.Response;
        foreach (var (name, values) in page.Headers)
        {
            response.Headers[name] = values;
        }

        // This visitor's own cookie and token (and the no-store and frame headers that come with them).
        var tokens = antiforgery.GetAndStoreTokens(context);
        var html = page.HasToken ? page.Html.Replace(TokenMark, tokens.RequestToken, StringComparison.Ordinal) : page.Html;
        var bytes = Encoding.UTF8.GetBytes(html);
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentLength = bytes.Length;
        if (!HttpMethods.IsHead(context.Request.Method))
        {
            await response.Body.WriteAsync(bytes, context.RequestAborted);
        }
    }
}
