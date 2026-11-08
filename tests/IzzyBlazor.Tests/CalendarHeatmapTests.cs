using System.Globalization;

namespace IzzyBlazor.Tests;

public class CalendarHeatmapTests : BunitContext
{
    private record Commits(DateOnly Day, int? Count);

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public CalendarHeatmapTests()
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
    public void Weeks_are_columns_from_the_cultures_first_day_and_days_outside_the_range_are_not_drawn()
    {
        // Wednesday Jan 1, 2025 to Friday Feb 28: en-US weeks start on Sunday, Dec 29.
        DateOnly?[] dates = [new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 2), new DateOnly(2025, 2, 28)];
        CalendarModel Build(DayOfWeek? first) => CalendarLayout.Build(new CalendarInput
        {
            Dates = dates,
            Values = [3, null, 8],
            FirstDayOfWeek = first,
            Scale = new HeatmapInput { Columns = [], Rows = [], Values = [], Culture = CultureInfo.GetCultureInfo("en-US") },
        });

        var model = Build(null);
        Assert.Equal(new DateOnly(2024, 12, 29), model.Start);
        Assert.Equal(9, model.Weeks);
        Assert.Equal("Sun", model.Heat.RowTexts[0]);
        Assert.Equal([false, true, false, true, false, true, false], model.RowKept);

        // Sun Dec 29 to Tue Dec 31 are before the range; Jan 1 has a value, Jan 2 is no data.
        var cells = model.Heat.Cells;
        Assert.True(cells[model.CellOf(new DateOnly(2024, 12, 31))].Out);
        Assert.Equal(3, cells[model.CellOf(new DateOnly(2025, 1, 1))].Value);
        var empty = cells[model.CellOf(new DateOnly(2025, 1, 2))];
        Assert.False(empty.Out);
        Assert.Equal(-1, empty.Class);
        Assert.True(cells[model.CellOf(new DateOnly(2025, 3, 1))].Out);
        Assert.Equal("Wednesday, January 1, 2025", model.DateTexts[model.CellOf(new DateOnly(2025, 1, 1))]);

        // February is named at the first week that starts in it (Sunday Feb 2, the sixth column).
        Assert.Equal(["Jan", "Feb"], model.Heat.Ticks.Select(t => t.Text));
        Assert.Equal(5.0 / 9, model.Heat.Ticks[1].Pos, 6);

        // Weeks from Monday: Dec 30 starts the first.
        var monday = Build(DayOfWeek.Monday);
        Assert.Equal(new DateOnly(2024, 12, 30), monday.Start);
        Assert.Equal("Mon", monday.Heat.RowTexts[0]);
    }

    [Fact]
    public void The_table_is_months_by_day_of_the_month_with_blanks_outside_the_range()
    {
        Commits[] commits = [new(new DateOnly(2025, 1, 30), 4), new(new DateOnly(2025, 2, 3), null)];
        var cut = Render<CalendarHeatmap<Commits>>(p => p
            .Add(c => c.Items, commits)
            .Add(c => c.Date, c => c.Day)
            .Add(c => c.Value, c => c.Count)
            .Add(c => c.Title, "Commits"));

        var table = cut.Find("table");
        Assert.Equal(["Day", "Jan", "Feb"], table.QuerySelectorAll("thead th").Select(th => th.TextContent));
        var rows = table.QuerySelectorAll("tbody tr");
        Assert.Equal(31, rows.Length);
        string[] Row(int day) => rows[day - 1].QuerySelectorAll("td").Select(td => td.TextContent).ToArray();
        Assert.Equal(["", "No data"], Row(1));    // Jan 1 is before the range; Feb 1 is in it with no item
        Assert.Equal(["4", ""], Row(30));
        Assert.Equal(["", "No data"], Row(3));
        Assert.Equal(["No data", ""], Row(31));   // Jan 31: in range, no item
        Assert.Equal(["", ""], Row(29));          // Feb 29 doesn't exist in 2025; Jan 29 is before the range
    }
}
