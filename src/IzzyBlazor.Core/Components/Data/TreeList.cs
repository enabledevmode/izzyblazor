using System.Globalization;
using Microsoft.AspNetCore.Components;

// GroupsProvider passes through to GridViewBase, as DataGrid's does.
#pragma warning disable BL0007

namespace IzzyBlazor;

/// <summary>
/// Displays rows that form a tree in a grid's columns, such as an org chart, a bill of materials or a
/// folder tree. A row opens to show the rows under it, and the first column shows each row's level. It
/// sorts, filters, pages and edits as a DataGrid does, with its open rows in the URL so it works on
/// static pages too.
/// </summary>
/// <typeparam name="TItem">The type of the items.</typeparam>
public partial class TreeList<TItem>
{
    private const int MaxOpenKeys = 50;
    private const int MaxDepth = 64;

    /// <summary>
    /// A row's children, for nested data: <c>Items</c> are then the top rows. Set this or
    /// <see cref="ItemParentKey"/>.
    /// </summary>
    [Parameter] public Func<TItem, IEnumerable<TItem>?>? ItemChildren { get; set; }

    /// <summary>
    /// A row's parent's key, for a flat list such as a table with a <c>ParentId</c>: <c>Items</c> are then
    /// every row, and a row whose parent key is null, or isn't another row's key, is a top row. Set this
    /// or <see cref="ItemChildren"/>.
    /// </summary>
    [Parameter] public Func<TItem, string?>? ItemParentKey { get; set; }

    /// <summary>
    /// Which rows are open, by key; <c>"*"</c> opens every row, <c>"!*"</c> closes every row, and
    /// <c>"!key"</c> closes one. Binding it (<c>@bind-ExpandedItems</c>) on an interactive page keeps it in
    /// the component; otherwise it's in the address, as <c>?open=</c>.
    /// </summary>
    [Parameter] public IReadOnlyList<string>? ExpandedItems { get; set; }

    /// <summary>Raised when a row opens or closes, on interactive pages.</summary>
    [Parameter, ModeSupport(NoScript = ModeLevel.None, Static = ModeLevel.None)]
    public EventCallback<IReadOnlyList<string>?> ExpandedItemsChanged { get; set; }

    /// <summary>Whether every row starts open. They start closed by default, showing the top rows.</summary>
    [Parameter] public bool ExpandAll { get; set; }

    /// <summary>Starts chosen rows open, such as the top level of an org chart.</summary>
    [Parameter] public Func<TItem, bool>? ItemExpanded { get; set; }

    /// <summary>
    /// An icon before a row's value in the first column; the flag says whether the row is open, for an
    /// open or a closed folder.
    /// </summary>
    [Parameter] public Func<TItem, bool, IconData?>? ItemIcon { get; set; }

    /// <summary>
    /// While filtering or searching, keeps the rows under a match too, not only the match and the rows
    /// above it: a search for a department shows its people.
    /// </summary>
    [Parameter] public bool FilterBranch { get; set; }

    /// <summary>
    /// Whether a row has rows under it, so it gets a chevron before they load, with an
    /// <c>ItemsProvider</c>. Without it every row gets one, and a row that opens to none loses it.
    /// </summary>
    [Parameter] public Func<TItem, bool>? ItemHasChildren { get; set; }

    /// <summary>
    /// The most rows shown under one row; a Show more row after them shows as many again. 200 by
    /// default.
    /// </summary>
    [Parameter] public int ChildRowLimit { get; set; } = 200;

    /// <summary>
    /// With <c>SelectionMode.Multiple</c>, checking a row checks the rows under it, and a row with
    /// some of them checked shows a mixed box. A checked row stands for its whole branch: rows under
    /// it come in checked as they're drawn, and <see cref="Selection.ApplyTree"/> finds them all. On
    /// by default; false makes each box its own.
    /// </summary>
    [Parameter] public bool Cascade { get; set; } = true;

    /// <summary>
    /// A new row's starting values under the row given, such as its parent's id set. Setting it lets an
    /// <c>AddChildAction</c> add rows under others; <c>OnSave</c> gets the row with <c>IsNew</c> set.
    /// <c>NewItem</c> still adds top rows, from the toolbar.
    /// </summary>
    [Parameter] public Func<TItem, TItem>? NewChild { get; set; }

