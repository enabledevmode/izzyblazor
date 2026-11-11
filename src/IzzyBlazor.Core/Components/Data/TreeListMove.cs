using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// A row of a <see cref="TreeList{TItem}"/> moved under another, or to the top level, for its
/// <c>OnReparent</c> to save: set the row's parent and its place among its new siblings. With an
/// error added, the move doesn't happen and the dialog stays open with the message.
/// </summary>
/// <typeparam name="TItem">The type of the rows.</typeparam>
public sealed class TreeListMove<TItem>
{
    private readonly Action<string> _addError;

    internal TreeListMove(TItem item, TItem? parent, int index, Action<string> addError) =>
        (Item, Parent, Index, _addError) = (item, parent, index, addError);

    /// <summary>The row moved.</summary>
    public TItem Item { get; }

    /// <summary>The row it goes under, or default for the top level.</summary>
    public TItem? Parent { get; }

    /// <summary>Where it goes among its new siblings, from 0: after them all, from the Move dialog.</summary>
    public int Index { get; }

    /// <summary>Whether an error was added, which stops the move.</summary>
    public bool HasErrors { get; private set; }

    /// <summary>Stops the move and says why, in the dialog.</summary>
    public void AddError(string message)
    {
        HasErrors = true;
        _addError(message);
    }
}

/// <summary>What the Move dialog posts: the key of the row chosen to go under, or empty for the top level.</summary>
internal sealed class TreeListMovePost
{
    public string? Parent { get; set; }
}

/// <summary>What a drop on a static page posts: the row dragged, the row it landed on, and where (before, after or into).</summary>
internal sealed class TreeListDropPost
{
    public string? Key { get; set; }

    public string? Near { get; set; }

    public string? At { get; set; }
}

/// <summary>Receives a drop's post on a static page, inside the tree list's form scope.</summary>
internal sealed class TreeListDropBinding : ComponentBase
{
    internal const string Prefix = "ib-drop";

    [SupplyParameterFromForm(Name = Prefix)]
    public TreeListDropPost? Posted { get; set; }

    [Parameter] public RenderFragment<TreeListDropPost?>? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, ChildContent, Posted);
}

/// <summary>A choice in the Move dialog's tree.</summary>
internal sealed record TreeListMoveTarget(string Key, string Text, List<TreeListMoveTarget> Children);

/// <summary>Receives the Move dialog's post on a static page, inside the tree list's form scope.</summary>
internal sealed class TreeListMoveBinding : ComponentBase
{
    internal const string Prefix = "ib-move";

    [SupplyParameterFromForm(Name = Prefix)]
    public TreeListMovePost? Posted { get; set; }

    [Parameter] public RenderFragment<TreeListMovePost?>? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, ChildContent, Posted);
}
