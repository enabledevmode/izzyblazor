using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>A card's move on a <see cref="Kanban{TItem}"/>, before the board changes.</summary>
/// <typeparam name="TItem">The type of the cards.</typeparam>
public sealed class KanbanMoveEventArgs<TItem>
{
    /// <summary>The card that moves.</summary>
    public required TItem Item { get; init; }

    /// <summary>Its key.</summary>
    public required string Key { get; init; }

    /// <summary>The column it moves from: a <see cref="KanbanColumn{TItem}"/>'s <c>Value</c>.</summary>
    public required string FromColumn { get; init; }

    /// <summary>The column it moves to; the same as <see cref="FromColumn"/> when it only changes place.</summary>
    public required string ToColumn { get; init; }

    /// <summary>Its place before the move, from 0, among its column's cards.</summary>
    public required int FromIndex { get; init; }

    /// <summary>Its place after the move, from 0, among the cards of the column it moves to.</summary>
    public required int ToIndex { get; init; }

    /// <summary>Set to keep the card where it was, such as when saving the move failed.</summary>
    public bool Cancel { get; set; }
}

/// <summary>
/// What a <see cref="Kanban{TItem}"/>'s own form posts on a static page: its cards' keys in the
/// order shown, each one's zone, the move a card's menu asked for or the card the script moved,
/// and the collapsed columns. Everything here came from the browser.
/// </summary>
internal sealed class KanbanPost
{
    private ListOrder? _list;

    public string[]? Keys { get; set; }

    public string[]? Zones { get; set; }

    public string? Move { get; set; }

    public string? Dropped { get; set; }

    public string[]? Collapsed { get; set; }

    /// <summary>Whether it's a post, not the empty model of a page that was only loaded.</summary>
    public bool Posted => Keys is not null || Move is not null;

    /// <summary>The card that moved: the menu's, or the one the script moved.</summary>
    public string? MovedKey => ListMove.TryParse(Move, out var move) ? move.Key : string.IsNullOrEmpty(Dropped) ? null : Dropped;

    public bool IsCollapsed(string column) => Collapsed is not null && Array.IndexOf(Collapsed, KanbanZone.Of(column)) >= 0;

    public ListOrder ToListOrder() => _list ??= new ListOrder { Keys = Keys, Zones = Zones, Move = Move, Dropped = Dropped };
}

/// <summary>Gets a static post's values. A part of its own for <c>FormMappingScope</c>, which keeps each Kanban's post to itself.</summary>
internal sealed class KanbanPostBinding : ComponentBase
{
    /// <summary>The form's name within its Kanban's scope.</summary>
    internal const string FormName = "move";

    /// <summary>What the fields' names start with.</summary>
    internal const string Prefix = "ib-kanban";

    [SupplyParameterFromForm(Name = Prefix)]
    public KanbanPost? Posted { get; set; }

    [Parameter] public RenderFragment<KanbanPost?>? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, ChildContent, Posted);
}

/// <summary>
/// A column's drop zone: its value written without the spaces and colons a zone can't hold, so a
/// column can be named as the data names it ("In progress").
/// </summary>
internal static class KanbanZone
{
    public static string Of(string column) => Uri.EscapeDataString(column);

    public static string Column(string zone) => Uri.UnescapeDataString(zone);
}
