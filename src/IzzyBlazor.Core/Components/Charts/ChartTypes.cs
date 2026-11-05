namespace IzzyBlazor;

/// <summary>How a <see cref="ChartSeries{TItem}"/> draws its values.</summary>
public enum ChartType
{
    /// <summary>A bar for each value: upright columns, or bars across with <see cref="Orientation.Horizontal"/>.</summary>
    Bar,

    /// <summary>A line through the values, with a marker at each when there are few.</summary>
    Line,

    /// <summary>A line with the space down to zero filled.</summary>
    Area,

    /// <summary>
    /// A marker at each value and no line, for how two measures relate: give the chart numbers or
    /// dates as its Argument.
    /// </summary>
    Scatter,
}

/// <summary>Whether a chart's bars and areas sit side by side or on top of each other.</summary>
public enum ChartStack
{
    /// <summary>Bars side by side, areas over each other.</summary>
    None,

    /// <summary>Each series on top of the one before, so the stack's height is the total.</summary>
    Stacked,

    /// <summary>Stacked, with each stack drawn to 100%, so the parts show as shares of the whole.</summary>
    Full,
}

/// <summary>The shape of a line between its values.</summary>
public enum ChartCurve
{
    /// <summary>Straight from value to value.</summary>
    Straight,

    /// <summary>A smooth curve that never overshoots a value (monotone).</summary>
    Smooth,

    /// <summary>Flat, with a step halfway between values.</summary>
    Step,
}

/// <summary>Where a chart's legend goes.</summary>
public enum ChartLegend
{
    /// <summary>Below the chart, when there are two series or more. A single series is named by the chart's label.</summary>
    Auto,

    /// <summary>Below the chart, even for one series.</summary>
    Bottom,

    /// <summary>No legend.</summary>
    None,
}

/// <summary>How a chart offers its values as a table.</summary>
public enum ChartDataTable
{
    /// <summary>A "Show data" disclosure under the chart, closed until opened.</summary>
    Collapsed,

    /// <summary>The table shows under the chart.</summary>
    Visible,

    /// <summary>No table, for a page that lists the values elsewhere.</summary>
    None,
}

/// <summary>How a line or area's line is drawn.</summary>
public enum ChartLineStyle
{
    /// <summary>A solid line.</summary>
    Solid,

    /// <summary>Dashes, as a plan or target line often is.</summary>
    Dashed,

    /// <summary>Dots.</summary>
    Dotted,
}
