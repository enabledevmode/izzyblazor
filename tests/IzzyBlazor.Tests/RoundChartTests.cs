using System.Globalization;

namespace IzzyBlazor.Tests;

public class RoundChartTests : BunitContext
{
    private record Plan(string Name, int Customers);

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public RoundChartTests()
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
    public void Pie_slices_share_the_turn_and_skip_missing_or_negative_values()
    {
        var slices = ChartShapes.Pie([1, null, 3, -2, 0], hole: 0);

        Assert.Equal([0, 2], slices.Select(s => s.Index));
        Assert.Equal(0.25, slices[0].End, 6);
        Assert.Equal(1, slices[1].End, 6);
        // The bigger slice takes the long way round.
        Assert.StartsWith("M0,-1A1,1 0 0,1 1,0", slices[0].Path);
        Assert.Contains("A1,1 0 1,1", slices[1].Path);

        // One slice is a whole ring, its hole drawn the other way.
        var ring = Assert.Single(ChartShapes.Pie([5], hole: 0.5));
        Assert.Equal("M0,-1A1,1 0 1,1 0,1A1,1 0 1,1 0,-1ZM0,-0.5A0.5,0.5 0 1,0 0,0.5A0.5,0.5 0 1,0 0,-0.5Z", ring.Path);
    }

    [Fact]
    public void Funnel_stages_slope_from_their_value_to_the_next_and_a_pyramid_stands_on_its_base()
    {
        // Equal heights; each top as wide as its value against the largest, down to the next's.
        var funnel = ChartShapes.Funnel([1000, 500, null, 250], pyramid: false);
        Assert.Equal([0, 1, 3], funnel.Select(s => s.Index));
        Assert.Equal("0,0 100,0 75,25 25,25", funnel[0].Points);
        Assert.Equal("25,25 75,25 50,50 50,50", funnel[1].Points);
        Assert.Equal("37.5,75 62.5,75 62.5,100 37.5,100", funnel[2].Points);

        // The first item is the base; each level as tall as its share of the total.
        var pyramid = ChartShapes.Funnel([75, 25], pyramid: true);
        Assert.Equal("37.5,25 62.5,25 100,100 0,100", pyramid[0].Points);
        Assert.Equal("50,0 50,0 62.5,25 37.5,25", pyramid[1].Points);
    }

    [Fact]
    public void A_funnel_labels_each_stage_with_its_share_of_the_first_and_of_the_one_before()
    {
        Plan[] stages = [new("Visited", 1000), new("Signed up", 400), new("Paid", 100)];
        var cut = Render<FunnelChart<Plan>>(p => p
            .Add(c => c.Items, stages)
            .Add(c => c.Argument, x => x.Name)
            .Add(c => c.Value, x => x.Customers)
            .Add(c => c.Title, "Sign-ups"));

        Assert.Equal(3, cut.FindAll("polygon.ib-funnel-stage").Count);
        Assert.Equal(cut.Find("figcaption").Id, cut.Find("ol.ib-funnel-labels").GetAttribute("aria-labelledby"));
        Assert.Equal(["10%", ", 10% of the first", ", 25% of the stage before"],
            cut.FindAll(".ib-funnel-labels > li")[2].QuerySelectorAll(".ib-funnel-share, .ib-visually-hidden").Select(s => s.TextContent));
        Assert.Equal(["Name", "Customers", "Of the first", "Of the stage before"], cut.FindAll(".ib-chart-table thead th").Select(th => th.TextContent));
    }

    [Fact]
    public void A_pie_chart_lists_each_slice_with_its_value_and_share()
    {
        Plan[] plans = [new("Free", 600), new("Pro", 380), new("Team", 20), new("Legacy", 0)];
        var cut = Render<PieChart<Plan>>(p => p
            .Add(c => c.Items, plans)
            .Add(c => c.Argument, x => x.Name)
            .Add(c => c.Value, x => x.Customers)
            .Add(c => c.Title, "Customers by plan")
            .Add(c => c.Donut, true)
            .Add(c => c.Center, b => b.AddContent(0, "1,000 customers")));

        var image = cut.Find(".ib-pie-frame[role=img]");
        Assert.Equal(cut.Find("figcaption").Id, image.GetAttribute("aria-labelledby"));
        Assert.Equal(3, cut.FindAll("path.ib-pie-slice").Count);
        Assert.Equal("1,000 customers", cut.Find(".ib-pie-center").TextContent);

        // A sliver keeps a tenth, and a zero keeps its row with no share.
        Assert.Equal(["Free", "600", "60%", "Team", "20", "2.0%", "Legacy", "0", ""],
            cut.FindAll(".ib-pie-legend li").Where((_, i) => i != 1).SelectMany(li => li.Children.Skip(1).Select(c => c.TextContent)));
        Assert.Equal(["Name", "Customers", "Share"], cut.FindAll(".ib-chart-table thead th").Select(th => th.TextContent));
        Assert.Equal("0 0.6,0.6 0.98,0.98 1,", cut.Find("ib-chart").GetAttribute("data-ib-pos"));
    }

