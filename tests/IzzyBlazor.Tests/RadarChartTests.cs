using System.Globalization;

namespace IzzyBlazor.Tests;

public class RadarChartTests : BunitContext
{
    private record Rating(string Candidate, string Skill, double? Score);

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public RadarChartTests()
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
    public void SeriesBy_splits_rows_into_an_outline_per_key_over_spokes_in_first_seen_order()
    {
        Rating[] ratings =
        [
            new("Ana", "Speed", 4), new("Ana", "Care", 3), new("Ana", "Ideas", 5),
            new("Ben", "Speed", 2), new("Ben", "Care", 5), new("Ben", "Ideas", 4),
        ];

        var chart = Render<RadarChart<Rating>>(p => p
            .Add(c => c.Items, ratings)
            .Add(c => c.Argument, r => r.Skill)
            .Add(c => c.Title, "Ratings")
            .AddChildContent<RadarSeries<Rating>>(s => s.Add(x => x.Value, r => r.Score).Add(x => x.SeriesBy, r => r.Candidate)));

        Assert.Equal(["Speed", "Care", "Ideas"], chart.FindAll(".ib-radar-names span").Select(s => s.TextContent));
        Assert.Equal(2, chart.FindAll("path.ib-radar-line").Count);
        Assert.Equal(["Ana", "Ben"], chart.FindAll(".ib-chart-toggle").Select(t => t.TextContent.Trim()));

        // The table is spokes by series.
        Assert.Equal(["Skill", "Ana", "Ben"], chart.FindAll(".ib-chart-table thead th").Select(t => t.TextContent));
        Assert.Equal(["Care", "3", "5"], chart.FindAll(".ib-chart-table tbody tr")[1].Children.Select(c => c.TextContent));
    }

    [Fact]
    public void A_missing_value_leaves_a_gap_not_a_point_at_the_center_and_the_scale_ends_on_a_round_number()
    {
        var model = RadarLayout.Build(new RadarInput(["A", "B", "C", "D"], [new RadarSeriesData("S", [8.5, null, 6, 7]) { Fill = true }]));

        Assert.Equal(10, model.Max);
        var shape = model.Series[0];
        // Three markers, and only the C–D and D–A segments: none to B, nor a closed shape to fill.
        Assert.Equal([0, 2, 3], shape.Marks.Select(m => m.Spoke));
        Assert.Equal(2, shape.Outline.Count(c => c == 'M'));
        Assert.DoesNotContain('Z', shape.Outline);
        Assert.Null(shape.Area);
    }
}