    /// <summary>
    /// Saves a row moved under another, or to the top level, from a <c>MoveAction</c>'s dialog. With
    /// no error added, the dialog closes and the rows load again. Needs <c>Items</c>.
    /// </summary>
    [Parameter] public EventCallback<TreeListMove<TItem>> OnReparent { get; set; }

    /// <summary>Which rows can be moved; all of them when it's not set. It's checked on a move too, not just when drawing the action.</summary>
    [Parameter]
    public new Func<TItem, bool>? ItemMovable { get => base.ItemMovable; set => base.ItemMovable = value; }

    private string MoveQueryKey => $"{QueryPrefix}move";

    private string? _stateMoveKey;
    private TreeListMovePost _moveModel = new();
    private string? _moveFor;
    private Microsoft.AspNetCore.Components.Forms.EditContext? _moveContext;
    private Microsoft.AspNetCore.Components.Forms.ValidationMessageStore? _moveMessages;

    // The row whose Move dialog is open: one that can move, and is here.
    private string? CurrentMoveKey => !OnReparent.HasDelegate || EditEnding ? null : UseState ? _stateMoveKey : QueryValue(MoveQueryKey);

    /// <inheritdoc />
    internal override IReadOnlyList<string> LinkClears => OnReparent.HasDelegate ? [.. base.LinkClears, MoveQueryKey] : base.LinkClears;

    /// <inheritdoc />
    internal override void EndOwnDialogs() => (_stateMoveKey, _moveFor) = (null, null);

    private bool CanMoveRow(TItem item) => ItemMovable?.Invoke(item) != false;

    // The tree to choose from: every row but the moving one and its branch, under the top level.
    private List<TreeListMoveTarget> MoveTargets(string moving)
    {
        TreeListMoveTarget Of(Node node) => new(node.Key, LabelOf(node.Item), [.. node.Children.Where(c => c.Key != moving).Select(Of)]);
        var roots = (_treeNodes?.Values.AsEnumerable() ?? []).Where(n => n.Parent is null && n.Key != moving).Select(Of).ToList();
        return [new TreeListMoveTarget("", TextDefaults.TreeTopLevel, roots)];
    }

    // Checked again on the post, which a hand-made one could aim anywhere.
    private async Task MoveAsync(string key)
    {
        if (_treeNodes?.GetValueOrDefault(key) is not { } node || !CanMoveRow(node.Item))
        {
            return;
        }

        _moveMessages!.Clear();
        var parentKey = string.IsNullOrEmpty(_moveModel.Parent) ? null : _moveModel.Parent;
        Node? parent = null;
        if (parentKey is not null && (_treeNodes.GetValueOrDefault(parentKey) is not { } found || Under(found, node)))
        {
            AddMoveError(TextDefaults.TreeMoveIntoItself);
            return;
        }
        else if (parentKey is not null)
        {
            parent = _treeNodes[parentKey];
        }

        var siblings = parent?.Children ?? [.. _treeNodes.Values.Where(n => n.Parent is null)];
        var move = new TreeListMove<TItem>(node.Item, parent is null ? default : parent.Item, siblings.Count(s => s.Key != key), AddMoveError);
        await OnReparent.InvokeAsync(move);
        if (!move.HasErrors)
        {
            _moveFor = null;
            await EndEditAsync(saved: true);
        }

        static bool Under(Node candidate, Node moving)
        {
            for (var at = candidate; at is not null; at = at.Parent)
            {
                if (at == moving)
                {
                    return true;
                }
            }

            return false;
        }
    }

    private void AddMoveError(string message)
    {
        _moveMessages!.Add(new Microsoft.AspNetCore.Components.Forms.FieldIdentifier(_moveModel, ""), message);
        _moveContext!.NotifyValidationStateChanged();
    }

    /// <inheritdoc />
    internal override Func<TItem, TItem>? ChildFactory => NewChild;

