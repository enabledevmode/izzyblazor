using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

public class DataGridFilterTemplateTests : BunitContext
{
    public sealed record Row(string Name, int Total);

    private static readonly Row[] Rows = [new("Ada", 10), new("Grace", 20), new("Alan", 30)];

    private static RenderFragment Columns(RenderFragment<FilterTemplateContext> template, Func<IReadOnlyList<string?>, string>? text = null, IReadOnlyList<FilterOperator>? operators = null) => b =>
    {
        b.OpenComponent<PropertyColumn<Row, string>>(0);
        b.AddComponentParameter(1, "Property", (System.Linq.Expressions.Expression<Func<Row, string>>)(r => r.Name));
        b.CloseComponent();
        b.OpenComponent<PropertyColumn<Row, int>>(2);
        b.AddComponentParameter(3, "Property", (System.Linq.Expressions.Expression<Func<Row, int>>)(r => r.Total));
        b.AddComponentParameter(4, "Filterable", true);
        b.AddComponentParameter(5, "FilterTemplate", template);
        b.AddComponentParameter(6, "FilterText", text);
        b.AddComponentParameter(7, "FilterOperators", operators);
        b.CloseComponent();
    };

    private static readonly RenderFragment<FilterTemplateContext> Input = context => b =>
    {
        b.OpenElement(0, "input");
        b.AddMultipleAttributes(1, context.Attributes);
        b.AddAttribute(2, "value", context.Value);
        b.CloseElement();
    };

    private static readonly RenderFragment<FilterTemplateContext> Checkboxes = context => b =>
    {
        foreach (var value in new[] { "10", "20" })
        {
            b.OpenElement(0, "input");
            b.AddMultipleAttributes(1, context.CheckboxAttributes(value));
            b.CloseElement();
        }
    };

    private IRenderedComponent<DataGrid<Row>> Static(string url, RenderFragment columns, bool row = false)
    {
        Services.GetRequiredService<BunitNavigationManager>().NavigateTo(url);
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        return Render<DataGrid<Row>>(p => p.Add(g => g.Items, Rows).Add(g => g.FilterRow, row).Add(g => g.ChildContent, columns));
    }

    private static string[] Names(IRenderedComponent<DataGrid<Row>> cut) =>
        cut.FindAll("tbody td:first-child").Select(c => c.TextContent.Trim()).ToArray();

    [Fact]
    public void Its_control_posts_under_the_columns_key_and_filters_by_the_first_operator()
    {
        var cut = Static("/orders?f-total=20", Columns(Input, operators: [FilterOperator.GreaterThanOrEqual]));

        var input = cut.Find(".ib-table-filter-panel input[data-ib-filter]");
        Assert.Equal("f-total", input.GetAttribute("name"));
        Assert.Equal(cut.Find("form[id]").Id, input.GetAttribute("form"));
        Assert.Equal("20", input.GetAttribute("value"));
        Assert.NotNull(cut.Find(".ib-table-filter-panel .ib-table-filter-apply"));
        Assert.Equal(new[] { "Grace", "Alan" }, Names(cut));
    }

    [Fact]
    public void Several_values_are_any_of_them_and_show_as_ticked()
    {
        var cut = Static("/orders?f-total=10&f-total=20", Columns(Checkboxes));

        Assert.Equal(new[] { "Ada", "Grace" }, Names(cut));
        Assert.All(cut.FindAll("input[type=checkbox][data-ib-filter]"), box => Assert.True(box.HasAttribute("checked")));
    }

    [Fact]
    public void In_a_row_it_opens_from_a_field_that_says_what_it_is_set_to()
    {
        var cut = Static("/orders?f-total=20", Columns(Input, values => $"From {values[0]}", [FilterOperator.GreaterThanOrEqual]), row: true);

        var summary = cut.Find(".ib-table-filter-row .ib-filter-cell-summary");
        Assert.Equal("From 20", summary.TextContent.Trim());
        Assert.NotNull(cut.Find($"[id='{summary.GetAttribute("popovertarget")}'] input[name='f-total']"));
        Assert.Empty(cut.FindAll(".ib-table-filter-row [data-ib-menu]"));
    }

    [Fact]
    public void A_bound_grids_control_changes_the_filter()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        IzzyBlazor.Filter? heard = null;
        var cut = Render<DataGrid<Row>>(p => p.Add(g => g.Items, Rows).Add(g => g.Filter, (IzzyBlazor.Filter?)null)
            .Add(g => g.FilterChanged, (IzzyBlazor.Filter? f) => heard = f)
            .Add(g => g.ChildContent, Columns(Input, operators: [FilterOperator.GreaterThanOrEqual])));

        var input = cut.Find(".ib-table-filter-panel input");
        Assert.False(input.HasAttribute("name"));
        input.Change("20");

        Assert.Contains("20", heard?.ToString());
    }

    [Fact]
    public void A_range_has_a_control_for_each_end()
    {
        RenderFragment<FilterTemplateContext> range = context => b =>
        {
            b.OpenElement(0, "input");
            b.AddMultipleAttributes(1, context.Attributes);
            b.AddAttribute(2, "value", context.Value);
            b.CloseElement();
            b.OpenElement(3, "input");
            b.AddMultipleAttributes(4, context.ToAttributes);
            b.AddAttribute(5, "value", context.To);
            b.CloseElement();
        };

        var cut = Static("/orders?f-total=15&f-total-to=25", Columns(range, operators: [FilterOperator.Between]));

        Assert.Equal(new[] { "Grace" }, Names(cut));
        Assert.Equal("25", cut.Find("input[name='f-total-to']").GetAttribute("value"));
    }

    [Fact]
    public void A_list_of_values_beside_it_is_refused()
    {
        RenderFragment columns = b =>
        {
            b.OpenComponent<PropertyColumn<Row, string>>(0);
            b.AddComponentParameter(1, "Property", (System.Linq.Expressions.Expression<Func<Row, string>>)(r => r.Name));
            b.AddComponentParameter(2, "Filterable", true);
            b.AddComponentParameter(3, "FilterList", true);
            b.AddComponentParameter(4, "FilterTemplate", Input);
            b.CloseComponent();
        };

        Assert.Throws<InvalidOperationException>(() => Static("/orders", columns));
    }
}
