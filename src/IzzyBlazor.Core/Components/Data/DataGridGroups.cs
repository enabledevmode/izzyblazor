using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

// A grid's groups: what's grouped and open (the address, or state when bound), and loading a page of
// groups. A page counts top-level groups. An open group's rows are one ordinary request, with its
// values in the filter, so an ItemsProvider needn't know about groups.
/// <summary>An aggregate, for a column's <c>AggregateTemplate</c>.</summary>
/// <param name="Type">What it is.</param>
/// <param name="Value">Its value, as the column's type for a Min or Max.</param>
/// <param name="Text">Its value in the column's format.</param>
/// <param name="Group">The group it's of: in the footer, all the rows.</param>
public sealed record DataGridAggregate(DataAggregate Type, object? Value, string? Text, DataGroup Group);

/// <summary>A group, for a column's <c>GroupTemplate</c>.</summary>
/// <param name="Value">Its value, or null for none.</param>
/// <param name="Text">Its value as the column shows it.</param>
/// <param name="Group">Its count and aggregates, for the whole group.</param>
/// <param name="Level">Its level, from 0.</param>
/// <param name="IsOpen">Whether it's open.</param>
public sealed record DataGridGroup(object? Value, string Text, DataGroup Group, int Level, bool IsOpen);

/// <summary>
/// What <see cref="DataGrid{TItem}"/> and <see cref="TreeList{TItem}"/> share: the grid's columns,
/// filters, toolbar, editing and export.
/// </summary>
/// <typeparam name="TItem">The type of the items.</typeparam>
public abstract partial class GridViewBase<TItem>
{
    private const int MaxOpenPaths = 50;
    private const int MaxGroupsOnALevel = 2000;

    internal bool Groupable { get; set; }

    internal string? Group { get; set; }

    internal string? InitialGroup { get; set; }

    internal EventCallback<string?> GroupChanged { get; set; }

    internal bool ExpandAllGroups { get; set; }

    internal IReadOnlyList<string>? ExpandedGroups { get; set; }

    internal EventCallback<IReadOnlyList<string>?> ExpandedGroupsChanged { get; set; }

    internal int GroupRowLimit { get; set; } = 100;

    internal bool GroupPanel { get; set; }

    internal bool ShowGroupedColumns { get; set; }

    private string GroupKey => $"{QueryPrefix}group";

    private string OpenKey => $"{QueryPrefix}open";

    private string ShutKey => $"{QueryPrefix}shut";

    private string? _stateGroup;
    private bool _stateGroupSet;
    private IReadOnlyList<string>? _stateOpen;

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
            var inputs = (UseState, UseState ? null : Navigation.Uri, AllColumns.Count, (Group, InitialGroup, ExpandAllGroups, Groupable), (_stateGroupSet, _stateGroup, _stateOpen, ExpandedGroups), (GroupChanged.HasDelegate, ExpandedGroupsChanged.HasDelegate));
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
            // What the page says can be anything; what's in an address only what people could choose.
            string? text;
            bool chosen;
            if (UseState)
            {
                chosen = !GroupChanged.HasDelegate && _stateGroupSet;
                text = GroupChanged.HasDelegate ? Group ?? InitialGroup : chosen ? _stateGroup : Group ?? InitialGroup;
            }
            else
            {
                chosen = query!.TryGetValue(GroupKey, out var inAddress) && TakesAddressGroup(inAddress.ToString());
                text = chosen ? query[GroupKey].ToString() : Group ?? InitialGroup;
            }

            var levels = new List<GroupLevel>();
            foreach (var token in SortField.ParseList(text))
            {
                if (levels.Count < DataGroupRequest<TItem>.MaxLevels
                    && AllColumns.FirstOrDefault(c => c.CanGroup && string.Equals(c.GroupToken, token.Key, StringComparison.OrdinalIgnoreCase)) is { } column
                    && (!chosen || column.Visible || column.Pickable)
                    && levels.All(l => l.Column != column))
                {
                    levels.Add(new GroupLevel(column, column.GroupField, token.Descending));
                }
            }

