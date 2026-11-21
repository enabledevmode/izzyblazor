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
/// What a <see cref="Kanban{TItem}"/> posts on a static page: its cards' keys in the order shown,
/// each one's column, the move a card's menu asked for, and the collapsed columns. Bind it with
/// <c>[SupplyParameterFromForm(FormName = "...", Name = "...")]</c> under the board's
/// <c>Name</c>, pass it back as <c>Order</c> so the board shows it, and save
/// <see cref="Arrange{TItem}"/>.
/// </summary>
/// <remarks>
/// Everything here came from the browser. Arrange only places the cards it's given: unknown keys
/// are dropped and cards the post left out keep their place. It doesn't know which columns the
/// board has or what each one accepts: check the columns it gives before saving them.
/// </remarks>
public sealed class KanbanOrder
{
    private ListOrder? _list;

    /// <summary>The cards' keys, in the order the board showed them.</summary>
    public string[]? Keys { get; set; }

    /// <summary>Each card's column, in the same order as <see cref="Keys"/>, as the board wrote it.</summary>
    public string[]? Zones { get; set; }

    /// <summary>The move a card's menu posted; null when the form's own submit posted.</summary>
    public string? Move { get; set; }

    /// <summary>The collapsed columns, as the board wrote them; read them with <see cref="IsCollapsed"/>.</summary>
    public string[]? Collapsed { get; set; }

    /// <summary>Every card with its column and its place in it, in the posted order, with the posted move applied.</summary>
    /// <typeparam name="TItem">The type of the cards.</typeparam>
    /// <param name="items">The cards, in the order they had before the post.</param>
    /// <param name="key">The board's <c>ItemKey</c>.</param>
    /// <param name="columnOf">The board's <c>ColumnOf</c>: a card's column before the post, for cards the post left out.</param>
    public IReadOnlyList<KanbanPlace<TItem>> Arrange<TItem>(IReadOnlyList<TItem> items, Func<TItem, string> key, Func<TItem, string> columnOf)
    {
        ArgumentNullException.ThrowIfNull(columnOf);
        var places = ToListOrder().Arrange(items, key, item => KanbanZone.Of(columnOf(item)));
        var board = new List<KanbanPlace<TItem>>(places.Count);
        foreach (var place in places)
        {
            board.Add(new(place.Item, KanbanZone.Column(place.Zone), place.Index));
        }

        return board;
    }

    /// <summary>Whether the post had a column collapsed.</summary>
    /// <param name="column">The column's <c>Value</c>.</param>
    public bool IsCollapsed(string column) => Collapsed is not null && Array.IndexOf(Collapsed, KanbanZone.Of(column)) >= 0;

    internal ListOrder ToListOrder() => _list ??= new ListOrder { Keys = Keys, Zones = Zones, Move = Move };
}

/// <summary>A card's place after a post to a <see cref="Kanban{TItem}"/>: its column, and its index among that column's cards.</summary>
/// <typeparam name="TItem">The type of the cards.</typeparam>
/// <param name="Item">The card.</param>
/// <param name="Column">Its column: a <see cref="KanbanColumn{TItem}"/>'s <c>Value</c>.</param>
/// <param name="Index">Its place in the column, from 0.</param>
public readonly record struct KanbanPlace<TItem>(TItem Item, string Column, int Index);

/// <summary>
/// A column's drop zone: its value written without the spaces and colons a zone can't hold, so a
/// column can be named as the data names it ("In progress").
/// </summary>
internal static class KanbanZone
{
    public static string Of(string column) => Uri.EscapeDataString(column);

    public static string Column(string zone) => Uri.UnescapeDataString(zone);
}
