using IzzyBlazor.Site.Services;

namespace IzzyBlazor.Site.Tests;

// With JavaScript off, an example's post or pager link loads the page again: the server points it
// at the example, so it lands there and not at the top.
public class OffExampleAnchorTests
{
    private const string Path = "/docs/components/card";

    [Theory]
    [InlineData("<form method=\"post\" action=\"/docs/components/card?mode=off\">", "<form method=\"post\" action=\"/docs/components/card?mode=off#ex-card-settings\">")]
    [InlineData("<a href=\"http://localhost:5190/docs/components/card?project=website\">W</a>", "<a href=\"http://localhost:5190/docs/components/card?project=website#ex-card-settings\">W</a>")]
    [InlineData("<a href=\"docs/components/card?a=1&amp;b=2\">2</a>", "<a href=\"docs/components/card?a=1&amp;b=2#ex-card-settings\">2</a>")]
    [InlineData("<a href=\"docs/components/table\">Table</a>", "<a href=\"docs/components/table\">Table</a>")]
    [InlineData("<a href=\"docs/components/card#usage\">Usage</a>", "<a href=\"docs/components/card#usage\">Usage</a>")]
    [InlineData("<a href=\"mailto:a@b.c\">Mail</a>", "<a href=\"mailto:a@b.c\">Mail</a>")]
    [InlineData("<a class=\"site-mode-link\" href=\"docs/components/card?mode=live\">Live</a>", "<a class=\"site-mode-link\" href=\"docs/components/card?mode=live#ex-card-settings\">Live</a>")]
    [InlineData("<a href=\"http://example.com/docs/components/card\">Elsewhere</a>", "<a href=\"http://example.com/docs/components/card\">Elsewhere</a>")]
    [InlineData("<a data-href=\"x\" href=\"docs/components/card?p=2\">2</a>", "<a data-href=\"x\" href=\"docs/components/card?p=2#ex-card-settings\">2</a>")]
    [InlineData("<button type=\"submit\" formaction=\"/docs/components/card?x=1\">Go</button>", "<button type=\"submit\" formaction=\"/docs/components/card?x=1#ex-card-settings\">Go</button>")]
    // A ColorSchemeSwitch link: the endpoint redirects to its return path, keeping the fragment.
    [InlineData("<a href=\"_izzy/scheme?value=dark&amp;return=docs%2Fcomponents%2Fcard%3Fmode%3Doff\">Dark</a>", "<a href=\"_izzy/scheme?value=dark&amp;return=docs%2Fcomponents%2Fcard%3Fmode%3Doff#ex-card-settings\">Dark</a>")]
    [InlineData("<a href=\"_izzy/scheme?value=dark&amp;return=docs%2Fcomponents%2Ftable\">Dark</a>", "<a href=\"_izzy/scheme?value=dark&amp;return=docs%2Fcomponents%2Ftable\">Dark</a>")]
    [InlineData("<a href=\"_izzy/scheme?value=dark\">Dark</a>", "<a href=\"_izzy/scheme?value=dark\">Dark</a>")]
    public void Links_and_forms_to_the_page_go_to_the_example(string inside, string expected)
    {
        var html = $"<section class=\"ex\" id=\"ex-card-settings\" data-ex=\"card-settings\">{inside}</section>";
        Assert.Equal($"<section class=\"ex\" id=\"ex-card-settings\" data-ex=\"card-settings\">{expected}</section>", OffExampleAnchors.Rewrite(html, Path, "localhost:5190"));
    }

    [Fact]
    public void The_id_can_come_after_data_ex() =>
        Assert.Equal("<section data-ex=\"a\" class=\"ex\" id=\"ex-a\"><a href=\"docs/components/card?p=2#ex-a\">2</a></section>",
            OffExampleAnchors.Rewrite("<section data-ex=\"a\" class=\"ex\" id=\"ex-a\"><a href=\"docs/components/card?p=2\">2</a></section>", Path, "localhost:5190"));

    [Fact]
    public void Only_inside_an_example_up_to_its_own_end()
    {
        const string html =
            "<a href=\"docs/components/card?mode=static\">Static</a>" +
            "<section class=\"ex\" id=\"ex-a\" data-ex=\"a\"><section class=\"ib-section\"><a href=\"docs/components/card?p=2\">2</a></section>" +
            "<a href=\"docs/components/card?p=3\">3</a></section>" +
            "<a href=\"docs/components/card?mode=live\">Live</a>";
        const string expected =
            "<a href=\"docs/components/card?mode=static\">Static</a>" +
            "<section class=\"ex\" id=\"ex-a\" data-ex=\"a\"><section class=\"ib-section\"><a href=\"docs/components/card?p=2#ex-a\">2</a></section>" +
            "<a href=\"docs/components/card?p=3#ex-a\">3</a></section>" +
            "<a href=\"docs/components/card?mode=live\">Live</a>";
        Assert.Equal(expected, OffExampleAnchors.Rewrite(html, Path, "localhost:5190"));
    }
}
