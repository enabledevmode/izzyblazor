using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

/// <summary>The sidebar below its breakpoint: the drawer's top row, the panel, and links that stay on the page.</summary>
public class ShellSmallScreenTests : BunitContext
{
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
