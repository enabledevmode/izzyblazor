using IzzyBlazor.Site.Services;

namespace IzzyBlazor.Site.Tests;

// The site's other names go to izzyblazor.com; every other host is served as it is.
public class CanonicalHostTests
{
    [Theory]
    [InlineData("izzyblazor.dev", "/", "https://izzyblazor.com/")]
    [InlineData("www.izzyblazor.dev", "/docs/components/toolbar?mode=noscript", "https://izzyblazor.com/docs/components/toolbar?mode=noscript")]
    [InlineData("www.izzyblazor.com", "/docs", "https://izzyblazor.com/docs")]
    [InlineData("WWW.IzzyBlazor.Com", "/docs", "https://izzyblazor.com/docs")]
    public void The_other_names_redirect_with_the_path_and_query(string host, string pathAndQuery, string expected) =>
        Assert.Equal(expected, CanonicalHost.Target(host, pathAndQuery));

    [Theory]
    [InlineData("izzyblazor.com")]
    [InlineData("izzyblazor.fly.dev")]
    [InlineData("localhost")]
    [InlineData("172.19.0.10")]
    public void Other_hosts_are_served(string host) =>
        Assert.Null(CanonicalHost.Target(host, "/docs"));
}
