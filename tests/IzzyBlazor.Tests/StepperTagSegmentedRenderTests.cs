using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace IzzyBlazor.Tests;

public class StepperTagSegmentedRenderTests : BunitContext
{
    public enum View
    {
        Day,
        [Display(Name = "Whole week")] Week,
    }

    public class Model
    {
        public View View { get; set; } = View.Week;
    }

    private readonly Model _model = new();

    private IRenderedComponent<Stepper> RenderStepper(int current) =>
        Render<Stepper>(p => p
            .Add(s => s.Current, current)
            .AddChildContent<Step>(s => s.Add(x => x.Label, "Cart").Add(x => x.Href, "/cart"))
            .AddChildContent<Step>(s => s.Add(x => x.Label, "Shipping").Add(x => x.Description, "Address"))
            .AddChildContent<Step>(s => s.Add(x => x.Label, "Pay")));

    [Fact]
    public void Stepper_marks_done_current_and_upcoming()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var cut = RenderStepper(2);

        var ol = cut.Find("ol.ib-steps");
        Assert.Equal("Progress", ol.GetAttribute("aria-label"));
        var steps = cut.FindAll("li.ib-step");
        Assert.Equal(3, steps.Count);

        Assert.Contains("ib-step-complete", steps[0].ClassName);
        Assert.NotNull(steps[0].QuerySelector(".ib-step-marker svg"));
        Assert.Equal("Cart (completed)", steps[0].QuerySelector("a.ib-step-label")!.TextContent);
        Assert.Equal("/cart", steps[0].QuerySelector("a")!.GetAttribute("href"));

        Assert.Contains("ib-step-current", steps[1].ClassName);
        Assert.Equal("step", steps[1].GetAttribute("aria-current"));
        Assert.Equal("2", steps[1].QuerySelector(".ib-step-marker")!.TextContent.Trim());
        Assert.Equal("Address", steps[1].QuerySelector(".ib-step-description")!.TextContent);

        Assert.Contains("ib-step-upcoming", steps[2].ClassName);
        Assert.Null(steps[2].GetAttribute("aria-current"));
        Assert.Equal("3", steps[2].QuerySelector(".ib-step-marker")!.TextContent.Trim());
    }

    [Fact]
    public void Segmented_control_is_a_fieldset_of_radios()
    {
        var context = new EditContext(_model);
        var cut = Render<SegmentedControl<View>>(p => p
            .AddCascadingValue(context)
            .Add(s => s.Value, _model.View)
            .Add(s => s.ValueExpression, () => _model.View)
            .Add(s => s.HideLabel, true)
            .Add(s => s.FullWidth, true));

        var legend = cut.Find("legend");
        Assert.Equal("View", legend.TextContent);
        Assert.Contains("ib-visually-hidden", legend.ClassName);
        Assert.Contains("ib-segmented-full", cut.Find(".ib-segmented").ClassName);
        var radios = cut.FindAll("input.ib-segment-input[type=radio]");
        Assert.Equal(2, radios.Count);
        Assert.True(radios[1].HasAttribute("checked"));
        Assert.Equal("Whole week", cut.FindAll(".ib-segment-label")[1].TextContent);
    }

    [Fact]
    public void Radios_share_a_name_where_blazor_generates_none()
    {
        // WebAssembly pages: no field names, so radios would each be their own group.
        var context = new EditContext(_model) { ShouldUseFieldIdentifiers = false };
        var radios = Render<SegmentedControl<View>>(p => p
            .AddCascadingValue(context)
            .Add(s => s.Value, _model.View)
            .Add(s => s.ValueExpression, () => _model.View)).FindAll("input[type=radio]");

        var name = radios[0].GetAttribute("name");
        Assert.False(string.IsNullOrEmpty(name));
        Assert.All(radios, r => Assert.Equal(name, r.GetAttribute("name")));
    }

    [Fact]
    public void Static_tag_removes_with_a_link_named_for_it()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var tag = Render<Tag>(p => p
            .Add(t => t.Label, "Region: Europe")
            .Add(t => t.Status, Status.Info)
            .Add(t => t.RemoveHref, "/invoices")
            .Add(t => t.OnRemove, () => { })).Find(".ib-tag");

        Assert.Contains("ib-tag-info", tag.ClassName);
        Assert.Contains("ib-tag-removable", tag.ClassName);
        Assert.Equal("Region: Europe", tag.QuerySelector(".ib-tag-label")!.TextContent);
        var remove = tag.QuerySelector("a.ib-tag-remove")!;
        Assert.Equal("/invoices", remove.GetAttribute("href"));
        Assert.Equal("Remove Region: Europe", remove.GetAttribute("aria-label"));
        Assert.Null(tag.QuerySelector("button"));
    }
}
