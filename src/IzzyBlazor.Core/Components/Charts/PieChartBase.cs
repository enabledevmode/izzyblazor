using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// What <see cref="PieChart{TItem}"/> and <see cref="NestedDonutChart{TItem}"/> share: the items
/// and their names, the title, the circle's size and start, the colors, the legend and the data table.
/// </summary>
public abstract class PieChartBase<TItem> : LayoutItemBase, IDisposable
{
    private protected PieChartBase()
    {
    }

    /// <summary>The items, one slice each (in each ring, on a <see cref="NestedDonutChart{TItem}"/>).</summary>
    [Parameter, EditorRequired] public IEnumerable<TItem>? Items { get; set; }

    /// <summary>Each slice's name, such as <c>p =&gt; p.Plan</c>.</summary>
    [Parameter] public Expression<Func<TItem, object?>>? Argument { get; set; }

    /// <summary>
    /// The chart's title, such as "Customers by plan", shown above it as its caption. It also names the chart
    /// for screen readers. A chart needs a Title or <see cref="TitledBy"/>.
    /// </summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>Hides the title, which still names the chart for screen readers.</summary>
    [Parameter] public bool HideTitle { get; set; }

    /// <summary>
    /// Shows a button across from the title that saves the chart as a PNG, named after its title,
    /// in the color scheme on screen. With JavaScript off there's no button.
    /// </summary>
    [Parameter] public bool Download { get; set; }

    /// <summary>
    /// Where the title sits: at the start, over the chart's first label, by default (the left, in
    /// a left-to-right page); <see cref="ChartAlign.Center"/> or <see cref="ChartAlign.End"/>.
    /// </summary>
    [Parameter] public ChartAlign TitleAlign { get; set; }

    /// <summary>
    /// The id of a heading already on the page that titles the chart, such as a card's, in place of
    /// <see cref="Title"/>: the chart takes its name from it and shows no title of its own.
    /// </summary>
    [Parameter] public string? TitledBy { get; set; }

    /// <summary>
    /// Where the first slice starts, in degrees clockwise from the top: 90 starts it at the right,
    /// so it runs down that side. 0 by default.
    /// </summary>
    [Parameter] public double StartAngle { get; set; }

    /// <summary>
    /// How far round the slices go, in degrees: 360 (the default) for a whole circle, 180 for a
    /// half. <c>StartAngle="-90" Sweep="180"</c> runs them across the top half, and the chart takes
    /// only the half's height.
    /// </summary>
    [Parameter] public double Sweep { get; set; } = 360;

    /// <summary>
    /// The circle's largest size. Defaults to 16rem; it's smaller when its container is narrower.
    /// </summary>
    [Parameter] public Length? Size { get; set; }

    /// <summary>A .NET format string for the values, such as <c>C0</c> or <c>N0</c>.</summary>
    [Parameter] public string? ValueFormat { get; set; }

    /// <summary>
    /// Shows a tooltip with the slice's value and share under the pointer or keyboard focus, with
    /// script. Defaults to true.
    /// </summary>
    [Parameter] public bool Tooltip { get; set; } = true;

    /// <summary>
    /// The colors in order: <see cref="ChartPalette.Colors"/> (the theme's eight, the default),
    /// <see cref="ChartPalette.Shades"/> of one color for slices in an order,
    /// <see cref="ChartPalette.Diverging"/> for slices either side of a middle, or your own with
    /// <see cref="ChartPalette.Of"/>.
    /// </summary>
    [Parameter] public ChartPalette? Palette { get; set; }

    /// <summary>
    /// The legend: each slice's name by its color, with its value and share on a
    /// <see cref="PieChart{TItem}"/> (names only on a <see cref="NestedDonutChart{TItem}"/>, whose
    /// values differ ring by ring). By default it's beside the circle, at the end side, when there's
    /// room (30rem, or 44rem with a PieChart's labels outside the circle) and under it otherwise; <see cref="ChartLegend.Start"/> puts it at
    /// the start side, <see cref="ChartLegend.Top"/> and <see cref="ChartLegend.Bottom"/> always
    /// above or below, and <see cref="ChartLegend.None"/> leaves it out. With script, each item hides and
    /// shows its slices, and the rest close up and share the circle.
    /// </summary>
    [Parameter] public ChartLegend Legend { get; set; }

