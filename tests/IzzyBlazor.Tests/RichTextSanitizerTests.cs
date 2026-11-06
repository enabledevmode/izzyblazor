using Microsoft.AspNetCore.Components;

namespace IzzyBlazor.Tests;

// A sanitizer is judged by its attack cases, so these are many (CONTRIBUTING's threshold allows
// them here): most come from OWASP's XSS filter evasion cheat sheet.
public class RichTextSanitizerTests : BunitContext
{
    public RichTextSanitizerTests() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    private static string S(string html, bool dataImages = false, int top = 1, string[]? fonts = null, string[]? sizes = null, bool colors = false) => RichTextSanitizer.Sanitize(html, top, fonts, sizes, dataImages, colors);

    [Theory]
    [InlineData("<script>alert(1)</script>ok", "ok")]
    [InlineData("<SCRIPT SRC=//x.example/x.js></SCRIPT>ok", "ok")]
    [InlineData("<script>document.write('</scr'+'ipt>')</script>ok", "ok")]
    [InlineData("<style>p{background:url(javascript:alert(1))}</style>ok", "ok")]
    [InlineData("<iframe src=\"javascript:alert(1)\"></iframe>ok", "ok")]
    [InlineData("<svg><script>alert(1)</script></svg>ok", "ok")]
    [InlineData("<svg/onload=alert(1)>ok", "")]
    [InlineData("<math><mtext><table><mglyph><style><img src=x onerror=alert(1)>", "")]
    [InlineData("<template><img src=x onerror=alert(1)></template>ok", "ok")]
    // A browser with script on reads noscript as raw text too, so the img is real; its handler goes.
    [InlineData("<noscript><p title=\"</noscript><img src=x onerror=alert(1)>\">", "<img src=\"x\">\"&gt;")]
    [InlineData("<textarea><img src=x onerror=alert(1)></textarea>ok", "ok")]
    [InlineData("<object data=\"javascript:alert(1)\"></object>ok", "ok")]
    [InlineData("<!--<img src=x onerror=alert(1)>-->ok", "ok")]
    [InlineData("<!--><img src=x onerror=alert(1)>-->", "<img src=\"x\">--&gt;")]
    [InlineData("<plaintext><img src=x onerror=alert(1)>", "")]
    public void Drops_code_and_foreign_documents_with_their_content(string html, string expected) =>
        Assert.Equal(expected, S(html));

