using System.Globalization;

namespace IzzyBlazor.Tests;

public class ChartScaleTests
{
    private static double[] Ticks(double start, double stop, double count)
    {
        var spec = ChartScale.TickSpec(start, stop, count);
        var ticks = new List<double>();
        for (var i = spec.I1; i <= spec.I2; i++) ticks.Add(spec.Value(i));
        return [.. ticks];
    }

    [Fact]
    public void Ticks_step_by_one_two_or_five_without_float_drift()
    {
        // D3's own test cases.
        Assert.Equal([0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1], Ticks(0, 1, 10));
        Assert.Equal([0, 0.2, 0.4, 0.6, 0.8, 1], Ticks(0, 1, 5));
        Assert.Equal([0, 0.5, 1], Ticks(0, 1, 2));
        Assert.Equal([-0.5, -0.4, -0.3, -0.2, -0.1, 0, 0.1, 0.2, 0.3, 0.4, 0.5], Ticks(-0.5, 0.5, 10));
        Assert.Equal([0, 20, 40, 60, 80], Ticks(0, 87, 5));
    }

    [Fact]
    public void Nice_rounds_the_ends_and_widens_a_single_value()
    {
        Assert.Equal((0, 80_000), ChartScale.Nice(0, 78_600, 5));
        Assert.Equal((-10, 50), ChartScale.Nice(-7, 48, 5));
        var (min, max) = ChartScale.Nice(5, 5, 5);
        Assert.True(min < 5 && max > 5);
    }

    [Fact]
    public void Coarser_steps_are_multiples_of_the_finer_ones()
    {
        Assert.Equal([1, 2, 10, 20, 100], ChartScale.Ladder(1, 100));
        Assert.Equal([1, 5, 10, 50, 100], ChartScale.Ladder(2, 100));
    }

    [Fact]
    public void Time_ticks_fall_on_whole_units_and_months_are_among_the_days()
    {
        var day = ChartTime.Ladder.First(i => i.Unit == ChartTime.Unit.Day && i.Count == 1);
        var month = ChartTime.Ladder.First(i => i.Unit == ChartTime.Unit.Month && i.Count == 1);
        var days = ChartTime.Ticks(new DateTime(2026, 8, 6, 9, 30, 0), new DateTime(2026, 11, 3), day);
        Assert.Equal(new DateTime(2026, 8, 7), days[0]);
        var months = ChartTime.Ticks(new DateTime(2026, 8, 6), new DateTime(2026, 11, 3), month);
        Assert.Equal([new DateTime(2026, 9, 1), new DateTime(2026, 10, 1), new DateTime(2026, 11, 1)], months);
        Assert.All(months, m => Assert.Contains(m, days));
    }
}

public class ChartLayoutTests
{
    private static readonly string[] Months = CultureInfo.InvariantCulture.DateTimeFormat.MonthNames[..12];

    private static ChartModel Twelve(bool horizontal = false) => ChartLayout.Build(new ChartInput(
        Months,
        [new ChartSeriesData("Revenue", ChartType.Bar, [.. Enumerable.Range(1, 12).Select(i => (double?)i * 1000)])])
    {
        Horizontal = horizontal,
        Culture = CultureInfo.InvariantCulture,
    });

    [Fact]
    public void Band_labels_thin_out_evenly_as_the_chart_narrows()
    {
        var model = Twelve();

        // January shows at every width; a label that shows at a width shows at every wider one,
        // and the labels shown at any width are evenly spaced.
        Assert.Equal(0, model.ArgumentLabels[0].From);
        for (var k = 0; k < ChartLayout.Widths.Length; k++)
        {
            var shown = model.ArgumentLabels.Select((l, i) => (l, i)).Where(x => x.l.From <= k).Select(x => Array.IndexOf(Months, x.l.Text)).ToList();
            var gaps = shown.Zip(shown.Skip(1), (a, b) => b - a).Distinct().ToList();
            Assert.True(gaps.Count <= 1, $"Uneven labels at width {ChartLayout.Widths[k]}: {string.Join(", ", shown)}");
        }

        // Narrow, few labels; wide, all twelve.
        Assert.True(model.ArgumentLabels.Count(l => l.From == 0) <= 4);
        Assert.Equal(12, model.ArgumentLabels.Count(l => l.From <= ChartLayout.Widths.Length - 1));
    }

