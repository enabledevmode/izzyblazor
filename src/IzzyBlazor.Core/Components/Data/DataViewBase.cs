using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IzzyBlazor;

/// <summary>
/// What <see cref="Table{TItem}"/> and <c>ListView</c> share: their items, from a list
/// or an <see cref="DataRequest{TItem}"/> provider, and paging through them by number or by cursor,
/// with the page, size and cursor in the URL (or, bound on an interactive page, in the component).
/// </summary>
/// <typeparam name="TItem">The type of the items.</typeparam>
public abstract partial class DataViewBase<TItem> : IDisposable
{
    /// <summary>The items. A list is paged in memory; an <c>IQueryable</c> (such as an EF Core query) is paged by the database.</summary>
    [Parameter] public IEnumerable<TItem>? Items { get; set; }

    /// <summary>
    /// Loads the items yourself, in place of <see cref="Items"/>: the request has the range (and in
    /// a Table the sort, the filter and the columns as fields), so
    /// <c>request.ToResultAsync(query, ...)</c> or <c>request.Apply(query)</c> does the work. An
    /// exception it throws shows "Couldn't load the rows." with Retry (and is logged); a request
    /// that was canceled is ignored.
    /// </summary>
    [Parameter] public Func<DataRequest<TItem>, ValueTask<DataResult<TItem>>>? ItemsProvider { get; set; }

    /// <summary>Items per page. Leave it out to show every item.</summary>
    [Parameter] public int? PageSize { get; set; }

    /// <summary>
    /// Sizes to choose from in a "Rows per page" menu in the footer, such as
    /// <c>new[] { 10, 25, 50 }</c>. The URL carries the choice (<c>?size=25</c>); only these sizes
    /// are accepted from it. <see cref="PageSize"/> is the default (the first option when not set).
    /// </summary>
    [Parameter] public IReadOnlyList<int>? PageSizeOptions { get; set; }

    /// <summary>
    /// Raised when a page size is chosen on an interactive page. Binding it
    /// (<c>@bind-PageSize</c>) keeps the size, with the page (and a Table's sort and filters), in the component.
    /// </summary>
    [Parameter, ModeSupport(NoScript = ModeLevel.None, Static = ModeLevel.None)]
    public EventCallback<int?> PageSizeChanged { get; set; }

    /// <summary>
    /// The page, from 1. Binding it (<c>@bind-Page</c>) on an interactive page keeps it in the
    /// component instead of the URL. Set it back to 1 when your filters change.
    /// </summary>
    [Parameter] public int? Page { get; set; }

    /// <summary>Raised when a page is chosen (and with 1 when a Table's sort or filters change), on interactive pages.</summary>
    [Parameter, ModeSupport(NoScript = ModeLevel.None, Static = ModeLevel.None)]
    public EventCallback<int> PageChanged { get; set; }

    /// <summary>
    /// How it moves through its pages when it has a <see cref="PageSize"/>: numbered pages
    /// (<see cref="DataPaging.Pages"/>, the default, which needs a total), or by cursor, with no
    /// total needed: First, Previous and Next (<see cref="DataPaging.NextPrevious"/>), a "Load
    /// more" button after the items (<see cref="DataPaging.LoadMore"/>, ListView only), or loading
    /// as the last item comes into view (<see cref="DataPaging.LoadOnScroll"/>).
    /// </summary>
    [Parameter, ModeSupport(NoScript = ModeLevel.Partial, NoScriptNotes = "With JavaScript off, LoadOnScroll doesn't load as you scroll: its Load more button shows, which reloads the page with one more page.")]
    public DataPaging Paging { get; set; }

    /// <summary>The "Load more" button's text, such as "Load earlier". Defaults to "Load more".</summary>
    [Parameter] public string? LoadMoreText { get; set; }

    /// <summary>
    /// The cursor of the page shown when paging by cursor (<see cref="Paging"/>); null for the first
    /// page. Binding it (<c>@bind-Cursor</c>) on an interactive page keeps it in the component
    /// instead of the URL; <see cref="Page"/> is then the page's position.
    /// </summary>
    [Parameter] public string? Cursor { get; set; }

    /// <summary>Raised when First, Previous or Next is chosen (and with null when a Table's sort or filters change), on interactive pages.</summary>
    [Parameter, ModeSupport(NoScript = ModeLevel.None, Static = ModeLevel.None)]
    public EventCallback<string?> CursorChanged { get; set; }

