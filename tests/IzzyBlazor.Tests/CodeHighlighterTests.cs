using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

public class CodeHighlighterTests : BunitContext
{
    private static IReadOnlyList<CodeToken> Lex(string language, string code)
    {
        Assert.True(IzzyCodeHighlighter.Instance.TryHighlight(code, language, out var tokens));
        Assert.Equal(code, string.Concat(tokens.Select(t => t.Text)));
        return tokens;
    }

    private static string? KindOf(IReadOnlyList<CodeToken> tokens, string text) =>
        tokens.FirstOrDefault(t => t.Text == text) is { Text: not null } t ? t.Kind.ToString() : null;

    // Plain text merges with its neighbors, so a plain word is inside a Text token, never its own.
    private static bool IsPlain(IReadOnlyList<CodeToken> tokens, string word) =>
        tokens.Any(t => t.Kind == CodeTokenKind.Text && t.Text.Contains(word)) &&
        !tokens.Any(t => t.Kind != CodeTokenKind.Text && t.Text == word);

    [Fact]
    public void CSharp_keywords_strings_types_calls_and_comments()
    {
        var t = Lex("csharp", "[Parameter] public string? Title { get; set; } = \"Hi\"; // note\nvar x = new List<Rule>(); Console.WriteLine(x.Count + 1);");

        Assert.Equal("Type", KindOf(t, "Parameter"));
        Assert.Equal("Keyword", KindOf(t, "public"));
        Assert.Equal("String", KindOf(t, "\"Hi\""));
        Assert.Equal("Comment", KindOf(t, "// note"));
        Assert.Equal("Type", KindOf(t, "List"));
        Assert.Equal("Type", KindOf(t, "Rule"));
        Assert.Equal("Function", KindOf(t, "WriteLine"));
        Assert.Equal("Number", KindOf(t, "1"));
        Assert.True(IsPlain(t, "Count"));
    }

    [Fact]
    public void Code_renders_spans_and_keeps_the_text()
    {
        var block = Render<Code>(p => p.Add(c => c.Block, true).Add(c => c.Language, "csharp").AddChildContent("var x = 1; // one"));

        Assert.Equal("var x = 1; // one", block.Find("pre code").TextContent);
        Assert.Equal("var", block.Find(".ib-tok-keyword").TextContent);
        Assert.Equal("// one", block.Find(".ib-tok-comment").TextContent);
    }

    [Fact]
    public void Code_decodes_encoded_text_and_skips_markup()
    {
        var encoded = Render<Code>(p => p.Add(c => c.Block, true).Add(c => c.Language, "html").AddChildContent("&lt;b&gt;x&lt;/b&gt;"));
        Assert.Equal("<b>x</b>", encoded.Find("pre code").TextContent);
        Assert.Equal("b", encoded.Find(".ib-tok-tag").TextContent);

        var markup = Render<Code>(p => p.Add(c => c.Block, true).Add(c => c.Language, "html").AddChildContent("<b>x</b>"));
        Assert.Empty(markup.FindAll(".ib-tok-tag"));
        Assert.Equal("x", markup.Find("pre code b").TextContent);
    }

    [Fact]
    public void A_registered_highlighter_replaces_the_built_in_one()
    {
        Services.AddSingleton<ICodeHighlighter>(new ShoutingHighlighter());

        var block = Render<Code>(p => p.Add(c => c.Block, true).Add(c => c.Language, "anything").AddChildContent("hi"));

        Assert.Equal("hi", block.Find(".ib-tok-keyword").TextContent);
    }

    private sealed class ShoutingHighlighter : ICodeHighlighter
    {
        public bool TryHighlight(string code, string language, out IReadOnlyList<CodeToken> tokens)
        {
            tokens = [new CodeToken(CodeTokenKind.Keyword, code)];
            return true;
        }
    }
}
