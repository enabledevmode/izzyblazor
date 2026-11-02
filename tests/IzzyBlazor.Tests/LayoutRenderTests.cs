namespace IzzyBlazor.Tests;

public class LayoutRenderTests : BunitContext
{
    [Fact]
    public void Invalid_As_throws()
    {
        Assert.Throws<ArgumentException>(() => Render<Box>(p => p.Add(b => b.As, "div onclick=x")));
    }

    [Fact]
    public void Map_adds_one_class_and_variable_per_breakpoint()
    {
        var cut = Render<Box>(p => p.Add(b => b.PaddingX, new Responsive<int> { Base = 4, Md = 10 }));
        var box = cut.Find("div");

        Assert.Equal("ib-box ib-px ib-px-md", box.ClassName);
        Assert.Equal("--ib-px:var(--ib-space-4);--ib-px-md:var(--ib-space-10);", box.GetAttribute("style"));
    }

    [Fact]
    public void Unmatched_attributes_pass_through()
    {
        var cut = Render<Box>(p => p
            .AddUnmatched("id", "hero")
            .AddUnmatched("aria-label", "Hero"));
        var box = cut.Find("div");

        Assert.Equal("hero", box.Id);
        Assert.Equal("Hero", box.GetAttribute("aria-label"));
    }
}
