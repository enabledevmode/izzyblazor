namespace IzzyBlazor;

/// <summary>How a series draws its values: which series component it is.</summary>
internal enum ChartType
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

    /// <summary>
    /// A scatter whose circles are sized by a third measure, the series' <c>Size</c>: by area, so a
    /// value twice as large is a circle twice the area.
    /// </summary>
    Bubble,
}

/// <summary>Whether a chart's bars and areas sit side by side or on top of each other.</summary>
public enum ChartStacking
{
    /// <summary>Bars side by side, areas over each other.</summary>
    None,

    /// <summary>Each series on top of the one before, so the stack's height is the total.</summary>
    Normal,

    /// <summary>Stacked, with each stack drawn to 100%, so the parts show as shares of the whole.</summary>
    Percent,
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

/// <summary>Whether a chart has a legend, and where it goes.</summary>
public enum ChartLegend
{
    /// <summary>
    /// A Chart's legend below it, when there are two series or more (a single series is named by
    /// the chart's title); a PieChart's beside the circle when there's room, and below it otherwise.
    /// </summary>
    Auto,

    /// <summary>Above the chart, even for one series.</summary>
    Top,

    /// <summary>Below the chart, even for one series.</summary>
    Bottom,

    /// <summary>
    /// Beside the chart on the start side (the left, in a left-to-right page), in a column; below
    /// 30rem wide, above it instead.
    /// </summary>
    Start,

    /// <summary>
    /// Beside the chart on the end side (the right, in a left-to-right page), in a column; below
    /// 30rem wide, below it instead.
    /// </summary>
    End,

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

/// <summary>How a <see cref="Sparkline"/> draws its values.</summary>
public enum SparklineType
{
    /// <summary>A line through the values.</summary>
    Line,

    /// <summary>A line with the space under it filled.</summary>
    Area,

    /// <summary>A bar for each value, up from zero (down, for a value under it).</summary>
    Bar,
}

/// <summary>Where a series writes each value beside its bar or point.</summary>
public enum ChartLabels
{
    /// <summary>No labels.</summary>
    None,

    /// <summary>
    /// Placed by type: past a bar's end, in the middle of a stacked segment, above a point. A label
    /// with no room there moves inside the bar or below the point, or is left out.
    /// </summary>
    Auto,

    /// <summary>Inside the bar, at its end (in the middle of a stacked segment), or below a point.</summary>
    Inside,

    /// <summary>Past the bar's end, or above a point, moving inside or below when there's no room.</summary>
    Outside,
}

/// <summary>What a series's labels do when they'd overlap other labels.</summary>
public enum ChartLabelOverlap
{
    /// <summary>A label that would overlap one placed before it is left out at the widths where it would.</summary>
    Hide,

    /// <summary>
    /// Every label shows, overlapping or not. They're placed before other series' labels, which
    /// step around them. A label still needs room in the plot (or in its bar, inside).
    /// </summary>
    Show,
}

/// <summary>How a <see cref="ChartArgumentAxis"/> fits the names along the bottom when they don't all fit side by side.</summary>
public enum ChartLabelFit
{
    /// <summary>Names that don't fit are skipped: every other one, then every fourth, and so on.</summary>
    Skip,

    /// <summary>Names are turned 45° before any are skipped, so more of them show.</summary>
    Rotate,

    /// <summary>Every name shows, wrapped onto more lines in its band. Suits a few long names.</summary>
    Wrap,
}
