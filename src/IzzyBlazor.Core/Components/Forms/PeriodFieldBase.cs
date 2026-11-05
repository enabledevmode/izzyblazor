using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// What every field with a calendar shares: the culture it names months and days in, and the page of
/// months or years it draws. Not a component of its own.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class PeriodFieldBase<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue> : FieldBase<TValue>
{
    private PeriodView? _drawnPeriod;

    private protected PeriodFieldBase()
    {
    }

    /// <summary>The culture for the month and day names, the first day and the week numbers. Defaults to the current culture.</summary>
    [Parameter] public CultureInfo? Culture { get; set; }

    /// <summary>Whether the field's label is part of the month's name. In a panel, the panel has its own name.</summary>
    private protected virtual bool LabelNamesMonth => true;

    /// <summary>The id of the field's label, which helps name the month.</summary>
    private protected string LabelId => FieldId + "-label";

    private protected CultureInfo ResolvedCulture => CalendarGrid.Gregorian(Culture ?? CultureInfo.CurrentCulture);

    /// <summary>What the script reads from a page of months or years, changing as the field does.</summary>
    /// <param name="years">Whether it's a page of years, or else of months.</param>
    /// <param name="value">The input's text for the value.</param>
    /// <param name="min">The input's text for the earliest.</param>
    /// <param name="max">The input's text for the latest.</param>
    private protected IReadOnlyDictionary<string, object> PeriodAttributes(bool years, string? value, string? min, string? max)
    {
        var attributes = new Dictionary<string, object>
        {
            ["data-ib-unit"] = years ? "year" : "month",
            ["data-ib-culture"] = ResolvedCulture.Name,
        };

        if (value is not null)
        {
            attributes["data-ib-value"] = value;
        }

        if (min is not null)
        {
            attributes["data-ib-min"] = min;
        }

        if (max is not null)
        {
            attributes["data-ib-max"] = max;
        }

        return attributes;
    }

    /// <summary>
    /// The first page stays, as a calendar's month does, so Blazor's diff never writes the cells the
    /// script has moved on from.
    /// </summary>
    /// <param name="years">Whether it's a page of years, or else of months.</param>
    /// <param name="selected">The chosen one's number: a year, or a month as year * 12 + month.</param>
    /// <param name="earliest">The earliest one's number, for a first page with no value.</param>
    /// <param name="latest">The latest one's number, for a first page with no value.</param>
    private protected PeriodView DrawnPeriod(bool years, int? selected, int? earliest, int? latest)
    {
        var culture = ResolvedCulture;
        if (_drawnPeriod is not { } drawn || drawn.Culture.Name != culture.Name || drawn.Size != Size || drawn.Years != years)
        {
            var now = DateTime.UtcNow;
            var today = years ? now.Year : (now.Year * 12) + now.Month - 1;
            var titleId = FieldId + "-page";
            _drawnPeriod = new PeriodView(
                years,
                selected ?? Math.Min(Math.Max(today, earliest ?? today), latest ?? today),
                selected,
                culture,
                IsDisabled,
                IsReadOnly,
                Size,
                titleId,
                string.IsNullOrEmpty(ResolvedLabel) || !LabelNamesMonth ? titleId : $"{LabelId} {titleId}",
                TextDefaults);
        }

        return _drawnPeriod.Value;
    }
}
