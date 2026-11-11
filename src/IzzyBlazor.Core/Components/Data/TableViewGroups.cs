using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

// A view's aggregates and groups, past its rows.
public abstract partial class TableViewBase<TItem>
{
    private DataGroup? _grandTotal;

    /// <summary>Where groups and aggregates come from when there's an <c>ItemsProvider</c>.</summary>
    internal virtual Func<DataGroupRequest<TItem>, ValueTask<DataGroupResult>>? GroupsSource => null;

    // Hidden columns' too, so showing one needs no reload.
    private protected List<DataAggregateField> AggregateFields(params AggregatePlace[] places) =>
        _columns.SelectMany(c => places.Select(c.AggregateIn).OfType<DataAggregate>().Select(t => new DataAggregateField(c.AggregateKey, t))).Distinct().ToList();

    /// <inheritdoc />
    internal override string? LoadProblem =>
        ItemsProvider is null || GroupsSource is not null ? null
        : GroupedNow ? $"A {ViewName} that groups and has an {nameof(ItemsProvider)} needs a GroupsProvider, as the rows it holds are only a page's."
        : _columns.Any(c => c.AggregateIn(AggregatePlace.Footer) is not null) ? $"A {ViewName} with an {nameof(ColumnBase<TItem>.Aggregate)} and an {nameof(ItemsProvider)} needs a GroupsProvider, as the rows it holds are only a page's."
        : null;

    /// <summary>The first cell of every row, before the selection, for a grid's row handles: in the header, an empty one, and a data row's.</summary>
    internal virtual RenderFragment? GripHeader => null;

    internal virtual RenderFragment? GripEmptyCell => null;

    internal virtual RenderFragment? GripCell(TItem item, int row) => null;

    /// <summary>How many columns the handles take: 0 or 1.</summary>
    internal virtual int GripSpan => 0;

    /// <summary>The class of the body's rows group, a grid's list of rows that move.</summary>
    internal virtual string? RowsClass => null;

    /// <summary>A class every data row has, a grid's rows that move.</summary>
    internal virtual string? OwnRowClass => null;

    /// <summary>A class a data row has by its item: a tree's row kept only for a match under it.</summary>
    internal virtual string? ItemRowClass(TItem item) => null;

    /// <summary>The first column's cell content, around what its column draws: a tree's indent and chevron.</summary>
    internal virtual RenderFragment FirstCell(TItem item, RenderFragment content) => content;

    /// <summary>Before the first column's header, in one line with it: a tree's chevron that opens every row.</summary>
    internal virtual RenderFragment? FirstHeaderLead => null;

    /// <summary>A data row's own attributes, for a grid's rows that move.</summary>
    internal virtual IReadOnlyDictionary<string, object>? RowAttributes(TItem item) => null;

    /// <summary>
    /// What keeps a data row and its detail when rows aren't virtual: nothing (rows are kept by
    /// place), or the item's key, where rows move, so focus and a detail's state move with them.
    /// </summary>
    internal virtual object? RowKey(string key) => null;

    /// <summary>The columns before the lead cells: the handles and the selection.</summary>
    internal int StartSpan => GripSpan + (SelectColumn ? 1 : 0);

    /// <summary>Whether a row's radio sits in its first cell, hidden, rather than in a column (SelectOnClick, one row).</summary>
    internal virtual bool SelectsInRow => false;

    /// <summary>Whether the rows have a column of boxes to select them.</summary>
    internal bool SelectColumn => Selectable && !SelectsInRow;

    /// <summary>Cells before the columns, for a grid's groups: in the header, and in a row.</summary>
    internal virtual RenderFragment? LeadHeader => null;

    internal virtual RenderFragment? LeadCell => null;

    /// <summary>How many columns they take.</summary>
    internal virtual int LeadSpan => 0;

    /// <summary>A data row's lead cells: <see cref="LeadCell"/>, or the grid's with its detail button.</summary>
    internal virtual RenderFragment? RowLead(TItem item, int row) => LeadCell;

    /// <summary>What follows a data row: its open detail, on a grid.</summary>
    internal virtual RenderFragment? AfterRow(TItem item, int row, int? indent) => null;

    /// <summary>What follows the new row being added.</summary>
    internal virtual RenderFragment? AfterNewRow(TItem item) => null;

    /// <summary>The rows in their groups, in place of the rows; null when there are no groups to draw.</summary>
    internal virtual RenderFragment? GroupRows => null;

    /// <summary>Whether the rows are grouped by this column.</summary>
    internal virtual bool GroupsBy(ColumnBase<TItem> column) => false;

    private protected ValueTask<DataGroupResult> LoadGroupsAsync(DataGroupRequest<TItem> request) =>
        GroupsSource is { } source && ItemsProvider is not null
            ? source(request)
            : ValueTask.FromResult(request.ToResult(AsQuery(SourceItems ?? [])));

    /// <inheritdoc />
    internal override async ValueTask<Action?> LoadWithRowsAsync(DataRequest<TItem> request)
    {
        var aggregates = AggregateFields(AggregatePlace.Footer);
        if (aggregates.Count == 0)
        {
            return () => _grandTotal = null;
        }

        var result = await LoadGroupsAsync(new DataGroupRequest<TItem>
        {
            Aggregates = aggregates,
            Filter = request.Filter,
            Search = request.Search,
            Fields = request.Fields,
            CancellationToken = request.CancellationToken,
        });
        var total = result.Groups.FirstOrDefault();
        return () => _grandTotal = total;
    }
}