    [Fact]
    public void A_sparkline_is_an_image_that_says_what_it_shows()
    {
        var cut = Render<Sparkline>(p => p
            .Add(c => c.Values, [410, 390, null, 520, 482])
            .Add(c => c.Label, "Signups, last 5 weeks"));

        var image = cut.Find(".ib-sparkline[role=img]");
        Assert.Equal("Signups, last 5 weeks: from 410 to 482, low 390, high 520", image.GetAttribute("aria-label"));
        // The missing week breaks the line in two.
        Assert.Equal(2, cut.Find("path.ib-sparkline-line").GetAttribute("d")!.Count(c => c == 'M'));
    }

    [Fact]
    public void A_win_loss_sparkline_counts_ups_and_downs()
    {
        var cut = Render<Sparkline>(p => p
            .Add(c => c.Values, [3, -1, 0, 2, null, -4])
            .Add(c => c.Label, "Games this season")
            .Add(c => c.Type, SparklineType.WinLoss));

        Assert.Equal("Games this season: 2 up, 2 down, 1 even", cut.Find("[role=img]").GetAttribute("aria-label"));
        Assert.Equal(2, cut.FindAll("rect.ib-sparkline-down").Count);
    }

    [Fact]
    public void A_stat_draws_its_spark_named_by_its_label()
    {
        var cut = Render<Stat>(p => p
            .Add(c => c.Label, "Revenue")
            .Add(c => c.Value, "$48,210")
            .Add(c => c.Sparkline, [41_000, 44_500, 48_210])
            .Add(c => c.SparklineFormat, "C0"));

        Assert.Equal("Revenue: from $41,000 to $48,210, low $41,000, high $48,210",
            cut.Find("dd.ib-stat-spark [role=img]").GetAttribute("aria-label"));
        Assert.Throws<InvalidOperationException>(() => Render<Stat>(p => p.Add(c => c.Sparkline, [1, 2])));
    }

    [Fact]
    public void Pie_labels_sit_in_columns_either_side_and_never_overlap()
    {
        // Two slices on the right (their middles before half a turn), three small ones bunched at
        // the left's top, which the spread pulls a line apart.
        var slices = ChartShapes.Pie([30, 30, 20, 10, 10], hole: 0);
        var labels = ChartShapes.PieLabels(slices, sizePx: 256, linePx: 20);

        Assert.Equal([0, 1, 2, 3, 4], labels.Select(l => l.Index));
        Assert.Equal([true, true, false, false, false], labels.Select(l => l.End));
        foreach (var side in labels.GroupBy(l => l.End))
        {
            var px = side.Select(l => l.Pos * 256).Order().ToArray();
            for (var i = 1; i < px.Length; i++) Assert.True(px[i] - px[i - 1] >= 20 - 1e-6, $"{px[i - 1]} and {px[i]}");
        }

        // Each leader runs from the slice's edge out to its column, level with the label.
        Assert.EndsWith($"L1.07,{(labels[0].Pos * 2 - 1).ToString("0.####", CultureInfo.InvariantCulture)}", labels[0].Leader);
    }

        [Fact]
    public void A_gauge_is_a_meter_with_its_value_in_words()
    {
        var cut = Render<Gauge>(p => p
            .Add(c => c.Label, "Disk used")
            .Add(c => c.Value, 1.2)
            .Add(c => c.ValueFormat, "P0"));

        var meter = cut.Find("[role=meter]");
        Assert.Equal(cut.Find(".ib-gauge-label").Id, meter.GetAttribute("aria-labelledby"));
        // Past the end, the dial is full and the value is still told as it is.
        Assert.Equal("1", meter.GetAttribute("aria-valuenow"));
        Assert.Equal("120%", meter.GetAttribute("aria-valuetext"));
        Assert.Equal(ChartShapes.GaugeArc(0, 1, 0.88), cut.Find("path.ib-gauge-fill").GetAttribute("d"));

        // In a band: the band's label is shown and read with the value, and the target after it.
        var banded = Render<Gauge>(p => p
            .Add(c => c.Label, "Memory")
            .Add(c => c.Value, 0.78)
            .Add(c => c.ValueFormat, "P0")
            .Add(c => c.Target, 0.8)
            .AddChildContent<GaugeBand>(b => b.Add(x => x.From, 0.7).Add(x => x.To, 0.9).Add(x => x.Accent, Accent.Danger).Add(x => x.Label, "High")));
        Assert.Equal("78%, High, target 80%", banded.Find("[role=meter]").GetAttribute("aria-valuetext"));
        Assert.Equal("High", banded.Find(".ib-gauge-state").TextContent);
    }
}