    /// <summary>
    /// Prefix for its query-string keys, so several can share a page:
    /// <c>QueryPrefix="orders-"</c> uses <c>?orders-page=</c> (and a Table's <c>?orders-sort=</c>).
    /// </summary>
    [Parameter] public string QueryPrefix { get; set; } = "";

    /// <summary>Shown when there are no items. Defaults to <see cref="EmptyText"/>.</summary>
    [Parameter] public RenderFragment? EmptyContent { get; set; }

    /// <summary>The text when there are no items. Defaults to "No items".</summary>
    [Parameter] public string? EmptyText { get; set; }

    /// <summary>Shown in place of "Couldn't load the rows." when loading fails, before the Retry button.</summary>
    [Parameter] public RenderFragment? ErrorContent { get; set; }

    /// <summary>
    /// A unique, stable key for each item, such as its id. Selection posts and binds these keys.
    /// Defaults to the item's string form.
    /// </summary>
    [Parameter] public Func<TItem, string>? ItemKey { get; set; }

    /// <summary>Shows it as loading: items dim, or placeholders show while there are none. Set automatically while an <see cref="ItemsProvider"/> loads on interactive pages.</summary>
    [Parameter] public bool Loading { get; set; }

    /// <summary>
    /// Adds a checkbox to each item. Inside a form, the checked items post their <see cref="ItemKey"/>
    /// under <see cref="SelectionName"/>; on interactive pages <c>@bind-SelectedKeys</c> follows them.
    /// With script, a checkbox above them selects every item on the page.
    /// </summary>
    [Parameter] public bool Selectable { get; set; }

    /// <summary>The form field name the checked items post under. Defaults to <c>"{QueryPrefix}selected"</c>.</summary>
    [Parameter] public string? SelectionName { get; set; }

    /// <summary>The keys of the selected items (on any page). Read the posted ones with <c>[SupplyParameterFromForm]</c> on static pages.</summary>
    [Parameter] public IReadOnlyCollection<string>? SelectedKeys { get; set; }

    /// <summary>Raised when items are selected or cleared on interactive pages.</summary>
    [Parameter, ModeSupport(NoScript = ModeLevel.None, Static = ModeLevel.None)]
    public EventCallback<IReadOnlyCollection<string>> SelectedKeysChanged { get; set; }

    /// <summary>The select-all checkbox's name for screen readers. Defaults to "Select all rows".</summary>
    [Parameter] public string? SelectAllLabel { get; set; }

    /// <summary>The start of each item checkbox's name for screen readers, before the item's label. Defaults to "Select".</summary>
    [Parameter] public string? SelectRowLabel { get; set; }

    [Inject] internal NavigationManager Navigation { get; set; } = null!;

    [Inject] internal IServiceProvider Services { get; set; } = null!;

    internal IReadOnlyList<TItem> _items = [];
    internal int? _total;
    // By cursor: the cursor of the first page shown (null for page 1), the next and previous ones,
    // and the last page shown (Load more adds pages after _page).
    internal string? _cursor;
    internal string? _next;
    internal string? _previous;
    internal int _lastPage = 1;
    internal int _page = 1;
    internal bool _loadingMore;
    internal DataRequest<TItem>? _loadedRequest;
    internal int _loadedSize;
    // A failed load: in place of the items (_loadError), or under them for Load more (_moreError).
    internal Exception? _loadError;
    internal bool _moreError;
    internal bool _focusAfterRetry;
    internal bool _focusScroll;
    internal CancellationTokenSource? _loadCts;
    // The first item of each page Load more added, and the one to focus when it arrives.
    internal readonly HashSet<int> _pageStarts = [];
    internal readonly Dictionary<int, ElementReference> _rowRefs = [];
    internal int? _focusRow;
    // On a static page that grew, the first item the last page added: it takes focus on arrival.
    internal int? _arriveAt;
    internal bool _providerLoading;

    internal bool _stateReady;
    internal string? _stateSort;
    internal int _statePage = 1;
    internal string? _lastSortParam;
    internal int? _lastPageParam;
    internal string? _stateCursor;
    internal string? _lastCursorParam;
    internal int? _statePageSize;
    internal int? _lastPageSizeParam;

    internal bool IsLoading => Loading || _providerLoading;

    /// <summary>What the logs and errors call it: "Table", "ListView".</summary>
    internal abstract string ViewName { get; }

    /// <summary>Said and shown when loading fails: "Couldn't load the rows." in a Table.</summary>
    internal virtual string LoadFailedText => TextDefaults.LoadFailed;

