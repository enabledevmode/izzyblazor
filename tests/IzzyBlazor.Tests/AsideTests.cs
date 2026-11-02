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
}
