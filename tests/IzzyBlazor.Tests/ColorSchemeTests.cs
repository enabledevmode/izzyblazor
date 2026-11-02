using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
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
        Services.GetRequiredService<NavigationManager>().NavigateTo("static/brand?brand=%230af");
        var cut = Render<ColorSchemeSwitch>();

        Assert.Contains("ib-scheme-switch-menu", cut.Find("ib-scheme-switch").ClassName);
        var trigger = cut.Find("button[popovertarget]");
        Assert.Equal("Color scheme", trigger.GetAttribute("aria-label"));
        Assert.Contains("ib-btn-ghost", trigger.ClassName);
        Assert.NotNull(trigger.QuerySelector(".ib-scheme-if-light"));
        Assert.NotNull(trigger.QuerySelector(".ib-scheme-if-dark"));

        var items = cut.FindAll("[popover] button.ib-menu-item");
        Assert.Equal(["system", "light", "dark"], items.Select(a => a.GetAttribute("data-ib-scheme")));
        Assert.Equal(["System", "Light", "Dark"], items.Select(a => a.QuerySelector(".ib-menu-label")!.TextContent));

        // No choice saved, so System is checked.
        Assert.Equal("menuitemradio", items[0].GetAttribute("role"));
        Assert.Equal(["true", "false", "false"], items.Select(a => a.GetAttribute("aria-checked")));
        Assert.All(items, a => Assert.NotNull(a.QuerySelector(".ib-scheme-check")));
    }

    [Fact]
    public void Menu_checks_the_saved_choice_and_a_bound_value_wins()
    {
        UseCookie("ib-scheme=dark");
        var saved = Render<ColorSchemeSwitch>().FindAll("button.ib-menu-item");
        Assert.Equal(["false", "false", "true"], saved.Select(a => a.GetAttribute("aria-checked")));

        var bound = Render<ColorSchemeSwitch>(p => p.Add(s => s.Value, ColorScheme.Light)).FindAll("button.ib-menu-item");
        Assert.Equal(["false", "true", "false"], bound.Select(a => a.GetAttribute("aria-checked")));
    }

    [Theory]
    [InlineData(null, ColorScheme.Dark, "dark")]
    [InlineData("ib-scheme=light", ColorScheme.Dark, "light")]
    [InlineData("ib-scheme=system", ColorScheme.Dark, null)]
    public void Theme_renders_the_saved_scheme_else_its_default(string? cookie, ColorScheme? fallback, string? expected)
    {
        UseCookie(cookie);
        var cut = Render<IzzyTheme>(p => p.Add(t => t.ColorScheme, fallback));

        var meta = cut.FindAll("meta[name='ib-color-scheme']");
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
