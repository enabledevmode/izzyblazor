using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

/// <summary>The shell below the sidebar's breakpoint: links that stay on the page.</summary>
public class ShellSmallScreenTests : BunitContext
{
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