    [Fact]
    public void Stacked_bars_sit_on_each_other_and_full_stacks_reach_the_top_with_a_rounded_end()
    {
        var model = ChartLayout.Build(new ChartInput(
            ["Q1", "Q2"],
            [
                new ChartSeriesData("A", ChartType.Bar, [30, 10]),
                new ChartSeriesData("B", ChartType.Bar, [70, 30]),
            ])
        { Stacked = ChartStack.Full, Culture = CultureInfo.InvariantCulture });

        var a = model.Series[0].Bars;
        var b = model.Series[1].Bars;
        Assert.Equal(a[0].X, b[0].X);
        Assert.Equal(b[0].Y + b[0].Height, a[0].Y, 6);
        Assert.Equal(0, b[0].Y, 6);
        Assert.Equal(0, b[1].Y, 6);
        Assert.Equal("100%", model.ValueLabels[^1].Text.Replace(" ", ""));

        // Only the top of each stack has a rounded end.
        Assert.All(a, bar => Assert.Null(bar.Base));
        Assert.All(b, bar => Assert.NotNull(bar.Base));
    }

    [Fact]
    public void A_missing_value_leaves_a_gap_in_a_line_unless_it_is_joined()
    {
        double?[] values = [1, 2, null, 4];
        ChartModel Build(bool connect) => ChartLayout.Build(new ChartInput(
            ["a", "b", "c", "d"],
            [new ChartSeriesData("S", ChartType.Line, values) { ConnectNulls = connect }])
        { Culture = CultureInfo.InvariantCulture });

        Assert.Equal(2, Build(false).Series[0].Line!.Count(c => c == 'M'));
        Assert.Equal(1, Build(true).Series[0].Line!.Count(c => c == 'M'));
        Assert.Equal(3, Build(false).Series[0].Marks.Count);
    }

    [Fact]
    public void Scatter_draws_a_marker_per_item_on_round_scales_that_needn_t_start_at_zero()
    {
        var model = ChartLayout.Build(new ChartInput(
            [800, 4_200, 12_500],
            [new ChartSeriesData("Revenue", ChartType.Scatter, [6_300, 19_800, 21_400])])
        { Culture = CultureInfo.InvariantCulture });

        var series = model.Series[0];
        Assert.Null(series.Line);
        Assert.Equal([0, 1, 2], series.Marks.Select(m => m.Index));
        Assert.Equal(ArgumentKind.Number, model.Kind);
        Assert.True(model.ArgumentPositions[0] > 0 && model.ArgumentPositions[2] < 1);
        Assert.DoesNotContain(model.ValueLabels, l => l.Text == "0");
    }

    [Fact]
    public void Dates_on_a_line_chart_are_a_scale_with_the_month_under_the_days()
    {
        var days = Enumerable.Range(0, 30).Select(i => (object?)new DateOnly(2026, 10, 20).AddDays(i)).ToList();
        var model = ChartLayout.Build(new ChartInput(days, [new ChartSeriesData("S", ChartType.Line, [.. days.Select((_, i) => (double?)i)])])
        { Culture = CultureInfo.InvariantCulture });

        Assert.Equal(ArgumentKind.Time, model.Kind);
        Assert.True(model.TwoLine);
        Assert.Equal("Oct 2026", model.ArgumentLabels[0].Context);
        var november = Assert.Single(model.ArgumentLabels, l => l.Text == "1");
        Assert.Equal("Nov", november.Context);
    }
}

