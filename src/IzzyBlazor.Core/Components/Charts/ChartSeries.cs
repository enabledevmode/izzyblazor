using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// One series of a <see cref="Chart{TItem}"/>: a value from each item, drawn as bars, a line or
/// an area. It renders nothing itself; the chart draws it.
/// </summary>
/// <typeparam name="TItem">The chart's item type.</typeparam>
public sealed class ChartSeries<TItem> : ComponentBase, IDisposable
{
    private Expression<Func<TItem, object?>>? _lastValue;
    private Func<TItem, object?>? _compiled;
    private string? _memberName;
    private Expression<Func<TItem, object?>>? _lastSeriesBy;
    private Func<TItem, object?>? _seriesBy;

    /// <summary>The value to draw, such as <c>m =&gt; m.Revenue</c>: any number, or null for none.</summary>
    [Parameter, EditorRequired] public Expression<Func<TItem, object?>> Value { get; set; } = default!;

    /// <summary>
    /// The series' name, in the legend, the data table and each value's description. Defaults to
    /// the property's <c>[Display]</c> name, or its name in words.
    /// </summary>
    [Parameter] public string? Name { get; set; }

    /// <summary>
    /// Splits the series into one per value of this, such as <c>p =&gt; p.Device</c>: each draws
    /// only its own items' values, is named by its value, and takes the next color and marker, in
    /// the order the values first appear. <see cref="Color"/> doesn't apply to a split series.
    /// </summary>
    [Parameter] public Expression<Func<TItem, object?>>? SeriesBy { get; set; }

    /// <summary>How it's drawn, when that differs from the chart's <see cref="Chart{TItem}.SeriesType"/>.</summary>
    [Parameter] public ChartType? Type { get; set; }

    /// <summary>
    /// A CSS color, such as <c>var(--ib-color-danger)</c> or <c>#0f766e</c>. Defaults to the next
    /// of the chart palette's eight colors (<c>--ib-chart-1</c> to <c>--ib-chart-8</c>).
    /// </summary>
    [Parameter] public string? Color { get; set; }

    /// <summary>
    /// How a line or area's line is drawn. Defaults to the next of solid, dashed, dotted and
    /// dash-dot, so lines differ by more than color; set it to mark a line apart, such as a plan
    /// as <see cref="ChartLineStyle.Dashed"/>.
    /// </summary>
    [Parameter] public ChartLineStyle? LineStyle { get; set; }

    /// <summary>A line's or area's shape between values. Defaults to straight.</summary>
    [Parameter] public ChartCurve Curve { get; set; }

    /// <summary>
    /// Whether a line or area marks each value. Defaults to marking a line's values when it has
    /// 40 or fewer, and not an area's.
    /// </summary>
    [Parameter] public bool? Markers { get; set; }

    /// <summary>Draws a line or area straight across a missing value, instead of leaving a gap.</summary>
    [Parameter] public bool ConnectNulls { get; set; }

    /// <summary>
    /// Writes each value beside its bar or point. Labels that would overlap others are left out at
    /// the widths where they would, and screen readers skip them: the data table has every value.
    /// </summary>
    [Parameter] public ChartLabels Labels { get; set; }

    /// <summary>A .NET format string for this series' labels, such as <c>C0</c>. Defaults to the values' own text.</summary>
    [Parameter] public string? LabelFormat { get; set; }

    /// <summary>
    /// Starts with the series hidden: the chart is drawn as if it weren't there, and it keeps its
    /// place in the legend, to show it again, and in the data table.
    /// </summary>
    [Parameter] public bool Hidden { get; set; }

    [CascadingParameter] private ChartContext<TItem>? Context { get; set; }

