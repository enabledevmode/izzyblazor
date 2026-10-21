using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

/// <summary>The sidebar below its breakpoint: the drawer's top row, the panel, and links that stay on the page.</summary>
public class ShellSmallScreenTests : BunitContext
{
    [Fact]
    public void The_drawer_shows_the_shells_brand_in_its_top_row_when_the_sidebar_has_no_header()
    {
        var cut = Render<ShellProbe>();

        var row = cut.Find(".ib-shell-sidebar .ib-shell-sidebar-head");
        Assert.Equal("Acme", row.QuerySelector(".ib-shell-brand")?.TextContent.Trim());
        Assert.NotNull(row.QuerySelector("[data-ib-shell-close]"));

        // A SidebarHeader is the top row instead.
        var withHeader = Render<ShellProbe>(p => p.Add(s => s.WithHeader, true));
        Assert.Equal("Workspace", withHeader.Find(".ib-shell-sidebar-head .ib-shell-sidebar-slot").TextContent.Trim());
        Assert.Empty(withHeader.FindAll(".ib-shell-sidebar .ib-shell-brand"));
    }

    [Fact]
    public void A_panel_sidebar_has_no_brand_copy_and_the_menu_button_has_a_close_button_beside_it()
    {
        var cut = Render<ShellProbe>(p => p.Add(s => s.SmallScreen, SidebarSmallScreen.Panel));

        Assert.Contains("ib-shell-sidebar-panel", cut.Find(".ib-shell-sidebar").ClassName);
        Assert.Empty(cut.FindAll(".ib-shell-sidebar .ib-shell-brand"));

        // The header holds both; CSS shows the close button in the menu button's place while it's open.
        var close = cut.Find(".ib-shell-header .ib-shell-toggle-close");
        Assert.Equal("Close menu", close.GetAttribute("aria-label"));
        Assert.Equal("ib-sidebar", close.GetAttribute("aria-controls"));
    }

    [Theory]
    [InlineData("docs/install?tab=cli")]
    // A route that echoes the request mustn't turn the links into script or another site.
    [InlineData("javascript:alert(1)//")]
    [InlineData("//evil.example/x")]
    public void Shell_links_to_places_on_the_page_stay_on_the_page_under_a_base_href(string path)
    {
        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        navigation.NavigateTo(navigation.BaseUri + path);
        var page = new Uri(navigation.Uri);
        var cut = Render<ShellProbe>();

        Uri Resolve(string selector) => new(new Uri(navigation.BaseUri), cut.Find(selector).GetAttribute("href"));
        foreach (var (selector, id) in new[] { (".ib-skip-link", "ib-main"), ("[data-ib-shell-toggle]", "ib-sidebar"), ("[data-ib-shell-close]", "ib-shell-closed"), (".ib-shell-backdrop", "ib-shell-closed") })
        {
            var target = Resolve(selector);
            Assert.Equal(page.GetLeftPart(UriPartial.Query), target.GetLeftPart(UriPartial.Query));
            Assert.Equal("#" + id, target.Fragment);
        }
    }
}
