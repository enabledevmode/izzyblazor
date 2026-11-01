using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// What every field with a month to choose from shares: its options, the answers the server writes
/// down for <c>Scripts/elements/calendar.ts</c>, and the month it draws.
/// </summary>
public abstract class CalendarFieldBase<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue> : FieldBase<TValue>
{
    private const int DefaultWindowDays = 366;

    /// <summary>The first column. Defaults to the culture's first day of the week.</summary>
    [Parameter] public DayOfWeek? FirstDayOfWeek { get; set; }

    /// <summary>The culture for the month and day names, the first day and the week numbers. Defaults to the current culture.</summary>
    [Parameter] public CultureInfo? Culture { get; set; }

    /// <summary>Adds a first column with the week of the year, counted as the culture counts them.</summary>
    [Parameter] public bool ShowWeekNumbers { get; set; }

    /// <summary>Whether to show the days of the months before and after, muted, in the first and last weeks. Defaults to true.</summary>
    [Parameter] public bool ShowAdjacentDays { get; set; } = true;

    /// <summary>
    /// Whether a day can't be chosen. Checked on the server too, so it stops <c>OnValidSubmit</c> whatever
    /// was posted. It's called for every day in the window on every render, so keep it cheap.
    /// </summary>
    [Parameter] public Func<DateOnly, bool>? DateDisabled { get; set; }

    /// <summary>
    /// How far each way from the value or today to call <see cref="DateDisabled"/> and <see cref="DayClass"/>,
    /// in whole days, for an end the limits don't set. About a year unless set. With DateDisabled, the
    /// month stops at the window's edge.
    /// </summary>
    [Parameter] public TimeSpan? DateDisabledWindow { get; set; }

    /// <summary>A class for a day's button, or null. Called like <see cref="DateDisabled"/>, so keep it cheap.</summary>
    [Parameter] public Func<DateOnly, string?>? DayClass { get; set; }

    private DateOnly? _windowFrom;
    private DateOnly? _windowTo;
    private HashSet<int>? _off;
    private Dictionary<int, string>? _dayClasses;
    private string? _offRuns;
    private string? _classRuns;
    private CalendarView? _drawn;

    /// <summary>The day the value falls on, if it has one.</summary>
    private protected abstract DateOnly? ChosenDay { get; }

    /// <summary>The earliest day that can be chosen.</summary>
    private protected abstract DateOnly? FirstDay { get; }

    /// <summary>The latest day that can be chosen.</summary>
    private protected abstract DateOnly? LastDay { get; }

    /// <summary>Whether the field's label is part of the month's name. In a panel, the panel has its own name.</summary>
    private protected virtual bool LabelNamesMonth => true;

    /// <summary>The id of the field's label, which helps name the month.</summary>
    private protected string LabelId => FieldId + "-label";

    // Past the window the server hasn't said what's allowed, so the script stops there.
    private DateOnly? ShownMin => DateDisabled is null ? FirstDay : _windowFrom;

    private DateOnly? ShownMax => DateDisabled is null ? LastDay : _windowTo;

    /// <summary>What the script reads from the element, changing as the field does.</summary>
    private protected IReadOnlyDictionary<string, object> CalendarAttributes
    {
        get
        {
            var culture = ResolvedCulture;
            var attributes = new Dictionary<string, object>
            {
                ["data-ib-culture"] = culture.Name,
                ["data-ib-first-day"] = (int)(FirstDayOfWeek ?? culture.DateTimeFormat.FirstDayOfWeek),
            };

            Add("data-ib-value", FormatDate(ChosenDay));
            Add("data-ib-min", FormatDate(ShownMin));
            Add("data-ib-max", FormatDate(ShownMax));
            Add("data-ib-off", _offRuns);
            Add("data-ib-classes", _classRuns);
            Add("data-ib-week-anchor", ShowWeekNumbers ? CalendarGrid.WeekAnchor(culture.DateTimeFormat.CalendarWeekRule).ToString(CultureInfo.InvariantCulture) : null);
            Add("data-ib-hide-adjacent", ShowAdjacentDays ? null : "");
            return attributes;

            void Add(string name, string? value)
            {
                if (value is not null)
                {
                    attributes[name] = value;
                }
            }
        }
    }

    private CultureInfo ResolvedCulture => CalendarGrid.Gregorian(Culture ?? CultureInfo.CurrentCulture);

