using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// One outline on a <see cref="RadarChart{TItem}"/>: a value at each spoke, joined around the
/// circle, with a marker at each. Renders nothing itself; the chart draws it.
/// </summary>
/// <typeparam name="TItem">The chart's item type.</typeparam>
[ModeSupport]
public sealed class RadarSeries<TItem> : ComponentBase, IDisposable
{
    private Expression<Func<TItem, object?>>? _lastValue;
    private Func<TItem, object?>? _value;
    private Expression<Func<TItem, object?>>? _lastSeriesBy;
    private Func<TItem, object?>? _seriesBy;
    private string? _memberName;

    [CascadingParameter] private RadarContext<TItem>? Context { get; set; }

    /// <summary>The value at each spoke, such as <c>s =&gt; s.Score</c>: any number, or null for none (a gap in the outline).</summary>
    [Parameter, EditorRequired] public Expression<Func<TItem, object?>> Value { get; set; } = default!;

    /// <summary>
    /// The series' name, in the legend, the tooltip and the data table. Defaults to the property's
    /// <c>[Display]</c> name, or its name in words.
    /// </summary>
    [Parameter] public string? Name { get; set; }

    /// <summary>
    /// Splits the series into one per value of this, such as <c>s =&gt; s.Player</c>, for items
    /// that each hold one value: rows of (player, skill, score) become an outline per player. Each
    /// is named by its value and takes the next color and marker, in the order the values first
    /// appear. <see cref="Color"/> doesn't apply to a split series.
    /// </summary>
    [Parameter] public Expression<Func<TItem, object?>>? SeriesBy { get; set; }

    /// <summary>
    /// A CSS color, such as <c>var(--ib-color-danger)</c>. Defaults to the next of the chart
    /// palette's eight colors.
    /// </summary>
    [Parameter] public string? Color { get; set; }

    /// <summary>
    /// Fills the outline lightly in its color. Off by default: area grows with the square of the
    /// values, so a fill makes differences look larger than they are, and fills cover each other.
    /// An outline with a gap isn't filled.
    /// </summary>
    [Parameter] public bool Fill { get; set; }

    /// <summary>
    /// Writes each value beside its marker, pushed out along its spoke: <see cref="ChartLabels.Auto"/>
    /// (or Outside); <see cref="ChartLabels.None"/> by default. Labels aren't thinned where they
    /// overlap, so use them with one or two series. Screen readers skip them: the data table has
    /// every value.
    /// </summary>
    [Parameter] public ChartLabels Labels { get; set; }

    /// <summary>A .NET format string for this series' labels, such as <c>N0</c>. Defaults to the chart's <c>ValueFormat</c>.</summary>
    [Parameter] public string? LabelFormat { get; set; }

    /// <summary>Starts with the series hidden; it keeps its place in the legend, to show it again, and in the data table.</summary>
    [Parameter] public bool Hidden { get; set; }

    internal string DisplayName => Name ?? _memberName ?? "";

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Context is null)
        {
            throw new InvalidOperationException("RadarSeries must be inside a RadarChart.");
        }

        Context.Series.Add(this);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Value is null)
        {
            throw new InvalidOperationException("RadarSeries needs a Value, such as s => s.Score.");
        }

        if (Labels == ChartLabels.Inside)
        {
            throw new InvalidOperationException("A RadarSeries' labels go beside its markers: Labels takes Auto, Outside or None.");
        }

        if (!ReferenceEquals(Value, _lastValue))
        {
            _lastValue = Value;
            _value = Value.Compile();
            _memberName = ChartMembers.Name(Value);
        }

        if (!ReferenceEquals(SeriesBy, _lastSeriesBy))
        {
            _lastSeriesBy = SeriesBy;
            _seriesBy = SeriesBy?.Compile();
        }

        Context!.Series.Seen(this);
        Context.Plot?.Refresh();
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
    }

    /// <summary>
    /// The series' values by spoke (<paramref name="spokeOf"/> maps each item to its spoke): one
    /// set, or one per SeriesBy key. An item repeated at a spoke keeps the last value.
    /// </summary>
    internal IEnumerable<RadarSeriesData> Read(IReadOnlyList<TItem> items, int[] spokeOf, int spokes)
    {
        var values = items.Select(i => ChartMembers.ToNumber(_value!(i))).ToArray();
        var labels = Labels == ChartLabels.Outside ? ChartLabels.Auto : Labels;
        if (_seriesBy is null)
        {
            yield return Data(DisplayName, Enumerable.Range(0, items.Count)) with { Color = Color };
            yield break;
        }

        var keys = items.Select(_seriesBy).ToArray();
        foreach (var key in keys.Distinct())
        {
            yield return Data(ChartLayout.ArgumentText(key, null, CultureInfo.CurrentCulture), Enumerable.Range(0, items.Count).Where(i => Equals(keys[i], key)));
        }

        RadarSeriesData Data(string name, IEnumerable<int> own)
        {
            var bySpoke = new double?[spokes];
            foreach (var i in own)
            {
                bySpoke[spokeOf[i]] = values[i];
            }

            return new RadarSeriesData(name, bySpoke) { Fill = Fill, Hidden = Hidden, Labels = labels, LabelFormat = LabelFormat };
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Context?.Series.Remove(this) == true)
        {
            Context.Plot?.Refresh();
        }
    }
}

/// <summary>What a <see cref="RadarChart{TItem}"/> shares with its series.</summary>
internal sealed class RadarContext<TItem>
{
    public ChildOrder<RadarSeries<TItem>> Series { get; } = new();

    public ChartPlot? Plot { get; set; }
}