    /// <inheritdoc />
    internal override bool NewRowOnTop => AddingUnder is null;

    /// <inheritdoc />
    // Adding under a closed row opens it, so the new row, and later the saved one, shows.
    internal override string AddUnderHref(string key)
    {
        var href = base.AddUnderHref(key);
        return !_treeRows.TryGetValue(key, out var row) || row.Open
            ? href
            : Navigation.GetUriWithQueryParameters(href, new Dictionary<string, object?> { [TreeOpenKey] = TreeToggled(key, false, row.OpenByDefault).ToArray() });
    }

    /// <inheritdoc />
    internal override bool AddsInPlace(string key) => !_treeRows.TryGetValue(key, out var row) || row.Open || !row.HasChildren;

    /// <inheritdoc />
    internal override async Task StartAddUnderAsync(string key)
    {
        if (_treeRows.TryGetValue(key, out var row) && !row.Open)
        {
            await SetTreeOpenAsync(TreeToggled(key, false, row.OpenByDefault));
        }

        await base.StartAddUnderAsync(key);
    }

    /// <summary>
    /// With an <c>ItemsProvider</c>, works out the footer's aggregates (a column's <c>Aggregate</c>)
    /// over every row the filters match, as the rows it holds are only a page's: answer it with
    /// <c>request.ToResultAsync</c> over the same query.
    /// </summary>
    [Parameter]
    public new Func<DataGroupRequest<TItem>, ValueTask<DataGroupResult>>? GroupsProvider { get => base.GroupsProvider; set => base.GroupsProvider = value; }

    // The last load's rows, by key, and the fields its request read them by: a row's branch totals
    // (RollUp) are worked out from them as its cells draw, once a load.
    private Dictionary<string, Node>? _treeNodes;
    private IReadOnlyList<DataField<TItem>>? _treeFields;
    private readonly Dictionary<string, DataGroup?> _treeRollUps = [];

    /// <inheritdoc />
    internal override bool RollsUp => ItemsProvider is null;

    /// <inheritdoc />
    internal override bool ExportsBranch => FilterBranch;

    /// <inheritdoc />
    // A row with rows under it shows its branch's total in a column with a RollUp.
    internal override RenderFragment CellOf(TItem item, ColumnBase<TItem> column) =>
        column.RollUp is { } type && _treeRows.TryGetValue(KeyOf(item), out var row) && row.HasChildren && RollUpOf(KeyOf(item)) is { } totals && column.TryAggregate(totals, type, out var value)
            ? builder => builder.AddContent(0, column.AggregateText(type, value))
            : base.CellOf(item, column);

    private bool _treeFiltering;

    /// <inheritdoc />
    // While filtering, a row with every child checked isn't checked itself: its children are only the matches.
    internal override string? CascadeMode => MultiSelect && Cascade ? (_treeFiltering ? "down" : "up") : null;

    /// <inheritdoc />
    // A row kept only for a match under it isn't a result, so it can't be selected.
    internal override bool HasSelectBox(TItem item) => !(_treeRows.TryGetValue(KeyOf(item), out var row) && row.Context);

    private string TreeOpenKey => $"{QueryPrefix}open";

    private string TreeMoreKey => $"{QueryPrefix}more";

    private IReadOnlyList<string>? _stateTreeMore;
    private Dictionary<string, List<TreeMore>> _treeMore = [];

    /// <summary>A Show more row, after the last row drawn under its parent.</summary>
    private sealed record TreeMore(string Parent, int Depth, int Shown, int Total);

    private IReadOnlyList<string>? _stateTreeOpen;
    private object? _treeOpenFor;
    private OpenState? _treeOpen;
    private Dictionary<string, TreeRow> _treeRows = [];
    private int _treeRootsShown;
    private bool _treeHasChildren;
    private int _treeDepth;

    /// <summary>A drawn row's place in the tree.</summary>
    private sealed record TreeRow(int Depth, int Position, int SetSize, bool HasChildren, bool Open, bool OpenByDefault, bool Context);

