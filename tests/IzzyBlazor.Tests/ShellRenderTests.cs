using Microsoft.Extensions.DependencyInjection;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Http;

namespace IzzyBlazor.Tests;

public class ShellRenderTests : BunitContext
{
    private void GoTo(string path) => Services.GetRequiredService<BunitNavigationManager>().NavigateTo(path);

    [Fact]
    public void Badge_classes_dot_and_pill()
    {
        var badge = Render<Badge>(p => p
            .Add(b => b.Status, Status.Success)
            .Add(b => b.Variant, BadgeVariant.Solid)
            .Add(b => b.Pill, true)
            .Add(b => b.Dot, true)
            .AddChildContent("Live")).Find("span");

        Assert.Equal("ib-badge ib-badge-success ib-badge-solid ib-badge-pill", badge.ClassName);
        Assert.NotNull(badge.QuerySelector(".ib-badge-dot[aria-hidden=true]"));
    }

    [Fact(Timeout = 10000)]
    public async Task A_brand_with_an_icon_in_the_sidebar_header_renders_once()
    {
        // It used to tell the sidebar it had changed on every render, and the sidebar re-rendered it.
        var shell = await Task.Run(() => Render<SidebarBrandProbe>());

        Assert.Single(shell.FindAll("a.ib-shell-brand"));
    }

    [Fact]
    public void SidebarToggle_links_to_the_sidebar()
    {
        var a = Render<SidebarToggle>().Find("a");

        Assert.Equal("./#ib-sidebar", a.GetAttribute("href"));
        Assert.Equal("ib-sidebar", a.GetAttribute("aria-controls"));
        Assert.Equal("Open menu", a.GetAttribute("aria-label"));
        // A plain link until the shell's script makes it a button and keeps aria-expanded.
        Assert.Null(a.GetAttribute("role"));
        Assert.Null(a.GetAttribute("aria-expanded"));
    }

    [Theory]
    [InlineData("/projects", "/projects", NavLinkMatch.Prefix, true)]
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

    [Fact]
    public void NavItem_links_to_other_sites_are_never_current()
    {
        GoTo("/org");
        var a = Render<NavItem>(p => p.Add(n => n.Href, "https://github.com/org").AddChildContent("GitHub")).Find("a");

        Assert.Null(a.GetAttribute("aria-current"));
    }

    [Fact]
    public void NavItem_updates_when_the_location_changes()
    {
        GoTo("/inbox");
        var cut = Render<NavItem>(p => p.Add(n => n.Href, "/settings").AddChildContent("Settings"));
        Assert.Null(cut.Find("a").GetAttribute("aria-current"));

        GoTo("/settings");

        cut.WaitForAssertion(() => Assert.Equal("page", cut.Find("a").GetAttribute("aria-current")));
    }

    [Theory]
    [InlineData("inbox", 0)]
    [InlineData("inbox?view=starred", 1)]
    [InlineData("inbox?view=archive", 2)]
    [InlineData("inbox?view=other", 0)]
    public void Nav_links_told_apart_by_the_query_mark_one(string uri, int current)
    {
        // The links with a query are in a group, rendered after the one without.
        GoTo(uri);
        var cut = Render<NavMenu>(p => p.Add(n => n.ChildContent, InboxLinks));

        Assert.Equal(Enumerable.Range(0, 3).Select(i => i == current ? "page" : null).ToArray(), CurrentOf(cut));
    }

    [Fact]
    public void Nav_links_told_apart_by_the_query_follow_the_location()
    {
        GoTo("inbox");
        var cut = Render<NavMenu>(p => p.Add(n => n.ChildContent, InboxLinks));

        GoTo("inbox?view=starred");
        cut.WaitForAssertion(() => Assert.Equal(new[] { null, "page", null }, CurrentOf(cut)));

        GoTo("inbox");
        cut.WaitForAssertion(() => Assert.Equal(new[] { "page", null, null }, CurrentOf(cut)));
    }

