using System.Linq.Expressions;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

// Columns that hide when the grid is narrow, by container queries the server works out, and show
// their values in the row's detail.
public class DataGridHideColumnsTests : BunitContext
{
    public sealed record Order(int Id, string Status, string Customer, decimal Total);

    private static readonly Order[] Orders = [new(1, "Paid", "Ada", 10), new(2, "Open", "Bo", 20)];

    public DataGridHideColumnsTests() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    private static void Column<TValue>(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder, Expression<Func<Order, TValue>> property, double width, int priority = 0)
    {
        builder.OpenComponent<PropertyColumn<Order, TValue>>(0);
        builder.AddComponentParameter(1, "Property", property);
        builder.AddComponentParameter(2, "Width", Length.Rem(width));
        builder.AddComponentParameter(3, "Priority", priority);
        builder.CloseComponent();
    }

    // Id 6rem, Status 8rem, Customer 10rem (stays longest), Total 8rem: all 32rem.
    private static RenderFragment Columns => builder =>
    {
        Column(builder, o => o.Id, 6);
        Column(builder, o => o.Status, 8);
        Column(builder, o => o.Customer, 10, priority: 1);
        Column(builder, o => o.Total, 8);
    };

    private IRenderedComponent<DataGrid<Order>> Grid(string address, Action<ComponentParameterCollectionBuilder<DataGrid<Order>>>? more = null)
    {
        Services.GetRequiredService<BunitNavigationManager>().NavigateTo(address);
        return Render<DataGrid<Order>>(p =>
        {
            p.Add(x => x.Items, Orders);
            p.Add(x => x.ItemKey, o => o.Id.ToString());
            p.Add(x => x.RowLabel, o => $"Order {o.Id}");
            p.Add(x => x.SmallScreen, DataGridSmallScreen.HideColumns);
            p.Add(x => x.ChildContent, Columns);
            more?.Invoke(p);
        });
    }

    [Fact]
    public void Columns_hide_lowest_priority_and_last_first_at_the_widths_they_need()
    {
        var cut = Grid("/orders");

        // Total (last of the 0s), then Status, then Customer; the first column never hides.
        var header = cut.FindAll("thead > tr > th").Select(th => (th.TextContent.Trim(), th.ClassName)).ToList();
        Assert.Contains(("Total", "ib-cell-end ib-h1"), header);
        Assert.Contains(("Status", "ib-h2"), header);
        Assert.Contains(("Customer", "ib-h3"), header);
        Assert.Contains(("Id", "ib-cell-end"), header);
        Assert.Equal(["ib-h2", "ib-h3", "ib-h1"], cut.FindAll("col[class^=ib-h]").Select(c => c.ClassName));

        // Total goes below the 32rem everything needs; the rest one by one, each width with the
        // button column (2.25rem) that comes with the first.
        var css = cut.Find("style").TextContent;
        var id = cut.Find("table").Id;
        Assert.Contains($"@container (width < 32rem){{#{id}>colgroup>.ib-h1{{width:0!important}}", css);
        Assert.Contains("@container (width < 26.25rem){", css);
        Assert.Contains("@container (width < 18.25rem){", css);
        Assert.Contains("@container (width >= 32rem){", css);
        Assert.Equal("container-type:inline-size", cut.Find(".ib-table-scroll").GetAttribute("style"));
    }

    [Fact]
    public void An_open_rows_detail_lists_the_columns_that_can_hide_before_its_template()
    {
        var cut = Grid("/orders?detail=2", p => p.Add(x => x.DetailTemplate, o => b => b.AddContent(0, $"Lines of {o.Id}")));

        var detail = cut.Find("tr.ib-grid-detail");
        var entries = detail.QuerySelectorAll("dl.ib-grid-hidden > div")
            .Select(d => (d.ClassName, d.QuerySelector("dt")!.TextContent, d.QuerySelector("dd")!.TextContent.Trim()))
            .ToList();
        Assert.Equal([("ib-h2", "Status", "Open"), ("ib-h3", "Customer", "Bo"), ("ib-h1", "Total", "20")], entries);
        Assert.EndsWith("Lines of 2", detail.TextContent.Trim());

        // With a template, the button column stays; the row's button opens it whatever's hidden.
        Assert.DoesNotContain("ib-hx", cut.Find("tbody a.ib-grid-expand").ClassName);
    }

    [Fact]
    public void Without_a_template_the_button_column_and_its_details_go_while_nothing_is_hidden()
    {
        var cut = Grid("/orders?detail=2");

        Assert.Contains("ib-hx", cut.Find("tr.ib-grid-detail").ClassName);
        Assert.Contains("ib-hx", cut.Find("tbody a.ib-grid-expand").ClassName);
        var id = cut.Find("table").Id;
        Assert.Contains($"@container (width >= 32rem){{#{id}{{--ib-grid-expander:0px}}", cut.Find("style").TextContent);
    }
}
