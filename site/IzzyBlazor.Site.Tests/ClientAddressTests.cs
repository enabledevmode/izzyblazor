using Microsoft.AspNetCore.Http;

namespace IzzyBlazor.Site.Tests;

// Behind Cloudflare, Fly's Fly-Client-IP is a Cloudflare address and the visitor is in
// CF-Connecting-IP; the limits and blocked networks go by the visitor. Anyone else's
// CF-Connecting-IP is ignored, so a client can't pick its own address.
public class ClientAddressTests
{
    [Theory]
    [InlineData("162.158.10.1", "198.51.100.4", "198.51.100.4")]
    [InlineData("2606:4700::1", "2001:db8:1:2::9", "2001:db8:1:2::9")]
    [InlineData("203.0.113.7", "198.51.100.4", "203.0.113.7")]
    [InlineData("203.0.113.7", null, "203.0.113.7")]
    public void The_visitor_is_Cloudflares_header_only_from_Cloudflare(string fly, string? cloudflare, string expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["Fly-Client-IP"] = fly;
        if (cloudflare is not null)
        {
            context.Request.Headers["CF-Connecting-IP"] = cloudflare;
        }

        Assert.Equal(expected, SiteApp.ClientAddress(context)?.ToString());
    }

    [Theory]
    [InlineData("162.158.10.1", true)]
    [InlineData("203.0.113.7", false)]
    public void Through_Cloudflare_is_a_caller_from_its_addresses(string fly, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["Fly-Client-IP"] = fly;
        Assert.Equal(expected, SiteApp.ViaCloudflare(context));
    }
}
