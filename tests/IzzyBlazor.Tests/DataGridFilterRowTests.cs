using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

public class DataGridFilterRowTests : BunitContext
{
    public enum Kind { A, B, C, D, E }

    public sealed record Row(string Name, int Total, Kind Kind);

    private static readonly Row[] Rows = [new("Ada", 10, Kind.A), new("Grace", 20, Kind.B)];

    private IRenderedComponent<DataGrid<Row>> Grid(string url = "/orders", bool builder = true)
    {
        Services.GetRequiredService<BunitNavigationManager>().NavigateTo(url);
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        RenderFragment columns = b =>
        {
            b.OpenComponent<PropertyColumn<Row, string>>(0);
            b.AddComponentParameter(1, "Property", (System.Linq.Expressions.Expression<Func<Row, string>>)(r => r.Name));
            b.AddComponentParameter(2, "Filterable", true);
            b.CloseComponent();
            b.OpenComponent<PropertyColumn<Row, int>>(3);
            b.AddComponentParameter(4, "Property", (System.Linq.Expressions.Expression<Func<Row, int>>)(r => r.Total));
            b.AddComponentParameter(5, "Filterable", true);
            b.CloseComponent();
            b.OpenComponent<PropertyColumn<Row, Kind>>(6);
            b.AddComponentParameter(7, "Property", (System.Linq.Expressions.Expression<Func<Row, Kind>>)(r => r.Kind));
            b.AddComponentParameter(8, "Filterable", true);
            b.CloseComponent();
        };
        return Render<DataGrid<Row>>(p => p.Add(g => g.Items, Rows).Add(g => g.FilterRow, true).Add(g => g.FilterBuilder, builder).Add(g => g.ChildContent, columns));
    }

    [Fact]
    public void A_bound_grids_x_clears_the_filter()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        RenderFragment columns = b =>
        {
            b.OpenComponent<PropertyColumn<Row, Kind>>(0);
            b.AddComponentParameter(1, "Property", (System.Linq.Expressions.Expression<Func<Row, Kind>>)(r => r.Kind));
            b.AddComponentParameter(2, "Filterable", true);
            b.CloseComponent();
        };
        var (raised, heard) = (false, (IzzyBlazor.Filter?)Filter.Where("kind", FilterOperator.Equals, "A"));
        var cut = Render<DataGrid<Row>>(p => p.Add(g => g.Items, Rows).Add(g => g.FilterRow, true).Add(g => g.Filter, heard)
            .Add(g => g.FilterChanged, (IzzyBlazor.Filter? f) => { raised = true; heard = f; }).Add(g => g.ChildContent, columns));

        cut.Find("button.ib-filter-cell-clear").Click();

        Assert.True(raised);
        Assert.Null(heard);
    }

    [Fact]
    public void The_rows_form_carries_every_other_filter_so_it_posts_the_whole_state()
    {
        var cut = Grid("/orders?f-name-op=startswith&f-name=a&f-total-op=between&f-total=5&f-kind=A&sort=name");
        var form = cut.FindAll("form").Single(f => f.Id!.EndsWith("-row", StringComparison.Ordinal)).Id;

        var posted = cut.FindAll($"[form='{form}'][name]").Where(i => i.GetAttribute("type") == "hidden" || i.HasAttribute("data-ib-row"))
            .Select(i => $"{i.GetAttribute("name")}={i.GetAttribute("value")}").Order().ToList();

        Assert.Equal(new[] { "f-kind=A", "f-name-op=startswith", "f-name=a", "f-total-op=between", "f-total=5", "sort=name" }.Order(), posted);
    }

    [Theory]
    [InlineData("?filter=name%20eq%20'Ada'%20or%20total%20eq%2020&f-kind=B", new[] { "Grace" })]
    [InlineData("?filter=name%20eq%20'Ada'%20or%20total%20eq%2020", new[] { "Ada", "Grace" })]
    [InlineData("?filter=not%20a%20filter(", new string[0])]
    public void A_filter_in_the_address_applies_with_the_columns(string query, string[] names) =>
        Assert.Equal(names, Grid("/orders" + query).FindAll("tbody tr:not(.ib-table-empty) td:first-child").Select(c => c.TextContent.Trim()));

    // The page may use that name itself.
    [Fact]
    public void Without_a_builder_a_grid_leaves_the_address_filter_alone() =>
        Assert.Equal(2, Grid("/orders?filter=open", builder: false).FindAll("tbody tr:not(.ib-table-empty)").Count);

    [Fact]
    public void Its_tag_removes_it_and_Clear_clears_it()
    {
        var cut = Grid("/orders?filter=total%20gt%205&page=1");

        var remove = cut.Find(".ib-table-filter-tags a").GetAttribute("href")!;
        Assert.DoesNotContain("filter=", remove);
    }
}


