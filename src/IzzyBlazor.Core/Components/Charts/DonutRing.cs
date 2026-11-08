using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// One ring of a <see cref="NestedDonutChart{TItem}"/>: a measure of the chart's items, such as
/// <c>&lt;DonutRing Value="c =&gt; c.Exports" Name="Exports" /&gt;</c>. The first ring is the
/// outer one. It renders nothing itself.
/// </summary>
[ModeSupport]
public sealed class DonutRing<TItem> : ComponentBase, IDisposable
{
    [CascadingParameter] private DonutRings<TItem>? Rings { get; set; }

    /// <summary>Each item's slice of the ring, such as <c>c =&gt; c.Exports</c>: any number. The slices share the ring by their values.</summary>
    [Parameter, EditorRequired] public Expression<Func<TItem, object?>> Value { get; set; } = default!;

    /// <summary>
    /// The ring's name, such as "Exports", under the chart's title, in the tooltip and as its column
    /// in the data table. Defaults to the property <see cref="Value"/> reads, in words.
    /// </summary>
    [Parameter] public string? Name { get; set; }

    private Expression<Func<TItem, object?>>? _lastValue;

    internal Func<TItem, object?> Read { get; private set; } = default!;

    internal string DisplayName { get; private set; } = "";

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Rings is null)
        {
            throw new InvalidOperationException("DonutRing must be inside a NestedDonutChart.");
        }

        Rings.Add(this);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Value is null)
        {
            throw new InvalidOperationException("A DonutRing needs a Value, each item's slice of the ring, such as c => c.Exports.");
        }

        if (!ReferenceEquals(Value, _lastValue))
        {
            _lastValue = Value;
            Read = Value.Compile();
        }

        DisplayName = Name ?? ChartMembers.Name(Value)
            ?? throw new InvalidOperationException("A DonutRing needs a Name, such as \"Exports\", when its Value isn't a property.");
        Rings!.Seen(this);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
    }

    /// <inheritdoc />
    public void Dispose() => Rings?.Remove(this);
}

/// <summary>A NestedDonutChart's rings, in page order; the circle draws again when they change.</summary>
internal sealed class DonutRings<TItem>
{
    private readonly ChildOrder<DonutRing<TItem>> _rings = new();

    public IReadOnlyList<DonutRing<TItem>> Items => _rings.Items;

    public ChartPlot? Plot { get; set; }

    public void BeginPass() => _rings.BeginPass();

    public void Add(DonutRing<TItem> ring) => _rings.Add(ring);

    public void Seen(DonutRing<TItem> ring)
    {
        _rings.Seen(ring);
        Plot?.Refresh();
    }

    public void Remove(DonutRing<TItem> ring)
    {
        if (_rings.Remove(ring))
        {
            Plot?.Refresh();
        }
    }
}