    private sealed class Node(TItem item, string key)
    {
        public TItem Item { get; } = item;
        public string Key { get; } = key;
        public Node? Parent { get; set; }
        public List<Node> Children { get; } = [];
        public int Rank { get; set; }
        public bool Kept { get; set; } = true;
        public bool Context { get; set; }
    }

    /// <summary>What's open: by key, with every row (<c>*</c>) or none (<c>!*</c>) as the default.</summary>
    private sealed record OpenState(bool All, bool None, IReadOnlyList<string> Open, IReadOnlyList<string> Shut)
    {
        public string Signature => $"{(All ? "*" : "")}{(None ? "!*" : "")}|{string.Join('/', Open)}|{string.Join('/', Shut)}";
    }

    private bool TreeBound => ExpandedItemsChanged.HasDelegate;

    /// <inheritdoc />
    internal override string ViewName => "TreeList";

    /// <inheritdoc />
    internal override bool PagesByTop => true;

    /// <inheritdoc />
    internal override bool ExtraBound => base.ExtraBound || TreeBound;

    /// <inheritdoc />
    internal override string FilterSignature() => $"{base.FilterSignature()}|{TreeOpen.Signature}|{string.Join('/', TreeMoreList)}";

    // Each row given more children, once for each Show more pressed under it.
    private IReadOnlyList<string> TreeMoreList => UseState
        ? _stateTreeMore ?? []
        : QueryString.Of(Navigation.Uri).TryGetValue(TreeMoreKey, out var values) ? values.OfType<string>().TakeLast(MaxOpenKeys).ToList() : [];

    private int ChildLimit(string key) => Math.Max(1, ChildRowLimit) * (1 + TreeMoreList.Count(k => k == key));

    private IReadOnlyList<string> TreeMoreWith(string key) => [.. TreeMoreList.TakeLast(MaxOpenKeys - 1), key];

    private string TreeMoreLink(string key) => LinkWith(new Dictionary<string, object?> { [TreeMoreKey] = TreeMoreWith(key).ToArray() });

    private async Task ShowMoreAsync(string key)
    {
        _stateTreeMore = TreeMoreWith(key);
        await ReloadIfStaleAsync();
    }

    private OpenState TreeOpen
    {
        get
        {
            var inputs = (UseState, UseState ? null : Navigation.Uri, _stateTreeOpen, ExpandedItems, TreeBound);
            if (_treeOpen is null || !Equals(_treeOpenFor, inputs))
            {
                (_treeOpenFor, _treeOpen) = (inputs, ReadTreeOpen());
            }

            return _treeOpen;
        }
    }

    private OpenState ReadTreeOpen()
    {
        IReadOnlyList<string> list;
        if (UseState)
        {
            list = TreeBound ? ExpandedItems ?? [] : _stateTreeOpen ?? ExpandedItems ?? [];
        }
        else
        {
            list = QueryString.Of(Navigation.Uri).TryGetValue(TreeOpenKey, out var values) ? values.OfType<string>().ToList() : [];
        }

        var open = list.Where(k => k != "*" && !k.StartsWith('!')).Distinct().TakeLast(MaxOpenKeys).ToList();
        var shut = list.Where(k => k.StartsWith('!') && k != "!*").Select(k => k[1..]).Distinct().TakeLast(MaxOpenKeys).ToList();
        return new OpenState(list.Contains("*"), list.Contains("!*"), open, shut);
    }

    // Whether a row is open, by the state, else by its default: every row, the ones ItemExpanded opens,
    // and while filtering, the rows above a match.
    private bool IsOpen(OpenState state, string key, TItem item, bool filtering)
    {
        if (state.Shut.Contains(key))
        {
            return false;
        }

        return state.Open.Contains(key) || state.All || (!state.None && OpensByDefault(item, filtering));
    }

    private bool OpensByDefault(TItem item, bool filtering) =>
        ExpandAll || filtering || ItemExpanded?.Invoke(item) == true;

