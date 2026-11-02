namespace IzzyBlazor.Tests;

/// <summary>What the docs site needs (alpha.12): the page outline, heading anchors, code lines, copy and previous/next links.</summary>
public class DocsSiteRenderTests : BunitContext
{
    [Fact]
    public void OnThisPage_lists_the_main_areas_h2_and_h3_headings_on_the_first_render_with_ids_from_their_text()
    {
        var cut = Render<OutlineProbe>();

        var links = cut.FindAll(".ib-toc-link");
        Assert.Equal(
            ["Install|./#install", "From the CLI|./#from-the-cli", "Rows & columns|./#rows-columns", "Install|./#install-2", "Its own id|./#custom", "Linked elsewhere|./#linked-elsewhere", "Via a component|./#via-a-component", "Alpha|./#alpha", "Beta|./#beta"],
            links.Select(a => $"{a.TextContent}|{a.GetAttribute("href")}"));

        // The headings carry the ids the links point to: every level gets one, so any section can be linked.
        Assert.Equal("getting-started", cut.Find("h1").Id);
        Assert.Equal("on-windows", cut.Find("h4").Id);
        Assert.NotNull(cut.Find("#install-2"));

        // A heading left out has no id; one outside the main area, or in an example, isn't listed.
        Assert.Null(cut.FindAll("h2").Single(h => h.TextContent == "Left out").GetAttribute("id"));
        Assert.Null(cut.FindAll("h2").Single(h => h.TextContent == "Demo heading").GetAttribute("id"));
        Assert.DoesNotContain(links, a => a.TextContent == "In the aside");

        // The list is a landmark named by its heading.
        var nav = cut.Find("ib-on-this-page nav");
        Assert.Equal("On this page", cut.Find($"#{nav.GetAttribute("aria-labelledby")}").TextContent);
    }

    [Theory]
    [InlineData("Getting started", "getting-started")]
    [InlineData("  Rows & columns  ", "rows-columns")]
    public void Heading_ids_are_slugs_of_their_text(string text, string slug) =>
        Assert.Equal(slug, PageOutline.Slug(text));

    private const string Sample = "var a = 1;\n\n// two\nvar b = \"x\";\n";

    [Fact]
    public void Code_line_numbers_and_highlights_keep_the_code_as_written_for_copying()
    {
        var cut = Render<Code>(p => p
            .Add(c => c.Block, true)
            .Add(c => c.Language, "csharp")
            .Add(c => c.LineNumbers, true)
            .Add(c => c.HighlightLines, "1, 3-4")
            .AddChildContent(Sample));

        // The copy button copies the text of <code>: the same text, less the final newline.
        Assert.Equal(Sample.TrimEnd('\n'), cut.Find("pre code").TextContent);
        Assert.Equal(4, cut.FindAll(".ib-code-line").Count);
        Assert.Equal(["var a = 1;", "// two", "var b = \"x\";"], cut.FindAll("mark.ib-code-line").Select(m => m.TextContent.TrimEnd('\n')));
        // Still colored.
        Assert.NotEmpty(cut.FindAll("mark .ib-tok-keyword"));
    }
}
