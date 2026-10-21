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

    [Fact]
    public void Shell_links_to_places_on_the_page_keep_the_pages_path_so_they_work_under_a_base_href()
    {
        Services.GetRequiredService<BunitNavigationManager>().NavigateTo("docs/install?tab=cli");
        var cut = Render<ShellProbe>();

        Assert.Equal("docs/install?tab=cli#ib-main", cut.Find(".ib-skip-link").GetAttribute("href"));
        Assert.Equal("docs/install?tab=cli#ib-sidebar", cut.Find("[data-ib-shell-toggle]").GetAttribute("href"));
        Assert.All(cut.FindAll("[data-ib-shell-close], .ib-shell-backdrop"), a => Assert.Equal("docs/install?tab=cli#ib-shell-closed", a.GetAttribute("href")));
    }
}
