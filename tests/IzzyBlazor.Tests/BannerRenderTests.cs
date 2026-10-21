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
    public void Banner_is_a_labelled_region_with_status_colors()
    {
        UseCookie(null);
        var banner = Render<Banner>(p => p
            .Add(b => b.Status, Status.Warning)
            .Add(b => b.Title, "Heads up")
            .AddChildContent("Maintenance tonight.")).Find("ib-banner");

        Assert.Equal("region", banner.GetAttribute("role"));
        Assert.Equal("Announcement", banner.GetAttribute("aria-label"));
        Assert.Contains("ib-banner", banner.ClassName);
        Assert.Contains("ib-alert-warning", banner.ClassName);
        Assert.Contains("ib-alert-soft", banner.ClassName);
        Assert.Equal("Heads up", banner.QuerySelector(".ib-banner-title")!.TextContent);
        Assert.Contains("Warning: ", banner.QuerySelector(".ib-visually-hidden")!.TextContent);
        // Not announced by default: it's there when the page loads.
        Assert.Null(banner.QuerySelector(".ib-banner-body")!.GetAttribute("role"));
        Assert.NotNull(banner.QuerySelector(".ib-banner-icon"));
    }

    [Theory]
    [InlineData(Status.Info, "status")]
    [InlineData(Status.Danger, "alert")]
    public void Announce_sets_a_live_role_on_the_message(Status status, string role)
    {
        UseCookie(null);
        var body = Render<Banner>(p => p.Add(b => b.Status, status).Add(b => b.Announce, true).AddChildContent("Offline"))
            .Find(".ib-banner-body");

        Assert.Equal(role, body.GetAttribute("role"));
    }

    [Theory]
    [InlineData(BarPosition.Static, false)]
    [InlineData(BarPosition.Sticky, true)]
    public void Position_sticky_adds_the_sticky_class(BarPosition position, bool sticky)
    {
        UseCookie(null);
        var banner = Render<Banner>(p => p.Add(b => b.Position, position)).Find("ib-banner");

        Assert.Equal(sticky, banner.ClassList.Contains("ib-banner-sticky"));
    }

    [Fact]
    public void Accent_overrides_the_status_color()
    {
        UseCookie(null);
        var banner = Render<Banner>(p => p.Add(b => b.Accent, Accent.Primary).Add(b => b.Variant, AlertVariant.Solid))
            .Find("ib-banner");

        Assert.Contains("ib-alert-solid", banner.ClassName);
        Assert.Contains("--ib-accent:var(--ib-color-primary)", banner.GetAttribute("style"));
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

    [Fact]
    public void RememberKey_names_the_cookie_the_element_writes()
    {
        UseCookie(null);
        var input = Render<Banner>(p => p.Add(b => b.Dismissible, true).Add(b => b.RememberKey, "trial-ends"))
            .Find("input.ib-banner-dismiss");

        Assert.Equal("ib-banner-trial-ends", input.GetAttribute("data-ib-remember"));
        // A key alone still means the session (alpha.12), not a year.
        Assert.Equal("session", input.GetAttribute("data-ib-remember-for"));
    }

    [Theory]
    [InlineData("ib-banner-trial-ends=1", false)]
    [InlineData("ib-banner-trial-ends=0", true)]
    [InlineData("ib-banner-old-key=1", true)]
    [InlineData(null, true)]
    public void A_remembered_dismissal_leaves_the_banner_out(string? cookie, bool shown)
    {
        UseCookie(cookie);
        var cut = Render<Banner>(p => p.Add(b => b.Dismissible, true).Add(b => b.RememberKey, "trial-ends").AddChildContent("Trial"));

        Assert.Equal(shown, cut.FindAll("ib-banner").Count == 1);
    }

    [Fact]
    public void Remembered_banner_can_be_shown_again_from_csharp()
    {
        UseCookie("ib-banner-k=1");
        var cut = Render<Banner>(p => p.Add(b => b.Dismissible, true).Add(b => b.RememberKey, "k"));
        Assert.Empty(cut.FindAll("ib-banner"));

        cut.Render(p => p.Add(b => b.Dismissed, true));
        cut.Render(p => p.Add(b => b.Dismissed, false));

        Assert.Single(cut.FindAll("ib-banner"));
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

    [Fact]
    public void A_remembered_session_dismissal_leaves_the_banner_out()
    {
        UseCookie($"ib-banner-{Banner.AutoKeyFor(null, b => b.AddContent(0, "Alpha"))}=1");
        Assert.Empty(Render<Banner>(p => p.Add(b => b.Dismissible, true).AddChildContent("Alpha")).FindAll("ib-banner"));
        Assert.Single(Render<Banner>(p => p.Add(b => b.Dismissible, true).AddChildContent("Beta")).FindAll("ib-banner"));
    }

    [Fact]
    public void A_banner_whose_text_this_cant_read_is_remembered_only_under_a_RememberKey()
    {
        UseCookie(null);
        // Text from a component of the app's own: two different messages could get the same key.
        RenderFragment opaque = b => { b.OpenComponent<OpaqueText>(0); b.CloseComponent(); };
        Assert.Null(Banner.AutoKeyFor(null, opaque));
        Assert.Null(Render<Banner>(p => p.Add(b => b.Dismissible, true).Add(b => b.ChildContent, opaque)).Find("input.ib-banner-dismiss").GetAttribute("data-ib-remember"));

        // An Anchor's text and link are read, so the key follows them.
        RenderFragment Link(string text, string href = "/x") => b => { b.OpenComponent<Anchor>(0); b.AddComponentParameter(1, nameof(Anchor.Href), href); b.AddComponentParameter(2, nameof(Anchor.ChildContent), (RenderFragment)(c => c.AddContent(0, text))); b.CloseComponent(); };
        Assert.NotEqual(Banner.AutoKeyFor(null, Link("Read the notes")), Banner.AutoKeyFor(null, Link("See the schedule")));
        Assert.NotEqual(Banner.AutoKeyFor(null, Link("Details")), Banner.AutoKeyFor(null, Link("Details", "/status")));
    }

    private sealed class OpaqueText : ComponentBase
    {
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder) => builder.AddContent(0, "Localized");
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("semi;colon")]
    [InlineData("")]
    public void RememberKey_must_be_cookie_safe(string key)
    {
        UseCookie(null);
        Assert.ThrowsAny<ArgumentException>(() => Render<Banner>(p => p.Add(b => b.RememberKey, key)));
    }
}
