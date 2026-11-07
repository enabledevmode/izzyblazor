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
        Assert.All(a, bar => Assert.Null(bar.End));
        Assert.All(b, bar => Assert.Equal('t', bar.End));
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
    public void A_set_step_puts_ticks_on_its_multiples_and_writes_them_with_its_decimals()
    {
        ChartModel Build(double step, double?[] values) => ChartLayout.Build(new ChartInput(["A", "B", "C"], [new ChartSeriesData("S", ChartType.Bar, values)])
        { ValueStep = step, Culture = CultureInfo.InvariantCulture });

        Assert.Equal(["0", "25,000", "50,000", "75,000", "100,000", "125,000", "150,000", "175,000"],
            Build(25_000, [90_000, 160_000, 120_000]).ValueLabels.Select(l => l.Text));
        Assert.Equal(["0.00", "0.25", "0.50", "0.75", "1.00"], Build(0.25, [0.3, 0.9, 0.6]).ValueLabels.Select(l => l.Text));
    }

    [Fact]
    public void A_log_scale_ticks_powers_of_ten_and_leaves_out_values_it_has_no_place_for()
    {
        var model = ChartLayout.Build(new ChartInput(["Acme", "Globex", "Initech", "Hooli"],
            [new ChartSeriesData("Employees", ChartType.Bar, [12, 3_400, 0, 250_000])])
        { ValueLog = true, Culture = CultureInfo.InvariantCulture });

        Assert.Equal(["10", "100", "1,000", "10,000", "100,000", "1,000,000"], model.ValueLabels.Select(l => l.Text));
        // Each power of ten an equal step up; bars from the bottom; zero not drawn.
        Assert.Equal(0.2, model.ValueLabels[1].Pos - model.ValueLabels[0].Pos, 6);
        var bars = model.Series[0].Bars;
        Assert.Equal(3, bars.Count);
        Assert.All(bars, b => Assert.Equal(100, b.Y + b.Height, 6));
    }

    [Fact]
    public void Crowded_names_turn_before_they_skip_or_all_show_wrapped()
    {
        string[] names = [.. Enumerable.Range(1, 12).Select(i => $"Region number {i}")];
        ChartModel Build(ChartArgumentLabels mode) => ChartLayout.Build(new ChartInput(names,
            [new ChartSeriesData("S", ChartType.Bar, [.. names.Select((_, i) => (double?)i)])])
        { ArgumentLabels = mode, Culture = CultureInfo.InvariantCulture });

        var auto = Build(ChartArgumentLabels.Auto);
        var turned = Build(ChartArgumentLabels.Rotate);
        Assert.Equal(0, auto.FlatFrom);
        Assert.True(turned.FlatFrom > 0 && turned.TurnedPx > 16);
        // Turned, more names show at the narrowest width than flat.
        Assert.True(turned.ArgumentLabels.Count(l => l.From == 0) > auto.ArgumentLabels.Count(l => l.From == 0));

        var all = Build(ChartArgumentLabels.All);
        Assert.True(all.Wrap);
        Assert.Equal(12, all.ArgumentLabels.Count(l => l.From == 0));
    }

    [Fact]
    public void Value_labels_sit_past_a_bars_end_move_inside_when_clipped_and_center_in_a_stack()
    {
        var model = ChartLayout.Build(new ChartInput(["Q1", "Q2"],
            [new ChartSeriesData("Revenue", ChartType.Bar, [100, 40]) { Labels = ChartLabels.Auto }])
        { ValueMax = 100, Culture = CultureInfo.InvariantCulture });

        // Q1 reaches the top, so its label goes inside; Q2's sits above its bar.
        Assert.Equal([true, false], model.DataLabels.Select(l => l.Inside));
        Assert.True(model.DataLabels[1].DY < 0);

        var stacked = ChartLayout.Build(new ChartInput(["Q1"],
        [
            new ChartSeriesData("A", ChartType.Bar, [60]) { Labels = ChartLabels.Auto },
            new ChartSeriesData("B", ChartType.Bar, [40]) { Labels = ChartLabels.Auto },
        ])
        { Stacked = ChartStack.Stacked, Culture = CultureInfo.InvariantCulture });
        Assert.All(stacked.DataLabels, l => Assert.True(l.Inside && l.DY == 0));
        Assert.Equal(["60", "40"], stacked.DataLabels.Select(l => l.Text));
    }

    [Fact]
    public void Value_labels_that_would_overlap_hide_at_the_widths_where_they_would()
    {
        var days = Enumerable.Range(0, 30).Select(i => (object?)$"D{i}").ToList();
        var model = ChartLayout.Build(new ChartInput(days,
            [new ChartSeriesData("Visits", ChartType.Line, [.. days.Select((_, i) => (double?)1_000 + i % 3)]) { Labels = ChartLabels.Auto, Markers = true }])
        { Culture = CultureInfo.InvariantCulture });

        var narrow = model.DataLabels.Count(l => l.From == 0);
        var wide = model.DataLabels.Count;
        Assert.True(narrow < wide && wide < 30, $"{narrow} narrow, {wide} wide");
        // A label shown narrow is shown at every width (its From is 0), never fewer as it widens.
        Assert.Equal(wide, model.DataLabels.Count(l => l.From <= ChartLayout.Widths.Length - 1));
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
            .Add(c => c.Title, "Revenue and costs")
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
            .Add(c => c.Title, "Revenue")
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
            .Add(c => c.Title, "Revenue by plan")
            .AddChildContent<ChartSeries<Month>>(s => s.Add(x => x.Value, m => m.Revenue).Add(x => x.SeriesBy, m => m.Costs == 1 ? "Basic" : "Pro")));

        Assert.Equal(["Basic", "Pro"], cut.FindAll(".ib-chart-legend li").Select(li => li.TextContent.Trim()));
        Assert.Equal(["Jan", "100", ""], cut.FindAll(".ib-chart-table tbody tr")[0].Children.Select(c => c.TextContent));
        Assert.Equal(2, cut.FindAll(".ib-chart-bars[data-ib-series='0'] rect title").Count);
    }

    [Fact]
    public void Axis_titles_sit_beside_their_axes_and_grid_lines_follow_GridLines()
    {
        Month[] months = [new("Jan", 100, 60), new("Feb", 120, 70), new("Mar", 90, 80)];
        IRenderedComponent<Chart<Month>> Build(ChartGridLines grid) => Render<Chart<Month>>(p => p
            .Add(c => c.Items, months)
            .Add(c => c.Argument, m => m.Name)
            .Add(c => c.Title, "Revenue")
            .Add(c => c.ValueTitle, "Revenue (USD)")
            .Add(c => c.ArgumentTitle, "Month")
            .Add(c => c.GridLines, grid)
            .AddChildContent<ChartSeries<Month>>(s => s.Add(x => x.Value, m => m.Revenue)));

        var cut = Build(ChartGridLines.Both);
        Assert.Equal("Revenue (USD)", cut.Find(".ib-chart-titled > .ib-chart-title-side").TextContent);
        Assert.Equal("Month", cut.Find(".ib-chart-frame > .ib-chart-title-bottom").TextContent);
        Assert.Equal("Month", cut.Find(".ib-chart-table thead th").TextContent);
        // Between the three bands: two lines.
        Assert.Equal(["33.333%", "66.667%"], cut.FindAll("line.ib-chart-agrid").Select(l => l.GetAttribute("x1")));

        var none = Build(ChartGridLines.None);
        Assert.All(none.FindAll("line.ib-chart-grid"), l => Assert.Contains("ib-chart-zero", l.ClassName));
    }

    [Fact]
    public void Formatters_write_the_labels_and_the_redraw_keeps_the_servers_axis()
    {
        Month[] months = [new("Jan", 1_200_000, 600_000), new("Feb", 2_450_000, 700_000)];
        var cut = Render<Chart<Month>>(p => p
            .Add(c => c.Items, months)
            .Add(c => c.Argument, m => m.Name)
            .Add(c => c.Title, "Revenue")
            .Add(c => c.ValueFormatter, v => $"${v / 1e6:0.#}M")
            .Add(c => c.ArgumentFormatter, a => $"{a} '26")
            .Add(c => c.HideArgumentAxis, true)
            .AddChildContent<ChartSeries<Month>>(s => s.Add(x => x.Value, m => m.Revenue))
            .AddChildContent<ChartSeries<Month>>(s => s.Add(x => x.Value, m => m.Costs)));

        Assert.Contains("$2.5M", cut.FindAll(".ib-chart-side span").Select(s => s.TextContent));
        Assert.Equal(["Jan '26", "$1.2M", "$0.6M"], cut.FindAll(".ib-chart-table tbody tr")[0].Children.Select(c => c.TextContent));
        Assert.Contains("ib-chart-off", cut.Find(".ib-chart-bottom").ClassName);

        // The legend's redraw gets the server's ticks, since it can't run the formatter.
        var draw = System.Text.Json.JsonDocument.Parse(cut.Find("ib-chart").GetAttribute("data-ib-draw")!).RootElement;
        Assert.Contains(draw.GetProperty("tk").EnumerateArray(), t => t[1].GetString() == "$2.5M");
    }

    [Fact]
    public void Tooltips_are_off_when_every_series_is_labeled_unless_asked_for()
    {
        IRenderedComponent<Chart<Month>> Build(bool? tooltip, ChartLabels second) => Render<Chart<Month>>(p => p
            .Add(c => c.Items, Data)
            .Add(c => c.Argument, m => m.Name)
            .Add(c => c.Title, "Revenue and costs")
            .Add(c => c.Tooltip, tooltip)
            .AddChildContent<ChartSeries<Month>>(s => s.Add(x => x.Value, m => m.Revenue).Add(x => x.Labels, ChartLabels.Auto))
            .AddChildContent<ChartSeries<Month>>(s => s.Add(x => x.Value, m => m.Costs).Add(x => x.Labels, second)));

        Assert.Empty(Build(null, ChartLabels.Auto).FindAll(".ib-chart-tips"));
        Assert.NotEmpty(Build(null, ChartLabels.None).FindAll(".ib-chart-tips"));
        Assert.NotEmpty(Build(true, ChartLabels.Auto).FindAll(".ib-chart-tips"));
    }

    [Fact]
    public void Needs_a_title_or_a_heading_that_titles_it()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Render<Chart<Month>>(p => p
            .Add(c => c.Items, Data)
            .Add(c => c.Argument, m => m.Name)));
        Assert.Contains("Title", error.Message);
        Assert.Contains("TitledBy", error.Message);
    }

    [Fact]
    public void TitledBy_names_the_chart_and_its_table_from_a_heading_on_the_page()
    {
        var cut = Render<Chart<Month>>(p => p
            .Add(c => c.Items, Data)
            .Add(c => c.Argument, m => m.Name)
            .Add(c => c.TitledBy, "revenue-heading")
            .AddChildContent<ChartSeries<Month>>(s => s.Add(x => x.Value, m => m.Revenue)));

        Assert.Empty(cut.FindAll("figcaption"));
        Assert.Equal("revenue-heading", cut.Find("figure").GetAttribute("aria-labelledby"));
        Assert.Equal("revenue-heading", cut.Find("[role=img]").GetAttribute("aria-labelledby"));
        Assert.Equal("revenue-heading", cut.Find(".ib-chart-table").GetAttribute("aria-labelledby"));
        // "Show data" is read with the heading, so each chart's is told apart.
        var summary = cut.Find("details.ib-chart-data > summary");
        Assert.EndsWith(" revenue-heading", summary.GetAttribute("aria-labelledby"));
        Assert.Equal("Show data", cut.Find("#" + summary.GetAttribute("aria-labelledby")!.Split(' ')[0]).TextContent);
    }
}
