using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

/// <summary>The server package's pieces, and the Core seams they plug into.</summary>
public class ServerSplitTests
{
    private sealed class StartableResponse : HttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _starting = [];

        public override void OnStarting(Func<object, Task> callback, object state) => _starting.Add((callback, state));

        public async Task StartAsync()
        {
            foreach (var (callback, state) in _starting)
            {
                await callback(state);
            }

            HasStartedValue = true;
        }

        public bool HasStartedValue { get; private set; }

        public override bool HasStarted => HasStartedValue;
    }

    private static (DefaultHttpContext Http, StartableResponse Response) Request(string? cookie = null)
    {
        var http = new DefaultHttpContext();
        var response = new StartableResponse();
        http.Features.Set<IHttpResponseFeature>(response);
        if (cookie is not null)
        {
            http.Request.Headers.Cookie = $"ib-toasts={cookie}";
        }

        return (http, response);
    }

    private static string SetCookie(HttpContext http) => http.Response.Headers.SetCookie.ToString();

    [Fact]
    public async Task Toasts_are_carried_over_a_redirect_in_a_cookie()
    {
        var (http, response) = Request();
        var carrier = new CookieToastCarrier(new FixedHttpContextAccessor { HttpContext = http });
        var toasts = new ToastService(new ServiceCollection().AddSingleton<IToastCarrier>(carrier).BuildServiceProvider());

        toasts.Success("Saved.");
        http.Response.StatusCode = 302;
        await response.StartAsync();

        Assert.StartsWith("ib-toasts=", SetCookie(http));
        Assert.Contains("httponly", SetCookie(http));
    }

    [Fact]
    public async Task No_cookie_when_the_toast_was_rendered()
    {
        var (http, response) = Request();
        var carrier = new CookieToastCarrier(new FixedHttpContextAccessor { HttpContext = http });
        var toasts = new ToastService(new ServiceCollection().AddSingleton<IToastCarrier>(carrier).BuildServiceProvider());

        toasts.Success("Saved.");
        await response.StartAsync();

        Assert.Equal("", SetCookie(http));
    }

    [Fact]
    public async Task Carried_toasts_come_first_and_the_cookie_is_deleted()
    {
        var json = Uri.EscapeDataString("""[{"Message":"Created.","Status":1}]""");
        var (http, response) = Request(json);
        var carrier = new CookieToastCarrier(new FixedHttpContextAccessor { HttpContext = http });
        var toasts = new ToastService(new ServiceCollection().AddSingleton<IToastCarrier>(carrier).BuildServiceProvider());

        toasts.Info("Now.");
        await response.StartAsync();

        Assert.Equal(new[] { "Created.", "Now." }, toasts.Pending.Select(t => t.Message));
        Assert.Contains("expires=Thu, 01 Jan 1970", SetCookie(http));
    }

    [Fact]
    public void Without_a_carrier_toasts_still_render_in_the_response()
    {
        // IzzyBlazor.Core alone (WebAssembly, or a server app that only called AddIzzyBlazorCore).
        var toasts = new ToastService(new ServiceCollection().BuildServiceProvider());

        toasts.Warning("Careful.");

        Assert.Equal("Careful.", Assert.Single(toasts.Pending).Message);
    }

    [Fact]
    public void Carrier_ignores_a_response_that_has_started()
    {
        // An interactive circuit's HttpContext belongs to a request already sent.
        var (http, _) = Request(Uri.EscapeDataString("""[{"Message":"Old."}]"""));
        http.Features.Set<IHttpResponseFeature>(new StartedResponse());
        var carrier = new CookieToastCarrier(new FixedHttpContextAccessor { HttpContext = http });

        Assert.Empty(carrier.Receive());
        Assert.Null(new HttpRequestCookies(new FixedHttpContextAccessor { HttpContext = http }).Get("ib-toasts"));
    }

    private sealed class StartedResponse : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }

    [Fact]
    public void AddIzzyBlazor_registers_core_and_server_services()
    {
        using var provider = new ServiceCollection().AddIzzyBlazor().BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetService<ToastService>());
        Assert.IsType<CookieToastCarrier>(scope.ServiceProvider.GetService<IToastCarrier>());
        Assert.IsType<HttpRequestCookies>(scope.ServiceProvider.GetService<IRequestCookies>());
    }

    [Fact]
    public void AddIzzyBlazorCore_registers_only_what_runs_everywhere()
    {
        using var provider = new ServiceCollection().AddIzzyBlazorCore().BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetService<ToastService>());
        Assert.Null(scope.ServiceProvider.GetService<IToastCarrier>());
        Assert.Null(scope.ServiceProvider.GetService<IRequestCookies>());
    }

    [Theory]
    [InlineData("?page=2&Sort=name", "page", "2")]
    [InlineData("?q=grace+hopper", "q", "grace hopper")]
    [InlineData("?q=caf%C3%A9", "Q", "café")]
    [InlineData("?tag=a&tag=b", "tag", "a,b")]
    [InlineData("?flag", "flag", "")]
    [InlineData("", "page", null)]
    [InlineData("?=x&c=", "", "x")]
    public void Query_parsing_matches_QueryHelpers(string query, string key, string? expected)
    {
        var parsed = QueryString.Parse(query);
        var ours = parsed.TryGetValue(key, out var v) ? v.ToString() : null;
        var server = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(query).TryGetValue(key, out var s) ? s.ToString() : null;

        Assert.Equal(expected, ours);
        Assert.Equal(server, ours);
    }
}
