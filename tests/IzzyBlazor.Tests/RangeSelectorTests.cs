using System.Globalization;

namespace IzzyBlazor.Tests;

public class RangeSelectorTests : BunitContext
{
    private record Visit(DateOnly Day, double Visits);

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    private static readonly Visit[] Days = [.. Enumerable.Range(0, 365).Select(i => new Visit(new DateOnly(2026, 1, 1).AddDays(i), 100 + i))];

    public RangeSelectorTests()
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
    public void On_its_own_it_draws_the_strip_with_a_slider_at_each_end_of_the_range_and_range_buttons()
    {
        var selector = Render<RangeSelector<Visit>>(p => p
            .Add(c => c.Items, Days)
            .Add(c => c.Argument, d => d.Day)
            .Add(c => c.View, new ChartView(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31)))
            .Add(c => c.Label, "Days to count")
            .Add(c => c.Ranges, true)
            .AddChildContent<ChartAreaSeries<Visit>>(s => s.Add(x => x.Value, d => d.Visits)));

        // The strip only: no plot, a named group, its series drawn small.
        Assert.Empty(selector.FindAll(".ib-chart-frame"));
        Assert.Equal("Days to count", selector.Find(".ib-chart-sel").GetAttribute("aria-label"));
        Assert.Single(selector.FindAll(".ib-chart-sel-area"));

        // A slider at each end, saying the range's first and last days.
        var handles = selector.FindAll("[role=slider]");
        Assert.Equal(["3/1/2026", "3/31/2026"], handles.Select(h => h.GetAttribute("aria-valuetext")));

        // Spans back from the last date (YTD and 1Y would show everything here, so they are left out), and All.
        var buttons = selector.FindAll("[data-ib-range]");
        Assert.Equal(["1M", "3M", "6M", "All"], buttons.Select(b => b.TextContent));
        var three = buttons[1].GetAttribute("data-ib-range")!.Split(' ').Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();
        Assert.Equal(1, three[1], 6);
        Assert.InRange(three[0], 0.74, 0.76);
    }

    [Fact]
    public void On_names_the_handles_name_the_first_and_last_in_view()
    {
        string[] months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
        var selector = Render<RangeSelector<string>>(p => p
            .Add(c => c.Items, months)
            .Add(c => c.Argument, m => m)
            .Add(c => c.View, new ChartView("Feb", "May")));
        Assert.Equal(["Feb", "May"], selector.FindAll("[role=slider]").Select(h => h.GetAttribute("aria-valuetext")));
    }
}
