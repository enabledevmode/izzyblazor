using Microsoft.AspNetCore.Components;

namespace IzzyBlazor.Tests;

public class PartSwapTests : BunitContext
{
    public PartSwapTests() => SetRendererInfo(new RendererInfo("Static", false));

    [Theory]
    [InlineData("sidebar", ".ib-shell-sidebar-head .ib-shell-sidebar-slot")]
    [InlineData("card", ".ib-card-heading")]
    [InlineData("dialog", ".ib-dialog-title")]
    public void A_part_swapped_in_with_if_else_keeps_following_its_content(string kind, string selector)
    {
        PartSwapProbe? probe = null;
        RenderFragment content = b =>
        {
            b.OpenComponent<PartSwapProbe>(0);
            b.AddComponentParameter(1, nameof(PartSwapProbe.Kind), kind);
            b.AddComponentReferenceCapture(2, r => probe = (PartSwapProbe)r);
            b.CloseComponent();
        };
        var text = kind switch
        {
            "sidebar" => SwapThenBump(Render<AppSidebar>(p => p.AddChildContent(content))),
            "card" => SwapThenBump(Render<Card>(p => p.AddChildContent(content))),
            _ => SwapThenBump(Render<Dialog>(p => p.Add(d => d.Id, "d").AddChildContent(content))),
        };

        string SwapThenBump<T>(IRenderedComponent<T> cut) where T : IComponent
        {
            cut.InvokeAsync(probe!.Swap);
            cut.InvokeAsync(probe!.Bump);
            return cut.Find(selector).TextContent.Trim();
        }

        Assert.Equal("B 1", text);
    }

    [Theory]
    [InlineData("banner", "BannerActions")]
    [InlineData("empty", "EmptyStateActions")]
    [InlineData("accordion", "AccordionHeader")]
    [InlineData("dialog", "DialogFooter")]
    [InlineData("sidebar", "SidebarHeader")]
    public void A_removed_slot_written_as_a_child_element_names_its_part(string kind, string part)
    {
        var error = Assert.ThrowsAny<InvalidOperationException>(() => Render<RemovedSlotProbe>(p => p.Add(r => r.Kind, kind)));

        Assert.Contains(part, error.Message);
    }

    [Fact]
    public void A_part_outside_its_component_says_where_it_belongs()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Render<CardActions>());

        Assert.Equal("CardActions must be inside a Card.", error.Message);
    }
}
