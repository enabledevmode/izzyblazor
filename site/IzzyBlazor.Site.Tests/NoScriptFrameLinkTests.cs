using IzzyBlazor.Site.Services;

namespace IzzyBlazor.Site.Tests;

// An no-script frame runs no script, so the server sends links to other pages to the whole window.
public class NoScriptFrameLinkTests
{
    [Theory]
    [InlineData("<a href=\"docs/components\">Docs</a>", "<a href=\"docs/components\" target=\"_top\">Docs</a>")]
    [InlineData("<a class=\"x\" href=\"https://github.com\">GitHub</a>", "<a class=\"x\" href=\"https://github.com\" target=\"_top\">GitHub</a>")]
    [InlineData("<a href=\"frame/table-paging?mode=noscript&amp;page=2\">2</a>", "<a href=\"frame/table-paging?mode=noscript&amp;page=2\">2</a>")]
    [InlineData("<a href=\"#section\">Section</a>", "<a href=\"#section\">Section</a>")]
    [InlineData("<a href=\"docs\" target=\"_blank\">Docs</a>", "<a href=\"docs\" target=\"_blank\">Docs</a>")]
    [InlineData("<a href=\"mailto:a@b.c\">Mail</a>", "<a href=\"mailto:a@b.c\">Mail</a>")]
    public void Links_that_leave_the_example_open_in_the_whole_window(string html, string expected) =>
        Assert.Equal(expected, NoScriptFrameLinks.Rewrite(html, "/frame/table-paging"));
}
