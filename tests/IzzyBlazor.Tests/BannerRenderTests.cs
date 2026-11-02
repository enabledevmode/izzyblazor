using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

public class BannerRenderTests : BunitContext
{
    // Services must be registered before anything starts the renderer, SetRendererInfo included.
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
    public void Dismissible_banner_works_without_script_and_reports_to_csharp()
    {
        UseCookie(null);
        bool? reported = null;
        var cut = Render<Banner>(p => p
            .Add(b => b.Id, "b1")
            .Add(b => b.Dismissible, true)
            .Add(b => b.DismissedChanged, (bool d) => reported = d)
            .AddChildContent("Hi"));

        var input = cut.Find("input.ib-banner-dismiss");
        Assert.Equal("b1-dismiss", input.Id);
        Assert.Equal("b1-dismiss", cut.Find("label.ib-banner-close").GetAttribute("for"));
        Assert.Equal("Dismiss", cut.Find("ib-dismiss > button.ib-banner-close").GetAttribute("aria-label"));

        input.Change(true);
        Assert.True(reported);
        Assert.Empty(cut.FindAll("ib-banner"));
    }

    [Theory]
    [InlineData("ib-banner-trial-ends=1", false)]
    [InlineData("ib-banner-old-key=1", true)]
    [InlineData(null, true)]
    public void A_remembered_dismissal_leaves_the_banner_out(string? cookie, bool shown)
    {
        UseCookie(cookie);
        var cut = Render<Banner>(p => p.Add(b => b.Dismissible, true).Add(b => b.RememberKey, "trial-ends").AddChildContent("Trial"));

        Assert.Equal(shown, cut.FindAll("ib-banner").Count == 1);
    }

    [Fact]
    public void A_dismissal_is_remembered_for_the_session_under_a_key_from_the_message_by_default()
    {
        UseCookie(null);
        string? Remember(string text, DismissMemory? memory = null) =>
            Render<Banner>(p =>
            {
                p.Add(b => b.Dismissible, true).AddChildContent(text);
                if (memory is { } m) p.Add(b => b.Remember, m);
            }).Find("input.ib-banner-dismiss").GetAttribute("data-ib-remember");

        var name = Remember("Maintenance on Sunday");
        Assert.StartsWith("ib-banner-auto-", name);
        Assert.Equal(name, Remember("Maintenance on Sunday"));
        // A new message gets a new key, so it shows again.
        Assert.NotEqual(name, Remember("Maintenance on Monday"));

        var session = Render<Banner>(p => p.Add(b => b.Dismissible, true).AddChildContent("x")).Find("input.ib-banner-dismiss");
        Assert.Equal("session", session.GetAttribute("data-ib-remember-for"));
        var persistent = Render<Banner>(p => p.Add(b => b.Dismissible, true).Add(b => b.Remember, DismissMemory.Persistent).AddChildContent("x")).Find("input.ib-banner-dismiss");
        Assert.Null(persistent.GetAttribute("data-ib-remember-for"));
        Assert.Null(Remember("x", DismissMemory.None));
    }

    private sealed class OpaqueText : ComponentBase
    {
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder) => builder.AddContent(0, "Localized");
    }

    [Theory]
    [InlineData("semi;colon")]
    public void RememberKey_must_be_cookie_safe(string key)
    {
        UseCookie(null);
        Assert.ThrowsAny<ArgumentException>(() => Render<Banner>(p => p.Add(b => b.RememberKey, key)));
    }
}
