namespace IzzyBlazor.Tests;

public class TypographyRenderTests : BunitContext
{
    [Fact]
    public void TextBlock_renders_a_paragraph_by_default()
    {
        var cut = Render<TextBlock>(p => p.AddChildContent("Hello"));

        cut.MarkupMatches("<p class=\"ib-text\">Hello</p>");
    }

    [Fact]
    public void TextBlock_As_changes_the_element()
    {
        var cut = Render<TextBlock>(p => p.Add(t => t.As, "span"));

        Assert.Equal("SPAN", cut.Find(".ib-text").TagName);
    }

    [Fact]
    public void Typography_props_add_classes()
    {
        var cut = Render<TextBlock>(p => p
            .Add(t => t.Weight, FontWeight.Semibold)
            .Add(t => t.Family, FontFamily.Mono)
            .Add(t => t.LineHeight, LineHeight.Relaxed)
            .Add(t => t.LetterSpacing, LetterSpacing.Wider)
            .Add(t => t.Transform, TextTransform.Uppercase)
            .Add(t => t.Decoration, TextDecoration.LineThrough)
            .Add(t => t.Wrap, TextWrap.NoWrap)
            .Add(t => t.Italic, true)
            .Add(t => t.Truncate, true)
            .Add(t => t.TabularNums, true));

        Assert.Equal(
            "ib-text ib-fw-semibold ib-ff-mono ib-lh-relaxed ib-ls-wider ib-tt-uppercase ib-td-line-through ib-tw-no-wrap ib-italic ib-truncate ib-tabular",
            cut.Find("p").ClassName);
    }

    [Fact]
    public void Size_and_align_are_responsive()
    {
        var cut = Render<TextBlock>(p => p
            .Add(t => t.Size, new Responsive<TextSize> { Base = TextSize.Lg, Md = TextSize.Xl2 })
            .Add(t => t.Align, new Responsive<TextAlign> { Base = TextAlign.Center, Md = TextAlign.Start }));
        var p = cut.Find("p");

        Assert.Equal("ib-text ib-fs ib-fs-md ib-ta ib-ta-md", p.ClassName);
        Assert.Equal("--ib-fs:var(--ib-text-lg);--ib-fs-md:var(--ib-text-2xl);--ib-ta:center;--ib-ta-md:start;", p.GetAttribute("style"));
    }

    [Fact]
    public void LineClamp_sets_the_line_count()
    {
        var cut = Render<TextBlock>(p => p.Add(t => t.LineClamp, 3));
        var p = cut.Find("p");

        Assert.Contains("ib-clamp", p.ClassName);
        Assert.Equal("--ib-clamp:3;", p.GetAttribute("style"));
    }

    [Theory]
    [InlineData(1, "h1", "var(--ib-text-4xl)")]
    [InlineData(2, "h2", "var(--ib-text-3xl)")]
    [InlineData(6, "h6", "var(--ib-text-md)")]
    public void Heading_level_sets_element_and_default_size(int level, string tag, string size)
    {
        var cut = Render<Heading>(p => p.Add(h => h.Level, level));
        var heading = cut.Find(tag);

        Assert.Equal($"--ib-fs:{size};", heading.GetAttribute("style"));
    }

    [Fact]
    public void Heading_size_overrides_the_level_default()
    {
        var cut = Render<Heading>(p => p
            .Add(h => h.Level, 1)
            .Add(h => h.Size, TextSize.Sm));

        Assert.Equal("--ib-fs:var(--ib-text-sm);", cut.Find("h1").GetAttribute("style"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void Heading_level_out_of_range_throws(int level)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Render<Heading>(p => p.Add(h => h.Level, level)));
    }
}