    private CalendarView Current
    {
        get
        {
            var culture = ResolvedCulture;
            var monthId = FieldId + "-month";
            return new(
                FirstMonth,
                ChosenDay,
                ShownMin,
                ShownMax,
                FirstDayOfWeek ?? culture.DateTimeFormat.FirstDayOfWeek,
                culture,
                ShowWeekNumbers,
                ShowAdjacentDays,
                IsDisabled,
                IsReadOnly,
                Size,
                monthId,
                string.IsNullOrEmpty(ResolvedLabel) || !LabelNamesMonth ? monthId : $"{LabelId} {monthId}",
                TextDefaults,
                _off,
                _dayClasses);
        }
    }

    /// <summary>
    /// The first render stays, so Blazor's diff never writes the cells the script has moved on from:
    /// what changes reaches it in the element's attributes. Only a change of the grid's shape draws
    /// again, and those attributes then have the script draw over it.
    /// </summary>
    private protected CalendarView Drawn
    {
        get
        {
            var now = Current;
            if (_drawn is not { } drawn
                || drawn.Culture.Name != now.Culture.Name
                || drawn.FirstDay != now.FirstDay
                || drawn.ShowWeekNumbers != now.ShowWeekNumbers
                || drawn.ShowAdjacentDays != now.ShowAdjacentDays
                || drawn.Size != now.Size
                || drawn.LabelledBy != now.LabelledBy)
            {
                _drawn = now;
            }

            return _drawn.Value;
        }
    }

    // The chosen day's month. With none, this month in UTC, as the server doesn't know the person's
    // zone, or the nearest with a day that can be chosen.
    private DateOnly FirstMonth
    {
        get
        {
            if (ChosenDay is { } chosen)
            {
                return chosen;
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            return today < FirstDay ? FirstDay.Value : today > LastDay ? LastDay.Value : today;
        }
    }

    /// <summary>The text a date input holds for a day.</summary>
    private protected static string? FormatDate(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// Asks once for every day in the window, and writes the answers as runs of days, for the script:
    /// "2027-03-06/2027-03-07,2027-03-13".
    /// </summary>
    private protected void AskAboutDays()
    {
        (_windowFrom, _windowTo, _off, _dayClasses, _offRuns, _classRuns) = (null, null, null, null, null, null);
        if (DateDisabled is null && DayClass is null)
        {
            return;
        }

        var (min, max) = (FirstDay, LastDay);
        var anchor = ChosenDay ?? DateOnly.FromDateTime(DateTime.UtcNow);
        anchor = anchor < min ? min.Value : anchor > max ? max.Value : anchor;
        var each = DateDisabledWindow is { } window ? (int)Math.Max(0, Math.Min(window.TotalDays, DateOnly.MaxValue.DayNumber)) : DefaultWindowDays;
        var from = min?.DayNumber ?? Math.Max(anchor.DayNumber - each, DateOnly.MinValue.DayNumber);
        var to = max?.DayNumber ?? (int)Math.Min((long)anchor.DayNumber + each, DateOnly.MaxValue.DayNumber);
        (_windowFrom, _windowTo) = (DateOnly.FromDayNumber(from), DateOnly.FromDayNumber(to));

        var off = new List<(int From, int To)>();
        var classed = new Dictionary<string, List<(int From, int To)>>();
        for (var number = from; number <= to; number++)
        {
            var day = DateOnly.FromDayNumber(number);
            if (DateDisabled?.Invoke(day) == true)
            {
                (_off ??= []).Add(number);
                Extend(off, number);
            }

            if (DayClass?.Invoke(day) is { Length: > 0 } name)
            {
                (_dayClasses ??= [])[number] = name;
                Extend(classed.TryGetValue(name, out var runs) ? runs : classed[name] = [], number);
            }
        }

        _offRuns = off.Count > 0 ? Runs(off) : null;
        _classRuns = classed.Count > 0 ? string.Join('\n', classed.Select(entry => $"{Runs(entry.Value)}={entry.Key}")) : null;
    }

    private static void Extend(List<(int From, int To)> runs, int number)
    {
        if (runs.Count > 0 && runs[^1].To == number - 1)
        {
            runs[^1] = (runs[^1].From, number);
        }
        else
        {
            runs.Add((number, number));
        }
    }

    private static string Runs(List<(int From, int To)> runs) => string.Join(',', runs.Select(run =>
        run.From == run.To
            ? FormatDate(DateOnly.FromDayNumber(run.From))
            : $"{FormatDate(DateOnly.FromDayNumber(run.From))}/{FormatDate(DateOnly.FromDayNumber(run.To))}"));
}
