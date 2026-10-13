using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

public class ColorSchemeRenderTests : BunitContext
{
    private void UseCookie(string? header)
    {
        var http = new DefaultHttpContext();
        if (header is not null)
        {
            http.Request.Headers.Cookie = header;
        }

        Services.AddSingleton<IHttpContextAccessor>(new FixedHttpContextAccessor { HttpContext = http });
        Services.AddScoped<IRequestCookies, HttpRequestCookies>();
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
    }

    [Fact]
    public void Switch_is_a_group_of_links_to_the_scheme_endpoint()
    {
        UseCookie(null);
        Services.GetRequiredService<NavigationManager>().NavigateTo("static/brand?primary=%230af");
        var cut = Render<ColorSchemeSwitch>();

        var root = cut.Find("ib-scheme-switch");
        Assert.Equal("group", root.GetAttribute("role"));
        Assert.Equal("Color scheme", root.GetAttribute("aria-label"));
        Assert.Contains("ib-segmented", root.ClassName);

        var links = cut.FindAll("a.ib-segment");
        Assert.Equal(["system", "light", "dark"], links.Select(a => a.GetAttribute("data-ib-scheme")));
        Assert.Equal("_izzy/scheme?value=dark&return=static%2Fbrand%3Fprimary%3D%25230af", links[2].GetAttribute("href"));
        Assert.Equal(["System", "Light", "Dark"], links.Select(a => a.TextContent.Trim()));

        // No choice saved: System is current.
        Assert.Equal("true", links[0].GetAttribute("aria-current"));
        Assert.Null(links[2].GetAttribute("aria-current"));
    }

    [Fact]
    public void Switch_shows_the_saved_choice_and_a_bound_value_wins()
    {
        UseCookie("ib-scheme=dark");
        var saved = Render<ColorSchemeSwitch>().FindAll("a.ib-segment");
        Assert.Equal("true", saved[2].GetAttribute("aria-current"));

        var bound = Render<ColorSchemeSwitch>(p => p.Add(s => s.Value, ColorScheme.Light)).FindAll("a.ib-segment");
        Assert.Equal("true", bound[1].GetAttribute("aria-current"));
    }

    [Fact]
    public void Icon_only_keeps_the_names_for_screen_readers_and_tooltips()
    {
        UseCookie(null);
        var cut = Render<ColorSchemeSwitch>(p => p.Add(s => s.IconOnly, true).Add(s => s.Size, ControlSize.Sm));

        Assert.Contains("ib-segmented-icon-only", cut.Find("ib-scheme-switch").ClassName);
        Assert.Contains("ib-scheme-switch-sm", cut.Find("ib-scheme-switch").ClassName);
        var light = cut.FindAll("a.ib-segment")[1];
        Assert.Equal("Light", light.GetAttribute("title"));
        Assert.Equal("Light", light.QuerySelector(".ib-visually-hidden")!.TextContent);
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData(null, ColorScheme.Dark, "dark")]
    [InlineData("ib-scheme=light", ColorScheme.Dark, "light")]
    [InlineData("ib-scheme=system", ColorScheme.Dark, null)]
    public void Theme_renders_the_saved_scheme_else_its_default(string? cookie, ColorScheme? fallback, string? expected)
    {
        UseCookie(cookie);
        var cut = Render<IzzyTheme>(p => p.Add(t => t.Scheme, fallback));

        var meta = cut.FindAll("meta[name='ib-scheme']");
        if (expected is null)
        {
            Assert.Empty(meta);
        }
        else
        {
            Assert.Equal(expected, Assert.Single(meta).GetAttribute("content"));
        }
    }
}

public class ColorSchemeEndpointTests
{
    [Theory]
    [InlineData("", "static/brand?x=1", "/static/brand?x=1")]
    [InlineData("/app", "static/brand", "/app/static/brand")]
    [InlineData("", "", "/")]
    [InlineData("", null, "/")]
    [InlineData("", "//evil.example", "/")]
    [InlineData("", "/\\evil.example", "/")]
    [InlineData("", "a\\b", "/")]
    [InlineData("/app", "/elsewhere", "/app/")]
    public void Return_url_stays_on_the_site(string pathBase, string? relative, string expected) =>
        Assert.Equal(expected, IzzyBlazorEndpointRouteBuilderExtensions.ReturnUrl(new PathString(pathBase), relative));

    [Fact]
    public void Scheme_endpoint_saves_the_cookie_and_goes_back()
    {
        var context = new DefaultHttpContext();
        context.Request.PathBase = "/app";

        var result = IzzyBlazorEndpointRouteBuilderExtensions.SetScheme(context, "dark", "static/brand");

        var cookie = context.Response.Headers.SetCookie.ToString();
        Assert.StartsWith("ib-scheme=dark;", cookie);
        Assert.Contains("path=/app", cookie);
        Assert.Contains("samesite=lax", cookie);
        Assert.DoesNotContain("httponly", cookie);
        Assert.Equal("/app/static/brand", Assert.IsType<RedirectHttpResult>(result).Url);
    }

    [Fact]
    public void An_unknown_value_saves_system()
    {
        var context = new DefaultHttpContext();
        IzzyBlazorEndpointRouteBuilderExtensions.SetScheme(context, "purple", null);
        Assert.StartsWith("ib-scheme=system;", context.Response.Headers.SetCookie.ToString());
    }
}
