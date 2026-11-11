using System.Globalization;

namespace IzzyBlazor.Tests;

public class BulletChartTests : BunitContext
{
    private record Kpi(string Name, double Actual, double Goal, double Max, double[] Thresholds, string Format);

    private record Page(string Name, double Seconds);

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public BulletChartTests()
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
    public void A_scorecard_row_is_a_meter_on_its_own_scale_read_with_its_target_and_range()
    {
        Kpi[] kpis =
        [
            new("Revenue", 275, 250, 300, [150, 225], "$0K"),
            new("Margin", 0.18, 0.27, 0.30, [0.20, 0.25], "0%"),
        ];
        var cut = Render<BulletChart<Kpi>>(p => p
            .Add(c => c.Items, kpis)
            .Add(c => c.Title, "Year to date")
            .Add(c => c.Label, k => k.Name)
            .Add(c => c.Value, k => k.Actual)
            .Add(c => c.Target, k => k.Goal)
            .Add(c => c.Max, k => k.Max)
            .Add(c => c.Ranges, k => k.Thresholds)
            .Add(c => c.RangeLabels, ["Poor", "Fair", "Good"])
            .Add(c => c.Format, k => k.Format));

        var meters = cut.FindAll("[role=meter]");
        Assert.Equal("$275K, Goal $250K, Good", meters[0].GetAttribute("aria-valuetext"));
        Assert.Equal("18%, Goal 27%, Poor", meters[1].GetAttribute("aria-valuetext"));
        Assert.Equal("0.3", meters[1].GetAttribute("aria-valuemax"));
        Assert.Equal(cut.FindAll(".ib-bullet-label")[1].Id, meters[1].GetAttribute("aria-labelledby"));

        // Each row's own ticks, in its own format; three ranges end to end, the last to its max.
        Assert.Equal("0%", meters[1].QuerySelector(".ib-bullet-tick")!.TextContent);
        var ranges = meters[0].QuerySelectorAll(".ib-bullet-range");
        Assert.Equal(3, ranges.Length);
        Assert.Contains("--p:75%;--l:25%", ranges[2].GetAttribute("style"));
        Assert.Empty(cut.FindAll(".ib-bullet-axis"));
    }

    [Fact]
    public void Shared_bands_put_every_row_on_one_scale_with_one_axis()
    {
        Page[] pages = [new("Home", 1.8), new("Checkout", 4.6)];
        var cut = Render<BulletChart<Page>>(p => p
            .Add(c => c.Items, pages)
            .Add(c => c.Title, "LCP")
            .Add(c => c.Label, x => x.Name)
            .Add(c => c.Value, x => x.Seconds)
            .Add(c => c.ValueFormat, "0.0 s")
            .AddChildContent<GaugeBand>(b => b.Add(x => x.From, 0).Add(x => x.To, 2.5).Add(x => x.Label, "Good"))
            .AddChildContent<GaugeBand>(b => b.Add(x => x.From, 2.5).Add(x => x.To, 4).Add(x => x.Label, "Needs improvement"))
            .AddChildContent<GaugeBand>(b => b.Add(x => x.From, 4).Add(x => x.To, 6).Add(x => x.Label, "Poor")));

        var meters = cut.FindAll("[role=meter]");
        Assert.Equal("4.6 s, Poor", meters[1].GetAttribute("aria-valuetext"));
        Assert.All(meters, m => Assert.Equal("6", m.GetAttribute("aria-valuemax")));
        Assert.Single(cut.FindAll(".ib-bullet-axis"));
        Assert.Empty(cut.FindAll(".ib-bullet-meter .ib-bullet-ticks"));
        Assert.Contains("--l:30%", cut.FindAll(".ib-bullet-bar")[0].GetAttribute("style"));
    }
}