    // The state with one row the other way: a key when its default is closed, "!key" when it's open.
    private IReadOnlyList<string> TreeToggled(string key, bool open, bool openByDefault)
    {
        var state = TreeOpen;
        var opens = state.Open.Where(k => k != key).ToList();
        var shuts = state.Shut.Where(k => k != key).ToList();
        var defaultOpen = state.All || (!state.None && openByDefault);
        if (open && defaultOpen)
        {
            shuts.Add(key);
        }
        else if (!open && !defaultOpen)
        {
            opens.Add(key);
        }

        IEnumerable<string> list = [.. state.All ? ["*"] : Array.Empty<string>(), .. state.None ? ["!*"] : Array.Empty<string>(),
            .. opens.TakeLast(MaxOpenKeys), .. shuts.TakeLast(MaxOpenKeys).Select(k => "!" + k)];
        return list.ToList();
    }

    private bool TreeAllOpen => TreeOpen is { Shut.Count: 0, None: false } state && (state.All || ExpandAll);

    private IReadOnlyList<string> TreeAll(bool open) => open ? ["*"] : ["!*"];

    private string TreeLink(IReadOnlyList<string> list) =>
        LinkWith(new Dictionary<string, object?> { [TreeOpenKey] = list.Count == 0 ? null : list.ToArray() });

    private async Task SetTreeOpenAsync(IReadOnlyList<string> list)
    {
        _stateTreeOpen = list;
        await ExpandedItemsChanged.InvokeAsync(list);
        await ReloadIfStaleAsync();
    }

    /// <inheritdoc />
    internal override void SeedStateFromAddress()
    {
        base.SeedStateFromAddress();
        if (!TreeBound && QueryString.Of(Navigation.Uri).TryGetValue(TreeOpenKey, out var values))
        {
            _stateTreeOpen = values.OfType<string>().ToList();
        }

        if (QueryString.Of(Navigation.Uri).TryGetValue(TreeMoreKey, out var more))
        {
            _stateTreeMore = more.OfType<string>().TakeLast(MaxOpenKeys).ToList();
        }
    }

    /// <inheritdoc />
    protected override Task OnParametersSetAsync()
    {
        if (ItemKey is null)
        {
            throw new InvalidOperationException("A TreeList needs an ItemKey: open rows are kept by it.");
        }

        if (ItemsProvider is not null && OnReparent.HasDelegate)
        {
            throw new InvalidOperationException($"A TreeList's {nameof(OnReparent)} moves rows among its {nameof(Items)}: it doesn't take an {nameof(ItemsProvider)} yet.");
        }

        if (ItemsProvider is not null)
        {
            if (ItemChildren is not null || ItemParentKey is not null)
            {
                throw new InvalidOperationException($"A TreeList with an {nameof(ItemsProvider)} loads each row's children from it (DataRequest.Parent): leave {nameof(ItemChildren)} and {nameof(ItemParentKey)} unset.");
            }
        }
        else if ((ItemChildren is null) == (ItemParentKey is null))
        {
            throw new InvalidOperationException($"A TreeList needs one of {nameof(ItemChildren)} (nested rows) or {nameof(ItemParentKey)} (a flat list), not both.");
        }

        if (SmallScreen == DataGridSmallScreen.HideColumns)
        {
            throw new InvalidOperationException("A TreeList can't hide columns to fit yet: it scrolls sideways.");
        }

        return base.OnParametersSetAsync();
    }

