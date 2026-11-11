using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// How a <see cref="ChartHistogramSeries{TItem}"/> cuts its values into bins: <see cref="Auto"/>,
/// <see cref="Count(int)"/> or <see cref="Width(double)"/>. Bins are equal in width and start on
/// round edges; the last takes in the largest value.
/// </summary>
public sealed record ChartBins
{
    private readonly int _count;
    private readonly double _width;
    private readonly TimeSpan _span;

    private ChartBins(int count, double width, TimeSpan span) => (_count, _width, _span) = (count, width, span);

    /// <summary>
    /// The default: about as many bins as Sturges' rule gives for the number of values (11 for
    /// 1,000), at the nearest round width (1, 2, 2.5 or 5 times a power of ten; for dates, a second to a
    /// week, then whole days).
    /// </summary>
    public static ChartBins Auto { get; } = new(0, 0, TimeSpan.Zero);

    /// <summary>At most <paramref name="count"/> bins, at the narrowest round width that covers the values in that many.</summary>
    public static ChartBins Count(int count) => count >= 1
        ? new(count, 0, TimeSpan.Zero)
        : throw new ArgumentOutOfRangeException(nameof(count), count, "ChartBins.Count is 1 or more.");

    /// <summary>Bins <paramref name="width"/> wide, each starting on a multiple of it, such as <c>ChartBins.Width(5)</c> for 0–5, 5–10….</summary>
    public static ChartBins Width(double width) => width > 0 && double.IsFinite(width)
        ? new(0, width, TimeSpan.Zero)
        : throw new ArgumentOutOfRangeException(nameof(width), width, "ChartBins.Width is a number over 0.");

    /// <summary>Bins of dates <paramref name="width"/> long, such as <c>ChartBins.Width(TimeSpan.FromDays(7))</c>.</summary>
    public static ChartBins Width(TimeSpan width) => width > TimeSpan.Zero
        ? new(0, 0, width)
        : throw new ArgumentOutOfRangeException(nameof(width), width, "ChartBins.Width is a time over zero.");

    internal int Most => _count;

    internal double Size => _width;

    internal TimeSpan Span => _span;

    /// <inheritdoc />
    public override string ToString() =>
        _count > 0 ? $"Count({_count})" : _width > 0 ? $"Width({_width.ToString(CultureInfo.InvariantCulture)})" : _span > TimeSpan.Zero ? $"Width({_span})" : "Auto";
}

/// <summary>
/// Counts how many items' <c>Value</c>s fall in each bin, to show how values spread, such as
/// delivery times.
/// </summary>
/// <typeparam name="TItem">The chart's item type.</typeparam>
/// <example>
/// <code>
/// &lt;Chart Items="orders" Title="Delivery time, March 2026"&gt;
///     &lt;ChartArgumentAxis Title="Days from order to delivery" /&gt;
///     &lt;ChartHistogramSeries Value="o =&gt; o.DeliveryDays" Name="Orders" /&gt;
/// &lt;/Chart&gt;
/// </code>
/// </example>
/// <remarks>
/// The bars touch, the axis along the bottom is the values' own scale with the bins' edges marked,
/// and the value axis counts from zero. <see cref="Bins"/> sets how they're cut. The chart takes no
/// <c>Argument</c>. <c>SeriesBy</c> splits the counts by a group, on one set of bins: stacked with
/// the chart's <c>Stacking</c>, or overlaid with a lower <see cref="Opacity"/>. A histogram's chart
/// holds only histogram series; mark a value on its scale with a <see cref="ChartReferenceLine"/>'s
/// <c>Argument</c>, such as a target time.
/// </remarks>
[ModeSupport]
public sealed class ChartHistogramSeries<TItem> : ChartSeriesBase<TItem>
{
    private object? _sample;

    /// <summary>The value each item is counted by, such as <c>o =&gt; o.DeliveryDays</c>: a number or a date, or null to leave the item out.</summary>
    [Parameter, EditorRequired] public Expression<Func<TItem, object?>> Value { get; set; } = default!;

    /// <summary>
    /// How the values are cut into bins: <see cref="ChartBins.Auto"/> (the default),
    /// <see cref="ChartBins.Count(int)"/> or <see cref="ChartBins.Width(double)"/>. The width
    /// changes the picture, so try a few. Every histogram series on a chart takes the same bins.
    /// </summary>
    [Parameter] public ChartBins? Bins { get; set; }