    /// <summary>Whether the items can load yet: a Table waits for its columns to register.</summary>
    internal virtual bool ReadyToLoad => true;

    /// <summary>A bound parameter of the derived view that also keeps state in the component (a Table's sort or filter).</summary>
    internal virtual bool ExtraBound => false;

    /// <summary>The bound sort, for a view that sorts.</summary>
    internal virtual string? SortParameter => null;

    /// <summary>The filter as a string that changes when it does, for a view that filters.</summary>
    internal virtual string FilterSignature() => "";

    /// <summary>Adds the derived view's sort, filter and fields to a request, from the raw sort.</summary>
    internal virtual DataRequest<TItem> Shape(DataRequest<TItem> request, string? sortRaw) => request;

    /// <summary>After a load succeeded: the derived view takes what it shaped (a Table's sort shown).</summary>
    internal virtual void Loaded()
    {
    }

    /// <summary>Takes a bound filter, the first time (<paramref name="first"/>) or when the parameter changes.</summary>
    internal virtual void SyncFilter(bool first)
    {
    }

    /// <summary>Where focus goes once Retry has brought the items (the button it was on is gone).</summary>
    internal virtual ValueTask FocusAfterRetryAsync() => ValueTask.CompletedTask;

    // Page and cursor live in the component (not the URL) on interactive pages that bind them.
    internal bool Bound => PageChanged.HasDelegate || PageSizeChanged.HasDelegate || CursorChanged.HasDelegate || ExtraBound;

    internal bool UseState => RendererInfo.IsInteractive && Bound;

    internal string SortQueryKey => $"{QueryPrefix}sort";

    internal string PageKey => $"{QueryPrefix}page";

    internal string CursorKey => $"{QueryPrefix}cursor";

    internal string SizeKey => $"{QueryPrefix}size";

    internal HashSet<string> _selected = new(StringComparer.Ordinal);
    private IReadOnlyCollection<string>? _paramKeys;
    private IReadOnlyCollection<string>? _reportedKeys;

    /// <summary>Names an item for screen readers, in its link and its checkbox.</summary>
    internal virtual string LabelOf(TItem item) => KeyOf(item);

    internal string EffectiveSelectionName => SelectionName ?? $"{QueryPrefix}selected";

    internal bool IsSelected(string key) => _selected.Contains(key);

    internal bool AllOnPageSelected => _items.Count > 0 && _items.All(i => _selected.Contains(KeyOf(i)));

    internal string SelectLabel(TItem item) => $"{SelectRowLabel ?? TextDefaults.SelectRow} {LabelOf(item)}";

    // Takes the parent's keys when the parameter changes (a static post, or C# clearing the
    // selection), but not the echo of a change this view just reported. Without a binding
    // the parameter never changes, so the view keeps its own selection.
    internal void SyncSelection()
    {
        if (!ReferenceEquals(SelectedKeys, _paramKeys))
        {
            _paramKeys = SelectedKeys;
            if (!ReferenceEquals(SelectedKeys, _reportedKeys))
            {
                _selected = new HashSet<string>(SelectedKeys ?? [], StringComparer.Ordinal);
            }
        }
    }

    internal async Task OnSelectChanged(string key, ChangeEventArgs e)
    {
        var on = e.Value is true || (e.Value is string text && bool.TryParse(text, out var parsed) && parsed);
        if (on ? !_selected.Add(key) : !_selected.Remove(key))
        {
            return;
        }

        var keys = _selected.ToArray();
        _reportedKeys = keys;
        await SelectedKeysChanged.InvokeAsync(keys);
    }

    internal string KeyOf(TItem item) => KeyFunc?.Invoke(item) ?? item?.ToString() ?? "";

    /// <summary>The items to show: <see cref="Items"/>, or a drop zone's share of its group's.</summary>
    internal virtual IEnumerable<TItem>? SourceItems => Items;

    /// <summary>The key: <see cref="ItemKey"/>, or a drop zone's group's.</summary>
    internal virtual Func<TItem, string>? KeyFunc => ItemKey;

    /// <inheritdoc />
    protected override void OnInitialized() => Navigation.LocationChanged += OnLocationChanged;

    /// <summary>Takes the bound state, then loads, unless Load more's added pages should stay.</summary>
    internal async Task SyncAndLoadAsync()
    {
        SyncState();
        if (ReadyToLoad && !KeepsAddedPages())
        {
            await LoadAsync();
        }
    }

    // The size when none has been chosen: PageSize, else the first option.
    internal int? DefaultPageSize => PageSize ?? (PageSizeOptions is { Count: > 0 } o ? o[0] : null);

