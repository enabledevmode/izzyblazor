namespace IzzyBlazor;

/// <summary>
/// What a <see cref="Chart{TItem}.TooltipTemplate"/> draws from: the item under the pointer (or
/// keyboard focus) and each series' value for it.
/// </summary>
/// <typeparam name="TItem">The chart's item type.</typeparam>
public sealed class ChartTooltip<TItem>
{
    internal ChartTooltip(TItem item, int index, string argument, IReadOnlyList<ChartTooltipPoint> points)
    {
        Item = item;
        Index = index;
        Argument = argument;
        Points = points;
    }

    /// <summary>The item.</summary>
    public TItem Item { get; }

    /// <summary>The item's place in the chart's <c>Items</c>, from 0.</summary>
    public int Index { get; }

    /// <summary>The item's argument as the chart shows it, formatted with <c>ArgumentFormat</c>.</summary>
    public string Argument { get; }

    /// <summary>Each series' value for the item, in the chart's order.</summary>
    public IReadOnlyList<ChartTooltipPoint> Points { get; }
}

/// <summary>One series' value in a <see cref="ChartTooltip{TItem}"/>.</summary>
/// <param name="Series">The series' name.</param>
/// <param name="Value">The value, or null where the item has none.</param>
/// <param name="Text">The value formatted with the chart's <c>ValueFormat</c>, or null.</param>
/// <param name="Color">The series' color, as a CSS color, such as <c>var(--ib-chart-1)</c>.</param>
public sealed record ChartTooltipPoint(string Series, double? Value, string? Text, string Color);
