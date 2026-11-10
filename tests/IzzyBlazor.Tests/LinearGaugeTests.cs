using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace IzzyBlazor.Tests;

public class LinearGaugeTests : BunitContext
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public LinearGaugeTests()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
    }

    protected override void Dispose(bool disposing)
    {
        CultureInfo.CurrentCulture = _culture;
        base.Dispose(disposing);
    }

    [Fact]
    public void A_linear_gauge_is_a_meter_whose_bar_takes_its_band_color_over_ticks_at_its_step()
    {
        var palette = ChartPalette.Of(Color.Danger, Color.Warning);
        RenderFragment bands = b =>
        {
            b.OpenComponent<GaugeBand>(0);
            b.AddAttribute(1, nameof(GaugeBand.From), 0.0);
            b.AddAttribute(2, nameof(GaugeBand.To), 100.0);
            b.AddAttribute(3, nameof(GaugeBand.Label), "Critical");
            b.CloseComponent();
            b.OpenComponent<GaugeBand>(4);
            b.AddAttribute(5, nameof(GaugeBand.From), 100.0);
            b.AddAttribute(6, nameof(GaugeBand.To), 250.0);
            b.AddAttribute(7, nameof(GaugeBand.Label), "Low");
            b.CloseComponent();
        };
        var cut = Render<LinearGauge>(p => p
            .Add(c => c.Label, "Day tank")
            .Add(c => c.Value, 210)
            .Add(c => c.Max, 1000)
            .Add(c => c.ValueFormat, "#,##0 L")
            .Add(c => c.Target, 800)
            .Add(c => c.Step, 250)
            .Add(c => c.MinorStep, 125)
            .Add(c => c.Palette, palette)
            .Add(c => c.ChildContent, bands));

        var meter = cut.Find("[role=meter]");
        Assert.Equal(cut.Find(".ib-lgauge-label").Id, meter.GetAttribute("aria-labelledby"));
        Assert.Equal("210", meter.GetAttribute("aria-valuenow"));
        Assert.Equal("210 L, Low, target 800 L", meter.GetAttribute("aria-valuetext"));
        Assert.Equal("Low", cut.Find(".ib-lgauge-state").TextContent);

        // The bar runs from the start to the value, in the band's color.
        Assert.Equal($"--p:0%;--l:21%;background:{palette.Color(1, 2)}", cut.Find(".ib-lgauge-bar").GetAttribute("style"));

        // Numbered ticks at each step, and minor ticks only between them.
        Assert.Equal(["0 L", "250 L", "500 L", "750 L", "1,000 L"], cut.FindAll(".ib-lgauge-tick > span").Select(t => t.TextContent));
        Assert.Equal(["--p:12.5%", "--p:37.5%", "--p:62.5%", "--p:87.5%"], cut.FindAll(".ib-lgauge-minor").Select(t => t.GetAttribute("style")));
    }

    [Fact]
    public void Each_pointer_is_a_meter_of_its_own_after_the_gauges()
    {
        RenderFragment Pointer(double value, string label) => b =>
        {
            b.OpenComponent<GaugePointer>(0);
            b.AddAttribute(1, nameof(GaugePointer.Value), value);
            b.AddAttribute(2, nameof(GaugePointer.Label), label);
            b.CloseComponent();
        };

        var linear = Render<LinearGauge>(p => p
            .Add(c => c.Label, "Temperature").Add(c => c.Value, 24).Add(c => c.Min, -10).Add(c => c.Max, 40)
            .Add(c => c.ValueFormat, "0°C").Add(c => c.Orientation, Orientation.Vertical)
            .Add(c => c.ChildContent, Pointer(12, "Dew point")));
        var meters = linear.FindAll("[role=meter]");
        Assert.Equal(2, meters.Count);
        Assert.Equal("Dew point", meters[1].GetAttribute("aria-label"));
        Assert.Equal("12°C", meters[1].GetAttribute("aria-valuetext"));
        Assert.Equal("Dew point12°C", meters[1].TextContent);
        Assert.StartsWith("--p:44%;", linear.Find(".ib-lgauge-pin").GetAttribute("style"));

        // On a dial: a notch, the readout under the label, and the sheet that styles it.
        var dial = Render<Gauge>(p => p
            .Add(c => c.Label, "Wind").Add(c => c.Value, 27).Add(c => c.Max, 100).Add(c => c.ValueFormat, "0 km/h")
            .Add(c => c.ChildContent, Pointer(44, "Gusts")));
        Assert.Equal("44 km/h", dial.FindAll("[role=meter]")[1].GetAttribute("aria-valuetext"));
        Assert.Contains("gauge", dial.Find("link[href*=gauge]").GetAttribute("href"));
    }

    [Fact]
    public void A_needle_points_into_the_bands_with_the_ticks_drawn_over_them()
    {
        var cut = Render<LinearGauge>(p => p
            .Add(c => c.Label, "Carbohydrate").Add(c => c.Value, 0.35).Add(c => c.ValueFormat, "P0").Add(c => c.Step, 0.25)
            .Add(c => c.Pointer, LinearGaugePointer.Needle).Add(c => c.TickPlacement, TickPlacement.Inside)
            .Add(c => c.ScaleSide, ScaleSide.Start)
            .AddChildContent<GaugeBand>(b => b.Add(x => x.From, 0.45).Add(x => x.To, 0.65).Add(x => x.Label, "In range")));

        Assert.Empty(cut.FindAll(".ib-lgauge-bar"));
        Assert.Equal("--p:35%", cut.Find(".ib-lgauge-needle").GetAttribute("style"));
        // The lines are in the track; the row beside it holds only the numbers, here above.
        Assert.Equal(5, cut.FindAll(".ib-lgauge-track .ib-lgauge-line").Count);
        Assert.Equal(["0%", "25%", "50%", "75%", "100%"], cut.FindAll(".ib-lgauge-tick > span").Select(t => t.TextContent));
        Assert.Contains("ib-lgauge-flip", cut.Find(".ib-lgauge").ClassName);
    }
}
