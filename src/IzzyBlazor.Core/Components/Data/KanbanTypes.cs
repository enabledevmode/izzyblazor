using System.ComponentModel.DataAnnotations;
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

/// <summary>A <see cref="Kanban{TItem}"/>'s card form: what it posts, on a static page too.</summary>
public sealed class KanbanEditFields
{
    /// <summary>The title. Required.</summary>
    [Required]
    public string? Title { get; set; }

    /// <summary>The description, when the board has an <c>ItemDescription</c>.</summary>
    public string? Description { get; set; }

    /// <summary>The column: a <see cref="KanbanColumn{TItem}"/>'s <c>Value</c>.</summary>
    public string? Column { get; set; }

    /// <summary>Set by the form's Delete button on a static page.</summary>
    public bool Remove { get; set; }
}

/// <summary>
/// A save from a <see cref="Kanban{TItem}"/>'s form, for its <c>OnSave</c>: the form's values, and
/// the card they're for (none for a new one). With no errors added, the form closes; with errors,
/// it stays open and shows them.
/// </summary>
/// <typeparam name="TItem">The type of the cards.</typeparam>
public sealed class KanbanSave<TItem>
{
    private readonly Action<string> _error;

    internal KanbanSave(TItem? item, bool isNew, KanbanEditFields fields, Action<string> error) =>
        (Item, IsNew, Fields, _error) = (item, isNew, fields, error);

    /// <summary>The card being edited, or the default for a new one.</summary>
    public TItem? Item { get; }

    /// <summary>Whether it's a new card.</summary>
    public bool IsNew { get; }

    /// <summary>The form's values.</summary>
    public KanbanEditFields Fields { get; }

    /// <summary>The title.</summary>
    public string Title => Fields.Title ?? "";

    /// <summary>The description, when the board has one.</summary>
    public string? Description => Fields.Description;

    /// <summary>The column: one of the board's, by its <c>Value</c>.</summary>
    public string Column => Fields.Column ?? "";

    internal bool HasErrors { get; private set; }

    /// <summary>Reports a problem with the save, shown at the top of the form, which stays open.</summary>
    public void AddError(string message)
    {
        HasErrors = true;
        _error(message);
    }
}

/// <summary>A delete from a <see cref="Kanban{TItem}"/>'s form, for its <c>OnDelete</c>. With an error added, the form stays open and shows it.</summary>
/// <typeparam name="TItem">The type of the cards.</typeparam>
public sealed class KanbanDelete<TItem>
{
    private readonly Action<string> _error;

    internal KanbanDelete(TItem item, Action<string> error) => (Item, _error) = (item, error);

    /// <summary>The card to delete.</summary>
    public TItem Item { get; }

    internal bool HasErrors { get; private set; }

    /// <summary>Reports why it can't be deleted, shown at the top of the form, which stays open.</summary>
    public void AddError(string message)
    {
        HasErrors = true;
        _error(message);
    }
}

/// <summary>What a <c>Kanban</c>'s <c>EditDialogForm</c> gets.</summary>
/// <typeparam name="TItem">The type of the cards.</typeparam>
public sealed class KanbanEditFormContext<TItem>
{
    private readonly Func<Task> _close;

    internal KanbanEditFormContext(TItem? item, bool isNew, string column, Func<Task> close) =>
        (Item, IsNew, Column, _close) = (item, isNew, column, close);

    /// <summary>The card as the board has it, which isn't a copy, or the default for a new one.</summary>
    public TItem? Item { get; }

    /// <summary>Whether it's a new card.</summary>
    public bool IsNew { get; }

    /// <summary>The card's column, or for a new card the column whose add button opened the form.</summary>
    public string Column { get; }

    /// <summary>Closes the dialog, once your form has saved.</summary>
    public Task CloseAsync() => _close();
}

/// <summary>Gets a static post of the card form. A part of its own for <c>FormMappingScope</c>.</summary>
internal sealed class KanbanEditBinding : ComponentBase
{
    internal const string FormName = "edit";

    internal const string Prefix = "ib-kanban-edit";

    [SupplyParameterFromForm(Name = Prefix)]
    public KanbanEditFields? Posted { get; set; }

    [Parameter] public RenderFragment<KanbanEditFields?>? ChildContent { get; set; }

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
