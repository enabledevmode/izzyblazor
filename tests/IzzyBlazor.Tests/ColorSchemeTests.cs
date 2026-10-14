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
    public void Default_is_an_icon_button_with_a_menu_of_links_to_the_scheme_endpoint()
    {
        UseCookie(null);
        Services.GetRequiredService<NavigationManager>().NavigateTo("static/brand?primary=%230af");
        var cut = Render<ColorSchemeSwitch>();

        Assert.Contains("ib-scheme-switch-menu", cut.Find("ib-scheme-switch").ClassName);
        var trigger = cut.Find("button[popovertarget]");
        Assert.Equal("Color scheme", trigger.GetAttribute("aria-label"));
        Assert.Contains("ib-btn-ghost", trigger.ClassName);
        Assert.NotNull(trigger.QuerySelector(".ib-scheme-if-light"));
        Assert.NotNull(trigger.QuerySelector(".ib-scheme-if-dark"));

        var items = cut.FindAll("[popover] a.ib-menu-item");
        Assert.Equal(["system", "light", "dark"], items.Select(a => a.GetAttribute("data-ib-scheme")));
        Assert.Equal("_izzy/scheme?value=dark&return=static%2Fbrand%3Fprimary%3D%25230af", items[2].GetAttribute("href"));
        Assert.Equal(["System", "Light", "Dark"], items.Select(a => a.QuerySelector(".ib-menu-label")!.TextContent));

        // Static: radio roles wait for the script; no choice saved, so System is checked.
        Assert.Equal("menuitemradio", items[0].GetAttribute("data-ib-role"));
        Assert.Equal(["true", "false", "false"], items.Select(a => a.GetAttribute("data-ib-checked")));
        Assert.Equal("true", items[0].GetAttribute("aria-current"));
        Assert.All(items, a => Assert.NotNull(a.QuerySelector(".ib-scheme-check")));
    }

    [Fact]
    public void Menu_checks_the_saved_choice_and_a_bound_value_wins()
    {
        UseCookie("ib-scheme=dark");
        var saved = Render<ColorSchemeSwitch>().FindAll("a.ib-menu-item");
        Assert.Equal(["false", "false", "true"], saved.Select(a => a.GetAttribute("data-ib-checked")));

        var bound = Render<ColorSchemeSwitch>(p => p.Add(s => s.Value, ColorScheme.Light)).FindAll("a.ib-menu-item");
        Assert.Equal(["false", "true", "false"], bound.Select(a => a.GetAttribute("data-ib-checked")));
    }

    [Fact]
    public void Toggle_is_two_links_and_css_shows_the_one_away_from_the_page_scheme()
    {
        UseCookie(null);
        var links = Render<ColorSchemeSwitch>(p => p.Add(s => s.Variant, ColorSchemeSwitchVariant.Toggle)).FindAll("a");

        Assert.Equal(2, links.Count);
        Assert.Equal("dark", links[0].GetAttribute("data-ib-scheme"));
        Assert.Contains("ib-scheme-if-light", links[0].ClassName);
        Assert.Equal("Switch to dark mode", links[0].GetAttribute("aria-label"));
        Assert.Equal("light", links[1].GetAttribute("data-ib-scheme"));
        Assert.Contains("ib-scheme-if-dark", links[1].ClassName);
        Assert.Equal("Switch to light mode", links[1].GetAttribute("aria-label"));
    }

    [Fact]
    public void Segmented_is_a_group_with_the_choice_marked()
    {
        UseCookie("ib-scheme=dark");
        var cut = Render<ColorSchemeSwitch>(p => p
            .Add(s => s.Variant, ColorSchemeSwitchVariant.Segmented)
            .Add(s => s.IconOnly, true)
            .Add(s => s.Size, ControlSize.Sm));

        var root = cut.Find("ib-scheme-switch");
        Assert.Equal("group", root.GetAttribute("role"));
        Assert.Equal("Color scheme", root.GetAttribute("aria-label"));
        Assert.Contains("ib-segmented", root.ClassName);
        Assert.Contains("ib-segmented-icon-only", root.ClassName);
        Assert.Contains("ib-scheme-switch-sm", root.ClassName);

        var segments = cut.FindAll("a.ib-segment");
        Assert.Equal("true", segments[2].GetAttribute("aria-current"));
        Assert.Equal("Light", segments[1].GetAttribute("title"));
        Assert.Equal("Light", segments[1].QuerySelector(".ib-visually-hidden")!.TextContent);
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
