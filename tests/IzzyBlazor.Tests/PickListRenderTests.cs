using Microsoft.AspNetCore.Components;

namespace IzzyBlazor.Tests;

public class PickListRenderTests : BunitContext
{
    public PickListRenderTests() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    [Fact]
    public void The_select_posts_the_chosen_first_in_their_order_and_the_lists_split_the_items()
    {
        var report = new Report();
        var cut = Render<PickList<List<string>, string>>(p => p
            .Add(f => f.Label, "Columns").Add(f => f.Items, ["Order", "Date", "Customer", "Total"])
            .Add(f => f.Value, report.Columns).Add(f => f.ValueExpression, () => report.Columns));

        var options = cut.FindAll("select[multiple] > option");
        Assert.Equal(["Total", "Order", "Date", "Customer"], options.Select(o => o.GetAttribute("value")));
        Assert.Equal(["Total", "Order"], options.Where(o => o.HasAttribute("selected")).Select(o => o.GetAttribute("value")));
        Assert.Equal(["Date", "Customer"], cut.FindAll("[data-ib-pane=available] [role=option]").Select(o => o.GetAttribute("data-value")));
        Assert.Equal(["Total", "Order"], cut.FindAll("[data-ib-pane=chosen] [role=option]").Select(o => o.GetAttribute("data-value")));
    }

    private sealed class Report
    {
        public List<string> Columns { get; set; } = ["Total", "Order"];
    }
}
