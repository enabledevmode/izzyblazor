using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace IzzyBlazor.Tests;

public class LinkMatchTests
{
    private sealed class Nav : NavigationManager
    {
        public Nav(string baseUri, string uri) => Initialize(baseUri, uri);

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
        }
    }

    [Theory]
    [InlineData("/app/", false)]
    [InlineData("/app", false)]
    [InlineData("/app/orders", true)]
    public void The_app_root_under_a_base_path_matches_only_itself(string href, bool current)
    {
        var nav = new Nav("https://example.com/app/", "https://example.com/app/orders/5");

        Assert.Equal(current, LinkMatch.Of(nav, href, nav.Uri, NavLinkMatch.Prefix).Active);
    }

    [Fact]
    public void The_app_root_is_current_on_itself()
    {
        var nav = new Nav("https://example.com/app/", "https://example.com/app/");

        Assert.Equal((true, true), LinkMatch.Of(nav, "/app/", nav.Uri, NavLinkMatch.Prefix));
    }
}
