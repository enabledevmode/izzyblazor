namespace IzzyBlazor.Tests;

public class BlockquoteRenderTests : BunitContext
{
    [Fact]
    public void An_attribution_puts_the_quote_in_a_figure_with_the_source_cited_and_linked()
    {
        var cut = Render<Blockquote>(p => p
            .Add(b => b.Author, "Ada Lovelace")
            .Add(b => b.Source, "Notes")
            .Add(b => b.Cite, "https://example.com/notes")
            .AddChildContent("Weaves algebraical patterns."));

        var figure = cut.Find("figure.ib-blockquote");
        var quote = figure.QuerySelector(":scope > blockquote")!;
        Assert.Equal("https://example.com/notes", quote.GetAttribute("cite"));
        Assert.Contains("Weaves algebraical patterns.", quote.TextContent);

        // The attribution is outside the quote: the author plain, the title in cite, linked.
        var caption = figure.QuerySelector(":scope > figcaption")!;
        Assert.Contains("Ada Lovelace", caption.TextContent);
        Assert.Equal("Notes", caption.QuerySelector("cite > a[href='https://example.com/notes']")!.TextContent);
    }

    [Fact]
    public void Without_an_attribution_it_is_a_bare_blockquote()
    {
        var cut = Render<Blockquote>(p => p.Add(b => b.Cite, "https://example.com").AddChildContent("A quote."));

        Assert.Empty(cut.FindAll("figure"));
        Assert.Equal("https://example.com", cut.Find("blockquote.ib-blockquote").GetAttribute("cite"));
    }
}
