using Microsoft.AspNetCore.Http;

namespace IzzyBlazor.Tests;

public class RequestCookiesTests
{
    // A layout saved on this page's path, and an older one on a path above it (an example that moved
    // pages): browsers send the more specific first, and the page's script reads that one too.
    [Fact]
    public void Of_two_cookies_with_one_name_the_first_is_read()
    {
        var http = new DefaultHttpContext();
        http.Request.Headers.Cookie = "ib-grid-a=%7B%22new%22%7D; other=1; ib-grid-a=%7B%22old%22%7D";

        Assert.Equal("{\"new\"}", new HttpRequestCookies(new FixedHttpContextAccessor { HttpContext = http }).Get("ib-grid-a"));
    }
}