    /// <summary>
    /// How solid the bars are, from 0 to 1. Set, the bars are see-through, so the groups of a split
    /// series overlaid on one another show through, such as at 0.5. Defaults to opaque bars, 0.85
    /// of the series color in light mode and 0.9 in dark.
    /// </summary>
    [Parameter] public double? Opacity { get; set; }

    private protected override ChartType Type => ChartType.Bar;

    private protected override Expression<Func<TItem, object?>>? ValueOf => Value;

    private protected override double? OpacityOf => Opacity;

    private protected override ChartBins? BinsOf => Bins ?? ChartBins.Auto;

    private protected override object? DateOf => _sample;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Trend != ChartTrend.None)
        {
            throw new InvalidOperationException("A ChartHistogramSeries takes no Trend: its bars are counts of bins, not a run of values.");
        }

        base.OnParametersSet();
    }

    // Dates are counted by their ticks; numbers as numbers, never both.
    private protected override double? Number(object? value)
    {
        var date = value is DateTime or DateOnly or DateTimeOffset;
        if (value is null) return null;
        if (_sample is not null && date != (_sample is DateTime or DateOnly or DateTimeOffset))
        {
            throw new InvalidOperationException("A ChartHistogramSeries' values are all numbers or all dates.");
        }

        if (!date) return ChartMembers.ToNumber(value);
        _sample ??= value;
        return ChartLayout.ToDouble(value, ArgumentKind.Time);
    }

    private protected override void BeginRead() => _sample = null;
}

/// <summary>A histogram's bins: their edges (a date's in ticks), the width, and how each edge reads.</summary>
internal sealed record ChartHistogram(double[] Edges, double Step, ArgumentKind Kind, string[] EdgeTexts)
{
    public int Count => Math.Max(0, Edges.Length - 1);

    /// <summary>Which bin a value falls in: the last takes in its upper edge.</summary>
    public int IndexOf(double v)
    {
        var i = (int)Math.Floor((v - Edges[0]) / Step + 1e-9);
        return Math.Clamp(i, 0, Count - 1);
    }

    /// <summary>Each bin as "10 – 20".</summary>
    public string[] BinTexts => Enumerable.Range(0, Count).Select(i => $"{EdgeTexts[i]} – {EdgeTexts[i + 1]}").ToArray();

    // Round widths for dates: a second to a week, then whole days by 1, 2 and 5.
    private static readonly TimeSpan[] Spans =
    [
        TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(1), TimeSpan.FromHours(2), TimeSpan.FromHours(3), TimeSpan.FromHours(6), TimeSpan.FromHours(12),
        TimeSpan.FromDays(1), TimeSpan.FromDays(2), TimeSpan.FromDays(7),
    ];

    private const int MostBins = 1000;

