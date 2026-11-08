using System.Globalization;

namespace IzzyBlazor.Tests;

public class TreemapChartTests : BunitContext
{
    private record Cost(string Category, string Service, double Amount, double Change);

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public TreemapChartTests()
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
    public void Tiles_fill_the_box_by_their_shares_largest_first_and_near_square()
    {
        double[] values = [6, 6, 4, 3, 2, 2, 1];
        foreach (var (w, h) in new[] { (720.0, 360.0), (360.0, 360.0) })
        {
            var boxes = TreemapLayout.Squarify(values, w, h);

            // Each tile's area is its share of the box, and together they fill it.
            for (var i = 0; i < values.Length; i++)
            {
                Assert.Equal(values[i] / values.Sum() * w * h, boxes[i].W * boxes[i].H, 6);
            }

            Assert.All(boxes, b => Assert.True(b.X >= -1e-9 && b.Y >= -1e-9 && b.X + b.W <= w + 1e-9 && b.Y + b.H <= h + 1e-9));
            Assert.Equal(0, boxes[0].X, 6);
            Assert.Equal(0, boxes[0].Y, 6);

            // Squarified: no tile more than five times as long as it's wide (slices here reach 12).
            Assert.All(boxes, b => Assert.True(Math.Max(b.W / b.H, b.H / b.W) < 5, $"{b} in {w}x{h}"));
        }
    }

    [Fact]
    public void Groups_take_a_color_each_and_the_table_lists_every_tile_with_its_shares()
    {
        Cost[] costs =
        [
            new("Compute", "App servers", 600, 0.1), new("Compute", "Jobs", 200, -0.2),
            new("Storage", "Backups", 300, 0.05), new("Storage", "Objects", 100, 0.3),
            // Zero and negative values are left out.
            new("Storage", "Archive", 0, 0), new("Network", "CDN", -5, 0),
        ];

        var chart = Render<TreemapChart<Cost>>(p => p
            .Add(c => c.Items, costs)
            .Add(c => c.Name, x => x.Service)
            .Add(c => c.Value, x => x.Amount)
            .Add(c => c.Group, x => x.Category)
            .Add(c => c.Title, "Costs"));

        // Two groups, largest first, each under its name; every tile in its group's color.
        Assert.Equal(["Compute", "Storage"], chart.FindAll(".ib-treemap-head .ib-treemap-name").Select(h => h.TextContent));
        var tiles = chart.FindAll(".ib-treemap-tile");
        Assert.Equal(4, tiles.Count);
        Assert.Equal(["ib-chart-s1", "ib-chart-s1", "ib-chart-s2", "ib-chart-s2"], tiles.Select(t => t.ClassList.First(c => c.StartsWith("ib-chart-s"))));
        Assert.Equal("4 tiles in 2 groups, the largest App servers, 600", chart.Find("[id$='-desc']").TextContent);

        var rows = chart.FindAll(".ib-chart-table tbody tr").Select(r => string.Join("|", r.Children.Select(c => c.TextContent))).ToList();
        Assert.Equal(["Compute|App servers|600|75%|50%", "Compute|Jobs|200|25%|17%", "Storage|Backups|300|75%|25%", "Storage|Objects|100|25%|8.3%"], rows);

        // A second value colors the tiles on a scale instead, with its legend and a column in the table.
        var byChange = Render<TreemapChart<Cost>>(p => p
            .Add(c => c.Items, costs)
            .Add(c => c.Name, x => x.Service)
            .Add(c => c.Value, x => x.Amount)
            .Add(c => c.ColorValue, x => x.Change)
            .Add(c => c.Palette, ChartPalette.Diverging(Color.Danger, Color.Primary))
            .Add(c => c.Title, "Costs"));
        Assert.All(byChange.FindAll(".ib-treemap-tile"), t => Assert.Contains(t.ClassList, c => c.StartsWith("ib-k")));
        Assert.NotNull(byChange.Find(".ib-heat-legend"));
        Assert.Equal("Change", byChange.FindAll(".ib-chart-table thead th").Last().TextContent);
    }
}
