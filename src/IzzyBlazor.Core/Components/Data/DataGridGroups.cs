using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

// A grid's groups: what's grouped and open (the address, or state when bound), and loading a page of
// groups. A page counts top-level groups. An open group's rows are one ordinary request, with its
// values in the filter, so an ItemsProvider needn't know about groups.
/// <summary>Where a <c>DataGrid</c> draws a group's aggregates.</summary>
public enum DataGridGroupAggregates
{
    /// <summary>On the group's own row, in their columns, whether it's open or not. The default.</summary>
    Row,

    /// <summary>On a row under the group's rows, while it's open.</summary>
    Footer,
}

/// <summary>A group, for a column's <c>GroupTemplate</c>.</summary>
/// <param name="Value">Its value, or null for none.</param>
/// <param name="Text">Its value as the column shows it.</param>
/// <param name="Group">Its count and aggregates, for the whole group.</param>
/// <param name="Level">Its level, from 0.</param>
/// <param name="IsOpen">Whether it's open.</param>
public sealed record DataGridGroup(object? Value, string Text, DataGroup Group, int Level, bool IsOpen);

/// <summary>A grid's groups.</summary>
/// <typeparam name="TItem">The type of the items.</typeparam>
public partial class DataGrid<TItem>
{
    private const int MaxOpenPaths = 50;
    private const int MaxGroupsOnALevel = 2000;

    /// <summary>
    /// Lets people group by columns, from a header's menu. Grouped, the grid's pages count top-level
    /// groups, so a group is never split, and each group's count and aggregates are the whole group's.
    /// With an <c>ItemsProvider</c>, it needs a <see cref="GroupsProvider"/>.
    /// </summary>
    [Parameter] public bool Groupable { get; set; }

    /// <summary>
    /// The grouping, outermost first, such as <c>"status,-customer"</c> (<c>-</c> for descending); null for
    /// <see cref="InitialGroup"/>. Works without <see cref="Groupable"/>, for a grouping people can't change.
    /// Binding it (<c>@bind-Group</c>) on an interactive page keeps it in the component instead of the URL.
    /// </summary>
    [Parameter] public string? Group { get; set; }

    /// <summary>The grouping when the URL has none, such as <c>"status"</c>.</summary>
    [Parameter] public string? InitialGroup { get; set; }

    /// <summary>Raised when the grouping changes, on interactive pages.</summary>
    [Parameter, ModeSupport(NoScript = ModeLevel.None, Static = ModeLevel.None)]
    public EventCallback<string?> GroupChanged { get; set; }

    /// <summary>Whether groups start open. They start closed by default, which is what a page of groups is for.</summary>
    [Parameter] public bool GroupsOpen { get; set; }

    /// <summary>
    /// Which groups are open, each by its values joined with <c>/</c>, escaped, such as <c>"Shipped/Acme"</c>; <c>"*"</c>
    /// is all of them, and <c>"!Shipped"</c> an exception to that. Binding it on an interactive page keeps it in the component.
    /// </summary>
    [Parameter] public IReadOnlyList<string>? OpenGroups { get; set; }

    /// <summary>Raised when a group opens or closes, on interactive pages.</summary>
    [Parameter, ModeSupport(NoScript = ModeLevel.None, Static = ModeLevel.None)]
    public EventCallback<IReadOnlyList<string>?> OpenGroupsChanged { get; set; }

    /// <summary>
    /// The most rows drawn in one open group. Past it, the group says how many it has and links to
    /// showing only that group, with the grid's pages. Defaults to 100.
    /// </summary>
    [Parameter] public int GroupRowLimit { get; set; } = 100;

    /// <summary>Where a group's aggregates are drawn: on its own row, or under its rows when it's open.</summary>
    [Parameter] public DataGridGroupAggregates GroupAggregates { get; set; }

    /// <summary>Keeps a grouped column in the table. By default it leaves, as each group's row says its value.</summary>
    [Parameter] public bool ShowGroupedColumns { get; set; }

    private string GroupKey => $"{QueryPrefix}group";

    private string OpenKey => $"{QueryPrefix}open";

    private string ShutKey => $"{QueryPrefix}shut";

    private string? _stateGroup;
    private bool _stateGroupSet;
    private IReadOnlyList<string> _stateOpen = [];

    private List<GroupNode>? _groupRoots;

    /// <summary>One level of the grouping: its column, and the field a request names.</summary>
    internal readonly record struct GroupLevel(ColumnBase<TItem> Column, string Field, bool Descending);

    /// <summary>What's grouped and open now.</summary>
    internal sealed record GroupState(IReadOnlyList<GroupLevel> Levels, bool AllOpen, IReadOnlyList<string> Open, IReadOnlyList<string> Shut)
    {
        public bool IsOpen(string path) => AllOpen ? !Shut.Contains(path) : Open.Contains(path);

