using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;

namespace IzzyBlazor;

/// <summary>What a DataGrid's toolbar items draw, from the grid they're in. Its cascade isn't fixed, so they follow the grid's parameters.</summary>
internal interface IDataGridToolbar
{
    bool HasSearch { get; }

    bool HasColumnPicker { get; }

    bool HasFilterBuilder { get; }

    string FilterBuilderLabel { get; }

    RenderFragment FilterBuilderBar(bool showText);

    bool HasAdd { get; }

    string AddLabel { get; }

    RenderFragment AddBar(bool showText);

    bool HasExport { get; }

    /// <summary>Whether export opens a menu of its formats, with a chevron beside its icon: a little wider.</summary>
    bool ExportHasMenu { get; }

    bool ToolbarLabels { get; }

    string ColumnPickerLabel { get; }

    string ExportLabel { get; }

    RenderFragment SearchBar { get; }

    RenderFragment ColumnPickerBar(bool showText);

    RenderFragment ExportBar(bool showText);

    RenderFragment ExportMenu { get; }

    /// <summary>An id for the grid's element, which its parts outside it carry so its script finds them.</summary>
    string PartsId { get; }
}

/// <summary>
/// One of a <c>DataGrid</c>'s own tools, written in its <c>ToolbarContent</c> to put it among your
/// items, in the order they're written, or outside the grid, such as in a <c>Tile</c>'s
/// <c>HeaderEnd</c>. The grid's parameter still turns the tool on; without it this draws nothing. A
/// tool you don't write keeps its usual place. Write them directly in <c>ToolbarContent</c>, as inside
/// a component of your own the grid can't see them and draws its own as well. One turned on later (its
/// grid parameter set) keeps its place among the others.
/// </summary>
/// <remarks>
/// Outside the grid it acts on the grid whose <c>id</c> is <see cref="For"/>, or else the one grid in
/// its <c>Tile</c> or <c>Card</c>, and the grid leaves its own out. With every tool outside and no
/// <c>ToolbarContent</c>, the grid has no toolbar.
/// </remarks>
public abstract class DataGridToolbarItem : ComponentBase, IToolbarItem, IDisposable
{
    private readonly ToolbarSlot _slot;

    private protected DataGridToolbarItem() => _slot = new ToolbarSlot(this, GetType().Name);

    /// <summary>Where it sits, among the items of that position in the order they're written.</summary>
    [Parameter] public ToolbarPosition Position { get; set; }

    [CascadingParameter] private ToolbarContext? Toolbar { get; set; }

    /// <summary>
    /// The <c>id</c> of the grid it acts on, when it's outside the grid. Leave it out in the grid's
    /// <c>ToolbarContent</c>, or in a <c>Tile</c> or <c>Card</c> with one grid: it acts on that one.
    /// </summary>
    [Parameter] public string? For { get; set; }

    [CascadingParameter] private IDataGridToolbar? GridValue { get; set; }

    [CascadingParameter] private HeaderHost? Host { get; set; }

    [Inject] private IServiceProvider Services { get; set; } = default!;

    private PartLink? _link;
    private Action? _redraw;
    private bool _joined;

    // Outside the grid, the grid it draws from once that grid has drawn.
    private IDataGridToolbar? Target => _link is not null ? _link.Target as IDataGridToolbar : GridValue;

    private protected IDataGridToolbar Grid => Target ?? throw new InvalidOperationException($"{GetType().Name} must be in a DataGrid's ToolbarContent.");

    private protected abstract bool On { get; }

    private protected virtual int ItemPriority => 0;

    private protected virtual ToolbarOverflow ItemOverflow => ToolbarOverflow.Never;

    private protected abstract RenderFragment ItemBar { get; }

    private protected virtual RenderFragment? ItemMenu => null;

    private protected abstract double? ItemWidth { get; }

    /// <summary>What the toolbar reckons for it on the first paint.</summary>
    internal double? EstimatedWidth => Target is null ? null : ItemWidth;

    // A table's small icons are 18px (data.css), where the estimate counts a small button's 16.
    private protected const double TableIconExtra = 2;