    /// <summary>
    /// Where the legend sits along its edge: centered on the circle, by default (beside it or
    /// under it); <see cref="ChartAlign.Start"/> for the left above or below (in a left-to-right
    /// page) or the top beside it, <see cref="ChartAlign.End"/> for the right or the bottom.
    /// </summary>
    [Parameter] public ChartAlign? LegendAlign { get; set; }

    /// <summary>The values as a table: by default behind a "Show data" button that opens it over the chart; <see cref="ChartDataTable.Collapsed"/> puts it in a disclosure under the chart.</summary>
    [Parameter] public ChartDataTable DataTable { get; set; } = ChartDataTable.Popover;

    private protected readonly string _labelId = IdGenerator.Next("ib-chart");
    private Expression<Func<TItem, object?>>? _lastArgument;
    private protected Func<TItem, object?>? _argument;
    private protected string? _argumentName;
    private protected string? _class;
    private protected string? _style;

    /// <summary>The component's name in its errors, such as "PieChart".</summary>
    private protected abstract string ComponentName { get; }

    // What names the chart: its own caption, or the heading TitledBy points at.
    private protected string NameId => TitledBy ?? _labelId;

    private protected string TitleClass => HideTitle ? "ib-visually-hidden" : TitleAlign switch
    {
        ChartAlign.Center => "ib-chart-label ib-chart-label-center",
        ChartAlign.End => "ib-chart-label ib-chart-label-end",
        _ => "ib-chart-label",
    };

    private protected string LegendListClass => LegendAlign switch
    {
        ChartAlign.Start => "ib-chart-legend ib-pie-legend ib-chart-legend-start",
        ChartAlign.End => "ib-chart-legend ib-pie-legend ib-chart-legend-end",
        _ => "ib-chart-legend ib-pie-legend ib-chart-legend-center",
    };

    // The circle and its legend: beside each other when there's room (Auto, Start, End), or one
    // over the other (chart.css).
    private protected string PieClass => Legend switch
    {
        ChartLegend.None or ChartLegend.Bottom => "ib-pie",
        ChartLegend.Top => "ib-pie ib-pie-lg-first",
        ChartLegend.Start => "ib-pie ib-pie-legended ib-pie-lg-first",
        _ => "ib-pie ib-pie-legended",
    };

    // The circle's size the labels are spread for: Size in pixels or rems, or 16rem.
    private protected double SizeInPixels => Size?.Css is { } css && System.Text.RegularExpressions.Regex.Match(css, @"^([\d.]+)(px|rem)$") is { Success: true } m
        && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? m.Groups[2].Value == "rem" ? v * 16 : v : 256;

    // The items as a list and each one's name.
    /// <summary>The chart takes ChartDrillLevels (PieChart), which can give its arguments in place of Argument.</summary>
    private protected virtual bool Drills => false;

    private protected (IReadOnlyList<TItem> Items, string[] Names) ReadItems()
    {
        var items = Items as IReadOnlyList<TItem> ?? Items?.ToList() ?? [];
        return (items, items.Select(i => ChartLayout.ArgumentText(_argument!(i), null, CultureInfo.CurrentCulture)).ToArray());
    }

    // The turn the first slice starts at, 0 to 1.
    private protected double StartTurn => (StartAngle % 360 + 360) % 360 / 360;

    // The share of the turn the slices fill, 0 to 1.
    private protected double SweepTurn => Sweep / 360;

    // The arc's box in the -1 -1 2 2 viewBox: the whole square, unless Sweep cuts it.
    private protected (double X, double Y, double W, double H) Box => ChartShapes.PieBox(StartTurn, SweepTurn);

    private protected bool Swept => SweepTurn < 0.99999;

    private protected string ViewBox => $"{Num(Box.X)} {Num(Box.Y)} {Num(Box.W)} {Num(Box.H)}";

