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

    [Fact]
    public void Listed_headings_link_to_themselves_so_a_section_can_be_linked()
    {
        var cut = Render<OutlineProbe>();

        // The text is the link, so the heading's name stays its text.
        var h2 = cut.FindAll("h2").First(h => h.Id == "install");
        var anchor = h2.QuerySelector("a.ib-heading-anchor");
        Assert.Equal("./#install", anchor?.GetAttribute("href"));
        Assert.Equal("Install", h2.TextContent);

        // Levels the outline doesn't list (h1, h4) keep plain text; a heading with its own link isn't wrapped in another.
        Assert.Null(cut.Find("h1").QuerySelector("a"));
        Assert.Null(cut.Find("h4").QuerySelector("a"));
        Assert.Single(cut.Find("#linked-elsewhere").QuerySelectorAll("a"));
        Assert.Single(cut.Find("#via-a-component").QuerySelectorAll("a"));
    }

    [Fact]
    public void Without_OnThisPage_headings_render_as_before()
    {
        var cut = Render<OutlineProbe>(p => p.Add(o => o.WithOutline, false));

        Assert.All(cut.FindAll("h1, h2, h3, h4").Where(h => h.TextContent != "Its own id"), h => Assert.Null(h.GetAttribute("id")));
        Assert.Empty(cut.FindAll(".ib-heading-anchor"));
    }

    [Fact]
    public void OnThisPage_follows_headings_that_come_and_go_and_hides_with_none()
    {
        var cut = Render<OutlineProbe>();
        cut.Render(p => p.Add(o => o.ShowLate, true));
        Assert.Contains(cut.FindAll(".ib-toc-link"), a => a.TextContent == "Added later");

        cut.Render(p => p.Add(o => o.ShowLate, false));
        Assert.DoesNotContain(cut.FindAll(".ib-toc-link"), a => a.TextContent == "Added later");

        // Headings that swap text swap ids too, so a shared link still finds its section.
        cut.Render(p => p.Add(o => o.Swap, true));
        Assert.Equal("Beta", cut.Find("#beta").TextContent);
        Assert.Equal("Alpha", cut.Find("#alpha").TextContent);

        var empty = Render<OnThisPage>();
        Assert.True(empty.Find("ib-on-this-page").HasAttribute("hidden"));
    }

    [Theory]
    [InlineData("Getting started", "getting-started")]
    [InlineData("  Rows & columns  ", "rows-columns")]
    [InlineData("Code.HighlightLines", "code-highlightlines")]
    [InlineData("Événements", "événements")]
    [InlineData("?!", "section")]
    public void Heading_ids_are_slugs_of_their_text(string text, string slug) =>
        Assert.Equal(slug, PageOutline.Slug(text));

    [Fact]
    public void OnThisPage_checks_its_levels() =>
        Assert.Throws<ArgumentException>(() => Render<OnThisPage>(p => p.Add(o => o.MinLevel, 4).Add(o => o.MaxLevel, 3)));

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

    [Fact]
    public void PrevNext_links_the_pages_around_this_one()
    {
        var both = Render<PrevNext>(p => p
            .Add(n => n.PreviousHref, "/docs/install").Add(n => n.PreviousTitle, "Install")
            .Add(n => n.NextHref, "/docs/theming").Add(n => n.NextTitle, "Theming"));

        Assert.Equal("More pages", both.Find("nav").GetAttribute("aria-label"));
        Assert.Equal("Previous Install", both.Find("a[rel=prev]").TextContent.Trim().Replace("\n", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries).Aggregate((a, b) => $"{a} {b}"));
        Assert.Equal("/docs/theming", both.Find("a[rel=next]").GetAttribute("href"));

        var last = Render<PrevNext>(p => p.Add(n => n.PreviousHref, "/docs/install").Add(n => n.PreviousTitle, "Install"));
        Assert.Empty(last.FindAll("a[rel=next]"));
        Assert.Empty(Render<PrevNext>().FindAll("nav"));
        Assert.Throws<InvalidOperationException>(() => Render<PrevNext>(p => p.Add(n => n.NextHref, "/x")));
    }

    [Fact]
    public void CodeExample_shows_the_live_example_over_its_source()
    {
        var cut = Render<CodeExample>(p => p
            .Add(e => e.Code, "<Button>Save</Button>")
            .Add(e => e.Title, "Save.razor")
            .AddChildContent("<button>Save</button>"));

        Assert.Equal("Save", cut.Find(".ib-example-preview button").TextContent);
        var code = cut.Find(".ib-example-source ib-code");
        Assert.Equal("Save.razor", code.QuerySelector(".ib-code-head")!.TextContent.Trim());
        Assert.Equal("<Button>Save</Button>", code.QuerySelector("pre code")!.TextContent);
    }

    [Fact]
    public void CodeExample_files_are_tabs_and_follow_changes_without_rendering_forever()
    {
        var cut = Render<ExampleProbe>();

        Assert.Equal(["Save.razor", "Save.razor.cs"], cut.FindAll(".ib-example-source label.ib-tab").Select(l => l.TextContent.Trim()));
        Assert.Equal(2, cut.FindAll(".ib-example-source ib-code").Count);

        // The parent re-renders (the files re-register): the example settles, and a changed file shows.
        cut.InvokeAsync(cut.Instance.Bump);
        Assert.Equal("Save 1", cut.Find(".ib-example-preview button").TextContent);
        cut.Render(p => p.Add(x => x.Markup, "<button>Saved</button>"));
        Assert.Contains("<button>Saved</button>", cut.Find(".ib-example-source pre code").TextContent);
    }

    [Fact]
    public void A_collapsible_CodeExample_hides_its_source_in_a_details_element()
    {
        var cut = Render<ExampleProbe>(p => p.Add(x => x.Collapsible, true));

        var details = cut.Find("details.ib-example-source");
        Assert.False(details.HasAttribute("open"));
        Assert.Equal("Show code", details.QuerySelector(".ib-example-show")!.TextContent);
    }
}
