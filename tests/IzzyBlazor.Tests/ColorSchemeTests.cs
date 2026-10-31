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

    [Fact]
    public void A_bound_value_reaches_the_script_only_on_interactive_pages()
    {
        UseCookie(null);
        var staticSwitch = Render<ColorSchemeSwitch>(p => p.Add(s => s.Value, ColorScheme.Dark));
        Assert.False(staticSwitch.Find("ib-scheme-switch").HasAttribute("data-ib-value"));

        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var live = Render<ColorSchemeSwitch>(p => p.Add(s => s.Value, ColorScheme.Dark));
        Assert.Equal("dark", live.Find("ib-scheme-switch").GetAttribute("data-ib-value"));

        live.Render(p => p.Add(s => s.Value, ColorScheme.Auto));
        Assert.Equal("system", live.Find("ib-scheme-switch").GetAttribute("data-ib-value"));

        var unbound = Render<ColorSchemeSwitch>();
        Assert.False(unbound.Find("ib-scheme-switch").HasAttribute("data-ib-value"));
    }

    [Fact]
    public void A_pick_matching_a_stale_current_scheme_still_raises_ValueChanged()
    {
        // Interactive with no prerender: the cookie can't be read, so the switch thinks System
        // while the page is dark. Picking System must still reach the app.
        UseCookie("ib-scheme=dark");
        SetRendererInfo(new RendererInfo("WebAssembly", isInteractive: true));
        ColorScheme? picked = null;
        var cut = Render<ColorSchemeSwitch>(p => p.Add(s => s.ValueChanged, (ColorScheme v) => picked = v));

        cut.Find("ib-scheme-switch").TriggerEvent("onibscheme", new IzzySchemeEventArgs { Scheme = "system" });

        Assert.Equal(ColorScheme.Auto, picked);
    }

    [Fact]
    public void Toggle_is_two_buttons_and_css_shows_the_one_away_from_the_page_scheme()
    {
        UseCookie(null);
        var links = Render<ColorSchemeSwitch>(p => p.Add(s => s.Variant, ColorSchemeSwitchVariant.Toggle)).FindAll("button");

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

        var segments = cut.FindAll("button.ib-segment");
        Assert.Equal("true", segments[2].GetAttribute("aria-pressed"));
        Assert.Equal("false", segments[1].GetAttribute("aria-pressed"));
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
