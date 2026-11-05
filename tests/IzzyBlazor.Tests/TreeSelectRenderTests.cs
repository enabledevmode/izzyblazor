using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace IzzyBlazor.Tests;

public class TreeSelectRenderTests : BunitContext
{
    public class Product
    {
        public int? CategoryId { get; set; } = 12;
    }

    private sealed record Kind(int Id, string Name, Kind[] Children);

    private static readonly Kind[] Categories =
    [
        new(1, "Computers", [new(11, "Laptops", []), new(12, "Desktops", [])]),
        new(2, "Phones", [new(21, "Cases", [])]),
    ];

    private readonly Product _model = new();

    private IRenderedComponent<TreeSelect<int?, Kind>> Render_(Action<int?>? changed = null) => Render<TreeSelect<int?, Kind>>(p => p
        .AddCascadingValue(new EditContext(_model))
        .Add(t => t.Value, _model.CategoryId)
        .Add(t => t.ValueExpression, () => _model.CategoryId)
        .Add(t => t.ValueChanged, (int? v) => { _model.CategoryId = v; changed?.Invoke(v); })
        .Add(t => t.Items, Categories)
        .Add(t => t.ItemChildren, c => c.Children)
        .Add(t => t.ItemValue, c => c.Id)
        .Add(t => t.ItemText, c => c.Name)
        .Add(t => t.Label, "Category")
        .Add(t => t.Clearable, true));

    [Fact]
    public void A_static_render_shows_the_choice_and_its_radios_post_under_the_fields_name_with_its_branch_open()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var cut = Render_();

        var button = cut.Find(".ib-treeselect-button");
        Assert.Equal("Desktops", cut.Find(".ib-treeselect-text").TextContent);
        Assert.Equal(cut.Find(".ib-treeselect-panel").Id, button.GetAttribute("popovertarget"));
        Assert.Equal($"{cut.Find("label").Id} {cut.Find(".ib-treeselect-text").Id}", button.GetAttribute("aria-labelledby"));

        // The tree's radios and the empty one are one group, under the field's name.
        var radios = cut.FindAll("input[type=radio]");
        Assert.Single(radios.Select(r => r.GetAttribute("name")).Distinct());
        Assert.Equal("12", cut.Find("input[type=radio][checked]").GetAttribute("value"));
        Assert.Equal("1", cut.Find("details[open]").ParentElement!.GetAttribute("data-ib-key"));
    }

    [Fact]
    public void Choosing_in_the_tree_or_clearing_sets_the_value_on_an_interactive_page()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var cut = Render_();

        cut.Find("li[data-ib-key='21'] input").Change(true);
        Assert.Equal(21, _model.CategoryId);

        cut.Find(".ib-treeselect-none").Change(true);
        Assert.Null(_model.CategoryId);
    }
}