    /// <inheritdoc />
    internal override async ValueTask<GroupedPage?> LoadGroupedAsync(DataRequest<TItem> request, int page, int? pageSize)
    {
        var filtering = request.Filter is not null || !string.IsNullOrWhiteSpace(request.Search);
        var context = new HashSet<string>();
        IReadOnlyList<TItem> top;
        int total;
        var anyChildren = false;
        var deepest = 0;
        Func<string, int, Task<(IReadOnlyList<TItem> Items, int Total)>> childrenOf;
        Func<TItem, string, bool> mayHaveChildren;
        Dictionary<string, Node>? nodes = null;
        if (ItemsProvider is { } provider)
        {
            // One level at a time: the top rows, then the children of each open row.
            var ask = request with { Parent = null, FilterBranch = FilterBranch };
            var result = await provider(ask);
            if (pageSize is not null && result.Total is null)
            {
                throw new InvalidOperationException("A TreeList's ItemsProvider returns the total count of top rows, for its pages.");
            }

            total = result.Total ?? result.Items.Count;
            if (pageSize is { } size && result.Items.Count == 0 && total > 0 && page > LastPage(total, size))
            {
                page = LastPage(total, size);
                result = await provider(ask with { Skip = (page - 1) * size });
            }

            top = Mark(result.Items);
            childrenOf = async (key, take) =>
            {
                var children = await provider(request with { Parent = key, FilterBranch = FilterBranch, Skip = 0, Take = take, Cursor = null, NeedsTotal = true });
                return (Mark(children.Items), children.Total ?? children.Items.Count);
            };
            mayHaveChildren = (item, _) => ItemHasChildren?.Invoke(item) ?? true;

            // While filtering, a row the provider kept for a match under it is muted.
            IReadOnlyList<TItem> Mark(IReadOnlyList<TItem> items)
            {
                if (filtering)
                {
                    var matched = request.ApplyFiltering(AsQuery(items)).AsEnumerable().Select(KeyOf).ToHashSet();
                    context.UnionWith(items.Select(KeyOf).Where(k => !matched.Contains(k)));
                }

                return items;
            }
        }
        else
        {
            var roots = Build(SourceItems ?? [], request, out var all);
            if (filtering)
            {
                KeepMatches(all, request);
            }

            if (request.Sort.Count > 0)
            {
                var rank = 0;
                var ranks = new Dictionary<string, int>();
                foreach (var item in request.ApplySorting(AsQuery(all.Select(n => n.Item))))
                {
                    ranks.TryAdd(KeyOf(item), rank++);
                }

                foreach (var node in all)
                {
                    node.Rank = ranks.GetValueOrDefault(node.Key);
                }

                foreach (var node in all)
                {
                    node.Children.Sort((a, b) => a.Rank.CompareTo(b.Rank));
                }

                roots.Sort((a, b) => a.Rank.CompareTo(b.Rank));
            }

            var kept = roots.Where(n => n.Kept).ToList();
            total = kept.Count;
            if (pageSize is { } size)
            {
                page = Math.Clamp(page, 1, LastPage(total, size));
                kept = kept.Skip((page - 1) * size).Take(size).ToList();
            }

            top = kept.Select(n => n.Item).ToList();
            var byKey = all.ToDictionary(n => n.Key);
            context.UnionWith(all.Where(n => n.Kept && n.Context).Select(n => n.Key));
            anyChildren = all.Any(n => n.Children.Count > 0);
            deepest = all.Where(n => n.Kept).Select(n => { var d = 0; for (var up = n.Parent; up is not null; up = up.Parent) { d++; } return d; }).DefaultIfEmpty().Max();
            childrenOf = (key, take) =>
            {
                var children = byKey[key].Children.Where(c => c.Kept).ToList();
                return Task.FromResult<(IReadOnlyList<TItem>, int)>((children.Take(take).Select(c => c.Item).ToList(), children.Count));
            };
            mayHaveChildren = (_, key) => byKey[key].Children.Any(c => c.Kept);
            nodes = byKey;
        }

        var state = TreeOpen;
        var rows = new List<TItem>();
        var info = new Dictionary<string, TreeRow>();
        var more = new Dictionary<string, List<TreeMore>>();

        async Task Walk(IReadOnlyList<TItem> siblings, int offset, int setSize, int depth)
        {
            if (depth > MaxDepth)
            {
                throw new InvalidOperationException($"The TreeList's rows go deeper than {MaxDepth} levels.");
            }

            for (var i = 0; i < siblings.Count; i++)
            {
                var item = siblings[i];
                var key = KeyOf(item);
                if (info.ContainsKey(key))
                {
                    throw new InvalidOperationException($"The TreeList has the row \"{key}\" twice: each row needs its own key, and no row can be under itself.");
                }

                var has = mayHaveChildren(item, key);
                var open = has && IsOpen(state, key, item, filtering);
                IReadOnlyList<TItem> children = [];
                var count = 0;
                if (open)
                {
                    (children, count) = await childrenOf(key, ChildLimit(key));
                    has = open = children.Count > 0;
                }

                anyChildren |= has;
                deepest = Math.Max(deepest, depth);
                info[key] = new TreeRow(depth, offset + i + 1, setSize, has, open, OpensByDefault(item, filtering), context.Contains(key));
                rows.Add(item);
                if (open)
                {
                    await Walk(children, 0, count, depth + 1);
                    if (count > children.Count)
                    {
                        var last = KeyOf(rows[^1]);
                        if (!more.TryGetValue(last, out var after))
                        {
                            more[last] = after = [];
                        }

                        after.Add(new TreeMore(key, depth + 1, children.Count, count));
                    }
                }
            }
        }

        await Walk(top, pageSize is { } shownSize ? (page - 1) * shownSize : 0, total, 0);
        var shown = top.Count;
        return new GroupedPage(rows, total, page, () =>
        {
            (_treeRows, _treeRootsShown, _treeHasChildren, _treeDepth, _treeMore, _treeFiltering) = (info, shown, anyChildren, deepest, more, filtering);
            (_treeNodes, _treeFields) = (nodes, request.Fields);
            _treeRollUps.Clear();
        });
    }

