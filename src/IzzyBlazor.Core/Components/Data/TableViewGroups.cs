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

    /// <summary>Cells before the columns, for a grid's groups: in the header, and in a row.</summary>
    internal virtual RenderFragment? LeadHeader => null;

    internal virtual RenderFragment? LeadCell => null;

    /// <summary>How many columns they take.</summary>
    internal virtual int LeadSpan => 0;

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