    [Theory]
    [InlineData("<img src=x onerror=alert(1)>", "<img src=\"x\">")]
    [InlineData("<p onclick=\"alert(1)\" style=\"color:red\" class=\"x\" id=\"y\">a</p>", "<p>a</p>")]
    [InlineData("<a href=\"/x\" onmouseover=alert(1)>a</a>", "<a href=\"/x\">a</a>")]
    [InlineData("<img src=\"x\" alt=\"a\" onerror\n=alert(1)>", "<img src=\"x\" alt=\"a\">")]
    [InlineData("<img/src=\"x\"/onerror=alert(1)>", "<img src=\"x\">")]
    [InlineData("<img src=\"x\" alt=\"\"\"onerror=alert(1)>", "<img src=\"x\" alt=\"\">")]
    [InlineData("<p title=\"a\" >b</p>", "<p>b</p>")]
    public void Drops_event_handlers_styles_and_other_attributes(string html, string expected) =>
        Assert.Equal(expected, S(html));

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JaVaScRiPt:alert(1)")]
    [InlineData(" javascript:alert(1)")]
    [InlineData("\u0001javascript:alert(1)")]
    [InlineData("java\tscript:alert(1)")]
    [InlineData("java\nscript:alert(1)")]
    [InlineData("java&#x09;script:alert(1)")]
    [InlineData("&#106;&#97;&#118;&#97;&#115;&#99;&#114;&#105;&#112;&#116;&#58;alert(1)")]
    [InlineData("&#x6A;avascript:alert(1)")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==")]
    [InlineData("file:///etc/passwd")]
    public void Drops_links_to_scripts_and_other_schemes(string href) =>
        Assert.Equal("x", S($"<a href=\"{href}\">x</a>"));

    [Fact]
    public void Keeps_web_mail_phone_and_relative_links()
    {
        Assert.Equal("<a href=\"https://example.com/a?b=1&amp;c=2\">x</a>", S("<a href=\"https://example.com/a?b=1&amp;c=2\">x</a>"));
        Assert.Equal("<a href=\"mailto:a@example.com\">x</a>", S("<a href=\"mailto:a@example.com\">x</a>"));
        Assert.Equal("<a href=\"tel:+15551234\">x</a>", S("<a href=\"tel:+15551234\">x</a>"));
        Assert.Equal("<a href=\"/docs/a:b\">x</a>", S("<a href=\"/docs/a:b\">x</a>"));
        Assert.Equal("<a href=\"#top\">x</a>", S("<a href=\"#top\">x</a>"));
        // A link with no href left is only its text.
        Assert.Equal("x", S("<a>x</a>"));
    }

    [Fact]
    public void Opens_new_tabs_with_no_opener()
    {
        Assert.Equal("<a href=\"https://example.com\" target=\"_blank\" rel=\"noopener noreferrer\">x</a>",
            S("<a href=\"https://example.com\" target=\"_blank\" rel=\"opener\">x</a>"));
        Assert.Equal("<a href=\"/a\">x</a>", S("<a href=\"/a\" target=\"_top\">x</a>"));
    }

    [Fact]
    public void Keeps_data_images_only_when_allowed_and_never_svg()
    {
        const string png = "<img src=\"data:image/png;base64,iVBORw0KGgo=\" alt=\"a\">";
        Assert.Equal("", S(png));
        Assert.Equal(png, S(png, dataImages: true));
        Assert.Equal("", S("<img src=\"data:image/svg+xml;base64,PHN2Zz4=\">", dataImages: true));
    }

    [Fact]
    public void Encodes_text_and_attributes_again()
    {
        Assert.Equal("&lt;script&gt;alert(1)&lt;/script&gt;", S("&lt;script&gt;alert(1)&lt;/script&gt;"));
        Assert.Equal("<img src=\"x\" alt=\"&quot;&gt;&lt;script&gt;\">", S("<img src=x alt='\"><script>'>"));
        Assert.Equal("a &lt; b", S("a < b"));
        Assert.Equal("Tom &amp; Jerry", S("Tom &amp; Jerry"));
    }

    [Fact]
    public void Maps_pasted_markup_to_the_editor_s_elements()
    {
        Assert.Equal("<h1>T</h1><p><strong>b</strong> <em>i</em> <s>d</s></p><h6>s</h6>",
            S("<h1>T</h1><div><b>b</b> <i>i</i> <del>d</del></div><h6>s</h6>"));
        // Under a page's own title, a heading 1 moves down to the top level allowed.
        Assert.Equal("<h2>T</h2><h3>U</h3>", S("<h1>T</h1><h3>U</h3>", top: 2));
        // Word's and Docs' wrappers go; their text stays.
        Assert.Equal("<p>Hello <strong>world</strong></p>",
            S("<p class=\"MsoNormal\"><span style=\"font-family:Calibri\"><o:p></o:p>Hello <font color=red><b>world</b></font></span></p>"));
    }

    [Fact]
    public void Closes_what_was_left_open_and_drops_empty_elements()
    {
        Assert.Equal("<p><strong>a</strong></p><p>b</p>", S("<p><strong>a<p>b"));
        Assert.Equal("<ul><li>a</li><li>b<ol><li>c</li></ol></li></ul>", S("<ul><li>a<li>b<ol><li>c</ol></ul>"));
        Assert.Equal("<p>a</p>", S("<p>a</p><p></p><strong></strong>"));
        // A browser's indent: the inner list moves into the item before it.
        Assert.Equal("<ul><li>a<ul><li>b</li></ul></li><li>c</li></ul>", S("<ul><li>a</li><ul><li>b</li></ul><li>c</li></ul>"));
        Assert.Equal("<p><br></p>", S("<p><br></p>"));
        // A stray end tag is ignored.
        Assert.Equal("<ul><li>a<em>b</em></li><li>cd</li></ul>", S("<ul><li>a<em>b</li><li>c</em>d</li></ul>"));
    }

    [Fact]
    public void Keeps_only_the_editor_s_own_classes_and_the_app_s_fonts()
    {
        Assert.Equal("<p class=\"ib-align-center\">a <span class=\"ib-color-red ib-size-lg\">b</span> <sub>2</sub><sup>3</sup></p>",
            S("<p class=\"ib-align-center MsoNormal\" style=\"text-align:center\">a <span class=\"ib-color-red x ib-size-lg\" style=\"color:red\">b</span> <sub>2</sub><sup>3</sup></p>"));
        Assert.Equal("<p><span style=\"font-family: Georgia, serif\">a</span>b</p>",
            S("<p><span style=\"FONT-FAMILY: georgia, serif;\">a</span><span style=\"font-family: Comic Sans MS\">b</span></p>", fonts: ["Georgia, serif"]));
        // A fixed size only from the app's list; both styles together, in one attribute.
        Assert.Equal("<p><span style=\"font-family: Georgia, serif; font-size: 14px\">a</span>b</p>",
            S("<p><span style=\"font-size:14px; font-family: Georgia, serif; color: red\">a</span><span style=\"font-size: 13px\">b</span></p>", fonts: ["Georgia, serif"], sizes: ["14px"]));
        // Custom colors only with the option, and only as six hex digits, in the sanitizer's order.
        Assert.Equal("<p><span style=\"background-color: #fde68a; color: #0e7490\">a</span>b</p>",
            S("<p><span style=\"COLOR: #0E7490; background-color:#fde68a\">a</span><span style=\"color: red; background-color: url(x)\">b</span></p>", colors: true));
        Assert.Equal("<p>a</p>", S("<p><span style=\"color: #0e7490\">a</span></p>"));
        // A dropped span doesn't take its end tag from the span around it.
        Assert.Equal("<p><span class=\"ib-color-blue\">a b c</span></p>", S("<p><span class=\"ib-color-blue\">a <span>b</span> c</span></p>"));
        Assert.Equal("<p><span class=\"ib-highlight-yellow\">x</span></p>", S("<p><font color=red><span class=\"ib-highlight-yellow\" onclick=x>x</span></font></p>"));
    }

    [Fact]
    public void Keeps_tables_and_their_cell_attributes()
    {
        Assert.Equal(
            "<table><thead><tr><th scope=\"col\">A</th><th colspan=\"2\">B</th></tr></thead><tbody><tr><td rowspan=\"2\">1</td><td>2</td></tr></tbody></table>",
            S("<table border=1 style=\"x\"><thead><tr><th scope=col>A<th colspan=\"2\" width=9>B<tbody><tr><td rowspan=2 onclick=x>1<td>2</table>"));
        // Text straight in a table goes, as a browser would move it out.
        Assert.Equal("<table><tr><td>a</td></tr></table>", S("<table>junk<tr><td>a</td></tr></table>"));
    }

    [Theory]
    [InlineData("<h1>T</h1><div><b>b</b> & <i>i</i></div><ul><li>a<li>b</ul><a href=\"/x\" target=_blank>l</a>")]
    [InlineData("<p class=MsoNormal><span style=\"x\">Hi</span><img src=\"/a.png\" alt='\"q\"'></p><table><tr><th scope=col>A<td>1</table>")]
    [InlineData("<pre><code>a &lt; b\n  c</code></pre><blockquote><p>q</p></blockquote><hr><p>x<br>y</p>")]
    public void Its_own_output_comes_back_unchanged(string html)
    {
        // An editor's saved value loads and saves again as the same string.
        var once = S(html);
        Assert.Equal(once, S(once));
    }

    [Fact]
    public void Keeps_a_code_block_s_language_and_highlights_it_when_shown()
    {
        // Only a language-name class on code; the code stays text.
        Assert.Equal("<pre><code class=\"language-csharp\">var x = 1;</code></pre>",
            S("<pre><code class=\"hljs language-csharp\" onclick=x>var x = 1;</code></pre>"));
        Assert.Equal("<pre><code>a</code></pre>", S("<pre><code class=\"language-c<s\">a</code></pre>"));

        // Shown, it's highlighted on the server; the markup in the code stays text.
        var cut = Render<RichText>(p => p.Add(r => r.Html, "<pre><code class=\"language-csharp\">var s = \"&lt;b&gt;\";</code></pre>"));
        var code = cut.Find("pre > code");
        Assert.Equal("language-csharp", code.ClassName);
        Assert.Equal("var", code.QuerySelector(".ib-tok-keyword")!.TextContent);
        Assert.Empty(code.QuerySelectorAll("b"));
        Assert.Equal("var s = \"<b>\";", code.TextContent);
    }

    [Fact]
    public void Rich_text_shows_sanitized_html_as_prose()
    {
        var cut = Render<RichText>(p => p.Add(r => r.Html, "<h1 onclick=x>Title</h1><script>alert(1)</script>").Add(r => r.Size, ProseSize.Lg).Add(r => r.TopHeading, 2));

        cut.MarkupMatches("<div class=\"ib-prose ib-prose-lg\"><h2>Title</h2></div>");
    }
}