            IReadOnlyList<string> open, shut;
            if (UseState)
            {
                var list = ExpandedGroupsChanged.HasDelegate ? ExpandedGroups ?? [] : _stateOpen ?? ExpandedGroups ?? [];
                open = list.Where(p => !p.StartsWith('!')).Take(MaxOpenPaths).ToList();
                shut = list.Where(p => p.StartsWith('!')).Select(p => p[1..]).Take(MaxOpenPaths).ToList();
            }
            else
            {
                open = query!.TryGetValue(OpenKey, out var o) ? o.OfType<string>().Take(MaxOpenPaths).ToList() : [];
                shut = query.TryGetValue(ShutKey, out var s) ? s.OfType<string>().Take(MaxOpenPaths).ToList() : [];
            }

            var all = open.Contains("*") || (ExpandAllGroups && open.Count == 0 && !shut.Contains("*"));
            return new GroupState(levels, all, open, shut);
        }
    }

    // A value's place in a path: invariant, escaped, with its own tokens for no value and an empty one.
    private static string PathPart(object? value)
    {
        // The same in any culture, so a shared link opens the same group.
        var text = value switch
        {
            null => null,
            DateOnly or bool => FilterConvert.Format(value),
            DateTime d => d.TimeOfDay == TimeSpan.Zero ? FilterConvert.Format(d) : d.ToString("O", CultureInfo.InvariantCulture),
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

        var aggregates = AggregateFields(AggregatePlace.GroupRow, AggregatePlace.GroupFooter);
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
        var skip = pageSize is { } size ? (page - 1) * size : 0;
        var top = Asking(1) with { Skip = skip, Take = pageSize ?? MaxGroupsOnALevel, NeedsTotal = true };
        var result = await LoadGroupsAsync(top);
        var total = result.Total ?? (pageSize is null ? result.Groups.Count : throw new InvalidOperationException($"The DataGrid's {nameof(GroupsProvider)} returned no {nameof(DataGroupResult.Total)}, which pages of groups need."));
        if (result.Groups.Count == 0 && total > 0 && pageSize is { } last && page > LastPage(total, last))
        {
            page = LastPage(total, last);
            skip = (page - 1) * last;
            result = await LoadGroupsAsync(top with { Skip = skip });
        }

        var roots = Nodes(result.Groups, state, 0, "", skip, total);

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
            // By the exact path, or ignoring case when the database's comparison does, and it wrote the
            // value differently this time. Two values that differ by case keep their own children.
            var mine = under.Groups.Where(g => g.Keys.Count > depth).GroupBy(g => ParentPath(g, depth, state), StringComparer.Ordinal)
                .ToLookup(g => (parents.FirstOrDefault(p => p.Path == g.Key) ?? parents.FirstOrDefault(p => string.Equals(p.Path, g.Key, StringComparison.OrdinalIgnoreCase)))?.Path);
            foreach (var parent in parents)
            {
                var children = mine[parent.Path].SelectMany(g => g).ToList();
                parent.Children.AddRange(Nodes(children, state, depth, parent.Path + "/", 0, children.Count));
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

            filters.Add((column, FilterConvert.Format(value)));
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

        await SetGroupAsync("");
    }

    private async Task ToggleGroupAsync(GroupNode node)
    {
        var state = Grouping;
        var list = state.AllOpen
            ? (Toggled(state.Shut, node.Path, node.Open) ?? []).Select(p => "!" + p).Prepend("*").ToList()
            : (Toggled(state.Open, node.Path, !node.Open) ?? []).Concat(ExpandAllGroups ? ["!*"] : []).ToList();
        _stateOpen = list;
        await ExpandedGroupsChanged.InvokeAsync(list);
        await ReloadIfStaleAsync();
    }

    /// <summary>What's grouped and open, for telling whether it changed since the last load.</summary>
    // Only while grouped: a TreeList keeps its open rows under the same ?open= key, which isn't a grouping.
    private string GroupSignature => GroupedNow ? Grouping.Signature : "";

    // A new grouping, from the toolbar or a header: it starts from the first page, with nothing open.
    private async Task SetGroupAsync(string? text)
    {
        (_stateGroup, _stateGroupSet, _stateOpen, _statePage, _stateCursor) = (text, true, [], 1, null);
        await GroupChanged.InvokeAsync(text);
        if (ExpandedGroupsChanged.HasDelegate)
        {
            await ExpandedGroupsChanged.InvokeAsync([]);
        }

        await PageChanged.InvokeAsync(1);
        await ReloadIfStaleAsync();
    }

    // The bar of what's grouped: while grouped, or always with GroupPanel, where people can change it.
    private bool ShowsGroupBar => Groupable && GroupsAvailable && (GroupedNow || GroupPanel);

    private bool CanGroupBy(ColumnBase<TItem> column) => Groupable && GroupsAvailable && column.CanGroup;

    // Whether another level fits: past the last, one would be dropped without a word.
    private bool HasRoomToGroup => Grouping.Levels.Count < DataGroupRequest<TItem>.MaxLevels;

    // Without a GroupsProvider there's nothing to ask for groups, so the address can't start any.
    private bool GroupsAvailable => ItemsProvider is null || GroupsProvider is not null;

    // Whether the address's grouping counts. Empty does on any grid, as it's what "Show only this group" writes.
    private bool TakesAddressGroup(string text) => text.Length == 0 || (Groupable && GroupsAvailable);

    /// <inheritdoc />
    internal override void SeedStateFromAddress()
    {
        var query = QueryString.Of(Navigation.Uri);
        if (!GroupChanged.HasDelegate && query.TryGetValue(GroupKey, out var group) && TakesAddressGroup(group.ToString()))
        {
            (_stateGroup, _stateGroupSet) = (group.ToString(), true);
        }

        if (!ExpandedGroupsChanged.HasDelegate && (query.ContainsKey(OpenKey) || query.ContainsKey(ShutKey)))
        {
            var open = query.TryGetValue(OpenKey, out var o) ? o.OfType<string>() : [];
            var shut = query.TryGetValue(ShutKey, out var s) ? s.OfType<string>().Select(p => "!" + p) : [];
            _stateOpen = [.. open, .. shut];
        }

        SeedDetailsFromAddress(query);
    }

    // Whether its checkbox has anything to tick.
    private static bool HasRows(GroupNode node) => node.Rows.Count > 0 || node.Children.Any(HasRows);

    // The grouping with this column added at the end, or taken out.
    private string GroupTextWith(ColumnBase<TItem> column, bool on)
    {
        var tokens = Grouping.Levels.Where(l => l.Column != column).Select(l => (l.Descending ? "-" : "") + l.Column.GroupToken);
        return string.Join(",", on ? tokens.Append(column.GroupToken) : tokens);
    }

    // The grouping with this column's level swapped with the one before (-1) or after (+1), or
    // null at that end: Move group left and right in a column menu and a chip's, a drag without one.
    private string? GroupTextMoved(ColumnBase<TItem> column, int by)
    {
        var levels = Grouping.Levels.ToList();
        var at = levels.FindIndex(l => l.Column == column);
        var to = at + by;
        if (at < 0 || to < 0 || to >= levels.Count)
        {
            return null;
        }

        (levels[at], levels[to]) = (levels[to], levels[at]);
        return string.Join(",", levels.Select(l => (l.Descending ? "-" : "") + l.Column.GroupToken));
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
        [OpenKey] = open && !ExpandAllGroups ? "*" : null,
        [ShutKey] = !open && ExpandAllGroups ? "*" : null,
    });

    private async Task SetAllOpenAsync(bool open)
    {
        IReadOnlyList<string> list = open ? ["*"] : ExpandAllGroups ? ["!*"] : [];
        _stateOpen = list;
        await ExpandedGroupsChanged.InvokeAsync(list);
        await ReloadIfStaleAsync();
    }

    private bool GroupBound => GroupChanged.HasDelegate || ExpandedGroupsChanged.HasDelegate;
}
