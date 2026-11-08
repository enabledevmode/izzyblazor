using System.Globalization;

namespace IzzyBlazor.Tests;

public class SankeyChartTests : BunitContext
{
    private record Flow(string From, string To, double Amount);

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public SankeyChartTests()
    {
        SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Static", isInteractive: false));
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
    }

    protected override void Dispose(bool disposing)
    {
        CultureInfo.CurrentCulture = _culture;
        base.Dispose(disposing);
    }

    [Fact]
    public void Nodes_come_from_the_flows_and_every_end_node_sits_in_the_last_column()
    {
        Flow[] flows =
        [
            new("Salary", "Budget", 3000), new("Freelance", "Budget", 1000),
            new("Budget", "Rent", 1500), new("Budget", "Savings", 2000),
            new("Savings", "Pension", 1200), new("Savings", "Rainy day", 800),
            // Flows between the same two nodes add up; a zero flow is left out.
            new("Budget", "Rent", 500), new("Budget", "Gifts", 0),
        ];

        var chart = Render<SankeyChart<Flow>>(p => p
            .Add(c => c.Items, flows)
            .Add(c => c.From, f => f.From)
            .Add(c => c.To, f => f.To)
            .Add(c => c.Value, f => f.Amount)
            .Add(c => c.Title, "Budget")
            .Add(c => c.Values, true)
            .AddChildContent<SankeyNode>(n => n.Add(x => x.Name, "Rainy day").Add(x => x.Label, "Emergency fund")));

        // Seven nodes (no Gifts), each labeled with its value; Rent ends in the last column with Pension.
        var labels = chart.FindAll(".ib-sankey-label").ToDictionary(l => l.ChildNodes[0].TextContent, l => l);
        Assert.Equal(7, chart.FindAll(".ib-sankey-node").Count);
        Assert.Equal("2,000", labels["Rent"].QuerySelector(".ib-sankey-value")!.TextContent);
        Assert.Contains("ib-sankey-label-end", labels["Rent"].ClassName);
        Assert.Contains("ib-sankey-label-end", labels["Pension"].ClassName);
        Assert.Contains("Emergency fund", labels.Keys);

        // A node is as tall as what flows through it: Budget (4,000) twice Savings (2,000).
        var model = SankeyLayout.Build(3, [new(0, 1, 4000), new(1, 2, 2000)], ["A", "B", "C"]);
        Assert.Equal(2 * (model.Nodes[2].Bottom - model.Nodes[2].Top), model.Nodes[1].Bottom - model.Nodes[1].Top, 6);

        // The table: a row per flow with its share of what leaves its node.
        var rows = chart.FindAll(".ib-chart-table tbody tr").Select(r => string.Join("|", r.Children.Select(c => c.TextContent))).ToList();
        Assert.Equal(6, rows.Count);
        Assert.Contains("Budget|Rent|2,000|50%", rows);
        Assert.Equal(["From", "To", "Amount", "Share of From"], chart.FindAll(".ib-chart-table thead th").Select(t => t.TextContent));
    }

    [Fact]
    public void A_flow_that_loops_back_throws_naming_it()
    {
        var error = Assert.Throws<InvalidOperationException>(() => SankeyLayout.Build(3, [new(0, 1, 5), new(1, 2, 4), new(2, 0, 1)], ["Trial", "Paid", "Lapsed"]));
        Assert.Contains("\"Lapsed\" to \"Trial\"", error.Message);
    }
}