public class ChartRedrawTests
{
    [Fact]
    public void A_hidden_series_is_left_out_of_the_stacks_and_the_value_range()
    {
        var model = ChartLayout.Build(new ChartInput(["A", "B"],
        [
            new ChartSeriesData("Shown", ChartType.Bar, [10, 20]),
            new ChartSeriesData("Hidden", ChartType.Bar, [500, 500]) { Hidden = true },
        ])
        { Stacked = ChartStack.Stacked, Culture = CultureInfo.InvariantCulture });

        Assert.Empty(model.Series[1].Bars);
        Assert.Equal("20", model.ValueLabels[^1].Text);
        // Still in the table, for the legend to show it again.
        Assert.Equal("500 500", string.Join(" ", model.ValueTexts[1]));
    }

    [Fact]
    public void A_set_end_the_values_cross_widens_the_range_instead_of_reversing_it()
    {
        // ValueMin above every value: the axis starts there and runs up, not backward.
        var model = ChartLayout.Build(new ChartInput(["A", "B"], [new ChartSeriesData("S", ChartType.Bar, [5, 6])])
        { ValueMin = 100, Culture = CultureInfo.InvariantCulture });

        Assert.Equal("100", model.ValueLabels[0].Text);
        Assert.DoesNotContain(model.ValueLabels, l => l.Text.Contains("NaN"));
        Assert.Equal(0, ChartScale.TickSpec(10, 10, 5).I2 - ChartScale.TickSpec(10, 10, 5).I1 + 1);
    }

