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
    public void A_view_of_names_fills_the_plot_with_its_bands_and_fits_the_values_in_view()
    {
        var input = new ChartInput(
            Months,
            [new ChartSeriesData("Revenue", ChartType.Bar, [.. Enumerable.Range(1, 12).Select(i => (double?)i * 1000)])])
        { Culture = CultureInfo.InvariantCulture, View = new ChartView("March", "May") };
        var model = ChartLayout.Build(input);

        // Three bands fill the plot: March's bar starts in the first third, and only March to May are drawn and named.
        Assert.Equal(3, model.BandCount);
        Assert.Equal(["March", "April", "May"], model.ArgumentLabels.Select(l => l.Text));
        Assert.Equal([2, 3, 4], model.Series[0].Bars.Select(b => b.Index));
        Assert.InRange(model.Series[0].Bars[0].X, 0, 100 / 3.0);

        // The value axis reaches May's 5,000, not December's 12,000.
        Assert.InRange(model.ValueRange.Max, 5000, 6000);

        // Too narrow a view grows to MinZoomSpan items.
        var one = ChartLayout.Build(input with { View = new ChartView("March", "March"), MinZoomSpan = 4 });
        Assert.Equal(4, one.BandCount);
    }

    [Fact]
    public void A_view_of_dates_runs_the_scale_between_its_ends_with_finer_ticks()
    {
        var days = Enumerable.Range(0, 365).Select(i => (object?)new DateOnly(2026, 1, 1).AddDays(i)).ToList();
        var input = new ChartInput(
            days,
            [new ChartSeriesData("Visits", ChartType.Line, [.. Enumerable.Range(0, 365).Select(i => (double?)(i < 300 ? 100 : 1000))]) { Markers = true }])
        { Culture = CultureInfo.InvariantCulture };
        var whole = ChartLayout.Build(input);
        var march = ChartLayout.Build(input with { View = new ChartView(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31)) });

        // March 1 at the plot's start and March 31 at its end; April lies past it.
        Assert.Equal(0, march.ArgumentPositions[59], 6);
        Assert.Equal(1, march.ArgumentPositions[89], 6);
        Assert.True(march.ArgumentPositions[100] > 1);

        // Days along the bottom, where the whole year had months; only March's points are marked.
        Assert.DoesNotContain(whole.ArgumentLabels, l => l.Text == "16");
        Assert.Contains(march.ArgumentLabels, l => l.Text == "16");
        Assert.All(march.Series[0].Marks, m => Assert.InRange(m.Index, 59, 89));

        // The value axis fits March's 100s, not the 1,000s of the year's end.
        Assert.True(march.ValueRange.Max < 1000);
        Assert.Equal((59 / 364.0, 89 / 364.0), (Math.Round(march.View!.Share.F0, 6), Math.Round(march.View.Share.F1, 6)), new ShareComparer());
    }

    private sealed class ShareComparer : IEqualityComparer<(double, double)>
    {
        public bool Equals((double, double) a, (double, double) b) => Math.Abs(a.Item1 - b.Item1) < 1e-6 && Math.Abs(a.Item2 - b.Item2) < 1e-6;
        public int GetHashCode((double, double) v) => 0;
    }

    [Fact]
    public void Reversed_arguments_mirror_the_bars_and_labels_and_keep_the_leftmost_label()
    {
        var input = new ChartInput(
            Months,
            [new ChartSeriesData("Revenue", ChartType.Bar, [.. Enumerable.Range(1, 12).Select(i => (double?)i * 1000)])])
        { Culture = CultureInfo.InvariantCulture };
        var forward = ChartLayout.Build(input);
        var reversed = ChartLayout.Build(input with { ArgumentReverse = true });

        // January's bar at the right, where December's was, and each label mirrored.
        var (jan, dec) = (reversed.Series[0].Bars[0], forward.Series[0].Bars[11]);
        Assert.Equal(dec.X, jan.X, 6);
        Assert.Equal(1 - forward.ArgumentPositions[0], reversed.ArgumentPositions[0], 6);
        var label = (ChartModel m, string text) => m.ArgumentLabels.First(l => l.Text == text);
        Assert.Equal(1 - label(forward, "January").Pos, label(reversed, "January").Pos, 6);

        // Thinned, the labels count from the left: December shows at every width.
        Assert.Equal(0, label(reversed, "December").From);
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
        { Stacked = ChartStacking.Percent, Culture = CultureInfo.InvariantCulture });

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
            [new ChartSeriesData("S", ChartType.Line, values) { ConnectGaps = connect }])
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
        ChartModel Build(ChartLabelFit mode) => ChartLayout.Build(new ChartInput(names,
            [new ChartSeriesData("S", ChartType.Bar, [.. names.Select((_, i) => (double?)i)])])
        { ArgumentLabels = mode, Culture = CultureInfo.InvariantCulture });

        var auto = Build(ChartLabelFit.Skip);
        var turned = Build(ChartLabelFit.Rotate);
        Assert.Equal(0, auto.FlatFrom);
        Assert.True(turned.FlatFrom > 0 && turned.TurnedPx > 16);
        // Turned, more names show at the narrowest width than flat.
        Assert.True(turned.ArgumentLabels.Count(l => l.From == 0) > auto.ArgumentLabels.Count(l => l.From == 0));

        var all = Build(ChartLabelFit.Wrap);
        Assert.True(all.Wrap);
        Assert.Equal(12, all.ArgumentLabels.Count(l => l.From == 0));
    }

    [Fact]
    public void Value_labels_sit_past_a_bars_end_with_room_left_for_them_and_center_in_a_stack()
    {
        var model = ChartLayout.Build(new ChartInput(["Q1", "Q2"],
            [new ChartSeriesData("Revenue", ChartType.Bar, [100, 40]) { Labels = ChartLabels.Auto }])
        { ValueMax = 100, Culture = CultureInfo.InvariantCulture });

        // Q1 reaches the top, and its label still sits above it, in the room left over the plot.
        Assert.All(model.DataLabels, l => Assert.True(!l.Inside && l.DY < 0));
        Assert.True(model.OutsideRoom.Y > 0);

        var stacked = ChartLayout.Build(new ChartInput(["Q1"],
        [
            new ChartSeriesData("A", ChartType.Bar, [60]) { Labels = ChartLabels.Auto },
            new ChartSeriesData("B", ChartType.Bar, [40]) { Labels = ChartLabels.Auto },
        ])
        { Stacked = ChartStacking.Normal, Culture = CultureInfo.InvariantCulture });
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
        Assert.True(narrow < wide, $"{narrow} narrow, {wide} wide");
        // A label shown narrow is shown at every width (its From is 0), never fewer as it widens.
        Assert.Equal(wide, model.DataLabels.Count(l => l.From <= ChartLayout.Widths.Length - 1));

        var all = ChartLayout.Build(new ChartInput(days,
            [new ChartSeriesData("Visits", ChartType.Line, [.. days.Select((_, i) => (double?)1_000 + i % 3)]) { Labels = ChartLabels.Auto, Markers = true, LabelOverlap = ChartLabelOverlap.Show }])
        { Culture = CultureInfo.InvariantCulture });
        // LabelOverlap.Show: every label at every width; those at the ends slide in from the edges.
        Assert.Equal(30, all.DataLabels.Count(l => l.From == 0));
        Assert.Contains(all.DataLabels, l => l.Justify > 0);
    }

    [Fact]
    public void Reference_marks_stay_in_view_and_a_statistic_comes_from_its_series()
    {
        var model = ChartLayout.Build(new ChartInput(["Sep", "Oct", "Nov", "Dec"],
            [new ChartSeriesData("Revenue", ChartType.Bar, [90, 100, 110, 120])])
        {
            Culture = CultureInfo.InvariantCulture,
            Marks =
            [
                new(ChartMarkKind.Line, MarkAxis.Value, "Target") { Value = 150 },
                new(ChartMarkKind.Line, MarkAxis.Value, null) { Statistic = ChartStatistic.Average, Series = "Revenue" },
                new(ChartMarkKind.Band, MarkAxis.Argument, "Holiday season") { At = "Nov", AtTo = "Dec" },
                new(ChartMarkKind.Line, MarkAxis.Argument, "Launch") { At = "Jan" },
            ],
        });

        // The target above every value is on the axis; the average is the series'.
        Assert.True(double.Parse(model.ValueLabels[^1].Text, CultureInfo.InvariantCulture) >= 150, string.Join(" ", model.ValueLabels.Select(l => l.Text)));
        Assert.Equal(["Target: 150", "Average: 105", "Holiday season: Nov to Dec"], model.MarkTexts);
        // The band covers Nov's and Dec's bands; a line at an argument the chart hasn't got is left out.
        var band = model.Marks[2];
        Assert.Equal((0.5, 1.0), (band.P, band.Q));
        Assert.Equal(3, model.Marks.Count);
    }

    [Fact]
    public void Trends_fit_the_values_and_take_their_series_color_without_shifting_the_others()
    {
        // A linear trend through points on a line is that line, missing values and all; a
        // parabola fits a parabola exactly; a moving average starts when its window is full.
        double?[] x = [0, 1, 2, 3, 4];
        Assert.Equal([1, 3, 5, 7, 9], ChartTrends.Fit(x, [1, 3, null, 7, 9], 1).Select(v => Math.Round(v!.Value, 9)));
        Assert.Equal([0, 1, 4, 9, 16], ChartTrends.Fit(x, [0, 1, 4, 9, 16], 2).Select(v => Math.Round(v!.Value, 9)));
        Assert.Equal([null, null, 2, 3, 4], ChartTrends.MovingAverage([1, 2, 3, 4, 5], 3));
        // A SeriesBy split's other groups are missing values: the window is the series' own last three.
        Assert.Equal([null, null, null, null, 2, null, 4], ChartTrends.MovingAverage([1, null, 2, null, 3, null, 7], 3));

        var series = ChartTrends.Add(
        [
            new ChartSeriesData("Revenue", ChartType.Bar, [1, 2, 3]) { Trend = ChartTrend.Linear },
            new ChartSeriesData("Costs", ChartType.Bar, [1, 1, 1]),
        ], ["a", "b", "c"], new IzzyText());
        Assert.Equal(["Revenue", "Revenue trend", "Costs"], series.Select(s => s.Name));
        var model = ChartLayout.Build(new ChartInput(["a", "b", "c"], series) { Culture = CultureInfo.InvariantCulture });
        Assert.Equal([1, 1, 2], model.Series.Select(s => s.Slot));
        Assert.Equal(1, model.Series[1].Dash);
    }

    [Fact]
    public void A_second_axis_has_round_ticks_on_the_first_axis_grid_lines()
    {
        var model = ChartLayout.Build(new ChartInput(["Q1", "Q2", "Q3", "Q4"],
        [
            new ChartSeriesData("Revenue", ChartType.Bar, [120_000, 134_000, 129_000, 151_000]),
            new ChartSeriesData("Margin", ChartType.Line, [0.12, 0.18, 0.21, 0.31]) { Secondary = true },
        ])
        { SecondaryFormat = "P0", Culture = CultureInfo.InvariantCulture });

        // As many ticks as the first axis, at the same places, each a round step; a line's axis
        // starts near its values, not at zero.
        Assert.Equal(model.ValueLabels.Select(l => Math.Round(l.Pos, 6)), model.SecondaryLabels.Select(l => Math.Round(l.Pos, 6)));
        Assert.Equal("10 %", model.SecondaryLabels[0].Text);
        Assert.True(model.SecondaryRange.Max >= 0.31);
        // The margin line is drawn on its own scale, not the revenue's (where it would lie flat on
        // the bottom edge).
        var (q1, q4) = (model.Series[1].Marks[0], model.Series[1].Marks[3]);
        Assert.True(q4.Y < q1.Y && q1.Y < 99, $"Q1 at {q1.Y}%, Q4 at {q4.Y}% from the top");
    }

    [Fact]
    public void Bubbles_are_sized_by_area_on_one_scale_and_a_size_of_zero_draws_none()
    {
        var model = ChartLayout.Build(new ChartInput([10, 20, 30],
            [new ChartSeriesData("Markets", ChartType.Bubble, [5, 8, 6]) { Sizes = [100, 400, 0], SizeName = "Customers", ShowSizeKey = true }])
        { Culture = CultureInfo.InvariantCulture, HeightPx = 320 });

        // Largest first, so none covers a smaller one.
        var marks = model.Series[0].Marks;
        Assert.Equal([1, 0], marks.Select(m => m.Index));
        // Four times the size, twice the radius; the largest is an eighth of the plot's height.
        Assert.Equal(2, marks[0].R!.Value / marks[1].R!.Value, 6);
        Assert.Equal(40, marks[0].R!.Value, 6);
        Assert.Contains("Customers: 400", marks[0].Title);

        // The size key: round sizes near the largest and a quarter of it, at their bubbles' radii.
        Assert.Equal("Customers", model.SizeKey!.Value.Name);
        Assert.Equal([("400", 40.0), ("100", 20.0)], model.SizeKey.Value.Circles.Select(c => (c.Text, Math.Round(c.R, 6))));
    }

    [Fact]
    public void A_waterfalls_changes_start_where_the_last_ended_and_a_total_starts_from_zero()
    {
        // Revenue 100, costs -40, a total (everything so far, as the series reads it), tax -10.
        var model = ChartLayout.Build(new ChartInput(["Revenue", "Costs", "Gross", "Tax"],
            [new ChartSeriesData("Profit", ChartType.Bar, [100, -40, 60, -10]) { Totals = [false, false, true, false] }])
        { Culture = CultureInfo.InvariantCulture, ValueMin = 0, ValueMax = 100 });

        var bars = model.Series[0].Bars;
        // The start stands on the baseline as a total does.
        Assert.Equal(["total", "down", "total", "down"], bars.Select(b => b.Change));
        // Costs float from 100 down to 60; the total stands from zero to 60; tax from 60 to 50.
        Assert.Equal((0, 40), (Math.Round(bars[1].Y, 6), Math.Round(bars[1].Height, 6)));
        Assert.Equal((40, 60), (Math.Round(bars[2].Y, 6), Math.Round(bars[2].Height, 6)));
        Assert.Equal((40, 10), (Math.Round(bars[3].Y, 6), Math.Round(bars[3].Height, 6)));
        // Square ends: each is where one step ends and the next begins.
        Assert.All(bars, b => Assert.Null(b.End));
        // The start and totals read plain, the changes signed with a minus sign.
        Assert.Equal(["100", "−40", "60", "−10"], model.ValueTexts[0].Select(t => t!));
        // A connector from each bar to the next.
        Assert.Equal(3, model.Series[0].Connectors!.Split('M', StringSplitOptions.RemoveEmptyEntries).Length);
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
        { Stacked = ChartStacking.Normal, Culture = CultureInfo.InvariantCulture });

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
            .AddChildContent<ChartValueAxis>(a => a.Add(x => x.Format, "N1"))
            .AddChildContent<ChartBarSeries<Month>>(s => s.Add(x => x.Value, m => m.Revenue))
            .AddChildContent<ChartLineSeries<Month>>(s => s.Add(x => x.Value, m => m.Costs)));

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
            .AddChildContent<ChartTooltip<Month>>(t => t.Add(x => x.ChildContent, tip => builder => builder.AddContent(0, $"{tip.Argument}: {tip.Item.Revenue - tip.Item.Costs} margin, {tip.Points[0].Series}")))
            .AddChildContent<ChartBarSeries<Month>>(s => s.Add(x => x.Value, m => m.Revenue)));

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
            .AddChildContent<ChartBarSeries<Month>>(s => s.Add(x => x.Value, m => m.Revenue).Add(x => x.SeriesBy, m => m.Costs == 1 ? "Basic" : "Pro")));

        Assert.Equal(["Basic", "Pro"], cut.FindAll(".ib-chart-legend li").Select(li => li.TextContent.Trim()));
        Assert.Equal(["Jan", "100", ""], cut.FindAll(".ib-chart-table tbody tr")[0].Children.Select(c => c.TextContent));
        Assert.Equal(2, cut.FindAll(".ib-chart-bars[data-ib-series='0'] rect title").Count);
    }

    [Fact]
    public void Axis_titles_sit_beside_their_axes_and_grid_lines_follow_each_axis()
    {
        Month[] months = [new("Jan", 100, 60), new("Feb", 120, 70), new("Mar", 90, 80)];
        IRenderedComponent<Chart<Month>> Build(bool grid) => Render<Chart<Month>>(p => p
            .Add(c => c.Items, months)
            .Add(c => c.Argument, m => m.Name)
            .Add(c => c.Title, "Revenue")
            .AddChildContent<ChartValueAxis>(a => a.Add(x => x.Title, "Revenue (USD)").Add(x => x.GridLines, grid))
            .AddChildContent<ChartArgumentAxis>(a => a.Add(x => x.Title, "Month").Add(x => x.GridLines, grid))
            .AddChildContent<ChartBarSeries<Month>>(s => s.Add(x => x.Value, m => m.Revenue)));

        var cut = Build(true);
        Assert.Equal("Revenue (USD)", cut.Find(".ib-chart-titled > .ib-chart-title-side").TextContent);
        Assert.Equal("Month", cut.Find(".ib-chart-frame > .ib-chart-title-bottom").TextContent);
        Assert.Equal("Month", cut.Find(".ib-chart-table thead th").TextContent);
        // Between the three bands: two lines.
        Assert.Equal(["33.333%", "66.667%"], cut.FindAll("line.ib-chart-agrid").Select(l => l.GetAttribute("x1")));

        var none = Build(false);
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
            .AddChildContent<ChartValueAxis>(a => a.Add(x => x.Formatter, v => $"${v / 1e6:0.#}M"))
            .AddChildContent<ChartArgumentAxis>(a => a.Add(x => x.Formatter, a => $"{a} '26").Add(x => x.HideLabels, true))
            .AddChildContent<ChartBarSeries<Month>>(s => s.Add(x => x.Value, m => m.Revenue))
            .AddChildContent<ChartBarSeries<Month>>(s => s.Add(x => x.Value, m => m.Costs)));

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
            .AddChildContent<ChartBarSeries<Month>>(s => s.Add(x => x.Value, m => m.Revenue).Add(x => x.Labels, ChartLabels.Auto))
            .AddChildContent<ChartBarSeries<Month>>(s => s.Add(x => x.Value, m => m.Costs).Add(x => x.Labels, second)));

        var labeled = Build(null, ChartLabels.Auto);
        Assert.Empty(labeled.FindAll(".ib-chart-tips"));
        // Nor the browser's own: the marks carry no titles.
        Assert.Empty(labeled.FindAll("rect title, use title"));
        Assert.NotEmpty(Build(null, ChartLabels.None).FindAll(".ib-chart-tips"));
        Assert.NotEmpty(Build(true, ChartLabels.Auto).FindAll(".ib-chart-tips"));
    }

    [Fact]
    public void Reference_marks_are_drawn_and_said_in_the_charts_description()
    {
        var cut = Render<Chart<Month>>(p => p
            .Add(c => c.Items, Data)
            .Add(c => c.Argument, m => m.Name)
            .Add(c => c.Title, "Revenue")
            .AddChildContent<ChartValueAxis>(a => a.Add(x => x.Format, "$#,##0"))
            .AddChildContent<ChartBarSeries<Month>>(s => s.Add(x => x.Value, m => m.Revenue))
            .AddChildContent<ChartReferenceLine>(r => r.Add(x => x.Value, 150).Add(x => x.Label, "Target").Add(x => x.LabelAlign, ChartAlign.Start))
            .AddChildContent<ChartAnnotation>(a => a.Add(x => x.Argument, "Feb").Add(x => x.Value, 120).Add(x => x.Text, "Price change")));

        var frame = cut.Find("[role=img]");
        var description = cut.Find("#" + frame.GetAttribute("aria-describedby"));
        Assert.Contains("Target: $150", description.TextContent);
        Assert.Contains("Price change: Feb, $120", description.TextContent);
        Assert.Equal("Target", cut.Find(".ib-chart-over .ib-chart-rule > span").TextContent);
        Assert.NotNull(cut.Find(".ib-chart-over .ib-chart-rule.ib-chart-at-start[data-ib-v='150']"));
    }

    [Fact]
    public void A_trend_is_drawn_toward_the_text_color_unless_it_has_its_own()
    {
        IRenderedComponent<Chart<Month>> Build(string? color) => Render<Chart<Month>>(p => p
            .Add(c => c.Items, Data)
            .Add(c => c.Argument, m => m.Name)
            .Add(c => c.Title, "Revenue")
            .AddChildContent<ChartBarSeries<Month>>(s => s.Add(x => x.Value, m => m.Revenue).Add(x => x.Trend, ChartTrend.Linear).Add(x => x.TrendColor, color)));

        Assert.Contains("ib-chart-trend", Build(null).Find("path.ib-chart-line").ClassName);
        var own = Build("var(--ib-color-danger)").Find("path.ib-chart-line");
        Assert.DoesNotContain("ib-chart-trend", own.ClassName);
        Assert.Contains("var(--ib-color-danger)", own.GetAttribute("style"));
    }

    [Fact]
    public void A_second_axis_draws_its_labels_after_the_plot_and_needs_an_upright_chart()
    {
        IRenderedComponent<Chart<Month>> Build(Orientation orientation) => Render<Chart<Month>>(p => p
            .Add(c => c.Items, Data)
            .Add(c => c.Argument, m => m.Name)
            .Add(c => c.Title, "Revenue and costs")
            .Add(c => c.Orientation, orientation)
            .AddChildContent<ChartValueAxis>(a => a.Add(x => x.Name, "costs").Add(x => x.Title, "Costs"))
            .AddChildContent<ChartBarSeries<Month>>(s => s.Add(x => x.Value, m => m.Revenue))
            .AddChildContent<ChartLineSeries<Month>>(s => s.Add(x => x.Value, m => m.Costs).Add(x => x.ValueAxis, "costs")));

        var cut = Build(Orientation.Vertical);
        Assert.Contains("ib-chart-dual", cut.Find(".ib-chart-frame").ClassName);
        Assert.NotEmpty(cut.FindAll(".ib-chart-side2 span"));
        Assert.NotNull(cut.Find(".ib-chart-title-side2 .ib-chart-title-key.ib-chart-s2"));
        Assert.Throws<InvalidOperationException>(() => Build(Orientation.Horizontal));
    }

    [Fact]
    public void The_value_axis_at_the_right_swaps_sides_with_a_second_axis()
    {
        var cut = Render<Chart<Month>>(p => p
            .Add(c => c.Items, Data)
            .Add(c => c.Argument, m => m.Name)
            .Add(c => c.Title, "Revenue and costs")
            .AddChildContent<ChartValueAxis>(a => a.Add(x => x.Format, "0' USD'").Add(x => x.Position, ChartAxisPosition.Right))
            .AddChildContent<ChartValueAxis>(a => a.Add(x => x.Name, "costs").Add(x => x.Format, "N0"))
            .AddChildContent<ChartBarSeries<Month>>(s => s.Add(x => x.Value, m => m.Revenue))
            .AddChildContent<ChartLineSeries<Month>>(s => s.Add(x => x.Value, m => m.Costs).Add(x => x.ValueAxis, "costs")));

        var sides = cut.Find(".ib-chart-frame").Children.Where(e => e.ClassList.Contains("ib-chart-side")).ToList();
        Assert.Equal(2, sides.Count);
        Assert.DoesNotContain("USD", sides[0].TextContent);
        Assert.Contains("ib-chart-side2", sides[1].ClassName);
        Assert.Contains("USD", sides[1].TextContent);
    }

    [Fact]
    public void A_value_axis_name_no_axis_has_is_an_error()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Render<Chart<Month>>(p => p
            .Add(c => c.Items, Data)
            .Add(c => c.Argument, m => m.Name)
            .Add(c => c.Title, "Revenue and costs")
            .AddChildContent<ChartValueAxis>(a => a.Add(x => x.Name, "costs"))
            .AddChildContent<ChartBarSeries<Month>>(s => s.Add(x => x.Value, m => m.Revenue))
            .AddChildContent<ChartLineSeries<Month>>(s => s.Add(x => x.Value, m => m.Costs).Add(x => x.ValueAxis, "cost"))));
        Assert.Contains("\"cost\"", error.Message);
    }

    [Fact]
    public void A_bubbles_size_is_in_the_table_and_the_tooltip_and_it_needs_one()
    {
        var cut = Render<Chart<Month>>(p => p
            .Add(c => c.Items, Data)
            .Add(c => c.Argument, m => m.Revenue)
            .Add(c => c.Title, "Revenue against costs")
            .AddChildContent<ChartBubbleSeries<Month>>(s => s.Add(x => x.Value, m => m.Costs).Add(x => x.Size, m => m.Revenue)));

        Assert.Equal(["Revenue", "Costs", "Revenue"], cut.FindAll(".ib-chart-table thead th").Select(th => th.TextContent));
        Assert.Equal(2, cut.FindAll("circle.ib-chart-bubble[data-ib-i]").Count);
        Assert.Contains("Revenue", cut.FindAll("[data-ib-tip]")[1].TextContent);
        Assert.Throws<InvalidOperationException>(() => Render<Chart<Month>>(p => p
            .Add(c => c.Items, Data)
            .Add(c => c.Argument, m => m.Revenue)
            .Add(c => c.Title, "Costs")
            .AddChildContent<ChartBubbleSeries<Month>>(s => s.Add(x => x.Value, m => m.Costs))));
    }

    [Fact]
    public void A_waterfall_keys_its_colors_in_the_legend_and_is_its_charts_only_series()
    {
        var cut = Render<Chart<Month>>(p => p
            .Add(c => c.Items, Data)
            .Add(c => c.Argument, m => m.Name)
            .Add(c => c.Title, "Change")
            .AddChildContent<ChartWaterfallSeries<Month>>(s => s.Add(x => x.Value, m => m.Name == "Jan" ? m.Revenue : -m.Costs)));

        // Jan's 100 starts the run; Feb's 70.5 comes off it.
        Assert.Equal(["Decrease", "Total"], cut.FindAll(".ib-chart-legend li").Select(li => li.TextContent.Trim()));
        Assert.Empty(cut.FindAll(".ib-chart-toggle"));
        Assert.Null(cut.Find("ib-chart").GetAttribute("data-ib-draw"));
        Assert.Single(cut.FindAll(".ib-chart-bars rect.ib-chart-down"));
        Assert.Single(cut.FindAll("path.ib-chart-connect"));
        Assert.Throws<InvalidOperationException>(() => Render<Chart<Month>>(p => p
            .Add(c => c.Items, Data)
            .Add(c => c.Argument, m => m.Name)
            .Add(c => c.Title, "Change")
            .AddChildContent<ChartWaterfallSeries<Month>>(s => s.Add(x => x.Value, m => m.Revenue))
            .AddChildContent<ChartLineSeries<Month>>(s => s.Add(x => x.Value, m => m.Costs))));
    }

    [Fact]
    public void A_legend_at_the_top_or_start_comes_before_the_plot_shows_for_one_series_and_aligns()
    {
        var cut = Render<Chart<Month>>(p => p
            .Add(c => c.Items, Data)
            .Add(c => c.Argument, m => m.Name)
            .Add(c => c.Title, "Revenue")
            .Add(c => c.TitleAlign, ChartAlign.Center)
            .Add(c => c.Legend, ChartLegend.Start)
            .Add(c => c.LegendAlign, ChartAlign.End)
            .AddChildContent<ChartBarSeries<Month>>(s => s.Add(x => x.Value, m => m.Revenue)));

        var chart = cut.Find("ib-chart");
        Assert.Contains("ib-chart-lg-start", chart.ClassName);
        Assert.Equal(["ib-chart-legend ib-chart-legend-end", "ib-chart-main"], chart.Children.Take(2).Select(e => e.ClassName));
        Assert.Contains("ib-chart-label-center", cut.Find("figcaption").ClassName);
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
            .AddChildContent<ChartBarSeries<Month>>(s => s.Add(x => x.Value, m => m.Revenue)));

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
