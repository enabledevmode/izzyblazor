using System.Linq.Expressions;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

// Table with @bind-Sort / @bind-Page on an interactive page: state in the component, not the URL.
public class TableStateRenderTests : BunitContext
{
    public sealed record Row(string Name, int Qty);

    private static readonly Row[] Rows = [new("b", 2), new("a", 3), new("c", 1)];

    public TableStateRenderTests() => SetRendererInfo(new RendererInfo("Server", isInteractive: true));

    private BunitNavigationManager Nav => Services.GetRequiredService<BunitNavigationManager>();

    private static RenderFragment Columns => builder =>
    {
        builder.OpenComponent<PropertyColumn<Row, string>>(0);
        builder.AddComponentParameter(1, "Property", (Expression<Func<Row, string>>)(r => r.Name));
        builder.AddComponentParameter(2, "Sortable", true);
        builder.CloseComponent();
        builder.OpenComponent<PropertyColumn<Row, int>>(3);
        builder.AddComponentParameter(4, "Property", (Expression<Func<Row, int>>)(r => r.Qty));
        builder.AddComponentParameter(5, "Sortable", true);
        builder.CloseComponent();
    };

    private static string[] Names(IRenderedComponent<Table<Row>> cut) =>
        cut.FindAll("tbody tr").Select(r => r.Children[0].TextContent).ToArray();

    private static AngleSharp.Dom.IElement PageButton(IRenderedComponent<Table<Row>> cut, int page) =>
        cut.FindAll(".ib-table-footer button").First(b => b.TextContent.Trim().EndsWith(page.ToString(), StringComparison.Ordinal));

    [Fact]
    public void Bound_sort_sorts_in_place_and_reports()
    {
        var reported = new List<string?>();
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.SortChanged, (string? s) => reported.Add(s))
            .Add(t => t.ChildContent, Columns));
        var urlBefore = Nav.Uri;

        Assert.Empty(cut.FindAll("thead a"));
        cut.FindAll("thead button.ib-table-sort")[0].Click();
        Assert.Equal(new[] { "a", "b", "c" }, Names(cut));
        Assert.Equal("ascending", cut.FindAll("thead th")[0].GetAttribute("aria-sort"));

        cut.FindAll("thead button.ib-table-sort")[0].Click();
        Assert.Equal(new[] { "c", "b", "a" }, Names(cut));

        Assert.Equal(new[] { "name", "-name" }, reported);
        Assert.Equal(urlBefore, Nav.Uri);
    }

    [Fact]
    public void Bound_page_pages_in_place_and_a_new_sort_goes_back_to_page_one()
    {
        var pages = new List<int>();
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.PageSize, 1)
            .Add(t => t.PageChanged, (int page) => pages.Add(page))
            .Add(t => t.ChildContent, Columns));

        Assert.Equal(new[] { "b" }, Names(cut));
        PageButton(cut, 2).Click();
        Assert.Equal(new[] { "a" }, Names(cut));
        Assert.Contains("2–2 of 3", cut.Find(".ib-table-summary").TextContent);

        cut.FindAll("thead button.ib-table-sort")[1].Click();   // Qty ascending: c, b, a
        Assert.Equal(new[] { "c" }, Names(cut));
        Assert.Equal(new[] { 2, 1 }, pages);
    }

    [Fact]
    public void Setting_Sort_and_Page_from_csharp_updates_the_table()
    {
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.PageSize, 2)
            .Add(t => t.Sort, "name")
            .Add(t => t.SortChanged, (string? _) => { })
            .Add(t => t.ChildContent, Columns));
        Assert.Equal(new[] { "a", "b" }, Names(cut));

        cut.Render(p => p.Add(t => t.Sort, "-qty").Add(t => t.Page, 2));

        Assert.Equal(new[] { "c" }, Names(cut));
    }

    [Fact]
    public void Bound_table_starts_from_the_url()
    {
        Nav.NavigateTo("/orders?sort=-name&page=2");
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.PageSize, 1)
            .Add(t => t.PageChanged, (int _) => { })
            .Add(t => t.ChildContent, Columns));

        Assert.Equal(new[] { "b" }, Names(cut));
    }

    [Fact]
    public void Unbound_table_on_an_interactive_page_still_uses_links()
    {
        var cut = Render<Table<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.PageSize, 1)
            .Add(t => t.ChildContent, Columns));

        Assert.Equal(2, cut.FindAll("thead a.ib-table-sort").Count);
        Assert.Empty(cut.FindAll(".ib-table-footer button"));
    }
}
