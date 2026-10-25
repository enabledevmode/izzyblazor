using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

public class TabsExtrasRenderTests : BunitContext
{
    public TabsExtrasRenderTests() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    private void GoTo(string path) => Services.GetRequiredService<BunitNavigationManager>().NavigateTo(path);

    private static RenderFragment Links(bool disableLast = false) => builder =>
    {
        foreach (var (href, label, disabled) in new[] { ("settings/profile", "Profile", false), ("settings/billing", "Billing", false), ("settings/security", "Security", disableLast) })
        {
            builder.OpenComponent<TabLink>(0);
            builder.AddComponentParameter(1, nameof(TabLink.Href), href);
            builder.AddComponentParameter(2, nameof(TabLink.Label), label);
            builder.AddComponentParameter(3, nameof(TabLink.Disabled), disabled);
            builder.CloseComponent();
        }
    };

    [Fact]
    public void Link_tabs_render_a_nav_of_links_with_the_current_page_marked()
    {
        GoTo("settings/billing");
        var cut = Render<Tabs>(p => p.Add(t => t.Label, "Settings").Add(t => t.ChildContent, Links()));

        var nav = cut.Find("nav");
        var links = cut.FindAll("nav .ib-tabs-list > a.ib-tab");

        Assert.Equal("Settings", nav.GetAttribute("aria-label"));
        Assert.Contains("ib-tabs-nav", nav.ClassList);
        Assert.Equal(new[] { "settings/profile", "settings/billing", "settings/security" }, links.Select(a => a.GetAttribute("href")).ToArray());
        Assert.Equal(new string?[] { null, "page", null }, links.Select(a => a.GetAttribute("aria-current")).ToArray());
        Assert.Empty(cut.FindAll("input[type=radio]"));
    }

    [Fact]
    public void Link_tab_above_the_current_page_is_marked_true()
    {
        GoTo("settings/billing/invoices/42");
        var cut = Render<Tabs>(p => p.Add(t => t.Label, "Tabs").Add(t => t.ChildContent, Links()));

        Assert.Equal("true", cut.FindAll("a.ib-tab")[1].GetAttribute("aria-current"));
    }

    [Fact]
    public void Link_tabs_follow_navigation()
    {
        GoTo("settings/profile");
        var cut = Render<Tabs>(p => p.Add(t => t.Label, "Tabs").Add(t => t.ChildContent, Links()));

        GoTo("settings/security");

        cut.WaitForAssertion(() => Assert.Equal("page", cut.FindAll("a.ib-tab")[2].GetAttribute("aria-current")));
        Assert.Null(cut.FindAll("a.ib-tab")[0].GetAttribute("aria-current"));
    }

    [Theory]
    [InlineData("settings", 0)]
    [InlineData("settings?view=billing", 1)]
    [InlineData("settings?view=team", 2)]
    [InlineData("settings?view=other", 0)]
    public void Link_tabs_told_apart_by_the_query_mark_one(string uri, int current)
    {
        GoTo(uri);
        var cut = Render<Tabs>(p => p.Add(t => t.Label, "Tabs").Add(t => t.ChildContent, builder =>
        {
            foreach (var (href, label) in new[] { ("settings", "Profile"), ("settings?view=billing", "Billing"), ("settings?view=team", "Team") })
            {
                builder.OpenComponent<TabLink>(0);
                builder.AddComponentParameter(1, nameof(TabLink.Href), href);
                builder.AddComponentParameter(2, nameof(TabLink.Label), label);
                builder.CloseComponent();
            }
        }));

        var marked = cut.FindAll("a.ib-tab").Select(a => a.GetAttribute("aria-current")).ToArray();
        Assert.Equal(Enumerable.Range(0, 3).Select(i => i == current ? "page" : null).ToArray(), marked);
    }

    [Fact]
    public void Disabled_link_tab_has_no_href_and_reads_as_a_disabled_link()
    {
        var cut = Render<Tabs>(p => p.Add(t => t.Label, "Tabs").Add(t => t.ChildContent, Links(disableLast: true)));
        var disabled = cut.Find("span.ib-tab-disabled");

        Assert.Equal(("link", "true"), (disabled.GetAttribute("role"), disabled.GetAttribute("aria-disabled")));
        Assert.Contains("Security", disabled.TextContent);
        Assert.Equal(2, cut.FindAll("a.ib-tab").Count);
    }

    [Fact]
    public void Mixing_tabs_and_link_tabs_is_an_error()
    {
        RenderFragment mixed = builder =>
        {
            builder.OpenComponent<Tab>(0);
            builder.AddComponentParameter(1, nameof(Tab.Label), "Panel");
            builder.CloseComponent();
            builder.OpenComponent<TabLink>(2);
            builder.AddComponentParameter(3, nameof(TabLink.Href), "elsewhere");
            builder.AddComponentParameter(4, nameof(TabLink.Label), "Link");
            builder.CloseComponent();
        };

        Assert.ThrowsAny<InvalidOperationException>(() => Render<Tabs>(p => p.Add(t => t.Label, "Tabs").Add(t => t.ChildContent, mixed)));
    }

    [Fact]
    public void Vertical_tabs_mark_their_orientation()
    {
        RenderFragment two = builder =>
        {
            builder.OpenComponent<Tab>(0);
            builder.AddComponentParameter(1, nameof(Tab.Label), "One");
            builder.CloseComponent();
            builder.OpenComponent<Tab>(2);
            builder.AddComponentParameter(3, nameof(Tab.Label), "Two");
            builder.CloseComponent();
        };
        var cut = Render<Tabs>(p => p.Add(t => t.Label, "Tabs").Add(t => t.Orientation, Orientation.Vertical).Add(t => t.ChildContent, two));

        Assert.Contains("ib-tabs-vertical", cut.Find("ib-tabs").ClassList);
        Assert.Equal("vertical", cut.Find(".ib-tabs-list").GetAttribute("aria-orientation"));
        Assert.Null(Render<Tabs>(p => p.Add(t => t.Label, "Tabs").Add(t => t.ChildContent, two)).Find(".ib-tabs-list").GetAttribute("aria-orientation"));
    }

    [Fact]
    public void TabLink_needs_a_Tabs()
    {
        Assert.ThrowsAny<InvalidOperationException>(() => Render<TabLink>(p => p.Add(l => l.Href, "x").Add(l => l.Label, "X")));
    }
}
