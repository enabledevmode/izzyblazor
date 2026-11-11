using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

// A grid's detail rows.
public abstract partial class GridViewBase<TItem>
{
    internal RenderFragment<TItem>? DetailTemplate { get; set; }

    internal Func<TItem, bool>? ItemHasDetail { get; set; }

    internal IReadOnlyList<string>? ExpandedDetails { get; set; }

    internal EventCallback<IReadOnlyList<string>?> ExpandedDetailsChanged { get; set; }

    internal bool ExpandAllDetails { get; set; }

    internal bool SingleDetail { get; set; }

    private string DetailKey => $"{QueryPrefix}detail";

    private IReadOnlyList<string>? _stateDetails;
    private string? _detailIds;
    private object? _detailsFor;
    private DetailState? _details;

    /// <summary>Which rows' details are open now.</summary>
    internal sealed record DetailState(bool All, IReadOnlyList<string> Open, IReadOnlyList<string> Shut)
    {
        public bool IsOpen(string key) => All ? !Shut.Contains(key) : Open.Contains(key);

        public bool AllOpen => All && Shut.Count == 0;
    }

    internal bool HasDetails => DetailTemplate is not null;

    // A row's detail holds its template, or the values of hidden columns, or both.
    private bool HasTemplateDetail(TItem item) => HasDetails && (ItemHasDetail?.Invoke(item) ?? true);

    private bool HasDetail(TItem item) => HasTemplateDetail(item) || Hiding;

    // A row being edited shows its hidden columns' fields.
    private bool DetailOpen(TItem item) => HasDetail(item) && (Details.IsOpen(KeyOf(item)) || (Hiding && IsEditingRow(item)));

    private bool DetailBound => ExpandedDetailsChanged.HasDelegate;

    // A detail opening or closing isn't a page leaving fewer rows: the height held starts again.
    /// <inheritdoc />
    internal override string? HeldFor => base.HeldFor is { } held && HasExpander && (Details.All || Details.Open.Count > 0 || (Hiding && RowEditKey is not null))
        ? $"{held} {(Details.All ? "*" : "")}{string.Join(' ', Details.All ? Details.Shut : Details.Open)}{(Hiding && RowEditKey is { } editing ? " edit:" + editing : "")}"
        : base.HeldFor;

    // Read again when what it's read from changes: asked for at every row.
    private DetailState Details
    {
        get
        {
            var inputs = (UseState, UseState ? null : Navigation.Uri, _stateDetails, ExpandedDetails, ExpandAllDetails, SingleDetail, DetailBound);
            if (_details is null || !Equals(_detailsFor, inputs))
            {
                (_detailsFor, _details) = (inputs, ReadDetails());
            }

            return _details;
        }
    }

    private DetailState ReadDetails()
    {
        IReadOnlyList<string> list;
        if (UseState)
        {
            list = DetailBound ? ExpandedDetails ?? [] : _stateDetails ?? ExpandedDetails ?? [];
        }
        else
        {
            list = QueryString.Of(Navigation.Uri).TryGetValue(DetailKey, out var values) ? values.OfType<string>().ToList() : [];
        }

        var open = list.Where(k => k != "*" && !k.StartsWith('!')).TakeLast(SingleDetail ? 1 : MaxOpenPaths).ToList();
        var shut = list.Where(k => k.StartsWith('!') && k != "!*").Select(k => k[1..]).TakeLast(MaxOpenPaths).ToList();
        var all = !SingleDetail && (list.Contains("*") || (ExpandAllDetails && open.Count == 0 && !list.Contains("!*")));
        return new DetailState(all, open, shut);
    }

    // The open rows with this one the other way. Empty with ExpandAllDetails would open them all again.
    private IReadOnlyList<string> DetailsToggled(string key)
    {
        var state = Details;
        var open = state.IsOpen(key);
        if (SingleDetail)
        {
            return open ? [] : [key];
        }

        if (state.All)
        {
            var shut = state.Shut.Where(k => k != key);
            return [.. (open ? shut.Append(key) : shut).TakeLast(MaxOpenPaths).Select(k => "!" + k).Prepend("*")];
        }

        var kept = state.Open.Where(k => k != key);
        var list = (open ? kept : kept.Append(key)).TakeLast(MaxOpenPaths).ToList();
        return ExpandAllDetails && list.Count == 0 ? ["!*"] : list;
    }

    private static IReadOnlyList<string> AllDetails(bool open, bool startOpen) => open ? ["*"] : startOpen ? ["!*"] : [];

    private string DetailHref(string key) => DetailLink(DetailsToggled(key));

    private string AllDetailsHref(bool open) => DetailLink(AllDetails(open, ExpandAllDetails));

    private string DetailLink(IReadOnlyList<string> list) =>
        LinkWith(new Dictionary<string, object?> { [DetailKey] = list.Count == 0 ? null : list.ToArray() });

    private async Task ToggleDetailAsync(string key) => await SetDetailsAsync(DetailsToggled(key));

    private async Task SetDetailsAsync(IReadOnlyList<string> list)
    {
        _stateDetails = list;
        await ExpandedDetailsChanged.InvokeAsync(list);
    }

    // The detail row's id, for its button's aria-controls: by its place, as keys can be any text.
    private string DetailId(int row) => $"{_detailIds ??= IdGenerator.Next("ib-grid-detail")}-{row}";

    private void SeedDetailsFromAddress(Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query)
    {
        if (!DetailBound && query.TryGetValue(DetailKey, out var values))
        {
            _stateDetails = values.OfType<string>().ToList();
        }
    }

    private void CheckDetails()
    {
        if (!HasDetails && ItemHasDetail is not null)
        {
            throw new InvalidOperationException($"The DataGrid sets {nameof(ItemHasDetail)} with no {nameof(DetailTemplate)} to show.");
        }

        if (!HasExpander)
        {
            if (ExpandAllDetails || SingleDetail || ExpandedDetails is not null)
            {
                throw new InvalidOperationException($"The DataGrid sets up details with no {nameof(DetailTemplate)} to show.");
            }

            return;
        }

        // Hidden columns' values show in the row's detail, so hiding needs what a detail needs.
        var what = HasDetails ? $"a {nameof(DetailTemplate)}" : $"{nameof(SmallScreen)}=\"HideColumns\"";
        if (ItemKey is null)
        {
            throw new InvalidOperationException($"A DataGrid with {what} needs an ItemKey: open rows are kept by it.");
        }

        if (Virtualize)
        {
            throw new InvalidOperationException($"A DataGrid with {what} can't {nameof(Virtualize)}: an open detail changes a row's height.");
        }

        if (SingleDetail && ExpandAllDetails)
        {
            throw new InvalidOperationException($"A DataGrid can't have both {nameof(SingleDetail)} and {nameof(ExpandAllDetails)}.");
        }
    }
}
