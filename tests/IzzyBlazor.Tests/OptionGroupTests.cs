using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace IzzyBlazor.Tests;

public class OptionGroupTests : BunitContext
{
    public enum Zone
    {
        Utc,
        [Display(Name = "Paris", GroupName = "Europe")] Paris,
        [Display(Name = "Tokyo", GroupName = "Asia")] Tokyo,
        [Display(Name = "Berlin", GroupName = "Europe")] Berlin,
    }

    public class Model
    {
        public Zone Zone { get; set; }

        public string? City { get; set; }
    }

    private readonly Model _model = new();
    private readonly EditContext _context;

    public OptionGroupTests()
    {
        _context = new EditContext(_model);
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
    }

    // An enum's [Display(GroupName)] groups it: the ungrouped first, then groups as they first appear,
    // Berlin joining Paris though Tokyo came between.
    [Fact]
    public void A_select_lists_its_groups_as_optgroups_in_first_appearance_order()
    {
        var select = Render<Select<Zone>>(p => p
            .AddCascadingValue(_context)
            .Add(f => f.Value, _model.Zone)
            .Add(f => f.ValueExpression, () => _model.Zone)).Find("select");

        Assert.Equal("Utc", select.Children[0].TextContent);
        Assert.Equal(["Europe", "Asia"], select.QuerySelectorAll("optgroup").Select(g => g.GetAttribute("label")));
        Assert.Equal(["Paris", "Berlin"], select.QuerySelectorAll("optgroup")[0].QuerySelectorAll("option").Select(o => o.TextContent));
    }

    [Fact]
    public void A_combobox_list_has_a_labelled_group_for_each_and_options_numbered_through()
    {
        string[] cities = ["Lima", "Oslo", "Kyoto", "Rome"];
        var cut = Render<Combobox<string?>>(p => p
            .AddCascadingValue(_context)
            .Add(f => f.Value, _model.City)
            .Add(f => f.ValueExpression, () => _model.City)
            .Add(f => f.Items, cities)
            .Add(f => f.ItemGroup, c => c is "Lima" ? null : c is "Kyoto" ? "Asia" : "Europe"));

        var list = cut.Find("[role=listbox]");
        var groups = list.QuerySelectorAll("[role=group]");
        Assert.Equal(["Europe", "Asia"], groups.Select(g => list.QuerySelector($"#{g.GetAttribute("aria-labelledby")}")!.TextContent));
        Assert.Equal(["Oslo", "Rome"], groups[0].QuerySelectorAll("[role=option]").Select(o => o.GetAttribute("data-value")));
        Assert.Equal(Enumerable.Range(0, 4).Select(i => $"{cut.Find("select").Id}-list-{i}"), list.QuerySelectorAll("[role=option]").Select(o => o.Id));
        Assert.Equal(["Europe", "Asia"], cut.FindAll("select optgroup").Select(g => g.GetAttribute("label")));
    }
}