    // A ?size= from the URL, if it's one of the options.
    internal int? ReadSizeQuery() =>
        PageSizeOptions is { Count: > 0 } options
        && QueryString.Of(Navigation.Uri).TryGetValue(SizeKey, out var raw)
        && int.TryParse(raw.ToString(), out var size) && options.Contains(size) ? size : null;

    internal int? EffectivePageSize => UseState ? _statePageSize ?? DefaultPageSize : ReadSizeQuery() ?? DefaultPageSize;

    // The page that holds the first item on view now, at another size. A cursor is for one size,
    // so paging by cursor starts over.
    internal int PageAt(int newSize) => !CursorPaging && EffectivePageSize is { } current ? (_page - 1) * current / newSize + 1 : 1;

    internal string PageSizeHref(int size)
    {
        var page = PageAt(size);
        return Navigation.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            [SizeKey] = size == DefaultPageSize ? null : size,
            [PageKey] = page == 1 ? null : page,
            [CursorKey] = null,
        });
    }

    internal async Task OnPageSizeAsync(int size)
    {
        var page = PageAt(size);
        var (pageWas, cursorWas) = (_statePage, _stateCursor);
        (_statePageSize, _statePage, _stateCursor) = (size, page, null);
        await PageSizeChanged.InvokeAsync(size);
        if (page != pageWas)
        {
            await PageChanged.InvokeAsync(page);
        }

        if (cursorWas is not null)
        {
            await CursorChanged.InvokeAsync(null);
        }

        await ReloadIfStaleAsync();
    }

    // Takes the parent's Sort and Page when they change (C# setting them, or the echo of a click),
    // starting from the URL so a link to ?sort=&page= still opens on that view.
    internal void SyncState()
    {
        if (!UseState)
        {
            return;
        }

        if (!_stateReady)
        {
            var (sortRaw, pageRaw, cursorRaw) = ReadQuery();
            _stateSort = SortParameter ?? (string.IsNullOrEmpty(sortRaw) ? null : sortRaw);
            _statePage = Page ?? (int.TryParse(pageRaw, out var number) ? number : 1);
            _stateCursor = Cursor ?? (string.IsNullOrEmpty(cursorRaw) ? null : cursorRaw);
            _lastCursorParam = Cursor;
            SyncFilter(first: true);
            // A bound PageSize is the current size; otherwise it's the default, and the URL may pick another.
            _statePageSize = PageSizeChanged.HasDelegate ? PageSize : ReadSizeQuery();
            (_lastSortParam, _lastPageParam, _lastPageSizeParam, _stateReady) = (SortParameter, Page, PageSize, true);
            return;
        }

        if (PageSize != _lastPageSizeParam)
        {
            _lastPageSizeParam = PageSize;
            _statePageSize = PageSize;
        }

        SyncFilter(first: false);

        if (!string.Equals(SortParameter, _lastSortParam, StringComparison.Ordinal))
        {
            _lastSortParam = SortParameter;
            _stateSort = SortParameter;
        }

        if (Page != _lastPageParam)
        {
            _lastPageParam = Page;
            _statePage = Page ?? 1;

            // Page 1 by cursor is the first page: without a bound Cursor, setting it means start over.
            if (_statePage == 1 && !CursorChanged.HasDelegate)
            {
                _stateCursor = null;
            }
        }

        if (!string.Equals(Cursor, _lastCursorParam, StringComparison.Ordinal))
        {
            _lastCursorParam = Cursor;
            _stateCursor = Cursor;
        }
    }

    internal async Task OnPageClickAsync(int page)
    {
        _statePage = page;
        await PageChanged.InvokeAsync(page);
        await ReloadIfStaleAsync();
    }

    // First, Previous or Next, by cursor, with state in the component.
    internal async Task OnCursorClickAsync(PageTarget target)
    {
        var pageWas = _statePage;
        (_stateCursor, _statePage) = (target.Cursor, target.Page);
        await CursorChanged.InvokeAsync(target.Cursor);
        if (target.Page != pageWas)
        {
            await PageChanged.InvokeAsync(target.Page);
        }

        await ReloadIfStaleAsync();
    }

    // A new sort, filter or size starts from the first page: told to a bound parent when that changed.
    internal async Task ReportFirstPageAsync(int pageWas, string? cursorWas)
    {
        if (pageWas != 1)
        {
            await PageChanged.InvokeAsync(1);
        }

        if (cursorWas is not null)
        {
            await CursorChanged.InvokeAsync(null);
        }
    }

    // A bound parent re-renders the view, which loads; otherwise load here.
    internal async Task ReloadIfStaleAsync()
    {
        var (sortRaw, pageRaw, cursorRaw) = CurrentState();
        if (_loadedKey is { } loaded && loaded.Sort == sortRaw && loaded.Page == pageRaw && loaded.Cursor == cursorRaw && loaded.Filters == FilterSignature() && loaded.PageSize == EffectivePageSize)
        {
            return;
        }

        await LoadAsync();
        StateHasChanged();
    }

    // Where sort, page and cursor come from: the component's state, or the URL.
    internal (string? Sort, string? Page, string? Cursor) CurrentState() =>
        UseState ? (_stateSort, _statePage.ToString(System.Globalization.CultureInfo.InvariantCulture), _stateCursor) : ReadQuery();

    // Only this view's own sort or page changing calls for a reload here; anything else that
    // changes the data (filters) re-renders the page, which reloads through the parameters.
    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => _ = InvokeAsync(async () =>
    {
        if (!ReadyToLoad || UseState)
        {
            return;
        }

        // Another part of the address changed: the items stay, but every link (sort, size, filters,
        // cursor) carries the whole address, so they're drawn again.
        if (ReadQuery() == (_loadedKey?.Sort, _loadedKey?.Page, _loadedKey?.Cursor) && FilterSignature() == _loadedKey?.Filters && EffectivePageSize == _loadedKey?.PageSize)
        {
            StateHasChanged();
            return;
        }

        try
        {
            await LoadAsync();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            await DispatchExceptionAsync(ex);
        }
    });

    internal (string? Sort, string? Page, string? Cursor) ReadQuery()
    {
        var query = QueryString.Of(Navigation.Uri);
        return (query.TryGetValue(SortQueryKey, out var s) ? s.ToString() : null,
                query.TryGetValue(PageKey, out var p) ? p.ToString() : null,
                query.TryGetValue(CursorKey, out var c) ? c.ToString() : null);
    }

    internal sealed record LoadKey(string? Sort, string? Page, string? Cursor, string Filters, object? Items, object? Provider, int? PageSize);

    internal LoadKey? _loadedKey;
    internal int _loadVersion;

    // A list is queried as an EnumerableQuery, which runs the query through Enumerable's methods,
    // found by reflection; the DynamicDependency keeps them in a trimmed app.
    [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods, typeof(Enumerable))]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The DynamicDependency keeps the Enumerable methods the in-memory query binds to.")]
    private static IQueryable<TItem> AsQuery(IEnumerable<TItem> items) => items as IQueryable<TItem> ?? items.AsQueryable();

    internal async Task LoadAsync()
    {
        var (sortRaw, pageRaw, cursorRaw) = CurrentState();
        var pageSize = EffectivePageSize;
        // Always reload: the same list may have changed since the last render.
        _loadedKey = new LoadKey(sortRaw, pageRaw, cursorRaw, FilterSignature(), SourceItems, ItemsProvider, pageSize);
        var version = ++_loadVersion;
        _loadingMore = false;
        _loadCts?.Cancel();
        var cts = _loadCts = new CancellationTokenSource();

        var byCursor = CursorPaging && pageSize is not null;
        var growing = Appending && pageSize is not null;
        // A list that grows always starts at the top: ?page=3 means the first three pages, loaded
        // as one, so a static page (with nothing to add pages to) shows the list grown too.
        var cursor = byCursor && !growing && !string.IsNullOrEmpty(cursorRaw) ? cursorRaw : null;

        // Keep (page - 1) * size inside int range, whatever is in the URL. By cursor, the first
        // page is page 1 whatever the URL says.
        var maxPage = pageSize is > 0 and var size0 ? int.MaxValue / size0 : int.MaxValue;
        var asked = int.TryParse(pageRaw, out var number) ? Math.Clamp(number, 1, maxPage) : 1;
        var shownPages = growing ? asked : 1;
        var page = growing || (byCursor && cursor is null) ? 1 : asked;
        var request = Shape(new DataRequest<TItem>
        {
            Skip = pageSize is { } size1 ? (page - 1) * size1 : 0,
            Take = pageSize * shownPages,
            Cursor = cursor,
            NeedsTotal = !byCursor,
            CancellationToken = cts.Token,
            // No fields of its own (a Table's columns add theirs), so request.Apply(query) works in a provider.
            Fields = [],
        }, sortRaw);

        IReadOnlyList<TItem> items;
        int? total;
        string? next = null, previous = null;
        var noTotal = false;
        try
        {
            if (ItemsProvider is not null)
            {
                // Show the loading state while the provider works (on interactive pages it renders).
                _providerLoading = true;
                if (RendererInfo.IsInteractive)
                {
                    StateHasChanged();
                }

                DataResult<TItem> result;
                try
                {
                    result = await ItemsProvider(request);
                }
                finally
                {
                    if (version == _loadVersion)
                    {
                        _providerLoading = false;
                    }
                }

                (items, total, next, previous) = (result.Items, result.Total, result.Next, result.Previous);
                noTotal = !byCursor && pageSize is not null && result.Total is null;

                // A page past the end shows the last page.
                if (!noTotal && !byCursor && pageSize is { } size && items.Count == 0 && total is > 0 and var count && page > LastPage(count, size))
                {
                    page = LastPage(count, size);
                    request = request with { Skip = (page - 1) * size };
                    result = await ItemsProvider(request);
                    items = result.Items;
                    total = result.Total;
                }
            }
            else if (SourceItems is not null)
            {
                var shown = PageOfItems(request);
                (items, total, page, next, previous) = (shown.Items, shown.Total, shown.Page, shown.Next, shown.Previous);
                request = request with { Skip = pageSize is { } size2 ? (page - 1) * size2 : 0 };
            }
            else
            {
                items = [];
                total = 0;
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // A newer load, or the view went away.
            return;
        }
        catch (Exception ex)
        {
            if (version != _loadVersion)
            {
                return;
            }

            Logger?.LogError(ex, "The {View}'s items failed to load.", ViewName);
            (_items, _total, _loadError, _moreError, _focusAfterRetry) = ([], null, ex, false, false);
            (_cursor, _next, _previous) = (null, null, null);
            _loadedRequest = null;
            return;
        }

        // A slower, older load must not overwrite a newer one.
        if (version != _loadVersion)
        {
            return;
        }

        if (noTotal)
        {
            throw new InvalidOperationException(
                $"The {ViewName}'s {nameof(ItemsProvider)} returned no {nameof(DataResult<TItem>.Total)}, which numbered pages need. " +
                $"Return the total, or set {nameof(Paging)} to page by cursor (DataPaging.{nameof(DataPaging.NextPrevious)} or DataPaging.{nameof(DataPaging.LoadMore)}).");
        }

        if (_loadError is not null && _focusAfterRetry)
        {
            _focusScroll = true;
        }

        (_loadError, _moreError, _focusAfterRetry) = (null, false, false);
        (_items, _total, _page, _lastPage) = (items, total, page, page + shownPages - 1);
        Loaded();
        (_cursor, _next, _previous) = byCursor ? (cursor, next, previous) : (null, null, null);
        // Pages added later are one page each, after the ones loaded here.
        (_loadedRequest, _loadedSize) = (request with { Take = pageSize }, pageSize ?? 0);
        _pageStarts.Clear();
        _rowRefs.Clear();
        _focusRow = null;
        // A static page that grew by a link: focus goes to the first item it added, as Load more's
        // does on an interactive page (focus.ts, after enhanced navigation). Not native autofocus:
        // a browser may honor it on a row enhanced navigation inserts, after a load by scrolling
        // too. With script off, focus starts at the top of the page.
        _arriveAt = growing && shownPages > 1 && !RendererInfo.IsInteractive && (shownPages - 1) * pageSize!.Value is var first && first < items.Count ? first : null;
        Remember(_loadedKey with { Page = null, Cursor = null }, cursor, _next);

        // A page past the end showed the last one; keep that, so the next click counts from it.
        if (UseState)
        {
            _statePage = page;
        }
    }

    private ILogger? _logger;

    private ILogger? Logger => _logger ??= Services.GetService<ILoggerFactory>()?.CreateLogger(GetType());

    // Retry after a failed load: loads again; when the items come, focus goes to them (the Retry
    // button it was on is gone).
    internal async Task RetryAsync()
    {
        _focusAfterRetry = true;
        await LoadAsync();
        StateHasChanged();
    }

    // A page of Items: by number, or by cursor, where the cursor is the item offset.
    private (IReadOnlyList<TItem> Items, int Total, int Page, string? Next, string? Previous) PageOfItems(DataRequest<TItem> request)
    {
        var sorted = request.ApplySorting(request.ApplyFiltering(AsQuery(SourceItems!)));
        var total = sorted.Count();
        if (request.Take is not { } size)
        {
            return (sorted.ToList(), total, 1, null, null);
        }

        if (!CursorPaging)
        {
            var page = Math.Min(request.Page, LastPage(total, size));
            return (sorted.Skip((page - 1) * size).Take(size).ToList(), total, page, null, null);
        }

        var offset = int.TryParse(request.Cursor, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var at) ? Math.Min(at, total) : 0;
        var next = total - offset > size ? Offset(offset + size) : null;
        var previous = offset > size ? Offset(offset - size) : null;
        return (sorted.Skip(offset).Take(size).ToList(), total, offset / size + 1, next, previous);
    }

    private static string Offset(int offset) => offset.ToString(System.Globalization.CultureInfo.InvariantCulture);

    // Where each followed cursor came from, so Previous works without a provider's Previous. Kept
    // while the sort, filters, size and data stay the same; a cursor is only good for those.
    internal readonly Dictionary<string, string?> _cameFrom = new(StringComparer.Ordinal);
    private LoadKey? _cameFromKey;

    private void Remember(LoadKey key, string? cursor, string? next)
    {
        if (key != _cameFromKey)
        {
            _cameFrom.Clear();
            _cameFromKey = key;
        }

        if (next is not null)
        {
            _cameFrom[next] = cursor;
        }
    }

    // Load more: the next page, added under the items. Pressing the button moves focus to the first
    // new item; a load as the last item scrolls into view doesn't.
    internal async Task LoadMoreAsync(bool focus)
    {
        // Not while the list reloads (a new sort): its items and cursor are on their way out.
        if (_loadingMore || _providerLoading || _next is null || _loadedRequest is not { } basis)
        {
            return;
        }

        var version = _loadVersion;
        var page = _lastPage + 1;
        var request = basis with { Skip = (page - 1) * _loadedSize, Cursor = _next };
        _loadingMore = true;
        try
        {
            IReadOnlyList<TItem> items;
            int? total;
            string? next;
            if (ItemsProvider is not null)
            {
                var result = await ItemsProvider(request);
                (items, total, next) = (result.Items, result.Total, result.Next);
            }
            else
            {
                var result = PageOfItems(request);
                (items, total, next) = (result.Items, result.Total, result.Next);
            }

            // The list started over (a new sort or filter) while this page loaded.
            if (version != _loadVersion || !ReferenceEquals(basis, _loadedRequest))
            {
                return;
            }

            // Items added at the top between loads shift an offset or a time cursor onto items
            // already shown: with an ItemKey they aren't shown twice.
            var shown = KeyFunc is null ? null : _items.Select(KeyOf).ToHashSet(StringComparer.Ordinal);
            var added = shown is null ? items : items.Where(i => shown.Add(KeyOf(i))).ToList();
            if (added.Count > 0)
            {
                var first = _items.Count;
                _items = [.. _items, .. added];
                _pageStarts.Add(first);
                _focusRow = focus ? first : null;
            }

            (_next, _lastPage, _total, _moreError) = (next, page, total ?? _total, false);
            Remember(_loadedKey! with { Page = null, Cursor = null }, request.Cursor, next);
        }
        catch (OperationCanceledException) when (request.CancellationToken.IsCancellationRequested)
        {
            // The list started over, or the view went away.
        }
        catch (Exception ex)
        {
            // The items shown stay; the message and Retry go under them.
            if (version == _loadVersion && ReferenceEquals(basis, _loadedRequest))
            {
                Logger?.LogError(ex, "The {View}'s next page failed to load.", ViewName);
                _moreError = true;
            }
        }
        finally
        {
            if (version == _loadVersion)
            {
                _loadingMore = false;
            }
        }
    }

    /// <summary>
    /// Loads the items again, such as after an edit. With <see cref="DataPaging.LoadMore"/> it
    /// starts over from the page the list began at, dropping the pages added since.
    /// </summary>
    public async Task ReloadAsync()
    {
        if (!ReadyToLoad)
        {
            return;
        }

        await LoadAsync();
        StateHasChanged();
    }

    // Load more keeps the pages it added while nothing that shapes the list changes: a parent's
    // re-render would otherwise load the first page again and drop them. (With one page shown,
    // and no Load more under way, it reloads, as the other modes do: the same list may have changed.)
    private bool KeepsAddedPages()
    {
        if (!Appending || (_lastPage == _page && !_loadingMore) || _loadedKey is not { } loaded)
        {
            return false;
        }

        var (sortRaw, pageRaw, cursorRaw) = CurrentState();
        return loaded == new LoadKey(sortRaw, pageRaw, cursorRaw, FilterSignature(), SourceItems, ItemsProvider, EffectivePageSize);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // After Load more, the reader carries on from the first item it added.
        if (_focusRow is { } row && _rowRefs.TryGetValue(row, out var element))
        {
            _focusRow = null;
            await element.FocusAsync();
        }

        // After Retry brought the items, focus goes to them (the button it was on is gone).
        if (_focusScroll)
        {
            _focusScroll = false;
            await FocusAfterRetryAsync();
        }
    }

    internal static int LastPage(int total, int size) => Math.Max(1, (int)Math.Ceiling(total / (double)size));

    internal string Summary(int size)
    {
        if (_items.Count == 0)
        {
            return "";
        }

        var first = (_page - 1L) * size + 1;
        var last = first + _items.Count - 1;
        return _total is { } total
            ? string.Format(System.Globalization.CultureInfo.CurrentCulture, TextDefaults.TableRange, first, last, total)
            : string.Format(System.Globalization.CultureInfo.CurrentCulture, TextDefaults.TableRangeOpen, first, last);
    }

    internal bool CursorPaging => Paging != DataPaging.Pages;

    internal bool Appending => Paging is DataPaging.LoadMore or DataPaging.LoadOnScroll;

    // Loading on scroll: the button is for the keyboard, out of sight until it has focus (CSS shows
    // it with no script, where it's the only way on).
    internal bool OnFocusOnly => Paging == DataPaging.LoadOnScroll;

    // Numbered pages show the footer when there's anything to page; by cursor also on an empty
    // page reached by a cursor, so First leads back.
    internal bool ShowFooter => CursorPaging ? _items.Count > 0 || _cursor is not null : _total > 0;

    // Where First, Previous or Next lead: a cursor (null for the first page) and the position.
    internal readonly record struct PageTarget(bool Enabled, string? Cursor, int Page);

    internal PageTarget FirstTarget => new(_cursor is not null, null, 1);

    internal PageTarget NextTarget => new(_next is not null, _next, _lastPage + 1);

    // The provider's Previous; the second page's is the first; else a cursor this view followed here.
    internal PageTarget PreviousTarget =>
        _cursor is null ? default
        : _previous is not null ? new(true, _previous, Math.Max(1, _page - 1))
        : _page == 2 ? new(true, null, 1)
        : _cameFrom.TryGetValue(_cursor, out var before) ? new(true, before, before is null ? 1 : Math.Max(1, _page - 1))
        : default;

    // A list that grows, on a static page: the same list with one more page.
    internal string MoreHref =>
        Navigation.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            [PageKey] = _lastPage + 1,
            [CursorKey] = null,
        });

    internal string CursorHref(PageTarget target) =>
        Navigation.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            [CursorKey] = target.Cursor,
            [PageKey] = target.Cursor is null || target.Page <= 1 ? null : target.Page,
        });

    /// <inheritdoc />
    public virtual void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
        _loadCts?.Cancel();
    }
}