    /// <summary>
    /// The bins for the values of every series (dates as ticks, with <paramref name="sample"/> one
    /// of them, for its type): their edges round multiples of the width. None with no values.
    /// </summary>
    public static ChartHistogram Build(IEnumerable<double> values, ChartBins bins, object? sample, Func<object?, string>? formatter, string? format, CultureInfo culture)
    {
        var all = values.Where(double.IsFinite).ToArray();
        var time = sample is not null;
        var kind = time ? ArgumentKind.Time : ArgumentKind.Number;
        if (time && bins.Size > 0)
        {
            throw new InvalidOperationException("Dates are cut into bins of a time: give ChartBins.Width a TimeSpan, such as TimeSpan.FromDays(7).");
        }

        if (!time && bins.Span > TimeSpan.Zero)
        {
            throw new InvalidOperationException("Numbers are cut into bins of a number: give ChartBins.Width a number, such as 5.");
        }

        if (all.Length == 0) return new([], 1, kind, []);
        var (min, max) = (all.Min(), all.Max());

        // Whole numbers (or dates at midnight) are never cut finer than one (or a day).
        var whole = time ? all.All(v => v % TimeSpan.TicksPerDay == 0) : all.All(v => v == Math.Floor(v));
        var least = time ? whole ? TimeSpan.TicksPerDay : 0 : whole ? 1 : 0;

        double step;
        if (bins.Size > 0) step = bins.Size;
        else if (bins.Span > TimeSpan.Zero) step = bins.Span.Ticks;
        else
        {
            // Auto: the round width nearest the span over Sturges' count. Count: the narrowest
            // round width whose bins, on round edges, are no more than it.
            var most = bins.Most > 0 ? bins.Most : (int)Math.Ceiling(Math.Log2(all.Length)) + 1;
            var raw = (max - min) / most;
            step = Round(raw, time, up: false);
            if (bins.Most == 0 && step > 0 && Lower(step, time) is { } lower && step / raw > raw / lower) step = lower;
            step = Math.Max(least, step);
            if (step == 0) step = time ? TimeSpan.TicksPerSecond : Round(Math.Max(Math.Abs(min), 1) / 10, false, up: false);
            for (var tries = 0; bins.Most > 0 && Bins(min, max, step) > most && tries < 60; tries++) step = Round(step, time, up: true);
        }

        var count = Bins(min, max, step);
        if (count > MostBins)
        {
            throw new InvalidOperationException($"ChartBins.{bins} cuts these values into {count:N0} bins; a histogram takes at most {MostBins:N0}. Give a wider width.");
        }

        var start = Math.Floor(min / step + 1e-9) * step;
        var edges = Enumerable.Range(0, count + 1).Select(k => Exact(start, k, step)).ToArray();
        var decimals = ChartLayout.StepDecimals(step);
        string Text(double edge)
        {
            if (!time) return formatter?.Invoke(edge) ?? edge.ToString(format ?? "N" + decimals, culture);
            var date = new DateTime((long)edge);
            object value = sample switch
            {
                DateOnly => DateOnly.FromDateTime(date),
                DateTimeOffset o => new DateTimeOffset(date, o.Offset),
                _ => date,
            };
            return formatter?.Invoke(value) ?? ChartLayout.ArgumentText(value, format, culture);
        }

        return new(edges, step, kind, edges.Select(Text).ToArray());
    }

    // How many bins of a width, on its multiples, cover min to max (the last closed).
    private static int Bins(double min, double max, double step)
    {
        var start = Math.Floor(min / step + 1e-9) * step;
        var count = Math.Ceiling((max - start) / step - 1e-9);
        return count > int.MaxValue / 2 ? int.MaxValue / 2 : Math.Max(1, (int)count);
    }

    // start + k × step, without 0.1 + 0.2's error where the numbers allow.
    private static double Exact(double start, int k, double step)
    {
        if (Math.Abs(start) < 1e20 && step >= 1e-20 && step < 1e20)
        {
            try { return (double)((decimal)start + k * (decimal)step); }
            catch (OverflowException) { }
        }

        return start + k * step;
    }

    // The round width just under one (null under a second, for dates).
    private static double? Lower(double step, bool time)
    {
        if (time)
        {
            var i = Array.FindIndex(Spans, t => t.Ticks >= step);
            return i > 0 ? Spans[i - 1].Ticks : i < 0 ? Math.Max(Spans[^1].Ticks, step / 2) : null;
        }

        var e = (int)Math.Floor(Math.Log10(step) + 1e-9);
        double? best = null;
        foreach (var exp in new[] { e - 1, e })
        {
            foreach (var m in new[] { "1", "2", "2.5", "5" })
            {
                var w = double.Parse($"{m}e{exp}", CultureInfo.InvariantCulture);
                if (w < step * (1 - 1e-9)) best = w;
            }
        }

        return best;
    }

    // The round width at or past raw (up: the next one past it): 1, 2, 2.5 or 5 times a power of
    // ten; for dates, the next of Spans, then whole days by 1, 2 and 5.
    private static double Round(double raw, bool time, bool up)
    {
        if (!(raw > 0)) return 0;
        bool Past(double w) => up ? w > raw * (1 + 1e-9) : w >= raw * (1 - 1e-9);
        if (time)
        {
            foreach (var s in Spans)
            {
                if (Past(s.Ticks)) return s.Ticks;
            }

            return Round(raw / TimeSpan.TicksPerDay, false, up) is var days && days >= 10 ? days * TimeSpan.TicksPerDay : 10 * TimeSpan.TicksPerDay;
        }

        var e = (int)Math.Floor(Math.Log10(raw));
        foreach (var exp in new[] { e - 1, e, e + 1 })
        {
            foreach (var m in new[] { "1", "2", "2.5", "5" })
            {
                var w = double.Parse($"{m}e{exp}", CultureInfo.InvariantCulture);
                if (Past(w)) return w;
            }
        }

        return double.Parse($"1e{e + 2}", CultureInfo.InvariantCulture);
    }
}
