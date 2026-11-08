using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>What a chart draws for its RangeSelector.</summary>
internal interface IRangeSelector
{
    bool Ranges { get; }

    IEnumerable<DateRangePreset>? Presets { get; }

    Length? Height { get; }

    string? Label { get; }

    bool HideFlags { get; }
}

/// <summary>Tells a Chart it's a RangeSelector on its own: the strip only, no plot.</summary>
internal sealed class RangeSelectorHost;

/// <summary>
/// A strip for choosing a range of a chart's arguments: the whole range drawn small, with a window
/// over the part chosen. Drag the window to pan, its two handles to widen or narrow it, or click
/// the strip to move there; each handle is a slider for the keyboard.
/// <para>
/// Inside a <see cref="Chart{TItem}"/>, <c>&lt;RangeSelector /&gt;</c> sits under the plot,
/// draws the chart's series and zooms it (turning on <see cref="ChartArgumentAxis.Zoom"/>); the
/// chart's <c>View</c> says what's in view. On its own it draws its own <see cref="Items"/> by the
/// series inside it, and <see cref="View"/> is the range chosen, such as dates to filter a table
/// by: <c>&lt;RangeSelector Items="days" Argument="d =&gt; d.Day" @bind-View="range"&gt;&lt;ChartAreaSeries Value="d =&gt; d.Visits" /&gt;&lt;/RangeSelector&gt;</c>.
/// </para>
/// </summary>
/// <typeparam name="TItem">The items it draws: inside a Chart, the chart's.</typeparam>
[CascadingTypeParameter(nameof(TItem))]
[ModeSupport(NoScript = ModeLevel.Partial, NoScriptNotes = "With JavaScript off, the strip shows the range View sets, and its handles and buttons don't move it.")]
public sealed class RangeSelector<TItem> : ComponentBase, IRangeSelector, IDisposable
{
    private readonly RangeSelectorHost _host = new();

    [CascadingParameter] private ChartContextBase? Context { get; set; }

    [Inject] private IServiceProvider Services { get; set; } = default!;

    /// <summary>On its own: the items it draws, one point per series for each. Inside a Chart it takes the chart's.</summary>
    [Parameter] public IEnumerable<TItem>? Items { get; set; }

    /// <summary>On its own: what each item is at along the strip, such as <c>d =&gt; d.Day</c>: dates, numbers or names.</summary>
    [Parameter] public Expression<Func<TItem, object?>>? Argument { get; set; }

    /// <summary>
    /// On its own: the range chosen, from one argument to another (null: everything). Inside a
    /// Chart, set the chart's <c>View</c> instead.
    /// </summary>
    [Parameter] public ChartView? View { get; set; }

    /// <summary>On its own, on an interactive page: raised with the range chosen as each drag or key settles (<c>@bind-View</c>).</summary>
    [Parameter, ModeSupport(Static = ModeLevel.None, NoScript = ModeLevel.None)]
    public EventCallback<ChartView?> ViewChanged { get; set; }

    /// <summary>On its own: the series it draws, such as <c>&lt;ChartAreaSeries Value="d =&gt; d.Visits" /&gt;</c>. None draws only the scale.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Its name for screen readers. "Range in view" by default.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>
    /// Buttons for common spans back from the last date: 1 month, 3 and 6 months, the year to
    /// date, a year, and all. For dates.
    /// </summary>
    [Parameter] public bool Ranges { get; set; }

    /// <summary>Spans of your own to choose in a click, by their first and last days, beside or in place of <see cref="Ranges"/>.</summary>
    [Parameter] public IEnumerable<DateRangePreset>? Presets { get; set; }

    /// <summary>The strip's height. 4rem by default.</summary>
    [Parameter] public Length? Height { get; set; }

    /// <summary>
    /// Leaves out the flags over the handles that say where the range starts and ends. Under a
    /// chart they show only while a handle is pointed at, focused or dragged; on their own, always.
    /// Screen readers still hear each handle's value.
    /// </summary>
    [Parameter] public bool HideFlags { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Context is null) return;
        if (Items is not null || Argument is not null || View is not null || ViewChanged.HasDelegate || ChildContent is not null)
        {
            throw new InvalidOperationException("Inside a Chart, RangeSelector draws the chart's items and series: leave out Items, Argument and series, and set the chart's View.");
        }

        Context.Selector = this;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Context is not null)
        {
            Context.Plot?.Refresh();
            return;
        }

        if (Items is null || Argument is null)
        {
            throw new InvalidOperationException("RangeSelector on its own needs Items and an Argument, such as Argument=\"d => d.Day\" (or put it inside a Chart).");
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (Context is not null) return;

        // On its own: a Chart of the strip only, its View the range chosen.
        builder.OpenComponent<CascadingValue<RangeSelectorHost>>(0);
        builder.AddComponentParameter(1, "Value", _host);
        builder.AddComponentParameter(2, "IsFixed", true);
        builder.AddComponentParameter(3, "ChildContent", (RenderFragment)(b =>
        {
            b.OpenComponent<Chart<TItem>>(0);
            b.AddComponentParameter(1, nameof(Chart<TItem>.Items), Items);
            b.AddComponentParameter(2, nameof(Chart<TItem>.Argument), Argument);
            b.AddComponentParameter(3, nameof(Chart<TItem>.Title), Label ?? IzzyText.From(Services).RangeSelectorName);
            b.AddComponentParameter(4, nameof(Chart<TItem>.HideTitle), true);
            b.AddComponentParameter(5, nameof(Chart<TItem>.Legend), ChartLegend.None);
            b.AddComponentParameter(6, nameof(Chart<TItem>.DataTable), ChartDataTable.None);
            b.AddComponentParameter(7, nameof(Chart<TItem>.View), View);
            b.AddComponentParameter(8, nameof(Chart<TItem>.ViewChanged), ViewChanged);
            b.AddComponentParameter(9, nameof(Chart<TItem>.ChildContent), (RenderFragment)(c =>
            {
                c.AddContent(0, ChildContent);
                c.OpenComponent<RangeSelector<TItem>>(1);
                c.AddComponentParameter(2, nameof(Ranges), Ranges);
                c.AddComponentParameter(3, nameof(Presets), Presets);
                c.AddComponentParameter(4, nameof(Height), Height);
                c.AddComponentParameter(5, nameof(Label), Label);
                c.AddComponentParameter(6, nameof(HideFlags), HideFlags);
                c.CloseComponent();
            }));
            b.CloseComponent();
        }));
        builder.CloseComponent();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Context?.Selector == this)
        {
            Context.Selector = null;
            Context.Plot?.Refresh();
        }
    }
}