    // The totals of a row's branch: over its kept rows with no kept rows under them, so a value
    // isn't counted again in the rows above it.
    private DataGroup? RollUpOf(string key)
    {
        if (_treeRollUps.TryGetValue(key, out var known))
        {
            return known;
        }

        var fields = Shown.Where(c => c.RollUp is not null).Select(c => new DataAggregateField(c.AggregateKey, c.RollUp!.Value)).Distinct().ToList();
        DataGroup? totals = null;
        if (fields.Count > 0 && _treeNodes?.GetValueOrDefault(key) is { } parent)
        {
            var leaves = new List<TItem>();
            var stack = new Stack<Node>(parent.Children.Where(c => c.Kept));
            while (stack.TryPop(out var node))
            {
                var under = node.Children.Where(c => c.Kept).ToList();
                if (under.Count == 0)
                {
                    leaves.Add(node.Item);
                }

                under.ForEach(stack.Push);
            }

            var request = new DataGroupRequest<TItem> { Aggregates = fields, Fields = _treeFields };
            totals = request.ToResult(AsQuery(leaves)).Groups.FirstOrDefault();
        }

        return _treeRollUps[key] = totals;
    }

    // The nodes, linked: the top ones in order, and every one in all.
    private List<Node> Build(IEnumerable<TItem> source, DataRequest<TItem> request, out List<Node> all)
    {
        var list = new List<Node>();
        var roots = new List<Node>();
        if (ItemChildren is { } children)
        {
            var seen = new HashSet<string>();
            void Add(TItem item, Node? parent, int depth)
            {
                var key = KeyOf(item);
                if (depth > MaxDepth || !seen.Add(key))
                {
                    throw new InvalidOperationException($"The TreeList's rows go deeper than {MaxDepth} levels or repeat a key (\"{key}\"): each row needs its own key, and no row can be under itself.");
                }

                var node = new Node(item, key) { Parent = parent };
                list.Add(node);
                (parent?.Children ?? roots).Add(node);
                foreach (var child in children(item) ?? [])
                {
                    Add(child, node, depth + 1);
                }
            }

            foreach (var item in source)
            {
                Add(item, null, 0);
            }
        }
        else
        {
            var byKey = new Dictionary<string, Node>();
            foreach (var item in source)
            {
                var node = new Node(item, KeyOf(item));
                if (!byKey.TryAdd(node.Key, node))
                {
                    throw new InvalidOperationException($"The TreeList has two rows with the key \"{node.Key}\": each row needs its own.");
                }

                list.Add(node);
            }

            foreach (var node in list)
            {
                var parentKey = ItemParentKey!(node.Item);
                if (parentKey is not null && parentKey != node.Key && byKey.TryGetValue(parentKey, out var parent))
                {
                    node.Parent = parent;
                    parent.Children.Add(node);
                }
                else
                {
                    roots.Add(node);
                }
            }

            // A row in a loop of parents is under no top row, so it would never show.
            var reached = 0;
            var stack = new Stack<(Node Node, int Depth)>(roots.Select(r => (r, 0)));
            while (stack.TryPop(out var at))
            {
                reached++;
                if (at.Depth > MaxDepth)
                {
                    throw new InvalidOperationException($"The TreeList's rows go deeper than {MaxDepth} levels.");
                }

                foreach (var child in at.Node.Children)
                {
                    stack.Push((child, at.Depth + 1));
                }
            }

            if (reached < list.Count)
            {
                var stuck = list.First(n => n.Parent is not null && !Reaches(n));
                throw new InvalidOperationException($"The TreeList's row \"{stuck.Key}\" is under itself, through its parents: no row can be.");
            }
        }

        all = list;
        return roots;
    }

