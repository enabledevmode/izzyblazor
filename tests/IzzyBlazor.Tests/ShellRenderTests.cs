using Microsoft.Extensions.DependencyInjection;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Http;

namespace IzzyBlazor.Tests;

public class ShellRenderTests : BunitContext
{
    private void GoTo(string path) => Services.GetRequiredService<BunitNavigationManager>().NavigateTo(path);

    [Theory]
    [InlineData("/projects/website", "/projects", NavLinkMatch.Prefix, true)]
    [InlineData("/projects-old", "/projects", NavLinkMatch.Prefix, false)]
    [InlineData("/projects/website", "/projects", NavLinkMatch.All, false)]
    [InlineData("/settings", "/", NavLinkMatch.Prefix, false)]
    [InlineData("/", "/", NavLinkMatch.Prefix, true)]
    public void NavItem_active_state(string current, string href, NavLinkMatch match, bool active)
    {
        GoTo(current);
        var a = Render<NavItem>(p => p
            .Add(n => n.Href, href)
            .Add(n => n.Match, match)
            .AddChildContent("Link")).Find("a");

        // The page itself is "page"; a section above it is "true".
        Assert.Equal(active, a.GetAttribute("aria-current") is not null);
        Assert.Equal(active && current == href, a.GetAttribute("aria-current") == "page");
        Assert.Equal(active, a.ClassList.Contains("ib-nav-item-active"));
    }

    [Theory]
    [InlineData("inbox", 0)]
    [InlineData("inbox?view=starred", 1)]
    [InlineData("inbox?view=other", 0)]
    public void Nav_links_told_apart_by_the_query_mark_one(string uri, int current)
    {
        // The links with a query are in a group, rendered after the one without.
        GoTo(uri);
        var cut = Render<NavMenu>(p => p.Add(n => n.ChildContent, InboxLinks));

        Assert.Equal(Enumerable.Range(0, 3).Select(i => i == current ? "page" : null).ToArray(), CurrentOf(cut));
    }

    private static readonly RenderFragment InboxLinks = builder =>
    {
        builder.OpenComponent<NavItem>(0);
        builder.AddComponentParameter(1, nameof(NavItem.Href), "inbox");
        builder.AddComponentParameter(2, nameof(NavItem.ChildContent), (RenderFragment)(b => b.AddContent(0, "Inbox")));
        builder.CloseComponent();
        builder.OpenComponent<NavGroup>(3);
        builder.AddComponentParameter(4, nameof(NavGroup.Label), "Views");
        builder.AddComponentParameter(5, nameof(NavGroup.ChildContent), (RenderFragment)(group =>
        {
            foreach (var view in new[] { "starred", "archive" })
            {
                group.OpenComponent<NavItem>(0);
                group.AddComponentParameter(1, nameof(NavItem.Href), $"inbox?view={view}");
                group.AddComponentParameter(2, nameof(NavItem.ChildContent), (RenderFragment)(b => b.AddContent(0, view)));
                group.CloseComponent();
            }
        }));
        builder.CloseComponent();
    };

    private static string?[] CurrentOf(IRenderedComponent<NavMenu> cut) =>
        cut.FindAll("a.ib-nav-item").Select(a => a.GetAttribute("aria-current")).ToArray();

    private static RenderFragment Parts(params (Type Part, (string Name, object Value)[] Parameters, string Text)[] parts) => builder =>
    {
        foreach (var (part, parameters, text) in parts)
        {
            builder.OpenComponent(0, part);
            foreach (var (name, value) in parameters)
            {
                builder.AddComponentParameter(1, name, value);
            }

            builder.AddComponentParameter(2, "ChildContent", (RenderFragment)(b => b.AddContent(0, text)));
            builder.CloseComponent();
        }
    };