/// <summary>How a <see cref="Table{TItem}"/> or <c>ListView</c> with a <c>PageSize</c> moves through its pages.</summary>
public enum DataPaging
{
    /// <summary>Numbered pages and "1–10 of 42". Needs the total (<see cref="DataResult{TItem}.Total"/>). The default.</summary>
    Pages,

    /// <summary>
    /// First, previous and next only, by cursor (<see cref="DataResult{TItem}.Next"/>): for data
    /// that has no total, or that pages by a token. Each page replaces the one shown.
    /// </summary>
    NextPrevious,

    /// <summary>
    /// A "Load more" button after the items that adds the next page to them, by cursor: for a
    /// <c>ListView</c> (a Table pages by number, by cursor or on scroll). On static pages and with
    /// JavaScript off it's a link to the same list with one more page (<c>?page=3</c> shows the
    /// first three pages, asked of the provider as one request from the start); with script, focus
    /// goes to the first item it added.
    /// </summary>
    LoadMore,

    /// <summary>
    /// The next page loads when the last item scrolls into view and is added to the items: in place
    /// on interactive pages, and on static pages by following the Load more link, which enhanced
    /// navigation patches in with the scroll kept. The button is there only for the keyboard: out
    /// of sight until it has focus, with a spinner in its place while a page loads. With
    /// JavaScript off it shows, and grows the list by a page as for <see cref="LoadMore"/>. Best
    /// with a <c>MaxHeight</c>, so content under it stays within reach.
    /// </summary>
    LoadOnScroll,
}