        public string Signature => $"{string.Join(",", Levels.Select(l => (l.Descending ? "-" : "") + l.Field))}|{AllOpen}|{string.Join(" ", Open)}|{string.Join(" ", Shut)}";
    }

    /// <summary>A group on the page, with what's under it when it's open.</summary>
    internal sealed class GroupNode(DataGroup group, GroupLevel level, int depth, string path, object? value, int position, int setSize, bool open)
    {
        public DataGroup Group => group;

        public GroupLevel Level => level;

        public int Depth => depth;

        public string Path => path;

        public object? Value => value;

        public int Position => position;

        public int SetSize => setSize;

        public bool Open => open;

        public List<GroupNode> Children { get; } = [];

        public IReadOnlyList<TItem> Rows { get; set; } = [];

        public int FirstRow { get; set; }
    }

    /// <inheritdoc />
    internal override bool GroupedNow => Grouping.Levels.Count > 0;

    /// <inheritdoc />
    internal override bool GroupsBy(ColumnBase<TItem> column) => Grouping.Levels.Any(l => l.Column == column);

    private object? _groupingFor;
    private GroupState? _grouping;

    // Read again when what it's read from changes: the columns, the address or the state. Asked for at
    // every cell, through the columns shown.
    internal GroupState Grouping
    {
        get
        {
            var inputs = (UseState, UseState ? null : Navigation.Uri, Columns.Count, (Group, InitialGroup, GroupsOpen, Groupable), (_stateGroupSet, _stateGroup, _stateOpen, OpenGroups), (GroupChanged.HasDelegate, OpenGroupsChanged.HasDelegate));
            if (_grouping is null || !Equals(_groupingFor, inputs))
            {
                (_groupingFor, _grouping) = (inputs, ReadGrouping());
            }

            return _grouping;
        }
    }

    private GroupState ReadGrouping()
    {
        {
            var query = UseState ? null : QueryString.Of(Navigation.Uri);
            var text = UseState
                ? GroupChanged.HasDelegate ? Group : _stateGroupSet ? _stateGroup : Group ?? InitialGroup
                : Groupable && query!.TryGetValue(GroupKey, out var inAddress) ? inAddress.ToString() : Group ?? InitialGroup;

            var levels = new List<GroupLevel>();
            foreach (var token in SortField.ParseList(text))
            {
                if (levels.Count < DataGroupRequest<TItem>.MaxLevels
                    && Columns.FirstOrDefault(c => c.CanGroup && string.Equals(c.GroupToken, token.Key, StringComparison.OrdinalIgnoreCase)) is { } column
                    && levels.All(l => l.Column != column))
                {
                    levels.Add(new GroupLevel(column, column.GroupField, token.Descending));
                }
            }

            IReadOnlyList<string> open, shut;
            if (UseState)
            {
                var list = OpenGroupsChanged.HasDelegate ? OpenGroups ?? [] : _stateOpen.Count > 0 || OpenGroups is null ? _stateOpen : OpenGroups;
                open = list.Where(p => !p.StartsWith('!')).Take(MaxOpenPaths).ToList();
                shut = list.Where(p => p.StartsWith('!')).Select(p => p[1..]).Take(MaxOpenPaths).ToList();
            }
            else
            {
                open = query!.TryGetValue(OpenKey, out var o) ? o.OfType<string>().Take(MaxOpenPaths).ToList() : [];
                shut = query.TryGetValue(ShutKey, out var s) ? s.OfType<string>().Take(MaxOpenPaths).ToList() : [];
            }

            var all = open.Contains("*") || (GroupsOpen && open.Count == 0 && !shut.Contains("*"));
            return new GroupState(levels, all, open, shut);
        }
    }