    /// <summary>
    /// A swept circle's plot is just the arc's box: as wide as its share of the circle's, as tall as
    /// its shape gives. What's placed in it is placed by where it is on the circle (<see cref="Left"/>,
    /// <see cref="Top"/>), and the script reads the box from <c>data-ib-box</c>.
    /// </summary>
    private protected string? PlotStyle => Swept
        ? $"width:min(100%, calc(var(--ib-pie-size, 16rem) * {Num(Box.W / 2)}));aspect-ratio:{Num(Box.W)} / {Num(Box.H)}"
        : null;

    private protected string? BoxData => Swept ? ViewBox : null;

    // A point given in shares of the whole circle's square (0 to 1), as a % of the plot's box.
    private protected string Left(double x) => Num((x * 2 - 1 - Box.X) / Box.W * 100);

    private protected string Top(double y) => Num((y * 2 - 1 - Box.Y) / Box.H * 100);

    private protected string PlotClass => Swept ? "ib-pie-plot ib-pie-swept" : "ib-pie-plot";

    private bool Above => Box.Y + Box.H <= 0.0001;

    private bool Below => Box.Y >= -0.0001;

    /// <summary>A donut's Center: above the middle when the arc is all above it (a top half), below it when all below.</summary>
    private protected string CenterClass(string @class) => !Swept ? @class
        : Above ? @class + " ib-pie-center-above"
        : Below ? @class + " ib-pie-center-below"
        : @class;

    /// <summary>
    /// A swept donut's Center in its box: the square <paramref name="inset"/> of the whole circle
    /// keeps (a share of the side), cut at the middle when the arc is all above or below it.
    /// </summary>
    private protected string? CenterStyle(double inset)
    {
        if (!Swept) return null;
        var (x, y, w, h) = Box;
        double lo = -1 + 2 * inset, hi = 1 - 2 * inset;
        var top = Below ? 0 : lo;
        var bottom = Above ? 0 : hi;
        return $"inset:{Num((top - y) / h * 100)}% {Num((x + w - hi) / w * 100)}% {Num((y + h - bottom) / h * 100)}% {Num((lo - x) / w * 100)}%";
    }

    private protected string? FormatValue(double? value) => value?.ToString(ValueFormat ?? "#,##0.##", CultureInfo.CurrentCulture);

