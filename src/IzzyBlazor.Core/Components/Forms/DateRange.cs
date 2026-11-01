namespace IzzyBlazor;

/// <summary>
/// Two whole days, both included, either of which may be open. It holds days rather than times, so
/// midnight never enters it. The properties are settable for form binding on a static post.
/// </summary>
public sealed record DateRange
{
    /// <summary>A range from <paramref name="start"/> to <paramref name="end"/>, both included. One constructor, as form binding takes no more.</summary>
    public DateRange(DateOnly? start = null, DateOnly? end = null) => (Start, End) = (start, end);

    /// <summary>The first day, or null for no limit.</summary>
    public DateOnly? Start { get; set; }

    /// <summary>The last day, included, or null for no limit.</summary>
    public DateOnly? End { get; set; }

    /// <summary>How many days it spans, both ends counted, or null with an open end.</summary>
    public int? Days => Start is { } start && End is { } end ? end.DayNumber - start.DayNumber + 1 : null;

    /// <summary>Whether this day is in the range.</summary>
    public bool Contains(DateOnly day) => (Start is not { } start || day >= start) && (End is not { } end || day <= end);

    /// <summary>Whether this time's clock date is in the range. Any time on the last day counts.</summary>
    public bool Contains(DateTime time) => Contains(DateOnly.FromDateTime(time));

    /// <summary>
    /// The range as instants in UTC, for a query: <c>from &lt;= x &amp;&amp; x &lt; until</c>, either
    /// null with an open end. <c>Until</c> is the first instant of the day after <see cref="End"/> in
    /// this zone, so it's right across a change of the clocks, and an index can serve it.
    /// </summary>
    public (DateTimeOffset? From, DateTimeOffset? Until) ToUtc(TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        return (Start is { } start ? FirstInstant(start, zone) : null,
            End is { } end ? end == DateOnly.MaxValue ? null : FirstInstant(end.AddDays(1), zone) : null);
    }

    // A midnight the clocks skip takes the standard offset, which is the moment they change. One they
    // repeat takes the larger offset, which is the first time round.
    private static DateTimeOffset FirstInstant(DateOnly day, TimeZoneInfo zone)
    {
        var midnight = day.ToDateTime(TimeOnly.MinValue);
        var offset = zone.IsAmbiguousTime(midnight) ? zone.GetAmbiguousTimeOffsets(midnight).Max() : zone.GetUtcOffset(midnight);
        return new DateTimeOffset(midnight, offset).ToUniversalTime();
    }
}
