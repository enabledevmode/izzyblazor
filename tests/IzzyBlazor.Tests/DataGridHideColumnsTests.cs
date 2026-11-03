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
    public void An_open_rows_detail_lists_the_columns_that_can_hide_before_its_template()
    {
        var cut = Grid("/orders?detail=2", p => p.Add(x => x.DetailTemplate, o => b => b.AddContent(0, $"Lines of {o.Id}")));

        var detail = cut.Find("tr.ib-grid-detail");
        // In the columns' order; the grid's style shows only those hidden at its width.
        var entries = detail.QuerySelectorAll("dl.ib-grid-hidden > div")
            .Select(d => (d.QuerySelector("dt")!.TextContent, d.QuerySelector("dd")!.TextContent.Trim()))
            .ToList();
        Assert.Equal([("Status", "Open"), ("Customer", "Bo"), ("Total", "20")], entries);
        Assert.EndsWith("Lines of 2", detail.TextContent.Trim());
    }
}
