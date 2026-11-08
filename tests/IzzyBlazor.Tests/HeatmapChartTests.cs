using System.Globalization;

namespace IzzyBlazor.Tests;

public class HeatmapChartTests : BunitContext
{
    private record Orders(string Day, string Hour, int? Count);

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public HeatmapChartTests()
    {
        SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Static", isInteractive: false));
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
    }

    protected override void Dispose(bool disposing)
    {
        CultureInfo.CurrentCulture = _culture;
        base.Dispose(disposing);
    }

    private static HeatmapModel Build(double?[] values, bool diverging = false, int steps = 5) => HeatmapLayout.Build(new HeatmapInput
    {
        Columns = values.Select((_, i) => (object?)i).ToArray(),
        Rows = values.Select(_ => (object?)"r").ToArray(),
        Values = values,
        Steps = steps,
        Diverging = diverging,
    });

    [Fact]
    public void Values_fall_into_classes_between_round_bounds_and_a_diverging_scale_centers_on_its_midpoint()
    {
        // 6 to 55 in about five steps: six classes of 10 from 0, not five of 20 to 100.
        var model = Build([6, 12, 31, 55]);
        Assert.Equal(["0", "10", "20", "30", "40", "50", "60"], model.Bounds);
        Assert.Equal([0, 1, 3, 5], model.Cells.Select(c => c.Class));

        // −2.6 to 3.4 around 0: as far on both sides, so equal strengths mean equal distances.
        var diverging = Build([-2.6, 0.5, 3.4], diverging: true);
        Assert.Equal(["-4", "-2", "0", "2", "4"], diverging.Bounds);
        Assert.Equal([0, 2, 3], diverging.Cells.Select(c => c.Class));

        // Continuous: a place along the scale, and the ends for the legend.
        var blend = Build([0, 25, 100], steps: 0);
        Assert.True(blend.Continuous);
        Assert.Equal([0, 0.25, 1], blend.Cells.Select(c => c.T));
    }

    [Fact]
    public void Cells_go_in_first_seen_order_with_gaps_as_no_data_and_the_table_is_rows_by_columns()
    {
        Orders[] orders =
        [
            new("Mon", "9 AM", 12), new("Mon", "10 AM", 30),
            new("Tue", "10 AM", 41), new("Tue", "9 AM", null),
            new("Wed", "9 AM", 7),
        ];
        var cut = Render<HeatmapChart<Orders>>(p => p
            .Add(c => c.Items, orders)
            .Add(c => c.Column, o => o.Hour)
            .Add(c => c.Row, o => o.Day)
            .Add(c => c.Value, o => o.Count)
            .Add(c => c.Title, "Orders by hour"));

        var cells = cut.FindAll(".ib-heat-grid > div");
        // Mon, Tue, Wed by 9 AM, 10 AM; Tue 9 AM is null and Wed 10 AM missing: no data, no class.
        Assert.Equal(["12", "30", "", "41", "7", ""], cells.Select(c => c.TextContent));
        Assert.Null(cells[2].GetAttribute("class"));
        Assert.Null(cells[5].GetAttribute("class"));
        Assert.Equal(["Mon", "Tue", "Wed"], cut.FindAll(".ib-heat-rows > span").Select(s => s.TextContent));
        Assert.Equal("img", cut.Find(".ib-heat-frame").GetAttribute("role"));

        var rows = cut.FindAll(".ib-chart-table tbody tr");
        Assert.Equal(["Tue", "No data", "41"], rows[1].Children.Select(c => c.TextContent));
        Assert.Equal(["Day", "9 AM", "10 AM"], cut.FindAll(".ib-chart-table thead th").Select(c => c.TextContent));
    }

    [Fact]
    public void Ranges_name_their_classes_in_the_legend_and_a_value_in_none_is_drawn_as_no_data()
    {
        Orders[] orders = [new("Mon", "9 AM", 5), new("Mon", "10 AM", 20), new("Mon", "11 AM", 99)];
        var cut = Render<HeatmapChart<Orders>>(p => p
            .Add(c => c.Items, orders)
            .Add(c => c.Column, o => o.Hour)
            .Add(c => c.Row, o => o.Day)
            .Add(c => c.Value, o => o.Count)
            .Add(c => c.Title, "Queue")
            .AddChildContent<HeatmapRange>(r => r.Add(x => x.From, 0).Add(x => x.To, 10).Add(x => x.Color, Color.Of("#dbeafe")).Add(x => x.Label, "Quiet"))
            .AddChildContent<HeatmapRange>(r => r.Add(x => x.From, 10).Add(x => x.To, 20).Add(x => x.Color, Color.Of("#1e40af")).Add(x => x.Label, "Busy")));

        // 20 is the last range's To, so it's in; 99 is in none.
        var cells = cut.FindAll(".ib-heat-grid > div");
        Assert.StartsWith("ib-k1", cells[0].GetAttribute("class"));
        Assert.StartsWith("ib-k2", cells[1].GetAttribute("class"));
        Assert.Null(cells[2].GetAttribute("class"));
        Assert.Equal("99", cells[2].TextContent);

        var keys = cut.FindAll(".ib-heat-named li");
        Assert.Equal("Quiet0–100 to 10", keys[0].TextContent.Replace("\n", "").Replace(" ", "", StringComparison.Ordinal).Replace("to", " to ", StringComparison.Ordinal));
        Assert.Contains("--ib-heat-2:#1e40af", cut.Find("ib-chart").GetAttribute("style"));
    }

    private record Visit(DateOnly Day, string Hour, int Count);

    [Fact]
    public void Daily_columns_over_months_are_named_by_month()
    {
        var visits = Enumerable.Range(0, 120).Select(d => new Visit(new DateOnly(2025, 1, 1).AddDays(d), "9:00", d)).ToArray();
        var cut = Render<HeatmapChart<Visit>>(p => p
            .Add(c => c.Items, visits)
            .Add(c => c.Column, v => v.Day)
            .Add(c => c.Row, v => v.Hour)
            .Add(c => c.Value, v => v.Count)
            .Add(c => c.Title, "Visitors"));

        Assert.Equal(["Jan", "Feb", "Mar", "Apr"], cut.FindAll(".ib-chart-bottom span").Select(s => s.TextContent));
    }
}
