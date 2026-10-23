using IzzyBlazor.Site.Services;

namespace IzzyBlazor.Site.Tests;

// An Off frame runs no script, so the server sends links to other pages to the whole window.
public class OffFrameLinkTests
{
    [Theory]
    [InlineData("<a href=\"docs/components\">Docs</a>", "<a href=\"docs/components\" target=\"_top\">Docs</a>")]
    [InlineData("<a class=\"x\" href=\"https://github.com\">GitHub</a>", "<a class=\"x\" href=\"https://github.com\" target=\"_top\">GitHub</a>")]
    [InlineData("<a href=\"frame/table-paging?mode=off&amp;page=2\">2</a>", "<a href=\"frame/table-paging?mode=off&amp;page=2\">2</a>")]
    [InlineData("<a href=\"#section\">Section</a>", "<a href=\"#section\">Section</a>")]
    [InlineData("<a href=\"_izzy/scheme?value=dark&amp;return=frame%2Ftable-paging\">Dark</a>", "<a href=\"_izzy/scheme?value=dark&amp;return=frame%2Ftable-paging\">Dark</a>")]
    [InlineData("<a href=\"docs\" target=\"_blank\">Docs</a>", "<a href=\"docs\" target=\"_blank\">Docs</a>")]
    [InlineData("<a href=\"mailto:a@b.c\">Mail</a>", "<a href=\"mailto:a@b.c\">Mail</a>")]
    public void Links_that_leave_the_example_open_in_the_whole_window(string html, string expected) =>
        Assert.Equal(expected, OffFrameLinks.Rewrite(html, "/frame/table-paging"));

    [Theory]
    // GetUriWithQueryParameter gives an absolute URL on the request's own host: still the example.
    [InlineData("<a href=\"http://localhost:5190/frame/table-paging?mode=off&amp;page=2\">2</a>", "<a href=\"http://localhost:5190/frame/table-paging?mode=off&amp;page=2\">2</a>")]
    [InlineData("<a href=\"http://localhost:5190/docs\">Docs</a>", "<a href=\"http://localhost:5190/docs\" target=\"_top\">Docs</a>")]
    [InlineData("<a href=\"http://example.com/frame/table-paging\">Elsewhere</a>", "<a href=\"http://example.com/frame/table-paging\" target=\"_top\">Elsewhere</a>")]
    public void An_absolute_link_on_the_same_host_stays_in_the_frame(string html, string expected) =>
        Assert.Equal(expected, OffFrameLinks.Rewrite(html, "/frame/table-paging", "localhost:5190"));
}