    int IToolbarItem.Priority => ItemPriority;

    ToolbarOverflow IToolbarItem.Overflow => ItemOverflow;

    bool IToolbarItem.Separator => false;

    // Off, it keeps its place, so turned on later it comes back where it was.
    bool IToolbarItem.Hidden => Target is null || !On;

    RenderFragment IToolbarItem.Bar => Target is null ? _ => { } : OutsideParts.Wrap(_link is null ? null : Grid.PartsId, "data-ib-grid", null, ItemBar);

    RenderFragment? IToolbarItem.Menu => Target is null ? null : ItemMenu;

    double? IToolbarItem.Width => EstimatedWidth;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (For is null && GridValue is not null)
        {
            Leave();
            _joined = true;
            _slot.Join(Toolbar);
            return;
        }

        var link = For is { Length: > 0 } id ? PartLinks.For(Services, typeof(IDataGridToolbar), id, GetType().Name)
            : Host?.Parts(typeof(IDataGridToolbar))
            ?? throw new InvalidOperationException($"{GetType().Name} must be in a DataGrid's ToolbarContent, in a Tile or Card with a DataGrid, or name its grid with For.");
        if (!ReferenceEquals(link, _link))
        {
            Leave();
            _link = link;
            link.Place(GetType(), _redraw ??= StateHasChanged);
        }