    // A value's place in a path: invariant, escaped, with its own tokens for no value and an empty one.
    private static string PathPart(object? value)
    {
        var text = value switch
        {
            null => null,
            DateTime d => d.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset d => d.ToString("O", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };
        return text switch
        {
            null => "~",
            "" => "~~",
            _ => Uri.EscapeDataString(text).Replace("~", "%7E", StringComparison.Ordinal),
        };
    }

    private static IzzyBlazor.Filter PathFilter(IEnumerable<(string Field, object? Value)> path) =>
        IzzyBlazor.Filter.All(path.Select(p => (IzzyBlazor.Filter)new FieldFilter(p.Field, FilterOperator.Equals, p.Value, MatchCase: true)).ToArray());

    private static IzzyBlazor.Filter? And(IzzyBlazor.Filter? first, IzzyBlazor.Filter second) =>
        first is null ? second : IzzyBlazor.Filter.All(first, second);

    /// <inheritdoc />
    internal override async ValueTask<GroupedPage?> LoadGroupedAsync(DataRequest<TItem> request, int page, int? pageSize)
    {
        var state = Grouping;
        if (state.Levels.Count == 0)
        {
            return null;
        }

        var aggregates = AggregateFields();
        DataGroupRequest<TItem> Asking(int levels) => new()
        {
            Keys = state.Levels.Take(levels).Select(l => new SortField(l.Field, l.Descending)).ToList(),
            Aggregates = aggregates,
            Filter = request.Filter,
            Search = request.Search,
            Take = MaxGroupsOnALevel,
            Fields = request.Fields,
            CancellationToken = request.CancellationToken,
        };

        // The page's top-level groups, and how many there are in all.
        var top = Asking(1) with { Skip = pageSize is { } size ? (page - 1) * size : 0, Take = pageSize ?? MaxGroupsOnALevel, NeedsTotal = true };
        var result = await LoadGroupsAsync(top);
        var total = result.Total ?? (pageSize is null ? result.Groups.Count : throw new InvalidOperationException($"The DataGrid's {nameof(GroupsProvider)} returned no {nameof(DataGroupResult.Total)}, which pages of groups need."));
        if (result.Groups.Count == 0 && total > 0 && pageSize is { } last && page > LastPage(total, last))
        {
            page = LastPage(total, last);
            result = await LoadGroupsAsync(top with { Skip = (page - 1) * last });
        }

        var roots = Nodes(result.Groups, state, 0, "", top.Skip, total);

        // Each deeper level under the groups open above it, in one request.
        var above = roots;
        for (var depth = 1; depth < state.Levels.Count; depth++)
        {
            var parents = above.Where(n => n.Open).ToList();
            above = [];
            if (parents.Count == 0)
            {
                break;
            }

            var under = await LoadGroupsAsync(Asking(depth + 1) with
            {
                Filter = And(request.Filter, IzzyBlazor.Filter.Any(parents.Select(p => PathFilter(PathOf(p, state))).ToArray<IzzyBlazor.Filter>())),
            });
            foreach (var parent in parents)
            {
                var mine = under.Groups.Where(g => g.Keys.Count > depth && SamePath(ParentPath(g, depth, state), parent.Path)).ToList();
                parent.Children.AddRange(Nodes(mine, state, depth, parent.Path + "/", 0, mine.Count));
                above.AddRange(parent.Children);
            }
        }

        // Each open group at the last level: its rows, by the ordinary request.
        var rows = new List<TItem>();
        foreach (var leaf in Leaves(roots, state.Levels.Count - 1))
        {
            var ask = request with
            {
                Filter = And(request.Filter, PathFilter(PathOf(leaf, state))),
                Skip = 0,
                Take = Math.Max(1, GroupRowLimit),
                Cursor = null,
                NeedsTotal = false,
            };
            leaf.Rows = ItemsProvider is { } provider ? (await provider(ask)).Items : ask.Apply(AsQuery(SourceItems ?? [])).ToList();
            leaf.FirstRow = rows.Count;
            rows.AddRange(leaf.Rows);
        }

        return new GroupedPage(rows, total, page, () => _groupRoots = roots);
    }

    /// <inheritdoc />
    internal override void NotGrouped() => _groupRoots = null;

    private static IEnumerable<GroupNode> Leaves(IEnumerable<GroupNode> nodes, int lastDepth) =>
        nodes.Where(n => n.Open).SelectMany(n => n.Depth == lastDepth ? [n] : Leaves(n.Children, lastDepth));

    private static bool SamePath(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static string ParentPath(DataGroup group, int depth, GroupState state) =>
        string.Join("/", group.Keys.Take(depth).Select((k, i) => PathPart(state.Levels[i].Column.GroupValue(k))));

    private static IEnumerable<(string Field, object? Value)> PathOf(GroupNode node, GroupState state) =>
        node.Group.Keys.Select((k, i) => (state.Levels[i].Field, state.Levels[i].Column.GroupValue(k)));

    private static List<GroupNode> Nodes(IReadOnlyList<DataGroup> groups, GroupState state, int depth, string prefix, int skip, int setSize)
    {
        var level = state.Levels[depth];
        return groups.Select((group, i) =>
        {
            var value = level.Column.GroupValue(group.Keys[depth]);
            var path = prefix + PathPart(value);
            return new GroupNode(group, level, depth, path, value, skip + i + 1, setSize, state.IsOpen(path));
        }).ToList();
    }

    private bool GroupedColumnLeaves(ColumnBase<TItem> column) => !ShowGroupedColumns && GroupsBy(column);

    // The address with this group the other way: an exception to all open, or one of those open.
    private string ToggleHref(GroupNode node)
    {
        var state = Grouping;
        if (state.AllOpen)
        {
            return LinkWith(new Dictionary<string, object?> { [ShutKey] = Toggled(state.Shut, node.Path, node.Open) });
        }

        return LinkWith(new Dictionary<string, object?> { [OpenKey] = Toggled(state.Open, node.Path, !node.Open) });
    }

    private static string[]? Toggled(IReadOnlyList<string> paths, string path, bool add)
    {
        // A group closing takes what's open under it along.
        var kept = paths.Where(p => p != path && p != "*" && !p.StartsWith(path + "/", StringComparison.Ordinal));
        var list = (add ? kept.Append(path) : kept).ToList();
        return list.Count == 0 ? null : list.TakeLast(MaxOpenPaths).ToArray();
    }

    // The columns and values that make the ordinary grid show only this group: every level's column
    // must take an equals filter on a value. Null when one can't.
    private List<(ColumnBase<TItem> Column, string Value)>? OnlyThisGroup(GroupNode node)
    {
        var levels = Grouping.Levels;
        var filters = new List<(ColumnBase<TItem>, string)>();
        for (var i = 0; i < node.Group.Keys.Count; i++)
        {
            var column = levels[i].Column;
            if (!column.CanFilter || column.GroupBy is not null || column.GroupValue(node.Group.Keys[i]) is not { } value || !(column.FilterKind == ColumnFilterKind.Choice || ColumnFilter.OperatorsOf(column).Contains(FilterOperator.Equals)))
            {
                return null;
            }

            filters.Add((column, value is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : value.ToString() ?? ""));
        }

        return filters;
    }

    private string OnlyThisGroupHref(IEnumerable<(ColumnBase<TItem> Column, string Value)> filters)
    {
        var changes = FilterChanges(filters);
        changes[GroupKey] = "";
        changes[OpenKey] = null;
        changes[ShutKey] = null;
        return LinkWith(changes);
    }

    private async Task OnlyThisGroupAsync(IEnumerable<(ColumnBase<TItem> Column, string Value)> filters)
    {
        foreach (var (column, value) in filters)
        {
            await SetEqualsFilterAsync(column, value);
        }

        await SetGroupAsync(null);
    }

    private async Task ToggleGroupAsync(GroupNode node)
    {
        var state = Grouping;
        var list = state.AllOpen
            ? (Toggled(state.Shut, node.Path, node.Open) ?? []).Select(p => "!" + p).Prepend("*").ToList()
            : (Toggled(state.Open, node.Path, !node.Open) ?? []).ToList();
        _stateOpen = list;
        await OpenGroupsChanged.InvokeAsync(list);
        await ReloadIfStaleAsync();
    }

    /// <summary>What's grouped and open, for telling whether it changed since the last load.</summary>
    private string GroupSignature => Grouping.Signature;

    // A new grouping, from the toolbar or a header: it starts from the first page, with nothing open.
    private async Task SetGroupAsync(string? text)
    {
        (_stateGroup, _stateGroupSet, _stateOpen, _statePage, _stateCursor) = (text, true, [], 1, null);
        await GroupChanged.InvokeAsync(text);
        if (OpenGroupsChanged.HasDelegate)
        {
            await OpenGroupsChanged.InvokeAsync([]);
        }

        await PageChanged.InvokeAsync(1);
        await ReloadIfStaleAsync();
    }

    private bool CanGroupBy(ColumnBase<TItem> column) => Groupable && column.CanGroup;

    // The grouping with this column added at the end, or taken out.
    private string GroupTextWith(ColumnBase<TItem> column, bool on)
    {
        var tokens = Grouping.Levels.Where(l => l.Column != column).Select(l => (l.Descending ? "-" : "") + l.Column.GroupToken);
        return string.Join(",", on ? tokens.Append(column.GroupToken) : tokens);
    }

    // A new grouping starts at the first page with nothing open. None at all is an empty value, or
    // InitialGroup would come back.
    private string GroupHref(string text) => LinkWith(new Dictionary<string, object?>
    {
        [GroupKey] = text.Length > 0 || Group is not null || InitialGroup is not null ? text : null,
        [OpenKey] = null,
        [ShutKey] = null,
        [PageKey] = null,
        [CursorKey] = null,
    });

    private string AllOpenHref(bool open) => LinkWith(new Dictionary<string, object?>
    {
        [OpenKey] = open && !GroupsOpen ? "*" : null,
        [ShutKey] = !open && GroupsOpen ? "*" : null,
    });

    private async Task SetAllOpenAsync(bool open)
    {
        IReadOnlyList<string> list = open ? ["*"] : GroupsOpen ? ["!*"] : [];
        _stateOpen = list;
        await OpenGroupsChanged.InvokeAsync(list);
        await ReloadIfStaleAsync();
    }

    private bool GroupBound => GroupChanged.HasDelegate || OpenGroupsChanged.HasDelegate;
}
