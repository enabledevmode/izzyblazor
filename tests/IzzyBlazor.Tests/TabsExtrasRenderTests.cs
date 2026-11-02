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
    public void TabLink_Current_wins_over_the_URL()
    {
        // The page is billing, but the choice comes from elsewhere (a cookie, say): Profile is current.
        GoTo("settings/billing");
        var cut = Render<Tabs>(p => p.Add(t => t.Label, "Settings").Add(t => t.ChildContent, builder =>
        {
            foreach (var (href, label, current) in new[] { ("settings/profile", "Profile", (bool?)true), ("settings/billing", "Billing", false), ("settings/security", "Security", null) })
            {
                builder.OpenComponent<TabLink>(0);
                builder.AddComponentParameter(1, nameof(TabLink.Href), href);
                builder.AddComponentParameter(2, nameof(TabLink.Label), label);
                builder.AddComponentParameter(3, nameof(TabLink.Current), current);
                builder.CloseComponent();
            }
        }));

        var marked = cut.FindAll("a.ib-tab").Select(a => a.GetAttribute("aria-current")).ToArray();
        Assert.Equal(new string?[] { "true", null, null }, marked);
    }

    private static RenderFragment CurrentLinks(params (string Href, bool? Current)[] links) => builder =>
    {
        foreach (var (href, current) in links)
        {
            builder.OpenComponent<TabLink>(0);
            builder.AddComponentParameter(1, nameof(TabLink.Href), href);
            builder.AddComponentParameter(2, nameof(TabLink.Label), href);
            builder.AddComponentParameter(3, nameof(TabLink.Current), current);
            builder.CloseComponent();
        }
    };
}
