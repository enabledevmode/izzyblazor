namespace IzzyBlazor.Tests;

/// <summary>AppAside as a page's own panel: where it renders, its name, and what it does below its breakpoint.</summary>
public class AsideTests : BunitContext
{
    [Fact]
    public void An_aside_written_in_the_page_renders_in_the_shell_after_the_main_area()
    {
        var cut = Render<AsideProbe>();

        var shell = cut.Find("ib-shell");
        var parts = shell.Children.Select(c => c.TagName.ToLowerInvariant()).ToList();
        Assert.Equal(["a", "header", "main", "aside", "footer"], parts);
        Assert.Empty(cut.FindAll("main aside"));

        var aside = cut.Find("ib-shell > aside");
        Assert.Equal("ib-aside", aside.Id);
        Assert.Equal("Rule", aside.GetAttribute("aria-label"));
        Assert.Contains("--ib-aside-w:420px", aside.GetAttribute("style"));
        Assert.Contains("ib-shell-aside-hide", aside.ClassName);
    }

    [Fact]
    public void A_page_without_an_aside_leaves_no_column()
    {
        var cut = Render<AsideProbe>(p => p.Add(s => s.WithAside, false));

        Assert.Empty(cut.FindAll("aside"));
    }

    [Fact]
    public void Headings_in_the_panel_stay_out_of_the_pages_outline()
    {
        var cut = Render<AsideProbe>();

        var links = cut.FindAll(".ib-toc a").Select(a => a.TextContent.Trim()).ToList();
        Assert.Contains("Below the panel's markup", links);
        Assert.DoesNotContain("In the panel", links);
    }

    [Theory]
    [InlineData(AsideSmallScreen.Hidden, "ib-shell-aside-hide")]
    [InlineData(AsideSmallScreen.Below, "ib-shell-aside-below")]
    [InlineData(AsideSmallScreen.Drawer, "ib-shell-aside-drawer")]
    public void SmallScreen_sets_its_class(AsideSmallScreen mode, string css)
    {
        var aside = Render<AsideProbe>(p => p.Add(s => s.SmallScreen, mode)).Find("aside");

        Assert.Contains(css, aside.ClassName);
        Assert.Contains("ib-shell-aside-bp-lg", aside.ClassName);
    }

    [Fact]
    public void Only_a_drawer_has_a_close_button_and_a_backdrop()
    {
        var below = Render<AsideProbe>(p => p.Add(s => s.SmallScreen, AsideSmallScreen.Below));
        Assert.Empty(below.FindAll("aside [data-ib-shell-close]"));
        Assert.Empty(below.FindAll(".ib-shell-aside-backdrop"));

        var drawer = Render<AsideProbe>(p => p.Add(s => s.SmallScreen, AsideSmallScreen.Drawer));
        var close = drawer.Find("aside .ib-shell-aside-close a[data-ib-shell-close]");
        Assert.Equal("Close panel", close.GetAttribute("aria-label"));
        // It's inside the drawer and shown only while it's open, so it carries no aria-expanded or aria-controls.
        Assert.Null(close.GetAttribute("aria-controls"));
        Assert.EndsWith("#ib-shell-closed", close.GetAttribute("href"));

        // The backdrop is the aside's sibling in the shell, where the shell's CSS shows it.
        var backdrop = drawer.Find("ib-shell > a.ib-shell-aside-backdrop");
        Assert.Equal("true", backdrop.GetAttribute("aria-hidden"));
        Assert.Equal("-1", backdrop.GetAttribute("tabindex"));
    }

    [Fact]
    public void Padding_sets_the_inner_space_per_breakpoint()
    {
        var aside = Render<AsideProbe>(p => p.Add(s => s.Padding, new Responsive<int> { Base = 4, Md = 6 })).Find("aside");

        Assert.Contains("ib-shell-aside-pad", aside.ClassName);
        Assert.Contains("ib-shell-aside-pad-md", aside.ClassName);
        Assert.Contains("--ib-asidepad-md:", aside.GetAttribute("style"));
    }

    [Fact]
    public void Outside_a_shell_it_is_a_block_where_it_is_written_with_no_drawer()
    {
        // A page interactive on its own, in a static layout, gets neither the shell nor its section.
        var aside = Render<AppAside>(p => p
            .Add(a => a.Label, "Rule")
            .Add(a => a.SmallScreen, AsideSmallScreen.Drawer)
            .AddChildContent("Details")).Find("aside");

        Assert.Equal("ib-shell-aside ib-shell-aside-alone", aside.ClassName);
        Assert.Equal("Rule", aside.GetAttribute("aria-label"));
        Assert.Null(aside.QuerySelector("[data-ib-shell-close]"));
        Assert.Equal("Details", aside.QuerySelector(".ib-shell-aside-inner")?.TextContent);
    }

    [Fact]
    public void An_id_attribute_sets_the_asides_id()
    {
        // Parameter names match without case, so id="toc", as written before Id existed, sets Id.
        var aside = Render<AppAside>(p => p.AddUnmatched("id", "toc")).Find("aside");
        Assert.Equal("toc", aside.Id);
    }

    [Fact]
    public void A_pages_aside_is_marked_so_it_takes_the_place_of_the_layouts()
    {
        var cut = Render<AsideProbe>();

        Assert.Contains("ib-shell-aside-page", cut.Find("aside").ClassName);
        Assert.DoesNotContain("ib-shell-aside-page", Render<AppAside>().Find("aside").ClassName);
    }

    [Fact]
    public void AsideToggle_links_to_the_aside()
    {
        var a = Render<AsideToggle>().Find("a");

        Assert.Equal("./#ib-aside", a.GetAttribute("href"));
        Assert.Equal("ib-aside", a.GetAttribute("aria-controls"));
        Assert.Equal("Open panel", a.GetAttribute("aria-label"));
        Assert.Contains("ib-shell-aside-toggle", a.ClassName);
        Assert.Null(a.GetAttribute("role"));
    }
}