    /// <summary>
    /// What the script draws the circle again from when the legend hides slices: each ring's
    /// values, its inner and outer radius, the start, the shares' formats (P0 and P1 in the page's
    /// culture), how the slices are labeled (0 none, 1 outside, 2 inside) and the circle's size.
    /// </summary>
    private protected string DrawData(double?[][] values, (double Inner, double Outer)[] radii, int labels)
    {
        using var stream = new MemoryStream();
        using (var w = new System.Text.Json.Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteStartArray("v");
            foreach (var ring in values)
            {
                w.WriteStartArray();
                foreach (var v in ring)
                {
                    if (v is { } x && double.IsFinite(x)) w.WriteNumberValue(x);
                    else w.WriteNullValue();
                }

                w.WriteEndArray();
            }

            w.WriteEndArray();
            w.WriteStartArray("r");
            foreach (var (inner, outer) in radii)
            {
                w.WriteStartArray();
                w.WriteNumberValue(Math.Round(inner, 4));
                w.WriteNumberValue(Math.Round(outer, 4));
                w.WriteEndArray();
            }

            w.WriteEndArray();
            w.WriteNumber("f", StartTurn);
            w.WriteNumber("w", SweepTurn);
            WriteShape(w, "p0", NumberShape.Of("P0", CultureInfo.CurrentCulture)!);
            WriteShape(w, "p1", NumberShape.Of("P1", CultureInfo.CurrentCulture)!);
            w.WriteNumber("l", labels);
            w.WriteNumber("s", SizeInPixels);
            w.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteShape(System.Text.Json.Utf8JsonWriter w, string name, NumberShape shape)
    {
        w.WriteStartObject(name);
        w.WriteString("p", shape.Prefix);
        w.WriteString("s", shape.Suffix);
        w.WriteString("np", shape.NegativePrefix);
        w.WriteString("ns", shape.NegativeSuffix);
        w.WriteString("g", shape.Group);
        w.WriteString("d", shape.Decimal);
        w.WriteNumber("n", shape.Decimals);
        w.WriteNumber("t", shape.MinDecimals);
        w.WriteNumber("k", shape.Scale);
        w.WriteEndObject();
    }

    private protected static string Num(double value) => value.ToString("0.#####", CultureInfo.InvariantCulture);

    // The palette's eight colors, then a neutral one for the rest: a ninth slice in the first
    // color would sit beside the first.
    private protected static string Slot(int i) => i < 8 ? $"ib-chart-s{i + 1}" : "ib-chart-s9";

    private protected static string Share(double share) => ChartMembers.Share(share);

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add("ib-chart");
        classes.Add("ib-pie-chart");
        if (Size is { } size) styles.AddVar("pie-size", size.Css);
        base.BuildAttributes(classes, styles);
    }

    /// <summary>
    /// How often the chart reads its data again, at least a second, such as
    /// <c>Refresh="TimeSpan.FromSeconds(30)"</c>. On a static page the page (or
    /// <see cref="RefreshFrom"/>) is fetched again and the chart drawn from it, found by its
    /// <c>id</c>, which it then needs; on an interactive page <see cref="OnRefresh"/> is raised, for
    /// you to load new data. Not while the tab is hidden or the chart is off-screen, nor (static)
    /// while the pointer or focus is in it. Unlike Chart's, it has no Pause button.
    /// </summary>
    [Parameter, ModeSupport(NoScript = ModeLevel.None)]
    public TimeSpan? Refresh { get; set; }

    /// <summary>On a static page with <see cref="Refresh"/>: a lighter page to fetch than this one, which renders the same chart with the same <c>id</c>.</summary>
    [Parameter, ModeSupport(NoScript = ModeLevel.None)]
    public string? RefreshFrom { get; set; }

    /// <summary>On an interactive page with <see cref="Refresh"/>: raised at each interval, to load new data.</summary>
    [Parameter, ModeSupport(Static = ModeLevel.None, NoScript = ModeLevel.None)]
    public EventCallback OnRefresh { get; set; }

    private protected readonly ChartRefresh _refresh = new();

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        base.OnAfterRender(firstRender);
        _refresh.AfterRender(Refresh, RendererInfo, OnRefresh, InvokeAsync, DispatchExceptionAsync);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _refresh.Dispose();
        Disposing();
    }

    /// <summary>What a chart lets go of besides its refresh timer.</summary>
    private protected virtual void Disposing()
    {
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        _refresh.Check(ComponentName, Refresh, RefreshFrom, RendererInfo, AssignedRenderMode, AdditionalAttributes, OnRefresh.HasDelegate);
        if (string.IsNullOrWhiteSpace(Title) == string.IsNullOrWhiteSpace(TitledBy))
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(Title)
                ? $"{ComponentName} needs a Title, such as \"Customers by plan\", or TitledBy, the id of a heading on the page that titles it."
                : $"{ComponentName} takes a Title or TitledBy, not both: TitledBy names it from a heading already on the page.");
        }

        if (!double.IsFinite(Sweep) || Sweep <= 0 || Sweep > 360)
        {
            throw new ArgumentOutOfRangeException(nameof(Sweep), Sweep, $"{ComponentName}'s Sweep is how far round the slices go, more than 0 and up to 360 degrees, such as 180 for a half circle.");
        }

        if (Argument is null && !Drills)
        {
            throw new InvalidOperationException($"{ComponentName} needs an Argument, each slice's name, such as p => p.Plan.");
        }

        if (Argument is not null && !ReferenceEquals(Argument, _lastArgument))
        {
            _lastArgument = Argument;
            _argument = Argument.Compile();
            _argumentName = ChartMembers.Name(Argument);
        }

        base.OnParametersSet();
        (_class, _style) = BuildRootAttributes();
    }
}