        // In a toolbar of the page's, it's one of its items.
        _joined = Toolbar is not null && !ReferenceEquals(Toolbar, ToolbarContext.Outside);
        if (_joined)
        {
            _slot.Join(Toolbar);
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
    {
        if (_joined)
        {
            _slot.Rendered();
        }
        else if (Target is not null && On)
        {
            // As in the grid's own toolbar: small, neutral, ghost buttons.
            builder.AddContent(0, OutsideParts.Wrap(Grid.PartsId, "data-ib-grid", ButtonGroup.Defaults(Accent.Neutral, ButtonVariant.Ghost, ControlSize.Sm), ItemBar));
        }
    }

    private void Leave()
    {
        if (_link is not null)
        {
            _link.Unplace(GetType(), _redraw!);
            _link = null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Leave();
        _slot.Dispose();
    }

    // Which of these a grid's ToolbarContent writes, so the rest go in their usual places on the first
    // render, with or without script. Run here only to read it, as Code reads its text.
    internal static HashSet<Type> Placed(RenderFragment? content)
    {
        var placed = new HashSet<Type>();
        if (content is null)
        {
            return placed;
        }

#pragma warning disable BL0006
        using var builder = new Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder();
        content(builder);
        var frames = builder.GetFrames();
        for (var i = 0; i < frames.Count; i++)
        {
            ref var frame = ref frames.Array[i];
            if (frame.FrameType == RenderTreeFrameType.Component && frame.ComponentType.IsAssignableTo(typeof(DataGridToolbarItem)))
            {
                placed.Add(frame.ComponentType);
            }
        }
#pragma warning restore BL0006
        return placed;
    }
}

/// <summary>Puts a <c>DataGrid</c>'s search box, from <c>Searchable</c>, among your toolbar items. It stays in the bar, as a box.</summary>
/// <example>
/// <code>
/// &lt;ToolbarContent&gt;
///     &lt;ToolbarButton&gt;New order&lt;/ToolbarButton&gt;
///     &lt;DataGridSearch /&gt;
/// &lt;/ToolbarContent&gt;
/// </code>
/// </example>
[ModeSupport(NoScriptNotes = "With JavaScript off, the search applies on Enter.")]
public sealed class DataGridSearch : DataGridToolbarItem
{
    private protected override bool On => Grid.HasSearch;

    private protected override RenderFragment ItemBar => Grid.SearchBar;

    // What it can shrink to (data.css), so that the widths without script match the script's.
    private protected override double? ItemWidth => 96;
}

/// <summary>
/// Puts a <c>DataGrid</c>'s Filter builder button, from <c>FilterBuilder</c>, among your toolbar items.
/// It stays in the bar, at the start unless <see cref="DataGridToolbarItem.Position"/> says otherwise.
/// </summary>
[ModeSupport(NoScript = ModeLevel.None, NoScriptNotes = "With JavaScript off it isn't shown: the builder's rows need script.")]
public sealed class DataGridFilterBuilder : DataGridToolbarItem
{
    /// <summary>Its text beside its icon, or only its icon. Defaults to the grid's <c>ToolbarLabels</c>.</summary>
    [Parameter] public bool? ShowLabel { get; set; }

    private bool Text => ShowLabel ?? Grid.ToolbarLabels;

    private protected override bool On => Grid.HasFilterBuilder;

    private protected override RenderFragment ItemBar => Grid.FilterBuilderBar(Text);

    private protected override double? ItemWidth => ButtonWidth.Estimate(ControlSize.Sm, !Text, Text, Grid.FilterBuilderLabel, startIcon: true, endIcon: false) + (Text ? TableIconExtra : 0);
}

/// <summary>
/// Puts a <c>DataGrid</c>'s Columns button, from <c>ColumnPicker</c>, among your toolbar items. It
/// stays in the bar, at the end unless <see cref="DataGridToolbarItem.Position"/> says otherwise.
/// </summary>
[ModeSupport(NoScript = ModeLevel.None, NoScriptNotes = "With JavaScript off it isn't shown: its checkboxes need script.")]
public sealed class DataGridColumnPicker : DataGridToolbarItem
{
    /// <summary>Creates it, at the toolbar's end.</summary>
    public DataGridColumnPicker() => Position = ToolbarPosition.End;

    /// <summary>Its text beside its icon, or only its icon. Defaults to the grid's <c>ToolbarLabels</c>.</summary>
    [Parameter] public bool? ShowLabel { get; set; }

    private bool Text => ShowLabel ?? Grid.ToolbarLabels;

    private protected override bool On => Grid.HasColumnPicker;

    private protected override RenderFragment ItemBar => Grid.ColumnPickerBar(Text);

    private protected override double? ItemWidth => ButtonWidth.Estimate(ControlSize.Sm, !Text, Text, Grid.ColumnPickerLabel, startIcon: true, endIcon: false) + (Text ? TableIconExtra : 0);
}

/// <summary>
/// Puts a <c>DataGrid</c>'s Add button, from <c>NewItem</c>, among your toolbar items. It stays in the
/// bar, at the end unless <see cref="DataGridToolbarItem.Position"/> says otherwise.
/// </summary>
[ModeSupport]
public sealed class DataGridAdd : DataGridToolbarItem
{
    /// <summary>Creates it, at the toolbar's end.</summary>
    public DataGridAdd() => Position = ToolbarPosition.End;

    /// <summary>Its text beside its icon, or only its icon. Defaults to the grid's <c>ToolbarLabels</c>.</summary>
    [Parameter] public bool? ShowLabel { get; set; }

    private bool Text => ShowLabel ?? Grid.ToolbarLabels;

    private protected override bool On => Grid.HasAdd;

    private protected override RenderFragment ItemBar => Grid.AddBar(Text);

    private protected override double? ItemWidth => ButtonWidth.Estimate(ControlSize.Sm, !Text, Text, Grid.AddLabel, startIcon: true, endIcon: false);
}

/// <summary>
/// Puts a <c>DataGrid</c>'s export, from <c>ExportExcelHref</c>, <c>ExportCsvHref</c> and any
/// <see cref="DataGridExportFormat"/>, among your toolbar items: a link with one format, or with more a
/// button with a chevron that opens a menu of links. At the end unless <see cref="DataGridToolbarItem.Position"/> says otherwise.
/// </summary>
[ModeSupport(NoScript = ModeLevel.Partial, NoScriptNotes = "With JavaScript off, the links download. A menu of them opens and closes, but arrow keys don't move between items.")]
public sealed class DataGridExport : DataGridToolbarItem
{
    /// <summary>Creates it, at the toolbar's end.</summary>
    public DataGridExport() => Position = ToolbarPosition.End;

    /// <summary>Which items stay longest when there's no room: higher stays longer. Defaults to 0.</summary>
    [Parameter] public int Priority { get; set; }

    /// <summary>When it moves into the "…" menu. Defaults to <see cref="ToolbarOverflow.Auto"/>.</summary>
    [Parameter] public ToolbarOverflow Overflow { get; set; }

    /// <summary>Its text beside its icon, or only its icon. Defaults to the grid's <c>ToolbarLabels</c>.</summary>
    [Parameter] public bool? ShowLabel { get; set; }

    private bool Text => ShowLabel ?? Grid.ToolbarLabels;

    private protected override bool On => Grid.HasExport;

    private protected override int ItemPriority => Priority;

    private protected override ToolbarOverflow ItemOverflow => Overflow;

    private protected override RenderFragment ItemBar => Grid.ExportBar(Text);

    private protected override RenderFragment ItemMenu => Grid.ExportMenu;

    // With a menu: an 18px icon, a 2px gap and a 14px chevron (data.css), in 8px of padding and a border; or
    // with text, the usual button, where those two come to what its two 16px icons would.
    private protected override double? ItemWidth => !Grid.ExportHasMenu ? ButtonWidth.Estimate(ControlSize.Sm, !Text, Text, Grid.ExportLabel, startIcon: true, endIcon: false) + (Text ? TableIconExtra : 0)
        : Text ? ButtonWidth.Estimate(ControlSize.Sm, false, true, Grid.ExportLabel, startIcon: true, endIcon: true)
        : 52;
}

/// <summary>The grid an export format joins, whatever its rows' type.</summary>
internal interface IDataGridExportFormats
{
    void AddExportFormat(DataGridExportFormat format);

    void RemoveExportFormat(DataGridExportFormat format);

    void ExportFormatChanged();
}

/// <summary>
/// One more format in a <c>DataGrid</c>'s export, after <c>ExportExcelHref</c> and <c>ExportCsvHref</c>:
/// a link to an endpoint of yours, such as one that writes a PDF. Like theirs, it adds the current
/// <c>filter</c>, <c>sort</c>, <c>search</c> and <c>columns</c> to the address; the endpoint applies
/// them with <c>DataRequest&lt;TItem&gt;.Parse(query).Apply(source, fields)</c> and writes the rows.
/// Write it among the grid's columns. With more than one format, export is a menu of them.
/// </summary>
/// <example><code>&lt;DataGrid TItem="Order" ExportExcelHref="/orders/export.xlsx" ...&gt;
///     &lt;PropertyColumn Property="o =&gt; o.Number" /&gt;
///     &lt;DataGridExportFormat Label="Export PDF"
///                           Href="/orders/export.pdf" /&gt;
/// &lt;/DataGrid&gt;</code></example>
[ModeSupport(NoScript = ModeLevel.Partial, NoScriptNotes = "With JavaScript off, the link downloads. A menu of formats opens and closes, but arrow keys don't move between items.")]
public sealed class DataGridExportFormat : ComponentBase, IDisposable
{
    private (string?, string?) _shown;

    /// <summary>Its text: the menu item's, or the button's when it's the grid's only format, such as "Export PDF".</summary>
    [Parameter, EditorRequired] public string Label { get; set; } = default!;

    /// <summary>The endpoint that writes the file. The grid adds the query; a <c>#fragment</c> is dropped.</summary>
    [Parameter, EditorRequired] public string Href { get; set; } = default!;

    [CascadingParameter] private IDataGridExportFormats? Grid { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Grid is null)
        {
            throw new InvalidOperationException("A DataGridExportFormat must be inside a DataGrid, among its columns.");
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (string.IsNullOrEmpty(Label) || string.IsNullOrEmpty(Href))
        {
            throw new InvalidOperationException("A DataGridExportFormat needs a Label and an Href.");
        }

        if (_shown == default)
        {
            _shown = (Label, Href);
            Grid!.AddExportFormat(this);
        }
        else if (_shown != (Label, Href))
        {
            _shown = (Label, Href);
            Grid!.ExportFormatChanged();
        }
    }

    /// <inheritdoc />
    protected override bool ShouldRender() => false;

    /// <inheritdoc />
    public void Dispose() => Grid?.RemoveExportFormat(this);
}
