using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// A time as how long ago it was, or how soon it is: "2 hours ago", "Yesterday", "In 3 days". Past
/// <see cref="Threshold"/> it shows the date instead. The server writes the text, so it's right as the
/// page loads, with JavaScript off too; the library's script keeps it current as the page stays open and
/// writes it in the reader's language (the page's <c>lang</c>) and time zone.
/// </summary>
/// <remarks>
/// <para>Give <see cref="Value"/> in UTC or with its offset. A <c>DateTime</c> read back from a database
/// often has no kind (EF Core's SQLite provider), and converts to the server's zone: mark it as UTC.</para>
/// <para>The full date and time is the element's <c>title</c>, shown on hover. Where the exact time
/// matters, such as an audit record, show it on the page as well.</para>
/// <para>The server's words are IzzyText's <c>Relative…</c> strings; the script's are the browser's, in the
/// page's <c>lang</c>. An app that translates those strings and sets <c>lang</c> sees the same words before and
/// after the script runs. Changing the English strings ("just now") lasts only until the first update.</para>
/// </remarks>
/// <accessibility>
/// <list type="bullet">
/// <item>It renders a <c>time</c> element whose <c>datetime</c> is the exact time, for assistive technology.</item>
/// <item>Updates as time passes are silent: no live region announces them.</item>
/// <item>The <c>title</c> isn't reachable by keyboard or touch, so it repeats the time, never adds to it.</item>
/// </list>
/// </accessibility>
/// <seealso cref="Timeline"/>
/// <seealso cref="Comment"/>
[ModeSupport]
public class RelativeTime : LayoutItemBase
{
    /// <summary>The time. Nothing renders without one.</summary>
    [Parameter] public DateTimeOffset? Value { get; set; }

    /// <summary>
    /// How far from now it stays relative; past it, the date shows ("November 3", with the year when it
    /// isn't this one). Defaults to 30 days. Null keeps it relative however far.
    /// </summary>
    [Parameter] public TimeSpan? Threshold { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Starts it with a capital ("Yesterday", "Just now"), as where it stands alone: a table cell, under a
    /// title. Defaults to true; false within a sentence ("Signed in yesterday").
    /// </summary>
    [Parameter] public bool Capitalize { get; set; } = true;

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add("ib-relative-time");
        base.BuildAttributes(classes, styles);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (Value is not { } value)
        {
            return;
        }

        var (cssClass, style) = BuildRootAttributes();
        builder.OpenElement(0, "time");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        AddTimeAttributes(builder, 2, value, Threshold, Capitalize, CultureInfo.CurrentCulture);
        builder.AddAttribute(6, "class", cssClass);
        builder.AddAttribute(7, "style", style);
        builder.AddContent(8, Text(value, DateTimeOffset.UtcNow, Threshold, TextDefaults, CultureInfo.CurrentCulture, Capitalize));
        builder.CloseElement();
    }

    /// <summary>
    /// A relative time's attributes on a <c>time</c> element: <c>datetime</c>, the full date as
    /// <c>title</c>, the threshold the script reads (seconds; empty for none), and whether it starts with a
    /// capital. Takes four sequence numbers.
    /// </summary>
    internal static void AddTimeAttributes(RenderTreeBuilder builder, int sequence, DateTimeOffset value, TimeSpan? threshold, bool capitalize, CultureInfo culture)
    {
        builder.AddAttribute(sequence, "datetime", value.ToString("o", CultureInfo.InvariantCulture));
        builder.AddAttribute(sequence + 1, "title", value.ToString("f", culture));
        builder.AddAttribute(sequence + 2, "data-ib-relative",
            threshold is { } t ? ((long)t.TotalSeconds).ToString(CultureInfo.InvariantCulture) : "");
        builder.AddAttribute(sequence + 3, "data-ib-capitalize", capitalize ? "" : null);
    }

    /// <summary>
    /// The text for <paramref name="value"/> seen at <paramref name="now"/>. The script (relative-time.ts)
    /// picks the same unit: now under 45 seconds, minutes under 45 minutes, hours under 22 hours, then
    /// calendar days in the value's offset.
    /// </summary>
    internal static string Text(DateTimeOffset value, DateTimeOffset now, TimeSpan? threshold, IzzyText text, CultureInfo culture, bool capitalize)
    {
        var words = Words(value, now, threshold, text, culture);
        return capitalize && words.Length > 0 ? char.ToUpper(words[0], culture) + words[1..] : words;
    }

    private static string Words(DateTimeOffset value, DateTimeOffset now, TimeSpan? threshold, IzzyText text, CultureInfo culture)
    {
        var ago = now - value;
        var past = ago >= TimeSpan.Zero;
        var span = ago.Duration();
        var today = now.ToOffset(value.Offset);
        if (threshold is { } limit && span >= limit)
        {
            return value.Year == today.Year
                ? value.ToString(culture.DateTimeFormat.MonthDayPattern, culture)
                : value.ToString("d", culture);
        }

        if (span.TotalSeconds < 45)
        {
            return text.RelativeNow;
        }

        if (span.TotalMinutes < 45)
        {
            var minutes = Math.Max(1, (int)Math.Round(span.TotalMinutes));
            return minutes == 1 ? (past ? text.RelativeMinuteAgo : text.RelativeInMinute)
                : string.Format(culture, past ? text.RelativeMinutesAgo : text.RelativeInMinutes, minutes);
        }

        if (span.TotalHours < 22)
        {
            var hours = Math.Max(1, (int)Math.Round(span.TotalHours));
            return hours == 1 ? (past ? text.RelativeHourAgo : text.RelativeInHour)
                : string.Format(culture, past ? text.RelativeHoursAgo : text.RelativeInHours, hours);
        }

        var days = Math.Max(1, Math.Abs((today.Date - value.Date).Days));
        return days == 1 ? (past ? text.RelativeYesterday : text.RelativeTomorrow)
            : string.Format(culture, past ? text.RelativeDaysAgo : text.RelativeInDays, days);
    }
}