    [Fact]
    public void A_link_gives_way_only_to_a_link_to_the_same_page()
    {
        // A section link stays current above a page whose link has a query.
        GoTo("projects/42?tab=files");
        var cut = Render<NavMenu>(p => p.Add(n => n.ChildContent, builder =>
        {
            builder.OpenComponent<NavItem>(0);
            builder.AddComponentParameter(1, nameof(NavItem.Href), "projects");
            builder.CloseComponent();
            builder.OpenComponent<NavItem>(2);
            builder.AddComponentParameter(3, nameof(NavItem.Href), "projects/42?tab=files");
            builder.CloseComponent();
        }));

        Assert.Equal(new[] { "true", "page" }, CurrentOf(cut));
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

    [Fact]
    public void NavGroup_opens_for_its_active_prefix()
    {
        GoTo("/projects/website");
        var open = Render<NavGroup>(p => p.Add(g => g.Label, "Projects").Add(g => g.ActivePrefix, "/projects"));
        var closed = Render<NavGroup>(p => p.Add(g => g.Label, "Settings").Add(g => g.ActivePrefix, "/settings"));

        Assert.True(open.Find("details").HasAttribute("open"));
        Assert.False(closed.Find("details").HasAttribute("open"));
    }

    [Fact]
    public void Visibility_props_work_on_every_component_and_hide_the_whole_item()
    {
        var item = Render<NavItem>(p => p.Add(n => n.Href, "/docs").Add(n => n.HiddenFrom, Breakpoint.Lg).AddChildContent("Docs"));
        Assert.Equal("ib-nav-li ib-hidden-from-lg", item.Find("li").ClassName);
        Assert.DoesNotContain("ib-hidden-from", item.Find("a").ClassName);

        var menu = Render<NavMenu>(p => p.Add(n => n.Orientation, Orientation.Horizontal).Add(n => n.VisibleFrom, Breakpoint.Lg));
        Assert.Equal("ib-nav ib-nav-horizontal ib-visible-from-lg", menu.Find("ul").ClassName);

        var button = Render<Button>(p => p.Add(b => b.HiddenFrom, Breakpoint.Md).AddChildContent("Menu"));
        Assert.Contains("ib-hidden-from-md", button.Find("button").ClassName);
    }

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
    public void AppSidebar_side_height_width_and_breakpoint()
    {
        var nav = Render<AppSidebar>(p => p
            .Add(s => s.Position, SidebarPosition.End)
            .Add(s => s.FullHeight, true)
            .Add(s => s.Width, Length.Rem(18))
            .Add(s => s.Breakpoint, Breakpoint.Lg)).Find("nav");

        Assert.Equal("ib-shell-sidebar ib-shell-sidebar-bp-lg ib-shell-sidebar-end ib-shell-drawer-end ib-shell-sidebar-full", nav.ClassName);
        Assert.Equal("--ib-sidebar-w:18rem;", nav.GetAttribute("style"));
    }

    [Fact]
    public void DrawerPosition_follows_Position_unless_set()
    {
        string? Classes(SidebarPosition position, SidebarPosition? drawer) => Render<AppSidebar>(p => p
            .Add(s => s.Position, position)
            .Add(s => s.DrawerPosition, drawer)).Find("nav").ClassName;

        Assert.Equal("ib-shell-sidebar ib-shell-sidebar-bp-md", Classes(SidebarPosition.Start, null));
        Assert.Equal("ib-shell-sidebar ib-shell-sidebar-bp-md ib-shell-drawer-end", Classes(SidebarPosition.Start, SidebarPosition.End));
        Assert.Equal("ib-shell-sidebar ib-shell-sidebar-bp-md ib-shell-sidebar-end", Classes(SidebarPosition.End, SidebarPosition.Start));
    }

    [Fact]
    public void Parts_take_colors_and_bars_take_positions()
    {
        var header = Render<AppHeader>(p => p.Add(h => h.Bg, Color.Primary).Add(h => h.Position, BarPosition.Static)).Find("header");
        Assert.Equal("ib-shell-header", header.ClassName);
        Assert.Equal("--ib-shell-header-bg:var(--ib-color-primary);--ib-shell-header-fg:var(--ib-color-on-primary);", header.GetAttribute("style"));

        var footer = Render<AppFooter>(p => p.Add(f => f.Position, BarPosition.Sticky)).Find("footer");
        Assert.Equal("ib-shell-footer ib-shell-footer-sticky", footer.ClassName);

        var aside = Render<AppAside>(p => p.Add(a => a.Bg, Color.SurfaceContainer)).Find("aside");
        Assert.Equal("ib-shell-aside ib-shell-aside-bp-lg", aside.ClassName);
        Assert.Contains("--ib-shell-aside-bg:var(--ib-color-surface-container);", aside.GetAttribute("style"));
    }

    [Fact]
    public void AppShell_scroll_mode_and_dividers()
    {
        var shell = Render<AppShell>(p => p.Add(s => s.ScrollMode, ShellScroll.Main).Add(s => s.Dividers, false)).Find("ib-shell");

        Assert.Equal("ib-shell ib-shell-scroll-main ib-shell-tinted", shell.ClassName);
        Assert.Equal("--ib-shell-divider:transparent;", shell.GetAttribute("style"));
    }

    [Fact]
    public void ContentWidth_on_the_shell_and_on_parts()
    {
        var shell = Render<AppShell>(p => p.Add(s => s.ContentWidth, Length.Rem(42))).Find("ib-shell");
        Assert.Equal("ib-shell ib-shell-scroll-page ib-shell-tinted ib-shell-measured", shell.ClassName);
        Assert.Equal("--ib-shell-content-w:42rem;", shell.GetAttribute("style"));

        var header = Render<AppHeader>(p => p.Add(h => h.ContentWidth, Length.Rem(64))).Find("header");
        Assert.Equal("ib-shell-header ib-shell-header-sticky ib-shell-part-measured", header.ClassName);
        Assert.Equal("--ib-shell-content-w:64rem;", header.GetAttribute("style"));

        var main = Render<AppMain>(p => p.Add(m => m.ContentWidth, Length.Prose).AddChildContent("<p>Hi</p>")).Find("main");
        Assert.Equal("ib-shell-main ib-shell-main-measured", main.ClassName);
        Assert.Equal("--ib-shell-content-w:var(--ib-size-prose);", main.GetAttribute("style"));
        Assert.Equal("Hi", main.QuerySelector(":scope > .ib-shell-main-column > p")!.TextContent);

        // The shell's width reaches the main area too: its content goes in the column.
        var measuredShell = Render<AppShell>(p => p
            .Add(s => s.ContentWidth, Length.Rem(42))
            .Add(s => s.ChildContent, Parts((typeof(AppMain), [], "Body"))));
        Assert.NotNull(measuredShell.Find("main.ib-shell-main-measured > .ib-shell-main-column"));

        var plain = Render<AppMain>(p => p.AddChildContent("<p>Hi</p>")).Find("main");
        Assert.Empty(plain.QuerySelectorAll(".ib-shell-main-column"));

        var footer = Render<AppFooter>().Find("footer");
        Assert.Equal("ib-shell-footer", footer.ClassName);
        Assert.Null(footer.GetAttribute("style"));
    }

    [Theory]
    [InlineData(ShellVariant.Tinted, "ib-shell-tinted")]
    [InlineData(ShellVariant.Inset, "ib-shell-inset")]
    [InlineData(ShellVariant.Flat, "ib-shell-flat")]
    [InlineData(ShellVariant.Canvas, "ib-shell-canvas")]
    public void AppShell_variant_sets_its_class(ShellVariant variant, string expected)
    {
        var shell = Render<AppShell>(p => p.Add(s => s.Variant, variant)).Find("ib-shell");

        Assert.Equal($"ib-shell ib-shell-scroll-page {expected}", shell.ClassName);
    }

    [Fact]
    public void AppMain_uses_the_shell_main_id()
    {
        var cut = Render<AppShell>(p => p
            .Add(s => s.MainId, "content")
            .Add(s => s.ChildContent, Parts((typeof(AppMain), [], "Body"))));

        Assert.Equal("./#content", cut.Find(".ib-skip-link").GetAttribute("href"));
        Assert.Equal("content", cut.Find("main").Id);
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
    [InlineData(null, "ib-shell-collapse ib-shell-collapse-auto")]
    [InlineData(true, "ib-shell-collapse")]
    [InlineData(false, "ib-shell-collapse ib-shell-collapse-nobutton")]
    public void CollapseButton_is_automatic_unless_forced(bool? button, string expected)
    {
        var cut = Render<AppSidebar>(p => p
            .Add(s => s.Collapsible, true)
            .Add(s => s.CollapseButton, button));

        Assert.Equal(expected, cut.Find(".ib-shell-collapse").ClassName);
    }

    [Fact]
    public void Sidebar_header_and_footer_frame_the_scrolling_content()
    {
        var inner = Render<AppSidebar>(p => p
            .AddChildContent(b =>
            {
                b.AddContent(0, "Links");
                b.OpenComponent<SidebarFooter>(1);
                b.AddComponentParameter(2, nameof(SidebarFooter.ChildContent), (RenderFragment)(f => f.AddContent(0, "Account")));
                b.CloseComponent();
                b.OpenComponent<SidebarHeader>(3);
                b.AddComponentParameter(4, nameof(SidebarHeader.ChildContent), (RenderFragment)(h => h.AddContent(0, "Brand")));
                b.CloseComponent();
            })).Find(".ib-shell-sidebar-inner");

        // Written anywhere among the content; the drawer's close button goes in the header row.
        var rows = inner.Children.Select(c => c.ClassName).ToArray();
        Assert.Equal(new[] { "ib-shell-sidebar-head", "ib-shell-sidebar-body", "ib-shell-sidebar-foot" }, rows);
        Assert.Equal("Brand", inner.QuerySelector(".ib-shell-sidebar-head > .ib-shell-sidebar-slot")!.TextContent);
        Assert.NotNull(inner.QuerySelector(".ib-shell-sidebar-head > .ib-shell-sidebar-close"));
        Assert.Equal("Account", inner.QuerySelector(".ib-shell-sidebar-foot > .ib-shell-sidebar-slot")!.TextContent);
        Assert.Equal("Links", inner.QuerySelector(".ib-shell-sidebar-body")!.TextContent.Trim());
    }

    [Theory]
    [InlineData(VerticalPosition.Bottom, true, ".ib-shell-sidebar-foot > .ib-shell-collapse")]
    [InlineData(VerticalPosition.Top, true, ".ib-shell-sidebar-head > .ib-shell-collapse")]
    [InlineData(VerticalPosition.Bottom, false, ".ib-shell-sidebar-body + .ib-shell-collapse")]
    [InlineData(VerticalPosition.Top, false, ".ib-shell-sidebar-close + .ib-shell-collapse")]
    public void Chevron_goes_in_the_chosen_row(VerticalPosition position, bool slots, string selector)
    {
        RenderFragment slot = b => b.AddContent(0, "x");
        var cut = Render<AppSidebar>(p => p
            .Add(s => s.Collapsible, true)
            .Add(s => s.CollapseButtonPosition, position)
            .AddChildContent(b =>
            {
                if (slots)
                {
                    b.OpenComponent<SidebarHeader>(0);
                    b.AddComponentParameter(1, nameof(SidebarHeader.ChildContent), slot);
                    b.CloseComponent();
                    b.OpenComponent<SidebarFooter>(2);
                    b.AddComponentParameter(3, nameof(SidebarFooter.ChildContent), slot);
                    b.CloseComponent();
                }
            }));

        Assert.Single(cut.FindAll(".ib-shell-collapse"));
        Assert.NotNull(cut.Find(selector));
    }

    [Theory]
    [InlineData(ColorScheme.Auto, null)]
    [InlineData(ColorScheme.Light, "ib-scheme-light")]
    [InlineData(ColorScheme.Dark, "ib-scheme-dark")]
    [InlineData(ColorScheme.Inverse, "ib-scheme-inverse")]
    public void Parts_and_boxes_take_a_color_scheme(ColorScheme scheme, string? expected)
    {
        var sidebar = Render<AppSidebar>(p => p.Add(s => s.ColorScheme, scheme)).Find("nav");
        var header = Render<AppHeader>(p => p.Add(h => h.ColorScheme, scheme)).Find("header");
        var box = Render<Box>(p => p.Add(b => b.ColorScheme, scheme)).Find("div");

        foreach (var element in new[] { sidebar, header, box })
        {
            var schemes = element.ClassList.Where(c => c.StartsWith("ib-scheme-")).ToArray();
            Assert.Equal(expected is null ? Array.Empty<string>() : new[] { expected! }, schemes);
        }
    }

    [Fact]
    public void Sidebar_without_Collapsible_has_no_collapse_control()
    {
        Assert.Empty(Render<AppSidebar>().FindAll(".ib-shell-collapse-input"));
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

    [Fact]
    public void SidebarToggle_also_labels_the_collapse_checkbox()
    {
        var label = Render<SidebarToggle>().Find("label");

        Assert.Equal("ib-sidebar-collapse", label.GetAttribute("for"));
        Assert.Contains("ib-shell-collapse-toggle", label.ClassName);
        Assert.DoesNotContain("ib-shell-toggle ", label.ClassName + " ");
        Assert.Equal("true", label.GetAttribute("aria-hidden"));
    }
}

internal sealed class FixedHttpContextAccessor : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; }
}