    [Fact]
    public void AppShell_renders_landmarks_skip_link_and_drawer()
    {
        var cut = Render<AppShell>(p => p.Add(s => s.ChildContent, Parts(
            (typeof(AppHeader), [], "Brand"),
            (typeof(AppSidebar), [], "Nav"),
            (typeof(AppMain), [], "Body"))));

        Assert.Equal("./#ib-main", cut.Find(".ib-skip-link").GetAttribute("href"));
        Assert.Equal("ib-shell-header ib-shell-header-sticky", cut.Find("header").ClassName);
        var nav = cut.Find("nav");
        Assert.Equal("ib-sidebar", nav.Id);
        Assert.Equal("Main", nav.GetAttribute("aria-label"));
        Assert.Equal("ib-shell-sidebar ib-shell-sidebar-bp-md", nav.ClassName);
        Assert.Equal("--ib-sidebar-w:16rem;", nav.GetAttribute("style"));
        Assert.NotNull(nav.QuerySelector(".ib-shell-sidebar-inner [data-ib-shell-close]"));
        Assert.NotNull(cut.Find(".ib-shell-backdrop"));
        Assert.Equal("ib-main", cut.Find("main").Id);
        Assert.Equal("ib-shell ib-shell-scroll-page ib-shell-tinted", cut.Find("ib-shell").ClassName);
    }

    [Fact]
    public void Collapsible_sidebar_renders_a_checkbox_and_its_label()
    {
        var nav = Render<AppSidebar>(p => p
            .Add(s => s.Collapsible, true)
            .Add(s => s.CollapsedWidth, Length.Rem(4))).Find("nav");

        Assert.Contains("ib-shell-sidebar-collapsible", nav.ClassName);
        Assert.Contains("--ib-sidebar-collapsed-w:4rem;", nav.GetAttribute("style"));
        var input = nav.QuerySelector("input.ib-shell-collapse-input")!;
        Assert.Equal("ib-sidebar-collapse", input.Id);
        Assert.Equal("checkbox", input.GetAttribute("type"));
        Assert.Equal("Collapse sidebar", input.GetAttribute("aria-label"));
        Assert.Equal("ib-sidebar-collapsed", input.GetAttribute("data-ib-remember"));
        Assert.False(input.HasAttribute("checked"));
        Assert.Equal("ib-sidebar-collapse", nav.QuerySelector("label.ib-shell-collapse-btn")!.GetAttribute("for"));
    }

    [Theory]
    [InlineData(null, false, false)]
    [InlineData(null, true, true)]
    [InlineData("1", false, true)]
    [InlineData("0", true, false)]
    public void Remembered_cookie_wins_over_the_starting_value(string? cookie, bool collapsed, bool expected)
    {
        // Through the server package's IRequestCookies, as AddIzzyBlazor() registers it.
        var http = new DefaultHttpContext();
        if (cookie is not null)
        {
            http.Request.Headers.Cookie = $"ib-sidebar-collapsed={cookie}";
        }

        Services.AddSingleton<IHttpContextAccessor>(new FixedHttpContextAccessor { HttpContext = http });
        Services.AddScoped<IRequestCookies, HttpRequestCookies>();
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));

        var cut = Render<AppSidebar>(p => p
            .Add(s => s.Collapsible, true)
            .Add(s => s.Collapsed, collapsed));

        Assert.Equal(expected, cut.Find(".ib-shell-collapse-input").HasAttribute("checked"));
    }

    [Fact]
    public void Remembered_cookie_is_ignored_without_a_reader()
    {
        // WebAssembly (IzzyBlazor.Core alone): no request to read, so the starting value stands.
        SetRendererInfo(new RendererInfo("WebAssembly", isInteractive: true));

        var cut = Render<AppSidebar>(p => p.Add(s => s.Collapsible, true).Add(s => s.Collapsed, true));

        Assert.True(cut.Find(".ib-shell-collapse-input").HasAttribute("checked"));
    }

    [Fact]
    public void Change_event_reports_the_state()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        bool? reported = null;
        var cut = Render<AppSidebar>(p => p
            .Add(s => s.Collapsible, true)
            .Add(s => s.CollapsedChanged, (bool c) => reported = c));

        cut.Find(".ib-shell-collapse-input").Change(true);

        Assert.True(reported);
        Assert.Equal("1", cut.Find(".ib-shell-collapse-input").GetAttribute("data-ib-collapsed"));
    }

    [Fact]
    public void Setting_Collapsed_from_the_parent_updates_the_checkbox()
    {
        var cut = Render<AppSidebar>(p => p.Add(s => s.Collapsible, true));
        Assert.False(cut.Find(".ib-shell-collapse-input").HasAttribute("checked"));

        cut.Render(p => p.Add(s => s.Collapsed, true));

        Assert.True(cut.Find(".ib-shell-collapse-input").HasAttribute("checked"));
    }
}

internal sealed class FixedHttpContextAccessor : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; }
}
