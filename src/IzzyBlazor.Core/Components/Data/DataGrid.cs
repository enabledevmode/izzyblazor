using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

// These parameters pass through to GridViewBase, which TreeList shares without them.
#pragma warning disable BL0007

/// <summary>
/// Displays rows of data in a grid users can sort, filter, group and edit, with its state in the URL
/// so it works on static pages too. Users can resize, move and hide columns, open a row's detail,
/// and export to CSV or Excel. For a plain list of records, use Table.
/// </summary>
/// <typeparam name="TItem">The type of the items.</typeparam>
public partial class DataGrid<TItem>
{
    /// <summary>
    /// Lets people group by columns, from a header's menu. Grouped, the grid's pages count top-level
    /// groups, so a group is never split, and each group's count and aggregates are the whole group's.
    /// With an <c>ItemsProvider</c>, it needs a <see cref="GroupsProvider"/>.
    /// </summary>
    [Parameter]
    public new bool Groupable { get => base.Groupable; set => base.Groupable = value; }

    /// <summary>
    /// The grouping, outermost first, such as <c>"status,-customer"</c> (<c>-</c> for descending); null for
    /// <see cref="InitialGroup"/>. Works without <see cref="Groupable"/>, for a grouping people can't change.
    /// Binding it (<c>@bind-Group</c>) on an interactive page keeps it in the component instead of the URL.
    /// </summary>
    [Parameter]
    public new string? Group { get => base.Group; set => base.Group = value; }

    /// <summary>The grouping when the URL has none, such as <c>"status"</c>.</summary>
    [Parameter]
    public new string? InitialGroup { get => base.InitialGroup; set => base.InitialGroup = value; }

    /// <summary>Raised when the grouping changes, on interactive pages.</summary>
    [Parameter, ModeSupport(NoScript = ModeLevel.None, Static = ModeLevel.None)]
    public new EventCallback<string?> GroupChanged { get => base.GroupChanged; set => base.GroupChanged = value; }

    /// <summary>Whether groups start open. They start closed by default, which is what a page of groups is for.</summary>
    [Parameter]
    public new bool ExpandAllGroups { get => base.ExpandAllGroups; set => base.ExpandAllGroups = value; }

    /// <summary>
    /// Which groups are open, each by its values joined with <c>/</c>, escaped, such as <c>"Shipped/Acme"</c>; <c>"*"</c>
    /// is all of them, and <c>"!Shipped"</c> an exception to that. Binding it on an interactive page keeps it in the component.
    /// </summary>
    [Parameter]
    public new IReadOnlyList<string>? ExpandedGroups { get => base.ExpandedGroups; set => base.ExpandedGroups = value; }

    /// <summary>Raised when a group opens or closes, on interactive pages.</summary>
    [Parameter, ModeSupport(NoScript = ModeLevel.None, Static = ModeLevel.None)]
    public new EventCallback<IReadOnlyList<string>?> ExpandedGroupsChanged { get => base.ExpandedGroupsChanged; set => base.ExpandedGroupsChanged = value; }

    /// <summary>
    /// The most rows drawn in one open group. Past it, the group says how many it has and links to
    /// showing only that group, with the grid's pages. Defaults to 100.
    /// </summary>
    [Parameter]
    public new int GroupRowLimit { get => base.GroupRowLimit; set => base.GroupRowLimit = value; }

    /// <summary>
    /// Keeps the bar of what's grouped above the grid while nothing is, saying a header can be dragged
    /// there. Without it the bar shows only while grouped, and the first grouping is from a header's
    /// menu. Dragging needs script and a mouse or pen; the menu works everywhere.
    /// </summary>
    [Parameter, ModeSupport(NoScript = ModeLevel.Partial, NoScriptNotes = "With JavaScript off, the bar shows what's grouped, and headers can't be dragged to it.")]
    public new bool GroupPanel { get => base.GroupPanel; set => base.GroupPanel = value; }

    /// <summary>Keeps a grouped column in the table. By default it leaves, as each group's row says its value.</summary>
    [Parameter]
    public new bool ShowGroupedColumns { get => base.ShowGroupedColumns; set => base.ShowGroupedColumns = value; }

    /// <summary>
    /// Counts and aggregates for an <c>ItemsProvider</c>, which a column's <c>Aggregate</c> needs. It
    /// gets one request for each level of groups, and one with no keys for the grand total. Answer
    /// it from a query with <c>request.ToResultAsync</c>. With <c>Items</c>, the grid works these out itself.
    /// </summary>
    [Parameter]
    public new Func<DataGroupRequest<TItem>, ValueTask<DataGroupResult>>? GroupsProvider { get => base.GroupsProvider; set => base.GroupsProvider = value; }

    /// <summary>
    /// A row's detail: drawn under it, across the grid, while it's open. Setting it adds a column of
    /// buttons that open and close it. It needs an <c>ItemKey</c>, which open rows are kept by. Only
    /// open rows draw it, so a component in it loads its own data when it's opened.
    /// </summary>
    [Parameter]
    public new RenderFragment<TItem>? DetailTemplate { get => base.DetailTemplate; set => base.DetailTemplate = value; }

    /// <summary>
    /// Which rows have a detail; all of them when it's not set. A row without one has no button, except
    /// while the grid hides columns to fit, when its detail shows their values.
    /// </summary>
    [Parameter]
    public new Func<TItem, bool>? ItemHasDetail { get => base.ItemHasDetail; set => base.ItemHasDetail = value; }

