using System.Linq.Expressions;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

// A row's detail: under it, across the grid, opened by the row's own button.
public class DataGridDetailTests : BunitContext
{
    public sealed record Order(int Id, string Status, string Customer);

    private static readonly Order[] Orders = [new(1, "Paid", "Ada"), new(2, "Paid", "Bo"), new(3, "Open", "Cy")];

    public DataGridDetailTests() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    private BunitNavigationManager Nav => Services.GetRequiredService<BunitNavigationManager>();

    private static RenderFragment Columns => builder =>
    {
        builder.OpenComponent<PropertyColumn<Order, int>>(0);
        builder.AddComponentParameter(1, "Property", (Expression<Func<Order, int>>)(o => o.Id));
        builder.CloseComponent();
        builder.OpenComponent<PropertyColumn<Order, string>>(2);
        builder.AddComponentParameter(3, "Property", (Expression<Func<Order, string>>)(o => o.Status));
        builder.CloseComponent();
        builder.OpenComponent<PropertyColumn<Order, string>>(4);
        builder.AddComponentParameter(5, "Property", (Expression<Func<Order, string>>)(o => o.Customer));
        builder.CloseComponent();
    };

    private IRenderedComponent<DataGrid<Order>> Grid(string address, Action<ComponentParameterCollectionBuilder<DataGrid<Order>>>? more = null)
    {
        Nav.NavigateTo(address);
        return Render<DataGrid<Order>>(p =>
        {
            p.Add(x => x.Items, Orders);
            p.Add(x => x.ItemKey, o => o.Id.ToString());
            p.Add(x => x.RowLabel, o => $"Order {o.Id}");
            p.Add(x => x.ChildContent, Columns);
            p.Add(x => x.DetailTemplate, o => b => b.AddContent(0, $"Lines of {o.Id}"));
            more?.Invoke(p);
        });
    }

    // How many columns a row covers: every row adds up to the header's.
    private static int Span(AngleSharp.Dom.IElement row) => row.QuerySelectorAll(":scope > td, :scope > th").Sum(c => int.TryParse(c.GetAttribute("colspan"), out var n) ? n : 1);

    [Fact]
    public void On_a_static_page_a_link_opens_a_row_and_its_detail_spans_the_grid_under_it()
    {
        var cut = Grid("/orders?detail=2", p => p.Add(x => x.SelectionMode, SelectionMode.Multiple));

        var rows = cut.FindAll("tbody > tr");
        Assert.Equal(4, rows.Count);
        var detail = rows[2];
        Assert.Contains("ib-grid-detail", detail.ClassList);
        Assert.Equal("Lines of 2", detail.TextContent.Trim());
        Assert.Equal(Span(cut.Find("thead > tr")), Span(detail));
        Assert.Equal(Span(cut.Find("thead > tr")), Span(rows[0]));

        var open = rows[1].QuerySelector("a.ib-grid-expand")!;
        Assert.Equal("true", open.GetAttribute("aria-expanded"));
        Assert.Equal(detail.Id, open.GetAttribute("aria-controls"));
        Assert.Equal("Hide details for Order 2", open.GetAttribute("aria-label"));
        Assert.EndsWith("/orders", open.GetAttribute("href"));

        // Another one opens beside it, and the header opens them all.
        Assert.Contains("detail=2&detail=1", rows[0].QuerySelector("a.ib-grid-expand")!.GetAttribute("href"));
        Assert.Contains("detail=%2A", cut.Find("thead a.ib-grid-expand").GetAttribute("href"));
    }

    [Fact]
    public void Inside_a_group_the_detail_follows_its_row_and_the_button_sits_past_the_groups_lead()
    {
        var cut = Grid("/orders?group=status&open=Paid&detail=1", p => p.Add(x => x.Groupable, true));

        var rows = cut.FindAll("tbody > tr").ToList();
        var detail = rows.Single(r => r.ClassList.Contains("ib-grid-detail"));
        var row = rows[rows.IndexOf(detail) - 1];
        Assert.Equal("1", row.GetAttribute("data-ib-key"));
        Assert.Equal(row.GetAttribute("aria-level"), detail.GetAttribute("aria-level"));
        Assert.All(rows, r => Assert.Equal(Span(cut.Find("thead > tr")), Span(r)));
        var lead = row.QuerySelectorAll("td.ib-grid-lead");
        Assert.Equal(2, lead.Length);
        Assert.NotNull(lead[1].QuerySelector(".ib-grid-expand"));
    }

    [Fact]
    public void Bound_on_an_interactive_page_one_detail_at_a_time()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        IReadOnlyList<string>? open = ["1"];
        var cut = Grid("/orders", p => p
            .Add(x => x.SingleDetail, true)
            .Add(x => x.ExpandedDetails, open)
            .Add(x => x.ExpandedDetailsChanged, v => open = v)
            .Add(x => x.ItemHasDetail, o => o.Status == "Paid"));

        Assert.Empty(cut.FindAll("thead .ib-grid-expand"));
        Assert.Null(cut.FindAll("tbody > tr")[^1].QuerySelector(".ib-grid-expand"));

        cut.FindAll("tbody button.ib-grid-expand")[1].Click();

        Assert.Equal(["2"], open);
        Assert.Equal("/orders", new Uri(Nav.Uri).AbsolutePath + new Uri(Nav.Uri).Query);
    }

    [Fact]
    public void Without_an_ItemKey_it_says_what_it_needs()
    {
        var error = Assert.ThrowsAny<Exception>(() => Render<DataGrid<Order>>(p => p
            .Add(x => x.Items, Orders)
            .Add(x => x.ChildContent, Columns)
            .Add(x => x.DetailTemplate, o => b => { })));

        Assert.Contains("ItemKey", error.GetBaseException().Message);
    }
}
