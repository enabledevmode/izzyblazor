using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>What <see cref="CalendarGrid.Month"/> draws.</summary>
/// <param name="Month">Any day of the month to draw.</param>
/// <param name="Selected">The chosen day.</param>
/// <param name="Min">The earliest day that can be chosen.</param>
/// <param name="Max">The latest day that can be chosen.</param>
/// <param name="FirstDay">The first column.</param>
/// <param name="Culture">The culture for names, from <see cref="CalendarGrid.Gregorian"/>.</param>
/// <param name="ShowWeekNumbers">Whether a first column gives the week of the year.</param>
/// <param name="ShowAdjacentDays">Whether the cells before and after the month show their days.</param>
/// <param name="Disabled">Whether nothing can be chosen, and the month shows it.</param>
/// <param name="ReadOnly">Whether nothing can be chosen.</param>
/// <param name="LabelId">The id for the month's name, which names the grid.</param>
/// <param name="WeekText">The week column's short heading.</param>
/// <param name="WeekLabel">The week column's full heading.</param>
internal readonly record struct CalendarView(
    DateOnly Month,
    DateOnly? Selected,
    DateOnly? Min,
    DateOnly? Max,
    DayOfWeek FirstDay,
    CultureInfo Culture,
    bool ShowWeekNumbers,
    bool ShowAdjacentDays,
    bool Disabled,
    bool ReadOnly,
    string LabelId,
    string WeekText,
    string WeekLabel);

/// <summary>
/// The month grid, drawn by everything with a calendar. It's a fragment and not a
/// <see cref="Calendar{TValue}"/>, as a field inside a field's panel would register twice on the form.
/// </summary>
internal static class CalendarGrid
{
    private const int Weeks = 6;
    private const int DaysInWeek = 7;

    /// <summary>
    /// The culture with its Gregorian calendar, since a native date input posts Gregorian whatever
    /// the culture. One with none keeps the invariant culture's names.
    /// </summary>
    public static CultureInfo Gregorian(CultureInfo culture)
    {
        if (culture.DateTimeFormat.Calendar is GregorianCalendar)
        {
            return culture;
        }

        if (culture.OptionalCalendars.OfType<GregorianCalendar>().FirstOrDefault() is not { } gregorian)
        {
            return CultureInfo.InvariantCulture;
        }

        var copy = (CultureInfo)culture.Clone();
        copy.DateTimeFormat.Calendar = gregorian;
        return copy;
    }

    /// <summary>The month's name and its grid: always six weeks, so another month moves nothing.</summary>
    public static RenderFragment Month(CalendarView view) => builder =>
    {
        var culture = view.Culture;
        var names = culture.DateTimeFormat;
        var first = new DateOnly(view.Month.Year, view.Month.Month, 1);
        var start = first.DayNumber - (((int)first.DayOfWeek - (int)view.FirstDay + DaysInWeek) % DaysInWeek);

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "ib-calendar-month");
        builder.AddAttribute(2, "id", view.LabelId);
        builder.AddContent(3, first.ToString("Y", culture));
        builder.CloseElement();

        builder.OpenElement(4, "table");
        builder.AddAttribute(5, "class", "ib-calendar-grid");
        builder.AddAttribute(6, "role", "grid");
        builder.AddAttribute(7, "aria-labelledby", view.LabelId);
        builder.AddAttribute(8, "aria-readonly", view.ReadOnly ? "true" : null);

        builder.OpenElement(9, "thead");
        builder.OpenElement(10, "tr");
        if (view.ShowWeekNumbers)
        {
            builder.OpenElement(11, "th");
            builder.AddAttribute(12, "scope", "col");
            builder.AddAttribute(13, "abbr", view.WeekLabel);
            builder.AddContent(14, view.WeekText);
            builder.CloseElement();
        }

        for (var column = 0; column < DaysInWeek; column++)
        {
            var day = ((int)view.FirstDay + column) % DaysInWeek;
            builder.OpenElement(15, "th");
            builder.AddAttribute(16, "scope", "col");
            builder.AddAttribute(17, "abbr", names.DayNames[day]);
            builder.AddContent(18, names.AbbreviatedDayNames[day]);
            builder.CloseElement();
        }

        builder.CloseElement();
        builder.CloseElement();

        builder.OpenElement(19, "tbody");
        for (var week = 0; week < Weeks; week++)
        {
            builder.OpenElement(20, "tr");
            if (view.ShowWeekNumbers)
            {
                builder.OpenElement(21, "th");
                builder.AddAttribute(22, "scope", "row");
                if (Day(start + (week * DaysInWeek)) is { } monday)
                {
                    builder.AddContent(23, WeekOfYear(monday, view));
                }

                builder.CloseElement();
            }

            for (var column = 0; column < DaysInWeek; column++)
            {
                var date = Day(start + (week * DaysInWeek) + column);
                var outside = date is not { } inMonth || inMonth.Month != first.Month || inMonth.Year != first.Year;
                builder.OpenElement(24, "td");
                builder.AddAttribute(25, "class", outside ? "ib-calendar-outside" : null);
                builder.AddAttribute(26, "aria-selected", date is not null && date == view.Selected ? "true" : null);
                if (date is { } shown && (!outside || view.ShowAdjacentDays))
                {
                    var allowed = !(shown < view.Min || shown > view.Max);
                    builder.OpenElement(27, "button");
                    builder.AddAttribute(28, "type", "button");
                    builder.AddAttribute(29, "class", "ib-calendar-day");
                    // Out of the tab order until the script makes one day the grid's stop.
                    builder.AddAttribute(30, "tabindex", "-1");
                    builder.AddAttribute(31, "aria-label", shown.ToString("D", culture));
                    builder.AddAttribute(32, "aria-disabled", allowed ? null : "true");
                    builder.AddAttribute(33, "disabled", view.Disabled);
                    builder.AddContent(34, shown.Day);
                    builder.CloseElement();
                }

                builder.CloseElement();
            }

            builder.CloseElement();
        }

        builder.CloseElement();
        builder.CloseElement();
    };

    // Null past the ends of the calendar, which January of year 1 and December of 9999 reach.
    private static DateOnly? Day(int dayNumber) =>
        dayNumber < DateOnly.MinValue.DayNumber || dayNumber > DateOnly.MaxValue.DayNumber ? null : DateOnly.FromDayNumber(dayNumber);

    private static int WeekOfYear(DateOnly day, CalendarView view)
    {
        var at = day.ToDateTime(TimeOnly.MinValue);
        var rule = view.Culture.DateTimeFormat.CalendarWeekRule;

        // Calendar.GetWeekOfYear calls the last days of 2024 week 53 by this rule, where ISO 8601 says week 1.
        return rule == CalendarWeekRule.FirstFourDayWeek && view.FirstDay == DayOfWeek.Monday
            ? ISOWeek.GetWeekOfYear(at)
            : view.Culture.DateTimeFormat.Calendar.GetWeekOfYear(at, rule, view.FirstDay);
    }
}
