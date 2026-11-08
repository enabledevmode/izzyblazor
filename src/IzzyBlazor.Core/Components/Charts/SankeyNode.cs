using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// One node of a <see cref="SankeyChart{TItem}"/> that needs its own color or label, such as
/// <c>&lt;SankeyNode Name="Net profit" Color="var(--ib-color-success)" /&gt;</c>. Nodes come from
/// the chart's flows; this only changes how one looks. It renders nothing itself.
/// </summary>
[ModeSupport]
public sealed class SankeyNode : ComponentBase, IDisposable
{
    [CascadingParameter] private SankeyNodes? Nodes { get; set; }

    /// <summary>The node's name, as the flows' <c>From</c> or <c>To</c> give it, such as "Net profit".</summary>
    [Parameter, EditorRequired] public string Name { get; set; } = "";

    /// <summary>
    /// The node's color, such as <c>var(--ib-color-success)</c> or <c>#2e7d32</c>, in place of the
    /// palette's next. Flows take it too when <c>LinkColor</c> colors them by this node.
    /// </summary>
    [Parameter] public string? Color { get; set; }

    /// <summary>The text shown for the node, when its name is an id or code, such as "Net profit" for <c>NP</c>.</summary>
    [Parameter] public string? Label { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Nodes is null)
        {
            throw new InvalidOperationException("SankeyNode must be inside a SankeyChart.");
        }

        Nodes.Add(this);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("A SankeyNode needs a Name, the node's name as the flows give it, such as \"Net profit\".");
        }

        Nodes!.Seen(this);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
    }

    /// <inheritdoc />
    public void Dispose() => Nodes?.Remove(this);
}

/// <summary>A SankeyChart's node options, in page order; the diagram draws again when they change.</summary>
internal sealed class SankeyNodes
{
    private readonly ChildOrder<SankeyNode> _nodes = new();

    public IReadOnlyList<SankeyNode> Items => _nodes.Items;

    public ChartPlot? Plot { get; set; }

    public void BeginPass() => _nodes.BeginPass();

    public void Add(SankeyNode node) => _nodes.Add(node);

    public void Seen(SankeyNode node)
    {
        _nodes.Seen(node);
        Plot?.Refresh();
    }

    public void Remove(SankeyNode node)
    {
        if (_nodes.Remove(node))
        {
            Plot?.Refresh();
        }
    }
}