    [Fact]
    public void A_palette_spreads_its_ramp_over_the_series_and_leaves_the_default_to_the_theme()
    {
        Assert.Null(ChartPalette.Colors.Style(3));

        // Three shades: light, the color itself, dark, each turned round for dark mode.
        var shades = ChartPalette.Shades(Color.Primary).Style(3)!;
        Assert.Equal(3, shades.Split(';', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Contains("--ib-chart-2:light-dark(color-mix(in oklab, var(--ib-color-primary) 100%, white)", shades);

        // An odd count meets in a gray middle; your own colors repeat.
        Assert.Contains("--ib-chart-3:light-dark(color-mix(in oklab, var(--ib-color-neutral)", ChartPalette.Diverging(Color.Danger, Color.Success).Style(5));
        Assert.Contains("--ib-chart-3:#111", ChartPalette.Of(Color.Of("#111"), Color.Of("#222")).Style(3));
        Assert.Equal(8, ChartPalette.Shades(Color.Primary).Style(12)!.Split(';', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Theory]
    [InlineData("C0", "en-US", "$", "", "-$", ",", ".", 0, 1)]
    [InlineData("P0", "en-US", "", "%", "-", ",", ".", 0, 100)]
    [InlineData("N1", "de-DE", "", "", "-", ".", ",", 1, 1)]
    [InlineData("0' ms'", "en-US", "", " ms", "-", "", ".", 0, 1)]
    [InlineData("#,##0,K", "en-US", "", "K", "-", ",", ".", 0, 0.001)]
    public void The_script_reads_how_values_look_from_the_format(string format, string culture, string prefix, string suffix, string negative, string group, string dec, int decimals, double scale)
    {
        var shape = NumberShape.Of(format, CultureInfo.GetCultureInfo(culture))!;
        Assert.Equal((prefix, suffix, negative, group, dec, decimals, scale),
            (shape.Prefix, shape.Suffix, shape.NegativePrefix, shape.Group, shape.Decimal, shape.Decimals, shape.Scale));
        Assert.Equal(0, NumberShape.Of("#,##0.##", CultureInfo.InvariantCulture)!.MinDecimals);
        Assert.Equal(-1, NumberShape.Of(null, CultureInfo.InvariantCulture)!.Decimals);
    }
}

public class ChartRenderTests : BunitContext
{
    private record Month(string Name, decimal Revenue, decimal Costs);

    private static readonly Month[] Data = [new("Jan", 100, 60), new("Feb", 120, 70.5m)];

    public ChartRenderTests() => SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Static", isInteractive: false));

    [Fact]
    public void Renders_a_captioned_image_with_a_legend_and_a_table_of_the_values()
    {
        var cut = Render<Chart<Month>>(p => p
            .Add(c => c.Items, Data)
            .Add(c => c.Argument, m => m.Name)
            .Add(c => c.Label, "Revenue and costs")
            .Add(c => c.ValueFormat, "N1")
            .AddChildContent<ChartSeries<Month>>(s => s.Add(x => x.Value, m => m.Revenue))
            .AddChildContent<ChartSeries<Month>>(s => s.Add(x => x.Value, m => m.Costs).Add(x => x.Type, ChartType.Line)));

        // One image, named by the caption.
        var caption = cut.Find("figure.ib-chart > figcaption");
        Assert.Equal("Revenue and costs", caption.TextContent);
        var image = cut.Find("[role=img]");
        Assert.Equal(caption.Id, image.GetAttribute("aria-labelledby"));

        // Bars and a line with its markers; each mark describes its value.
        Assert.Equal(2, cut.FindAll(".ib-chart-bars rect title").Count);
        Assert.Single(cut.FindAll("path.ib-chart-line"));
        Assert.Equal("Feb, Costs: 70.5", cut.FindAll(".ib-chart-marks use title")[1].TextContent);

        // Series named from their properties, in the legend and the table.
        Assert.Equal(["Revenue", "Costs"], cut.FindAll(".ib-chart-legend li").Select(li => li.TextContent.Trim()));
        // Each "Show data" says which chart it is, and its table is named by the caption.
        Assert.Equal("Show data, Revenue and costs", cut.Find("details.ib-chart-data > summary").TextContent.Trim());
        Assert.Equal(caption.Id, cut.Find(".ib-chart-table").GetAttribute("aria-labelledby"));
        Assert.Equal(["Name", "Revenue", "Costs"], cut.FindAll(".ib-chart-table thead th").Select(th => th.TextContent));
        Assert.Equal(["Feb", "120.0", "70.5"], cut.FindAll(".ib-chart-table tbody tr")[1].Children.Select(c => c.TextContent));
    }

    [Fact]
    public void Draws_each_items_tooltip_from_the_template_for_the_script_to_show()
    {
        var cut = Render<Chart<Month>>(p => p
            .Add(c => c.Items, Data)
            .Add(c => c.Argument, m => m.Name)
            .Add(c => c.Label, "Revenue")
            .Add(c => c.TooltipTemplate, tip => builder => builder.AddContent(0, $"{tip.Argument}: {tip.Item.Revenue - tip.Item.Costs} margin, {tip.Points[0].Series}"))
            .AddChildContent<ChartSeries<Month>>(s => s.Add(x => x.Value, m => m.Revenue)));

        var tips = cut.FindAll("ib-chart .ib-chart-tips[hidden] > [data-ib-tip]");
        Assert.Equal(["Jan: 40 margin, Revenue", "Feb: 49.5 margin, Revenue"], tips.Select(t => t.TextContent.Trim()));
        Assert.NotNull(cut.Find("ib-chart [data-ib-live][aria-live=polite]"));
    }

    [Fact]
    public void SeriesBy_splits_a_series_into_one_per_value_with_only_its_own_items()
    {
        Month[] months = [new("Jan", 100, 1), new("Feb", 120, 2), new("Mar", 90, 1)];
        var cut = Render<Chart<Month>>(p => p
            .Add(c => c.Items, months)
            .Add(c => c.Argument, m => m.Name)
            .Add(c => c.Label, "Revenue by plan")
            .AddChildContent<ChartSeries<Month>>(s => s.Add(x => x.Value, m => m.Revenue).Add(x => x.SeriesBy, m => m.Costs == 1 ? "Basic" : "Pro")));

        Assert.Equal(["Basic", "Pro"], cut.FindAll(".ib-chart-legend li").Select(li => li.TextContent.Trim()));
        Assert.Equal(["Jan", "100", ""], cut.FindAll(".ib-chart-table tbody tr")[0].Children.Select(c => c.TextContent));
        Assert.Equal(2, cut.FindAll(".ib-chart-bars[data-ib-series='0'] rect title").Count);
    }

    [Fact]
    public void Needs_a_label()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Render<Chart<Month>>(p => p
            .Add(c => c.Items, Data)
            .Add(c => c.Argument, m => m.Name)));
        Assert.Contains("Label", error.Message);
    }
}
