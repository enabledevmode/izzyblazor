using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace IzzyBlazor;

// Moving rows by a handle at their start: a drag, or the handle's menu of moves.
public abstract partial class GridViewBase<TItem>
{
    internal bool ReorderableRows { get; set; }

    internal EventCallback<IReadOnlyList<TItem>> ItemsChanged { get; set; }

    internal EventCallback<ListMoveEventArgs<TItem>> OnMove { get; set; }

    internal Func<TItem, bool>? ItemMovable { get; set; }

    internal string? OrderName { get; set; }

    internal ListOrder? Order { get; set; }

    internal bool SubmitOnMove { get; set; }

    private string? _moveFocusKey;
    private string? _moveAnnouncement;
    private int _rowMoveSeq;
    private bool _rowMoving;
    private bool _reorderWarned;

    // Interactive (or prerendering for it): C# applies the moves.
    private bool MovesLive => WillBeInteractive;

    // A static page without an OrderName: nothing could save a move, so no handles.
    private bool HasGrip => ReorderableRows && (MovesLive || OrderName is not null);

    // The order posts with the form: a static page with an OrderName.
    private bool PostsOrder => HasGrip && !MovesLive;

    /// <inheritdoc />
    internal override int GripSpan => HasGrip ? 1 : 0;

    // The rows are in the grid's own order, so a move has a place in it.
    private bool InOwnOrder =>
        _loadedRequest is { } request && request.Sort.Count == 0 && request.Filter is null && string.IsNullOrEmpty(request.Search) && !GroupedNow;

    // Why no row can move now, if none can.
    private string? MoveBlockedReason =>
        !InOwnOrder ? TextDefaults.MoveRowsUnavailable
        : RowEditKey is not null || _cellKey is not null ? TextDefaults.MoveRowsEditing
        : null;

    private bool CanMove(TItem item) => ItemMovable?.Invoke(item) != false;

    private string OrderKeysName => $"{OrderName}.Keys";

    private string OrderMoveName => $"{OrderName}.Move";

    private void CheckReorder()
    {
        if (!ReorderableRows)
        {
            return;
        }

        if (ItemKey is null)
        {
            throw new InvalidOperationException($"A DataGrid with {nameof(ReorderableRows)} needs an {nameof(ItemKey)}, such as ItemKey=\"s => s.Id\": it's what posts and what a move names.");
        }

        // A move on one page, or among the rows a provider returned, has no well-defined place in the whole list.
        if (ItemsProvider is not null || PageSize is not null || PageSizeOptions is { Count: > 0 } || Virtualize || Cursor is not null || Paging == DataPaging.LoadOnScroll)
        {
            throw new InvalidOperationException($"A DataGrid with {nameof(ReorderableRows)} shows every row: give it {nameof(Items)}, and leave out {nameof(ItemsProvider)}, {nameof(PageSize)}, {nameof(PageSizeOptions)}, {nameof(Virtualize)} and paging.");
        }

        // Every visit would start with the handles off.
        if (InitialSort is not null || InitialGroup is not null)
        {
            throw new InvalidOperationException($"A DataGrid with {nameof(ReorderableRows)} starts in its own order, so it can't take an {(InitialSort is not null ? nameof(InitialSort) : nameof(InitialGroup))}.");
        }

        if (PostsOrder && (CanEdit || CanDelete))
        {
            throw new InvalidOperationException($"On a static page, a DataGrid with {nameof(ReorderableRows)} posts its order with a form around it, and one that edits or deletes rows can't be inside a form, as forms don't nest. Make the page interactive, or move rows and edit them in separate grids.");
        }

        if (!HasGrip && !_reorderWarned)
        {
            _reorderWarned = true;
            Logger?.LogWarning("A DataGrid with ReorderableRows on a static page has no OrderName, so it shows no handles: give it an OrderName inside a form, and pass the posted ListOrder back as Order.");
        }
    }

    // A static post: the posted order, then the menu's move.
    private void ArrangePosted()
    {
        _moveFocusKey = null;
        if (!PostsOrder || Order is null || !InOwnOrder)
        {
            return;
        }

        var entries = ListArrangement.Order(_items, KeyOf, Order.Keys, zones: null);
        if (ListMove.TryParse(Order.Move, out var move) && move.Kind is not ListMoveKind.To
            && entries.Find(e => e.Key == move.Key) is { Key: not null } entry && CanMove(entry.Item))
        {
            ListArrangement.Apply(entries, move);
            _moveFocusKey = move.Key;
        }

        _items = entries.ConvertAll(e => e.Item);
    }

    private Task MoveRowAsync(ListMove move) => ApplyRowMoveAsync(move.Key, entries => ListArrangement.Apply(entries, move));

    // A drag on an interactive page. The script holds the dragged row where it was dropped until a
    // render carries this count, so it's counted once the move is done, whatever comes of it.
    private async Task DropRowAsync(IzzyMoveEventArgs e)
    {
        try
        {
            if (e.Key is { } key)
            {
                await ApplyRowMoveAsync(key, entries => ListArrangement.MoveTo(entries, key, null, e.ToIndex));
            }
        }
        finally
        {
            _rowMoveSeq = Math.Max(_rowMoveSeq, e.Seq);
        }
    }

    // One move at a time: one that arrives while OnMove awaits would start from the old order.
    private async Task ApplyRowMoveAsync(string key, Func<List<ListEntry<TItem>>, ListMoved?> move)
    {
        if (_rowMoving || MoveBlockedReason is not null)
        {
            return;
        }

        _rowMoving = true;
        try
        {
            var entries = _items.Select(i => new ListEntry<TItem>(i, KeyOf(i), null)).ToList();
            var at = entries.FindIndex(e => e.Key == key);
            if (at < 0 || !CanMove(entries[at].Item) || move(entries) is not { } moved)
            {
                return;
            }

            var item = entries[moved.ToIndex].Item;
            var args = new ListMoveEventArgs<TItem> { Item = item, Key = key, FromIndex = moved.FromIndex, ToIndex = moved.ToIndex };
            await OnMove.InvokeAsync(args);
            if (args.Cancel)
            {
                _moveAnnouncement = string.Format(CultureInfo.CurrentCulture, TextDefaults.MoveCancelled, LabelOf(item));
                return;
            }

            var items = entries.ConvertAll(e => e.Item);
            _items = items;
            _moveAnnouncement = string.Format(CultureInfo.CurrentCulture, TextDefaults.MovedTo, LabelOf(item), moved.ToIndex + 1, entries.Count);
            await ItemsChanged.InvokeAsync(items);
        }
        finally
        {
            _rowMoving = false;
        }
    }
}
