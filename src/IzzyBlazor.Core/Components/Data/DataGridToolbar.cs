using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;

namespace IzzyBlazor;

/// <summary>What a DataGrid's toolbar items draw, from the grid they're in. Its cascade isn't fixed, so they follow the grid's parameters.</summary>
internal interface IDataGridToolbar
{
    bool HasSearch { get; }

    bool HasColumnPicker { get; }

    bool HasExport { get; }

    bool ToolbarLabels { get; }

    string ColumnPickerLabel { get; }

    string ExportLabel { get; }

    RenderFragment SearchBar { get; }

    RenderFragment ColumnPickerBar(bool showText);

    RenderFragment ExportBar(bool showText);

    RenderFragment ExportMenu { get; }
}

/// <summary>
/// One of a <c>DataGrid</c>'s own tools, written in its <c>ToolbarContent</c> to put it among your
/// items, in the order they're written. The grid's parameter still turns the tool on; without it this
/// draws nothing. A tool you don't write keeps its usual place. Write them directly in
/// <c>ToolbarContent</c>, as inside a component of your own the grid can't see them and draws its own
/// as well. One turned on later joins the end of its position, as any toolbar item an <c>@if</c> adds.
/// </summary>
public abstract class DataGridToolbarItem : ComponentBase, IToolbarItem, IDisposable
{
    private readonly ToolbarSlot _slot;

    private protected DataGridToolbarItem() => _slot = new ToolbarSlot(this, GetType().Name);

    /// <summary>Where it sits, among the items of that position in the order they're written.</summary>
    [Parameter] public ToolbarPosition Position { get; set; }

    [CascadingParameter] private ToolbarContext? Toolbar { get; set; }

    [CascadingParameter] private IDataGridToolbar? GridValue { get; set; }

    private protected IDataGridToolbar Grid => GridValue ?? throw new InvalidOperationException($"{GetType().Name} must be in a DataGrid's ToolbarContent.");

    private protected abstract bool On { get; }

    private protected virtual int ItemPriority => 0;

    private protected virtual ToolbarOverflow ItemOverflow => ToolbarOverflow.Never;

    private protected abstract RenderFragment ItemBar { get; }

    private protected virtual RenderFragment? ItemMenu => null;

    private protected abstract double? ItemWidth { get; }

    int IToolbarItem.Priority => ItemPriority;

    ToolbarOverflow IToolbarItem.Overflow => ItemOverflow;

    bool IToolbarItem.Separator => false;

    RenderFragment IToolbarItem.Bar => ItemBar;

    RenderFragment? IToolbarItem.Menu => ItemMenu;

    double? IToolbarItem.Width => ItemWidth;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (On)
        {
            _slot.Join(Toolbar);
        }
        else
        {
            _slot.Dispose();
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder) => _slot.Rendered();

    /// <inheritdoc />
    public void Dispose() => _slot.Dispose();

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
/// Puts a <c>DataGrid</c>'s Columns button, from <c>ColumnPicker</c>, among your toolbar items. It
/// stays in the bar, at the end unless <see cref="DataGridToolbarItem.Position"/> says otherwise.
/// </summary>
[ModeSupport(NoScript = ModeLevel.None, NoScriptNotes = "With JavaScript off it isn't shown: its checkboxes need script.")]
public sealed class DataGridColumnPicker : DataGridToolbarItem
{
    /// <summary>Creates it, at the toolbar's end.</summary>
    public DataGridColumnPicker() => Position = ToolbarPosition.End;

    /// <summary>Its text beside its icon, or only its icon. Defaults to the grid's <c>ToolbarLabels</c>.</summary>
    [Parameter] public bool? ShowText { get; set; }

    private bool Text => ShowText ?? Grid.ToolbarLabels;

    private protected override bool On => Grid.HasColumnPicker;

    private protected override RenderFragment ItemBar => Grid.ColumnPickerBar(Text);

    private protected override double? ItemWidth => ButtonWidth.Estimate(ControlSize.Sm, !Text, Text, Grid.ColumnPickerLabel, startIcon: true, endIcon: false);
}

/// <summary>
/// Puts a <c>DataGrid</c>'s export, from <c>ExportCsvHref</c> and <c>ExportExcelHref</c>, among your
/// toolbar items: a link with one of them, a menu of links with both. At the end unless
/// <see cref="DataGridToolbarItem.Position"/> says otherwise.
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
    [Parameter] public bool? ShowText { get; set; }

    private bool Text => ShowText ?? Grid.ToolbarLabels;

    private protected override bool On => Grid.HasExport;

    private protected override int ItemPriority => Priority;

    private protected override ToolbarOverflow ItemOverflow => Overflow;

    private protected override RenderFragment ItemBar => Grid.ExportBar(Text);

    private protected override RenderFragment ItemMenu => Grid.ExportMenu;

    private protected override double? ItemWidth => ButtonWidth.Estimate(ControlSize.Sm, !Text, Text, Grid.ExportLabel, startIcon: true, endIcon: false);
}
