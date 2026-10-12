using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace IzzyBlazor;

/// <summary>
/// Carries toasts over a redirect in a short-lived, HTTP-only cookie (<c>ib-toasts</c>). The cookie
/// is written only when a static response that queued toasts is a redirect, and deleted by the
/// next request that reads it. Registered by <c>AddIzzyBlazor()</c>.
/// </summary>
internal sealed class CookieToastCarrier(IHttpContextAccessor http) : IToastCarrier
{
    internal const string CookieName = "ib-toasts";
    private const int MaxCookieLength = 3000;

    private Func<IReadOnlyList<ToastMessage>>? _queued;
    private bool _hadCookie;
    private bool _hooked;

    // Only while the response can still change. An interactive circuit's HttpContext belongs to
    // the request that started it, long since sent.
    private HttpContext? Context => http.HttpContext is { Response.HasStarted: false } context ? context : null;

    public IReadOnlyList<ToastMessage> Receive()
    {
        if (Context is not { } context || context.Request.Cookies[CookieName] is not { Length: > 0 } cookie)
        {
            return [];
        }

        _hadCookie = true;
        Hook(context);
        try
        {
            return JsonSerializer.Deserialize<List<ToastMessage>>(Uri.UnescapeDataString(cookie)) ?? [];
        }
        catch (JsonException)
        {
            // A damaged cookie: ignore it (and it's deleted).
            return [];
        }
    }

    public void CarryIfRedirected(Func<IReadOnlyList<ToastMessage>> queued)
    {
        if (Context is not { } context)
        {
            return;
        }

        _queued = queued;
        Hook(context);
    }

    // When the response starts: a redirect carries the toasts on; otherwise they were rendered.
    private void Hook(HttpContext context)
    {
        if (_hooked)
        {
            return;
        }

        _hooked = true;
        context.Response.OnStarting(() =>
        {
            var response = context.Response;
            var options = new CookieOptions
            {
                Path = context.Request.PathBase.HasValue ? context.Request.PathBase.Value : "/",
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = context.Request.IsHttps,
                IsEssential = true,
            };

            if (response.StatusCode is >= 300 and < 400 && _queued?.Invoke() is { Count: > 0 } toasts)
            {
                var json = Uri.EscapeDataString(JsonSerializer.Serialize(toasts));
                if (json.Length <= MaxCookieLength)
                {
                    response.Cookies.Append(CookieName, json, options);
                    return Task.CompletedTask;
                }
            }

            if (_hadCookie)
            {
                response.Cookies.Delete(CookieName, options);
            }

            return Task.CompletedTask;
        });
    }
}

/// <summary>Reads request cookies through the current HttpContext, during static rendering only.</summary>
internal sealed class HttpRequestCookies(IHttpContextAccessor http) : IRequestCookies
{
    public string? Get(string name) =>
        http.HttpContext is { Response.HasStarted: false } context ? context.Request.Cookies[name] : null;
}
