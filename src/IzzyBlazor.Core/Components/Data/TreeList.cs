using System.Globalization;
using Microsoft.AspNetCore.Components;

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

    private string TreeOpenKey => $"{QueryPrefix}open";

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
    internal override string FilterSignature() => $"{base.FilterSignature()}|{TreeOpen.Signature}";

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
    private bool IsOpen(OpenState state, Node node, bool filtering)
    {
        if (state.Shut.Contains(node.Key))
        {
            return false;
        }

        return state.Open.Contains(node.Key) || state.All || (!state.None && OpensByDefault(node, filtering));
    }

    private bool OpensByDefault(Node node, bool filtering) =>
        ExpandAll || filtering || ItemExpanded?.Invoke(node.Item) == true;

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
    }

    /// <inheritdoc />
    protected override Task OnParametersSetAsync()
    {
        if (ItemKey is null)
        {
            throw new InvalidOperationException("A TreeList needs an ItemKey: open rows are kept by it.");
        }

        if ((ItemChildren is null) == (ItemParentKey is null))
        {
            throw new InvalidOperationException($"A TreeList needs one of {nameof(ItemChildren)} (nested rows) or {nameof(ItemParentKey)} (a flat list), not both.");
        }

        if (ItemsProvider is not null)
        {
            throw new InvalidOperationException($"A TreeList takes its rows from Items for now, not an {nameof(ItemsProvider)}.");
        }

        if (SmallScreen == DataGridSmallScreen.HideColumns)
        {
            throw new InvalidOperationException("A TreeList can't hide columns to fit yet: it scrolls sideways.");
        }

        return base.OnParametersSetAsync();
    }

    /// <inheritdoc />
    internal override ValueTask<GroupedPage?> LoadGroupedAsync(DataRequest<TItem> request, int page, int? pageSize)
    {
        var roots = Build(SourceItems ?? [], request, out var all);
        var filtering = request.Filter is not null || !string.IsNullOrWhiteSpace(request.Search);
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
        var total = kept.Count;
        var first = 0;
        var shown = kept;
        if (pageSize is { } size)
        {
            page = Math.Clamp(page, 1, LastPage(total, size));
            first = (page - 1) * size;
            shown = kept.Skip(first).Take(size).ToList();
        }

        var state = TreeOpen;
        var rows = new List<TItem>();
        var info = new Dictionary<string, TreeRow>();
        var anyChildren = all.Any(n => n.Children.Count > 0);
        var deepest = all.Where(n => n.Kept).Select(n => { var d = 0; for (var up = n.Parent; up is not null; up = up.Parent) { d++; } return d; }).DefaultIfEmpty().Max();

        void Walk(List<Node> siblings, int offset, int setSize, int depth)
        {
            for (var i = 0; i < siblings.Count; i++)
            {
                var node = siblings[i];
                var children = node.Children.Where(c => c.Kept).ToList();
                var open = children.Count > 0 && IsOpen(state, node, filtering);
                info[node.Key] = new TreeRow(depth, offset + i + 1, setSize, children.Count > 0, open, OpensByDefault(node, filtering), node.Context);
                rows.Add(node.Item);
                if (open)
                {
                    Walk(children, 0, children.Count, depth + 1);
                }
            }
        }

        Walk(shown, first, total, 0);
        var count = shown.Count;
        return ValueTask.FromResult<GroupedPage?>(new GroupedPage(rows, total, page, () => (_treeRows, _treeRootsShown, _treeHasChildren, _treeDepth) = (info, count, anyChildren, deepest)));
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
