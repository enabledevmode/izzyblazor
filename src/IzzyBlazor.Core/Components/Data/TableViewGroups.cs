using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

// A view's aggregates and groups, past its rows.
public abstract partial class TableViewBase<TItem>
{
    private DataGroup? _grandTotal;

    /// <summary>Where groups and aggregates come from when there's an <c>ItemsProvider</c>.</summary>
    internal virtual Func<DataGroupRequest<TItem>, ValueTask<DataGroupResult>>? GroupsSource => null;

    private protected List<DataAggregateField> AggregateFields() =>
        Shown.Where(c => c.EffectiveAggregate is not null).Select(c => new DataAggregateField(c.AggregateKey, c.EffectiveAggregate!.Value)).ToList();

    /// <inheritdoc />
    internal override string? LoadProblem =>
        ItemsProvider is null || GroupsSource is not null ? null
        : GroupedNow ? $"A {ViewName} that groups and has an {nameof(ItemsProvider)} needs a GroupsProvider, as the rows it holds are only a page's."
        : Shown.Any(c => c.EffectiveAggregate is not null) ? $"A {ViewName} with an {nameof(ColumnBase<TItem>.Aggregate)} and an {nameof(ItemsProvider)} needs a GroupsProvider, as the rows it holds are only a page's."
        : null;

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
        var aggregates = AggregateFields();
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