    internal string DisplayName => Name ?? _memberName ?? "";

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Context is null)
        {
            throw new InvalidOperationException("ChartSeries must be inside a Chart.");
        }

        Context.Register(this);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Value is null)
        {
            throw new InvalidOperationException("ChartSeries needs a Value, such as m => m.Revenue.");
        }

        if (!ReferenceEquals(Value, _lastValue))
        {
            _lastValue = Value;
            _compiled = Value.Compile();
            _memberName = ChartMembers.Name(Value);
        }

        if (!ReferenceEquals(SeriesBy, _lastSeriesBy))
        {
            _lastSeriesBy = SeriesBy;
            _seriesBy = SeriesBy?.Compile();
        }

        Context!.Seen(this);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
    }

    internal IEnumerable<ChartSeriesData> Read(IReadOnlyList<TItem> items, ChartType chartType)
    {
        var values = items.Select(item => ChartMembers.ToNumber(_compiled!(item))).ToArray();
        if (_seriesBy is null)
        {
            yield return Data(DisplayName, values, Color, chartType);
            yield break;
        }

        // One series per key, in the order the keys first appear; each keeps only its own items.
        var keys = items.Select(_seriesBy).ToArray();
        foreach (var key in keys.Distinct())
        {
            var own = values.Select((v, i) => Equals(keys[i], key) ? v : null).ToArray();
            yield return Data(ChartLayout.ArgumentText(key, null, CultureInfo.CurrentCulture), own, null, chartType);
        }
    }

    private ChartSeriesData Data(string name, double?[] values, string? color, ChartType chartType) =>
        new(name, Type ?? chartType, values)
        {
            Color = color,
            Curve = Curve,
            LineStyle = LineStyle,
            Markers = Markers,
            ConnectNulls = ConnectNulls,
            Hidden = Hidden,
            Labels = Labels,
            LabelFormat = LabelFormat,
        };

    /// <inheritdoc />
    public void Dispose() => Context?.Unregister(this);
}

/// <summary>What a <see cref="Chart{TItem}"/> shares with its series: the series, in order.</summary>
internal sealed class ChartContext<TItem>
{
    private readonly ChildOrder<ChartSeries<TItem>> _series = new();

    public IReadOnlyList<ChartSeries<TItem>> Series => _series.Items;

    public ChartPlot? Plot { get; set; }

    public void BeginPass() => _series.BeginPass();

    public void Seen(ChartSeries<TItem> series)
    {
        _series.Seen(series);

        // A series changed on its own (an interactive page): draw again.
        Plot?.Refresh();
    }

    public void Register(ChartSeries<TItem> series) => _series.Add(series);

    public void Unregister(ChartSeries<TItem> series)
    {
        if (_series.Remove(series))
        {
            Plot?.Refresh();
        }
    }
}

/// <summary>
/// Where a chart draws, rendered after its series so it knows them all. The chart passes the
/// drawing as a fragment that reads the series when it runs.
/// </summary>
internal sealed class ChartPlot : ComponentBase
{
    [Parameter] public RenderFragment? Content { get; set; }

    [Parameter] public Action<ChartPlot>? Attach { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized() => Attach?.Invoke(this);

    internal void Refresh() => StateHasChanged();

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, Content);
}

internal static class ChartMembers
{
    /// <summary>The member a lambda reads, such as Revenue in <c>m =&gt; m.Revenue</c>, as its display name.</summary>
    public static string? Name(LambdaExpression expression)
    {
        var body = expression.Body;
        while (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } convert)
        {
            body = convert.Operand;
        }

        return body is MemberExpression { Member: var member }
            ? member.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? DataFields.Words(member.Name)
            : null;
    }

    public static double? ToNumber(object? value) => value switch
    {
        null => null,
        double d => double.IsFinite(d) ? d : null,
        float f => float.IsFinite(f) ? f : null,
        decimal m => (double)m,
        IConvertible c when value is byte or sbyte or short or ushort or int or uint or long or ulong => c.ToDouble(CultureInfo.InvariantCulture),
        _ => throw new InvalidOperationException($"A chart value must be a number; this one is a {value.GetType().Name}."),
    };
}