    /// <summary>
    /// Which rows' details are open, by key; <c>"*"</c> is all of them, and <c>"!key"</c> an exception to
    /// that. Binding it on an interactive page keeps it in the component; otherwise it's in the address.
    /// </summary>
    [Parameter]
    public new IReadOnlyList<string>? ExpandedDetails { get => base.ExpandedDetails; set => base.ExpandedDetails = value; }

    /// <summary>Raised when a detail opens or closes, on interactive pages.</summary>
    [Parameter, ModeSupport(NoScript = ModeLevel.None, Static = ModeLevel.None)]
    public new EventCallback<IReadOnlyList<string>?> ExpandedDetailsChanged { get => base.ExpandedDetailsChanged; set => base.ExpandedDetailsChanged = value; }

    /// <summary>Whether every row's detail starts open.</summary>
    [Parameter]
    public new bool ExpandAllDetails { get => base.ExpandAllDetails; set => base.ExpandAllDetails = value; }

    /// <summary>Opening a row's detail closes the one that was open. There's no button to open them all.</summary>
    [Parameter]
    public new bool SingleDetail { get => base.SingleDetail; set => base.SingleDetail = value; }

    /// <summary>
    /// Lets people put the rows in their own order: each row starts with a handle to drag, which also
    /// opens a menu (Move up, Move down, Move to top, Move to bottom) for moving without dragging. The
    /// handles work while the rows are in the grid's own order, and are disabled while it's sorted,
    /// filtered, searched or grouped, so leave those off: the order is the data. Needs an <c>ItemKey</c> and every row shown: <c>Items</c>, with no
    /// paging, <c>ItemsProvider</c> or <c>Virtualize</c>. On interactive pages bind the order with
    /// <c>@bind-Items</c>; on static pages give it an <see cref="OrderName"/> inside a form and pass the
    /// posted <see cref="ListOrder"/> back as <see cref="Order"/>.
    /// </summary>
    [Parameter]
    public new bool ReorderableRows { get => base.ReorderableRows; set => base.ReorderableRows = value; }

    /// <summary>Runs with the rows in their new order after a move, on interactive pages; use <c>@bind-Items</c>.</summary>
    [Parameter, ModeSupport(NoScript = ModeLevel.None, Static = ModeLevel.None)]
    public new EventCallback<IReadOnlyList<TItem>> ItemsChanged { get => base.ItemsChanged; set => base.ItemsChanged = value; }

    /// <summary>
    /// Runs before a move changes the order, on interactive pages: save it here, or set
    /// <see cref="ListMoveEventArgs{TItem}.Cancel"/> to keep the row where it was. A dragged row stays
    /// where it was dropped while this runs, for up to 3 seconds.
    /// </summary>
    [Parameter, ModeSupport(NoScript = ModeLevel.None, Static = ModeLevel.None)]
    public new EventCallback<ListMoveEventArgs<TItem>> OnMove { get => base.OnMove; set => base.OnMove = value; }

    /// <summary>Which rows can move; the others have no handle, and stay put while rows move past them.</summary>
    [Parameter]
    public new Func<TItem, bool>? ItemMovable { get => base.ItemMovable; set => base.ItemMovable = value; }

    /// <summary>
    /// For <see cref="ReorderableRows"/> on a static page, the prefix of the fields the order posts:
    /// <c>{OrderName}.Keys</c> and <c>{OrderName}.Move</c>, bound as a <see cref="ListOrder"/>. Without
    /// it, a static page shows no handles.
    /// </summary>
    [Parameter]
    public new string? OrderName { get => base.OrderName; set => base.OrderName = value; }

    /// <summary>
    /// For <see cref="ReorderableRows"/> on a static page, what the form posted: the grid shows the
    /// posted order with the move applied, and focuses the moved row's handle.
    /// </summary>
    [Parameter]
    public new ListOrder? Order { get => base.Order; set => base.Order = value; }

    /// <summary>
    /// For <see cref="ReorderableRows"/> on a static page with script, posts the form after each move,
    /// for pages that save every move. Otherwise moves stay in the page until the form is submitted.
    /// With JavaScript off, each move from a handle's menu posts anyway.
    /// </summary>
    [Parameter, ModeSupport(Interactive = ModeLevel.None)]
    public new bool SubmitOnMove { get => base.SubmitOnMove; set => base.SubmitOnMove = value; }

    /// <summary>
    /// Draws only the rows in view, on an interactive page; on a static page they load on scroll
    /// instead, by <c>PageSize</c> (50 if not set), up to 2,000 rows. Needs a <c>MaxHeight</c>, and rows
    /// of one line, as tall as <see cref="RowHeight"/>. An <c>ItemsProvider</c> must return a
    /// <c>Total</c>, and read each window's <c>Skip</c> and <c>Take</c>, not <c>Page</c>, as a window
    /// needn't start on a page. It can't take paging by cursor. Pages, and their footer, don't apply.
    /// </summary>
    [Parameter]
    public new bool Virtualize { get => base.Virtualize; set => base.Virtualize = value; }

    /// <summary>
    /// The height of every row, in pixels, with <see cref="Virtualize"/>. Text past it is cut short.
    /// A control in a cell can't be cut short: a row with a checkbox or a ⋮ button needs at least 33.
    /// Defaults to 48.
    /// </summary>
    [Parameter]
    public new double RowHeight { get => base.RowHeight; set => base.RowHeight = value; }
}
