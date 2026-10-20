namespace IzzyBlazor.Tests;

/// <summary>What the docs site needs (alpha.12): the page outline, heading anchors, code lines, copy and previous/next links.</summary>
public class DocsSiteRenderTests : BunitContext
{
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

    [Theory]
    [InlineData("0")]
    [InlineData("5-3")]
    [InlineData("two")]
    [InlineData("1,,x")]
    public void Code_HighlightLines_rejects_what_isnt_line_numbers(string spec) =>
        Assert.Throws<ArgumentException>(() => Render<Code>(p => p.Add(c => c.Block, true).Add(c => c.HighlightLines, spec).AddChildContent("a")));

    [Fact]
    public void Code_title_heads_the_block_in_place_of_the_language()
    {
        var cut = Render<Code>(p => p
            .Add(c => c.Block, true)
            .Add(c => c.Language, "csharp")
            .Add(c => c.Title, "Program.cs")
            .AddChildContent("var a = 1;"));

        Assert.Equal("Program.cs", cut.Find(".ib-code-head").TextContent.Trim());
        Assert.Empty(cut.FindAll(".ib-code-language"));
        Assert.NotEmpty(cut.FindAll(".ib-tok-keyword"));
    }

    [Fact]
    public void CopyButton_copies_text_or_another_elements_value_and_is_named()
    {
        var icon = Render<CopyButton>(p => p.Add(c => c.Text, "dotnet add package IzzyBlazor"));
        Assert.Equal("dotnet add package IzzyBlazor", icon.Find("ib-copy").GetAttribute("data-copy-text"));
        var button = icon.Find("button");
        Assert.Equal("button", button.GetAttribute("type"));
        Assert.Equal("Copy", button.GetAttribute("aria-label"));
        Assert.Equal("Copied", button.GetAttribute("data-copied-label"));

        // With text, the text names it.
        var text = Render<CopyButton>(p => p.Add(c => c.For, "api-key").AddChildContent("Copy key"));
        Assert.Equal("api-key", text.Find("ib-copy").GetAttribute("data-copy-for"));
        Assert.Null(text.Find("button").GetAttribute("aria-label"));
        Assert.Equal("Copy key", text.Find("button").TextContent.Trim());

        Assert.Throws<InvalidOperationException>(() => Render<CopyButton>());
        Assert.Throws<InvalidOperationException>(() => Render<CopyButton>(p => p.Add(c => c.Text, "a").Add(c => c.For, "b")));
    }

}
