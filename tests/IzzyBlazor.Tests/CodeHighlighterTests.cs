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
    public void Unknown_languages_are_not_highlighted()
    {
        Assert.False(IzzyCodeHighlighter.Instance.TryHighlight("x", "text", out var tokens));
        Assert.Empty(tokens);
    }

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
    public void CSharp_verbatim_interpolated_and_raw_strings()
    {
        var t = Lex("cs", "var a = @\"C:\\x \"\"q\"\"\"; var b = $\"{a} y\"; var c = \"\"\"\n raw \"x\"\n \"\"\";");

        Assert.Equal("String", KindOf(t, "@\"C:\\x \"\"q\"\"\""));
        Assert.Equal("String", KindOf(t, "$\"{a} y\""));
        Assert.Equal("String", KindOf(t, "\"\"\"\n raw \"x\"\n \"\"\""));
    }

    [Fact]
    public void Razor_markup_transitions_and_code_blocks()
    {
        var t = Lex("razor", "@page \"/x\"\n<Button Variant=\"ButtonVariant.Outline\" @onclick=\"Save\">Save @Model.Name</Button>\n<a href=\"@Url(\"r\")\">me@example.com</a>\n@if (ok) { <p>Yes</p> } else { <p>No</p> }\n@* note *@\n@code {\n    private int count = 2;\n}");

        Assert.Equal("Meta", KindOf(t, "@page"));
        Assert.Equal("Tag", KindOf(t, "Button"));
        Assert.Equal("Attribute", KindOf(t, "@onclick"));
        Assert.Equal("String", KindOf(t, "\"ButtonVariant.Outline\""));
        Assert.Equal("Function", KindOf(t, "Url"));
        Assert.Equal("String", KindOf(t, "\"r\""));
        Assert.Contains(t, x => x.Kind == CodeTokenKind.Text && x.Text.Contains("me@example.com"));
        Assert.Equal("Keyword", KindOf(t, "if"));
        Assert.Equal("Keyword", KindOf(t, "else"));
        Assert.Equal("Comment", KindOf(t, "@* note *@"));
        Assert.Equal("Meta", KindOf(t, "@code"));
        Assert.Equal("Keyword", KindOf(t, "private"));
        Assert.Equal("Number", KindOf(t, "2"));
    }

    [Fact]
    public void Css_selectors_properties_values_and_at_rules()
    {
        var t = Lex("css", "@media (width >= 640px) {\n  .card:hover > a { color: var(--ib-x); margin: -0.5rem !important; background: #fff; }\n}");

        Assert.Equal("Meta", KindOf(t, "@media"));
        Assert.Equal("Number", KindOf(t, "640px"));
        Assert.Equal("Tag", KindOf(t, ".card"));
        Assert.Equal("Keyword", KindOf(t, ":hover"));
        Assert.Equal("Attribute", KindOf(t, "color"));
        Assert.Equal("Function", KindOf(t, "var"));
        Assert.Equal("Attribute", KindOf(t, "--ib-x"));
        Assert.Equal("Number", KindOf(t, "-0.5rem"));
        Assert.Equal("Keyword", KindOf(t, "!important"));
        Assert.Equal("Number", KindOf(t, "#fff"));
    }

    [Fact]
    public void Json_keys_values_and_literals()
    {
        var t = Lex("json", "{ \"name\": \"izzy\", \"n\": -2.5e3, \"ok\": true }");

        Assert.Equal("Attribute", KindOf(t, "\"name\""));
        Assert.Equal("String", KindOf(t, "\"izzy\""));
        Assert.Equal("Number", KindOf(t, "-2.5e3"));
        Assert.Equal("Keyword", KindOf(t, "true"));
    }

    [Fact]
    public void Shell_commands_flags_strings_and_comments()
    {
        var t = Lex("bash", "# build\n$ dotnet add package IzzyBlazor --version 0.1.0 && echo \"done $HOME\" | grep done\nexport PATH=$PATH:/x");

        Assert.Equal("Comment", KindOf(t, "# build"));
        Assert.Equal("Function", KindOf(t, "dotnet"));
        Assert.Equal("Attribute", KindOf(t, "--version"));
        Assert.Equal("Function", KindOf(t, "echo"));
        Assert.Equal("String", KindOf(t, "\"done $HOME\""));
        Assert.True(IsPlain(t, "done"));
        Assert.Equal("Keyword", KindOf(t, "export"));
        Assert.Equal("Meta", KindOf(t, "$PATH"));
    }

    [Fact]
    public void Html_script_and_style_use_their_own_lexers()
    {
        var t = Lex("html", "<!-- c --><style>.a { color: red; }</style><script>const x = 1;</script>");

        Assert.Equal("Comment", KindOf(t, "<!-- c -->"));
        Assert.Equal("Attribute", KindOf(t, "color"));
        Assert.Equal("Keyword", KindOf(t, "const"));
    }

    [Theory]
    [InlineData("xml")]
    [InlineData("html")]
    public void Cdata_is_one_string_whatever_it_holds(string language)
    {
        var t = Lex(language, "<value><![CDATA[<z> & a > b]]></value><value><![CDATA[open");

        Assert.Equal("String", KindOf(t, "<z> & a > b"));
        Assert.Equal("Meta", KindOf(t, "]]>"));
        Assert.Equal("String", KindOf(t, "open"));
        Assert.Equal(3, t.Count(token => token.Kind == CodeTokenKind.Tag && token.Text == "value"));
    }

    [Fact]
    public void Xml_style_and_script_elements_are_markup()
    {
        var t = Lex("xml", "<style><const>x</const></style><r:script a=\"1\"/>");

        Assert.Equal("Tag", KindOf(t, "const"));
        Assert.Null(KindOf(t, "x"));
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
    public void Code_highlight_off_and_unknown_language_render_plain()
    {
        var off = Render<Code>(p => p.Add(c => c.Block, true).Add(c => c.Language, "csharp").Add(c => c.Highlight, false).AddChildContent("var x = 1;"));
        Assert.Empty(off.FindAll("span[class^='ib-tok']"));

        var text = Render<Code>(p => p.Add(c => c.Block, true).Add(c => c.Language, "text").AddChildContent("var x = 1;"));
        Assert.Empty(text.FindAll("span[class^='ib-tok']"));
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
