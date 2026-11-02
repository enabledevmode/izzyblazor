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
/// <param name="Size">The size of its controls.</param>
/// <param name="MonthId">The id for the month's name.</param>
/// <param name="LabelledBy">The ids that name the grid.</param>
/// <param name="Text">The text defaults.</param>
/// <param name="Off">The day numbers of days that can't be chosen, from a disabled-date function.</param>
/// <param name="DayClasses">The classes of days that have one, by day number.</param>
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
    ControlSize Size,
    string MonthId,
    string LabelledBy,
    IzzyText Text,
    IReadOnlySet<int>? Off = null,
    IReadOnlyDictionary<int, string>? DayClasses = null);

/// <summary>
/// The month grid, drawn by everything with a calendar. It's a fragment and not a
/// <see cref="Calendar{TValue}"/>, as a field inside a field's panel would register twice on the form.
/// Every cell has its day's button, even a hidden one, as the script moves the month in these cells
/// and adds none (see <c>Scripts/elements/calendar.ts</c>).
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

    /// <summary>
    /// How many days into a week its number is counted from: the week of the year is that day's,
    /// so the culture's rule is one number, for the script too.
    /// </summary>
    public static int WeekAnchor(CalendarWeekRule rule) => rule switch
    {
        CalendarWeekRule.FirstFullWeek => 0,
        CalendarWeekRule.FirstFourDayWeek => 3,
        _ => DaysInWeek - 1,
    };

    /// <summary>The month's header, its grid (always six weeks, so another month moves nothing) and its announcements.</summary>
    /// <param name="view">What to draw.</param>
    /// <param name="describedBy">The ids that describe the grid, which change as errors come and go.</param>
    public static RenderFragment Month(CalendarView view, string? describedBy) => builder =>
    {
        var names = view.Culture.DateTimeFormat;
        var first = new DateOnly(view.Month.Year, view.Month.Month, 1);

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "ib-calendar-head");
        builder.AddContent(2, NavButton(view, view.Text.CalendarPrevious, IconRole.ChevronLeft));
        builder.OpenElement(3, "div");
        builder.AddAttribute(4, "class", "ib-calendar-month");
        builder.AddAttribute(5, "id", view.MonthId);
        builder.AddContent(6, first.ToString("Y", view.Culture));
        builder.CloseElement();
        builder.OpenElement(7, "div");
        builder.AddAttribute(8, "class", "ib-calendar-selects");
        builder.AddContent(9, Pick(view, view.Text.CalendarMonth, options =>
        {
            for (var month = 0; month < 12; month++)
            {
                options.OpenElement(0, "option");
                options.AddAttribute(1, "value", month);
                options.AddAttribute(2, "selected", month == first.Month - 1);
                options.AddContent(3, names.MonthNames[month]);
                options.CloseElement();
            }
        }));
        builder.AddContent(10, Pick(view, view.Text.CalendarYear, options =>
        {
            // The script adds the rest.
            options.OpenElement(0, "option");
            options.AddAttribute(1, "value", first.Year);
            options.AddContent(2, first.Year);
            options.CloseElement();
        }));
        builder.CloseElement();
        builder.AddContent(11, NavButton(view, view.Text.CalendarNext, IconRole.ChevronRight));
        builder.CloseElement();
        builder.AddContent(12, Days(view, describedBy));
        builder.AddContent(13, Live);
    };

    /// <summary>
    /// Months side by side, moved together by one pair of arrows at the outer edges, each named by its
    /// title: a year's select on each would make them move apart.
    /// </summary>
    /// <param name="views">What to draw, a month each.</param>
    public static RenderFragment Months(IReadOnlyList<CalendarView> views) => builder =>
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "ib-calendar-months");
        builder.AddAttribute(2, "style", $"--ib-calendar-months:{views.Count}");
        builder.AddContent(3, NavButton(views[0], views[0].Text.CalendarPrevious, IconRole.ChevronLeft));
        builder.AddContent(4, NavButton(views[0], views[0].Text.CalendarNext, IconRole.ChevronRight));
        foreach (var view in views)
        {
            builder.OpenElement(5, "div");
            builder.SetKey(view.MonthId);
            builder.AddAttribute(6, "class", "ib-calendar-sheet");
            builder.OpenElement(7, "div");
            builder.AddAttribute(8, "class", "ib-calendar-month");
            builder.AddAttribute(9, "id", view.MonthId);
            builder.AddContent(10, new DateOnly(view.Month.Year, view.Month.Month, 1).ToString("Y", view.Culture));
            builder.CloseElement();
            builder.AddContent(11, Days(view, null));
            builder.CloseElement();
        }

        builder.CloseElement();
        builder.AddContent(12, Live);
    };

    /// <summary>
    /// A box each for the parts of a time, as <c>Scripts/time-parts.ts</c> reads them: an hour and minute,
    /// a second if asked for, and AM or PM if the culture has them. With a NumberField's look and
    /// steppers, but not NumberFields, as a field inside a field's panel would register on the form.
    /// They have no name, so they don't post.
    /// </summary>
    public static RenderFragment TimeBoxes(IzzyText text, CultureInfo culture, TimeSpan? step, bool seconds, bool focusFirst) => builder =>
    {
        var names = culture.DateTimeFormat;
        var twelve = names.ShortTimePattern.Contains('t') && names.AMDesignator.Length > 0;
        // A box steps by Step, when that's whole units under the next one up.
        int? minuteStep = step is { TotalMinutes: > 1 and < 60 } minutes && minutes.Ticks % TimeSpan.TicksPerMinute == 0 ? (int)minutes.TotalMinutes : null;
        int? secondStep = step is { TotalSeconds: > 1 and < 60 } each && each.Ticks % TimeSpan.TicksPerSecond == 0 ? (int)each.TotalSeconds : null;

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "ib-calendar-time");
        builder.AddAttribute(2, "role", "group");
        builder.AddAttribute(3, "aria-label", text.CalendarTime);
        builder.AddContent(4, TimeBox(text, text.CalendarHour, twelve ? 1 : 0, twelve ? 12 : 23, null, focusFirst));
        builder.AddMarkupContent(5, "<span aria-hidden=\"true\">:</span>");
        builder.AddContent(6, TimeBox(text, text.CalendarMinute, 0, 59, minuteStep, false));
        if (seconds)
        {
            builder.AddMarkupContent(7, "<span aria-hidden=\"true\">:</span>");
            builder.AddContent(8, TimeBox(text, text.CalendarSecond, 0, 59, secondStep, false));
        }

        if (twelve)
        {
            builder.OpenElement(9, "div");
            builder.AddAttribute(10, "class", "ib-field-control ib-has-end");
            builder.OpenElement(11, "select");
            builder.AddAttribute(12, "class", "ib-field-input ib-field-select");
            builder.AddAttribute(13, "aria-label", text.CalendarDayPart);
            builder.OpenElement(14, "option");
            builder.AddAttribute(15, "value", "am");
            builder.AddContent(16, names.AMDesignator);
            builder.CloseElement();
            builder.OpenElement(17, "option");
            builder.AddAttribute(18, "value", "pm");
            builder.AddContent(19, names.PMDesignator);
            builder.CloseElement();
            builder.CloseElement();
            builder.OpenComponent<Icon>(20);
            builder.AddComponentParameter(21, nameof(Icon.Role), (IconRole?)IconRole.ChevronDown);
            builder.AddComponentParameter(22, nameof(Icon.Class), "ib-field-icon ib-field-icon-end");
            builder.CloseComponent();
            builder.CloseElement();
        }

        builder.CloseElement();
    };

    private static RenderFragment TimeBox(IzzyText text, string label, int min, int max, int? step, bool focus) => builder =>
    {
        builder.OpenElement(0, "ib-field-control");
        builder.AddAttribute(1, "class", "ib-field-control ib-has-steppers");
        builder.OpenElement(2, "input");
        builder.AddAttribute(3, "class", "ib-field-input ib-field-number");
        builder.AddAttribute(4, "type", "number");
        builder.AddAttribute(5, "inputmode", "numeric");
        builder.AddAttribute(6, "min", min);
        builder.AddAttribute(7, "max", max);
        builder.AddAttribute(8, "step", step);
        builder.AddAttribute(9, "aria-label", label);
        builder.AddAttribute(10, "autofocus", focus);
        builder.CloseElement();
        builder.OpenElement(11, "span");
        builder.AddAttribute(12, "class", "ib-field-steppers");
        builder.AddAttribute(13, "data-ib-steppers", true);
        builder.AddAttribute(14, "hidden", true);
        builder.AddContent(15, Stepper(text.Increase, "up", IconRole.ChevronUp));
        builder.AddContent(16, Stepper(text.Decrease, "down", IconRole.ChevronDown));
        builder.CloseElement();
        builder.CloseElement();
    };

    private static RenderFragment Stepper(string label, string direction, IconRole icon) => builder =>
    {
        builder.OpenElement(0, "button");
        builder.AddAttribute(1, "type", "button");
        builder.AddAttribute(2, "class", "ib-field-step");
        builder.AddAttribute(3, "data-ib-step", direction);
        builder.AddAttribute(4, "tabindex", "-1");
        builder.AddAttribute(5, "aria-label", label);
        builder.OpenComponent<Icon>(6);
        builder.AddComponentParameter(7, nameof(Icon.Role), (IconRole?)icon);
        builder.CloseComponent();
        builder.CloseElement();
    };

    private static readonly RenderFragment Live = builder =>
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "ib-visually-hidden");
        builder.AddAttribute(2, "aria-live", "polite");
        builder.AddAttribute(3, "data-ib-live", true);
        builder.CloseElement();
    };

    private static RenderFragment Days(CalendarView view, string? describedBy) => builder =>
    {
        var culture = view.Culture;
        var names = culture.DateTimeFormat;
        var first = new DateOnly(view.Month.Year, view.Month.Month, 1);
        var start = first.DayNumber - (((int)first.DayOfWeek - (int)view.FirstDay + DaysInWeek) % DaysInWeek);
        var anchor = WeekAnchor(names.CalendarWeekRule);

        builder.OpenElement(12, "table");
        builder.AddAttribute(13, "class", "ib-calendar-grid");
        builder.AddAttribute(14, "role", "grid");
        builder.AddAttribute(15, "aria-labelledby", view.LabelledBy);
        builder.AddAttribute(16, "aria-describedby", describedBy);
        builder.AddAttribute(17, "aria-readonly", view.ReadOnly ? "true" : null);

        builder.OpenElement(18, "thead");
        builder.OpenElement(19, "tr");
        if (view.ShowWeekNumbers)
        {
            builder.OpenElement(20, "th");
            builder.AddAttribute(21, "scope", "col");
            builder.AddAttribute(22, "abbr", view.Text.CalendarWeek);
            builder.AddContent(23, view.Text.CalendarWeekText);
            builder.CloseElement();
        }

        for (var column = 0; column < DaysInWeek; column++)
        {
            var day = ((int)view.FirstDay + column) % DaysInWeek;
            builder.OpenElement(24, "th");
            builder.AddAttribute(25, "scope", "col");
            builder.AddAttribute(26, "abbr", names.DayNames[day]);
            builder.AddContent(27, names.AbbreviatedDayNames[day]);
            builder.CloseElement();
        }

        builder.CloseElement();
        builder.CloseElement();

        builder.OpenElement(28, "tbody");
        for (var week = 0; week < Weeks; week++)
        {
            var row = start + (week * DaysInWeek);
            builder.OpenElement(29, "tr");
            if (view.ShowWeekNumbers)
            {
                builder.OpenElement(30, "th");
                builder.AddAttribute(31, "scope", "row");
                builder.AddContent(32, Day(row) is not null && Day(row + anchor) is { } counted ? ((counted.DayOfYear - 1) / DaysInWeek) + 1 : "");
                builder.CloseElement();
            }

            for (var column = 0; column < DaysInWeek; column++)
            {
                var date = Day(row + column);
                var outside = date is { } inMonth && (inMonth.Month != first.Month || inMonth.Year != first.Year);
                var off = view.Off?.Contains(row + column) == true;
                builder.OpenElement(33, "td");
                builder.AddAttribute(34, "class", outside ? "ib-calendar-outside" : null);
                builder.AddAttribute(35, "aria-selected", date is not null && date == view.Selected ? "true" : null);
                builder.OpenElement(36, "button");
                builder.AddAttribute(37, "type", "button");
                builder.AddAttribute(38, "class", new ClassBuilder()
                    .Add("ib-calendar-day")
                    .Add("ib-calendar-off", off)
                    .Add(view.DayClasses?.GetValueOrDefault(row + column))
                    .Build());
                // Out of the tab order until the script makes one day the grid's stop.
                builder.AddAttribute(39, "tabindex", "-1");
                builder.AddAttribute(40, "hidden", date is null || (outside && !view.ShowAdjacentDays));
                builder.AddAttribute(41, "aria-label", date?.ToString("D", culture));
                builder.AddAttribute(42, "aria-disabled", off || date < view.Min || date > view.Max ? "true" : null);
                builder.AddAttribute(43, "disabled", view.Disabled);
                builder.AddContent(44, date?.Day.ToString(CultureInfo.InvariantCulture) ?? "");
                builder.CloseElement();
                builder.CloseElement();
            }

            builder.CloseElement();
        }

        builder.CloseElement();
        builder.CloseElement();
    };

    private static RenderFragment NavButton(CalendarView view, string label, IconRole icon) => builder =>
    {
        builder.OpenElement(0, "button");
        builder.AddAttribute(1, "type", "button");
        builder.AddAttribute(2, "class", new ClassBuilder()
            .Add("ib-btn ib-btn-neutral ib-btn-ghost ib-btn-icon-only ib-calendar-nav")
            .Add("ib-btn-sm", view.Size == ControlSize.Sm)
            .Add("ib-btn-lg", view.Size == ControlSize.Lg)
            .Build());
        builder.AddAttribute(3, "aria-label", label);
        builder.AddAttribute(4, "disabled", view.Disabled);
        builder.OpenComponent<Icon>(5);
        builder.AddComponentParameter(6, nameof(Icon.Role), (IconRole?)icon);
        builder.CloseComponent();
        builder.CloseElement();
    };

    // A select with no name, as it isn't a value of the form.
    private static RenderFragment Pick(CalendarView view, string label, RenderFragment options) => builder =>
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "ib-field-control ib-has-end");
        builder.OpenElement(2, "select");
        builder.AddAttribute(3, "class", "ib-field-input ib-field-select");
        builder.AddAttribute(4, "aria-label", label);
        builder.AddAttribute(5, "disabled", view.Disabled);
        builder.AddContent(6, options);
        builder.CloseElement();
        builder.OpenComponent<Icon>(7);
        builder.AddComponentParameter(8, nameof(Icon.Role), (IconRole?)IconRole.ChevronDown);
        builder.AddComponentParameter(9, nameof(Icon.Class), "ib-field-icon ib-field-icon-end");
        builder.CloseComponent();
        builder.CloseElement();
    };

    // Null past the ends of the calendar, which January of year 1 and December of 9999 reach.
    private static DateOnly? Day(int dayNumber) =>
        dayNumber < DateOnly.MinValue.DayNumber || dayNumber > DateOnly.MaxValue.DayNumber ? null : DateOnly.FromDayNumber(dayNumber);
}
