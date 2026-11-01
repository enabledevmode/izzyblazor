namespace IzzyBlazor;

/// <summary>A range to choose in one click, drawn beside a <see cref="DateRangeField"/>'s months.</summary>
/// <param name="Label">What the button says.</param>
/// <param name="Start">Its first day.</param>
/// <param name="End">Its last day, included.</param>
public sealed record DateRangePreset(string Label, DateOnly Start, DateOnly End)
{
    /// <summary>
    /// Today, the last 7 and 30 days, this and last month, and this year. Pass today in the zone the
    /// app means, as the server's may not be the person's.
    /// </summary>
    /// <param name="today">The day they count from.</param>
    /// <param name="text">The words, for another language. Defaults to English.</param>
    public static IReadOnlyList<DateRangePreset> Common(DateOnly today, IzzyText? text = null)
    {
        text ??= new IzzyText();
        var month = new DateOnly(today.Year, today.Month, 1);
        return
        [
            new(text.PresetToday, today, today),
            new(text.PresetLast7Days, today.AddDays(-6), today),
            new(text.PresetLast30Days, today.AddDays(-29), today),
            new(text.PresetThisMonth, month, month.AddMonths(1).AddDays(-1)),
            new(text.PresetLastMonth, month.AddMonths(-1), month.AddDays(-1)),
            new(text.PresetThisYear, new DateOnly(today.Year, 1, 1), new DateOnly(today.Year, 12, 31)),
        ];
    }
}