    private static bool Reaches(Node node)
    {
        var seen = new HashSet<Node>();
        for (var at = node; at is not null; at = at.Parent)
        {
            if (!seen.Add(at))
            {
                return false;
            }
        }

        return true;
    }

    // Keeps the matches, the rows above them (muted) and, with FilterBranch, the rows under them.
    private void KeepMatches(List<Node> all, DataRequest<TItem> request)
    {
        var matched = request.ApplyFiltering(AsQuery(all.Select(n => n.Item))).AsEnumerable().Select(KeyOf).ToHashSet();
        foreach (var node in all)
        {
            node.Kept = false;
        }

        foreach (var node in all)
        {
            if (!matched.Contains(node.Key))
            {
                continue;
            }

            node.Kept = true;
            node.Context = false;
            for (var up = node.Parent; up is not null && !up.Kept; up = up.Parent)
            {
                up.Kept = true;
                up.Context = !matched.Contains(up.Key);
            }

            if (FilterBranch)
            {
                var stack = new Stack<Node>(node.Children);
                while (stack.TryPop(out var down))
                {
                    if (!down.Kept)
                    {
                        down.Kept = true;
                        down.Context = false;
                    }

                    foreach (var child in down.Children)
                    {
                        stack.Push(child);
                    }
                }
            }
        }
    }

    /// <inheritdoc />
    internal override string Summary(int size)
    {
        if (_treeRootsShown == 0)
        {
            return "";
        }

        var first = (_page - 1L) * size + 1;
        return string.Format(CultureInfo.CurrentCulture, TextDefaults.TableRange, first, first + _treeRootsShown - 1, _total ?? 0);
    }

    /// <inheritdoc />
    internal override IReadOnlyDictionary<string, object>? RowAttributes(TItem item)
    {
        var own = base.RowAttributes(item);
        if (!_treeRows.TryGetValue(KeyOf(item), out var row))
        {
            return own;
        }

        var attributes = own is null ? new Dictionary<string, object>() : new Dictionary<string, object>(own);
        attributes["aria-level"] = row.Depth + 1;
        attributes["aria-posinset"] = row.Position;
        attributes["aria-setsize"] = row.SetSize;
        if (row.HasChildren)
        {
            attributes["aria-expanded"] = row.Open ? "true" : "false";
        }

        return attributes;
    }

    /// <inheritdoc />
    // Without a RowLabel, a row is named by its first column, its name in a tree: "Expand Ada Lovelace".
    internal override string LabelOf(TItem item) =>
        RowLabel?.Invoke(item) ?? (Shown.Count > 0 && Shown[0].CellText(item) is { Length: > 0 } text ? text : KeyOf(item));

    /// <inheritdoc />
    // The deepest row, open or not, its chevron and its icon: a tree's names aren't squeezed by their indent.
    internal override string? FirstColumnLead
    {
        get
        {
            if (!_treeHasChildren && ItemIcon is null)
            {
                return null;
            }

            var steps = _treeDepth + (_treeHasChildren ? 1 : 0);
            var icon = ItemIcon is null ? "" : " + 1.25rem";
            return $"{steps} * (var(--ib-treelist-toggle) + var(--ib-treelist-gap)){icon}";
        }
    }

    /// <inheritdoc />
    internal override string? ItemRowClass(TItem item) => _treeRows.TryGetValue(KeyOf(item), out var row) && row.Context ? "ib-treelist-context" : null;
}
