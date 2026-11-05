using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace IzzyBlazor.Tests;

public class MultiTreeSelectRenderTests : BunitContext
{
    public class Report
    {
        public List<string>? TeamIds { get; set; } = ["billing"];
    }

    private sealed record Unit(string Id, string Name, Unit[] Teams);

    private static readonly Unit[] Departments =
    [
        new("eng", "Engineering", [new("web", "Web", []), new("mobile", "Mobile", [])]),
        new("support", "Support", [new("billing", "Billing", []), new("tech", "Technical", [])]),
    ];

    private readonly Report _model = new();

    private IRenderedComponent<MultiTreeSelect<List<string>?, Unit, string>> Render_() => Render<MultiTreeSelect<List<string>?, Unit, string>>(p => p
        .AddCascadingValue(new EditContext(_model))
        .Add(t => t.Value, _model.TeamIds)
        .Add(t => t.ValueExpression, () => _model.TeamIds)
        .Add(t => t.ValueChanged, (List<string>? v) => _model.TeamIds = v)
        .Add(t => t.Items, Departments)
        .Add(t => t.ItemChildren, d => d.Teams)
        .Add(t => t.ItemValue, d => d.Id)
        .Add(t => t.ItemText, d => d.Name)
        .Add(t => t.Label, "Visible to"));

    [Fact]
    public void A_static_render_shows_a_branch_checked_whole_as_one_chip_and_its_boxes_post_under_the_fields_name()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        _model.TeamIds = ["eng", "web", "mobile", "billing"];
        var cut = Render_();

        Assert.Equal(["Engineering", "Billing"], cut.FindAll(".ib-chip .ib-chip-text").Select(c => c.TextContent));
        Assert.Equal("Engineering, Billing", cut.Find(".ib-treeselect-said").TextContent);
        var boxes = cut.FindAll("input[type=checkbox]");
        Assert.Single(boxes.Select(b => b.GetAttribute("name")).Distinct());
        Assert.Equal(["eng", "web", "mobile", "billing"], cut.FindAll("input[type=checkbox][checked]").Select(b => b.GetAttribute("value")));
    }

    [Fact]
    public void Unchecking_a_child_of_a_checked_parent_on_an_interactive_page_keeps_its_siblings()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        _model.TeamIds = ["eng", "web", "mobile", "billing"];
        var cut = Render_();

        // Either order can reach Blazor: the box clicked, then the parent the script unchecks.
        cut.Find("li[data-ib-key='mobile'] input").Change(false);
        cut.Find("li[data-ib-key='eng'] input").Change(false);

        Assert.Equal(["web", "billing"], _model.TeamIds);
    }
}
