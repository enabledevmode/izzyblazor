using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

// A grid's detail rows.
public partial class DataGrid<TItem>
{
    /// <summary>
    /// A row's detail: drawn under it, across the grid, while it's open. Setting it adds a column of
    /// buttons that open and close it. It needs an <c>ItemKey</c>, which open rows are kept by. Only
    /// open rows draw it, so a component in it loads its own data when it's opened.
    /// </summary>
    [Parameter] public RenderFragment<TItem>? DetailTemplate { get; set; }

    /// <summary>Which rows have a detail; all of them when it's not set. A row without one has no button.</summary>
    [Parameter] public Func<TItem, bool>? ItemHasDetail { get; set; }

    /// <summary>
    /// Which rows' details are open, by key; <c>"*"</c> is all of them, and <c>"!key"</c> an exception to
    /// that. Binding it on an interactive page keeps it in the component; otherwise it's in the address.
    /// </summary>
    [Parameter] public IReadOnlyList<string>? OpenDetails { get; set; }

    /// <summary>Raised when a detail opens or closes, on interactive pages.</summary>
    [Parameter, ModeSupport(NoScript = ModeLevel.None, Static = ModeLevel.None)]
    public EventCallback<IReadOnlyList<string>?> OpenDetailsChanged { get; set; }

    /// <summary>Whether every row's detail starts open.</summary>
    [Parameter] public bool DetailsOpen { get; set; }

    /// <summary>Opening a row's detail closes the one that was open. There's no button to open them all.</summary>
    [Parameter] public bool SingleDetail { get; set; }

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

    private bool HasDetail(TItem item) => HasDetails && (ItemHasDetail?.Invoke(item) ?? true);

    private bool DetailOpen(TItem item) => HasDetail(item) && Details.IsOpen(KeyOf(item));

    private bool DetailBound => OpenDetailsChanged.HasDelegate;

    // Read again when what it's read from changes: asked for at every row.
    private DetailState Details
    {
        get
        {
            var inputs = (UseState, UseState ? null : Navigation.Uri, _stateDetails, OpenDetails, DetailsOpen, SingleDetail, DetailBound);
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
            list = DetailBound ? OpenDetails ?? [] : _stateDetails ?? OpenDetails ?? [];
        }
        else
        {
            list = QueryString.Of(Navigation.Uri).TryGetValue(DetailKey, out var values) ? values.OfType<string>().ToList() : [];
        }

        var open = list.Where(k => k != "*" && !k.StartsWith('!')).TakeLast(SingleDetail ? 1 : MaxOpenPaths).ToList();
        var shut = list.Where(k => k.StartsWith('!') && k != "!*").Select(k => k[1..]).TakeLast(MaxOpenPaths).ToList();
        var all = !SingleDetail && (list.Contains("*") || (DetailsOpen && open.Count == 0 && !list.Contains("!*")));
        return new DetailState(all, open, shut);
    }

    // The open rows with this one the other way. Empty with DetailsOpen would open them all again.
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
        return DetailsOpen && list.Count == 0 ? ["!*"] : list;
    }

    private static IReadOnlyList<string> AllDetails(bool open, bool startOpen) => open ? ["*"] : startOpen ? ["!*"] : [];

    private string DetailHref(string key) => DetailLink(DetailsToggled(key));

    private string AllDetailsHref(bool open) => DetailLink(AllDetails(open, DetailsOpen));

    private string DetailLink(IReadOnlyList<string> list) =>
        LinkWith(new Dictionary<string, object?> { [DetailKey] = list.Count == 0 ? null : list.ToArray() });

    private async Task ToggleDetailAsync(string key) => await SetDetailsAsync(DetailsToggled(key));

    private async Task SetDetailsAsync(IReadOnlyList<string> list)
    {
        _stateDetails = list;
        await OpenDetailsChanged.InvokeAsync(list);
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
        if (!HasDetails)
        {
            if (ItemHasDetail is not null || DetailsOpen || SingleDetail || OpenDetails is not null)
            {
                throw new InvalidOperationException($"The DataGrid sets up details with no {nameof(DetailTemplate)} to show.");
            }

            return;
        }

        if (ItemKey is null)
        {
            throw new InvalidOperationException($"A DataGrid with a {nameof(DetailTemplate)} needs an ItemKey: open rows are kept by it.");
        }

        if (Virtualize)
        {
            throw new InvalidOperationException($"A DataGrid with a {nameof(DetailTemplate)} can't {nameof(Virtualize)}: an open detail changes a row's height.");
        }

        if (SingleDetail && DetailsOpen)
        {
            throw new InvalidOperationException($"A DataGrid can't have both {nameof(SingleDetail)} and {nameof(DetailsOpen)}.");
        }
    }
}
