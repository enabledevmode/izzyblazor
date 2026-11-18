using Microsoft.AspNetCore.Components;

namespace IzzyBlazor.Tests;

/// <summary>The account row in a sidebar's footer.</summary>
public class SidebarAccountTests : BunitContext
{
    [Fact]
    public void With_items_the_row_is_a_button_that_opens_them_and_with_an_address_a_link()
    {
        SetRendererInfo(new RendererInfo("Static", false));
        var menu = Render<SidebarAccount>(p => p
            .Add(a => a.Name, "Ada Lovelace")
            .Add(a => a.Description, "Owner")
            .AddChildContent<MenuItem>(i => i.Add(m => m.Href, "/account").AddChildContent("Account")));

        var button = menu.Find("button.ib-sidebar-account");
        Assert.Equal("Ada LovelaceOwner", string.Concat(button.QuerySelectorAll(".ib-sidebar-account-text > span").Select(s => s.TextContent)));
        var panel = menu.Find("[popover]");
        Assert.Equal(panel.Id, button.GetAttribute("popovertarget"));
        Assert.Contains("ib-place-top-start", panel.ClassName);
        Assert.Equal("/account", panel.QuerySelector("a")!.GetAttribute("href"));

        var link = Render<SidebarAccount>(p => p.Add(a => a.Name, "Ada Lovelace").Add(a => a.Href, "/account"));
        Assert.Equal("/account", link.Find("a.ib-sidebar-account").GetAttribute("href"));
        Assert.Empty(link.FindAll("[popover]"));
    }
}
