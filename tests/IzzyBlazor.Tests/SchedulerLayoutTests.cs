namespace IzzyBlazor.Tests;

public class SchedulerLayoutTests
{
    private static readonly DateOnly Monday = new(2026, 11, 9);

    private static DateTime At(int day, int hour, int minute = 0) => Monday.AddDays(day).ToDateTime(new TimeOnly(hour, minute));

    [Fact]
    public void A_new_appointment_on_a_weekend_goes_on_a_day_the_work_week_shows()
    {
        // Saturday and Sunday: the week's Friday and the next week's Monday, as the view shows them.
        Assert.Equal(Monday.AddDays(4), SchedulerLayout.Shown(SchedulerView.WorkWeek, Monday.AddDays(5), DayOfWeek.Sunday, 7));
        Assert.Equal(Monday.AddDays(7), SchedulerLayout.Shown(SchedulerView.WorkWeek, Monday.AddDays(6), DayOfWeek.Sunday, 7));
        Assert.Equal(Monday.AddDays(2), SchedulerLayout.Shown(SchedulerView.WorkWeek, Monday.AddDays(2), DayOfWeek.Sunday, 7));
    }

    [Fact]
    public void Overlapping_appointments_sit_side_by_side_and_a_later_one_reuses_a_free_column()
    {
        SchedulerEntry[] entries =
        [
            new(0, At(0, 9), At(0, 10), false),
            new(1, At(0, 9, 30), At(0, 10, 30), false),
            new(2, At(0, 10), At(0, 11), false),
            new(3, At(0, 14), At(0, 15), false),
        ];
        var boxes = SchedulerLayout.Boxes(entries, Monday, 1).ToDictionary(b => b.Index);

        // The morning's three share two columns: 10:00 takes the first, free again at 10:00.
        Assert.Equal((0.0, 0.5), (boxes[0].Left, boxes[0].Width));
        Assert.Equal((0.5, 0.5), (boxes[1].Left, boxes[1].Width));
        Assert.Equal((0.0, 0.5), (boxes[2].Left, boxes[2].Width));
        // The afternoon's is alone, full width, at 14:00 of the day.
        Assert.Equal((0.0, 1.0), (boxes[3].Left, boxes[3].Width));
        Assert.Equal(14 / 24.0, boxes[3].Top, 6);
    }

    [Fact]
    public void Days_long_appointments_are_bars_in_lanes_and_say_when_they_go_on_past_the_week()
    {
        SchedulerEntry[] entries =
        [
            new(0, At(1, 0), At(3, 0), true),        // Tue and Wed, all day (ends at midnight)
            new(1, At(2, 9), At(2, 10), false),      // a timed one: a box, not a bar
            new(2, At(2, 0), At(2, 23), true),       // Wed, all day: under the first, which is longer
            new(3, At(5, 18), At(8, 9), false),      // Sat evening to the next Tue: three days, a bar
        ];
        var bars = SchedulerLayout.Bars(entries, Monday, 7).ToDictionary(b => b.Index);

        Assert.DoesNotContain(1, bars.Keys);
        Assert.Equal((1, 2, 0), (bars[0].FromDay, bars[0].ToDay, bars[0].Lane));
        Assert.Equal((2, 2, 1), (bars[2].FromDay, bars[2].ToDay, bars[2].Lane));
        Assert.Equal((5, 6, false, true), (bars[3].FromDay, bars[3].ToDay, bars[3].StartsBefore, bars[3].EndsAfter));
    }
}
