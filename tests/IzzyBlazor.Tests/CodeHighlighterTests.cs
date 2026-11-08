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
        Assert.Equal("Member", KindOf(t, "Count"));
        Assert.True(IsPlain(t, "x"));
    }

    [Fact]
    public void Razor_components_members_and_code_in_attribute_values()
    {
        var t = Lex("razor", "@using System.Text\n<div class=\"row\"><LineChart Items=\"_sales\" Date=\"s => s.Month\" Fill=\"Color.Muted\" Title=\"Revenue\" Height=\"300\" OnClick=\"SaveAsync\" />@context.Name <input @bind=\"_query\" /></div>");

        Assert.Equal("Component", KindOf(t, "LineChart"));
        Assert.Equal("Tag", KindOf(t, "div"));
        Assert.Equal("String", KindOf(t, "\"row\""));
        Assert.Equal("String", KindOf(t, "\"Revenue\""));
        Assert.Equal("Member", KindOf(t, "_sales"));
        Assert.Equal("Member", KindOf(t, "Month"));
        Assert.Equal("Type", KindOf(t, "Color"));
        Assert.Equal("Member", KindOf(t, "Muted"));
        Assert.Equal("Number", KindOf(t, "300"));
        Assert.Equal("Member", KindOf(t, "SaveAsync"));
        Assert.Equal("Member", KindOf(t, "context"));
        Assert.Equal("Member", KindOf(t, "Name"));
        Assert.Equal("Member", KindOf(t, "_query"));
        Assert.True(IsPlain(t, "Text"));
    }

    [Fact]
    public void Razor_attribute_values_that_read_as_text_stay_strings()
    {
        var t = Lex("razor", "<!DOCTYPE html>\n<Stat Value=\"18 ms\" Format=\"0.0' s'\" Height=\"300px\" />\n" +
                             "<CheckboxGroup Items=\"Enum.GetValues<Topping>()\" Disabled=\"card.Owner == Me\" Min=\"Model!.CheckIn\" Size=\"12.Rem\" />\n" +
                             "<form @formname=\"contact\"></form>");

        Assert.Equal("Tag", KindOf(t, "DOCTYPE"));
        Assert.Equal("String", KindOf(t, "\"18 ms\""));
        Assert.Equal("String", KindOf(t, "\"0.0' s'\""));
        Assert.Equal("String", KindOf(t, "\"300px\""));
        Assert.Equal("String", KindOf(t, "\"contact\""));
        Assert.Equal("Type", KindOf(t, "Topping"));
        Assert.Equal("Member", KindOf(t, "Owner"));
        Assert.Equal("Member", KindOf(t, "CheckIn"));
        Assert.Equal("Number", KindOf(t, "12"));
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
