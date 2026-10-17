using Microsoft.AspNetCore.Components;

namespace IzzyBlazor.Tests;

public class LayoutExtrasRenderTests : BunitContext
{
    [Fact]
    public void Grid_columns_per_breakpoint()
    {
        var div = Render<Grid>(p => p
            .Add(g => g.Columns, new Responsive<int> { Base = 1, Lg = 3 })
            .Add(g => g.Gap, (Responsive<int>)4)).Find("div");

        Assert.Equal("ib-grid ib-cols ib-cols-lg ib-gap", div.ClassName);
        Assert.Equal("--ib-cols:repeat(1, minmax(0, 1fr));--ib-cols-lg:repeat(3, minmax(0, 1fr));--ib-gap:var(--ib-space-4);", div.GetAttribute("style"));
    }

    [Fact]
    public void Grid_min_child_width_fits_columns()
    {
        var div = Render<Grid>(p => p.Add(g => g.MinChildWidth, Length.Rem(12))).Find("div");

        Assert.Equal("--ib-cols:repeat(auto-fill, minmax(min(12rem, 100%), 1fr));", div.GetAttribute("style"));
    }

    [Fact]
    public void Grid_stretch_uses_auto_fit()
    {
        var div = Render<Grid>(p => p.Add(g => g.MinChildWidth, Length.Rem(12)).Add(g => g.Stretch, true)).Find("div");

        Assert.Equal("--ib-cols:repeat(auto-fit, minmax(min(12rem, 100%), 1fr));", div.GetAttribute("style"));
    }

    [Fact]
    public void Grid_template_wins_over_columns()
    {
        var div = Render<Grid>(p => p
            .Add(g => g.Columns, (Responsive<int>)3)
            .Add(g => g.Template, new Responsive<string> { Base = "1fr", Md = "15rem 1fr" })).Find("div");

        Assert.Equal("ib-grid ib-cols ib-cols-md", div.ClassName);
        Assert.Equal("--ib-cols:1fr;--ib-cols-md:15rem 1fr;", div.GetAttribute("style"));
    }

    [Fact]
    public void Grid_template_is_checked()
    {
        Assert.Throws<FormatException>(() => Render<Grid>(p => p.Add(g => g.Template, (Responsive<string>)"1fr; color: red")));
    }

    [Fact]
    public void Grid_rejects_more_than_twelve_columns()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Render<Grid>(p => p.Add(g => g.Columns, (Responsive<int>)13)));
    }

    [Fact]
    public void Grid_item_spans()
    {
        var div = Render<GridItem>(p => p.Add(g => g.ColSpan, new Responsive<int> { Base = 1, Md = 2 })).Find("div");

        Assert.Contains("ib-colspan-md", div.ClassList);
        Assert.Contains("--ib-colspan-md:span min(2,", div.GetAttribute("style"));

        // Any component spans, fields included, and Full takes the whole row.
        var full = Render<Box>(p => p.Add(b => b.ColSpan, (Responsive<int>)GridSpan.Full)).Find("div");
        Assert.Contains("--ib-colspan:1 / -1;", full.GetAttribute("style"));
    }

    [Fact]
    public void Container_size_and_aspect_ratio()
    {
        Assert.Equal("ib-container ib-container-sm", Render<Container>(p => p.Add(c => c.Size, ContainerSize.Sm)).Find("div").ClassName);
        Assert.Equal("--ib-ratio:1;", Render<AspectRatio>(p => p.Add(a => a.Ratio, 1.0)).Find("div").GetAttribute("style"));
        Assert.Throws<ArgumentOutOfRangeException>(() => Render<AspectRatio>(p => p.Add(a => a.Ratio, 0.0)));
    }

    [Fact]
    public void Divider_forms()
    {
        Assert.Equal("hr", Render<Divider>().Find(".ib-divider").TagName.ToLowerInvariant());

        var vertical = Render<Divider>(p => p.Add(d => d.Orientation, Orientation.Vertical)).Find(".ib-divider");
        Assert.Equal("separator", vertical.GetAttribute("role"));
        Assert.Equal("vertical", vertical.GetAttribute("aria-orientation"));

        var labeled = Render<Divider>(p => p.Add(d => d.Label, "or")).Find(".ib-divider");
        Assert.Equal("or", labeled.TextContent);
        Assert.Contains("ib-divider-label", labeled.ClassName);
    }

    [Fact]
    public void External_link_opens_a_new_tab_and_says_so()
    {
        var a = Render<Anchor>(p => p
            .Add(l => l.Href, "https://example.com")
            .Add(l => l.External, true)
            .AddChildContent("Example")).Find("a");

        Assert.Equal("_blank", a.GetAttribute("target"));
        Assert.Equal("noopener noreferrer", a.GetAttribute("rel"));
        Assert.Equal("ib-anchor", a.ClassName);
        Assert.Contains("(opens in a new tab)", a.QuerySelector(".ib-visually-hidden")!.TextContent);
    }

    [Fact]
    public void Code_inline_and_block()
    {
        Assert.Equal("ib-code", Render<Code>(p => p.AddChildContent("x")).Find("code").ClassName);

        var block = Render<Code>(p => p.Add(c => c.Block, true).Add(c => c.Language, "bash").AddChildContent("dotnet build"));
        Assert.Equal("dotnet build", block.Find("pre code").TextContent);
        Assert.Equal("bash", block.Find(".ib-code-language").TextContent);
        Assert.Equal("Copy code", block.Find("button.ib-code-copy").GetAttribute("aria-label"));
    }
}
