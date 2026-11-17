using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>Where an item of a <see cref="Toolbar"/> sits.</summary>
public enum ToolbarPosition
{
    /// <summary>At the start (the left in left-to-right text), the default.</summary>
    Start,

    /// <summary>In the middle, kept centered between the start and the end.</summary>
    Center,

    /// <summary>At the end, before the "…" menu.</summary>
    End,
}

/// <summary>Reading an item's <c>Overflow</c>, which can change by breakpoint.</summary>
internal static class ToolbarOverflows
{
    private static readonly IReadOnlyList<(Breakpoint? Breakpoint, ToolbarOverflow Value)> Unset = [(null, ToolbarOverflow.Auto)];

    private static IEnumerable<(Breakpoint? Breakpoint, ToolbarOverflow Value)> Values(Responsive<ToolbarOverflow> overflow) =>
        overflow.IsEmpty ? Unset : overflow.Entries;

    /// <summary>The value on a wide screen (the widest breakpoint set): what the server plans the first paint with.</summary>
    public static ToolbarOverflow Wide(Responsive<ToolbarOverflow> overflow) => Values(overflow).Last().Value;

    /// <summary>Whether it stays in the bar at every width.</summary>
    public static bool Never(Responsive<ToolbarOverflow> overflow) => Values(overflow).All(v => v.Value == ToolbarOverflow.Never);

    /// <summary>
    /// The width below which it's always in the menu: its base value is <see cref="ToolbarOverflow.Always"/> and a
    /// breakpoint's isn't. Null otherwise. Lets the stylesheet place it before the script measures.
    /// </summary>
    public static Breakpoint? MenuBelow(Responsive<ToolbarOverflow> overflow)
    {
        var values = Values(overflow).ToList();
        return values[0] is { Breakpoint: null, Value: ToolbarOverflow.Always } ? values.Skip(1).FirstOrDefault(v => v.Value != ToolbarOverflow.Always).Breakpoint : null;
    }
}

/// <summary>When an item of a <see cref="Toolbar"/> moves into its "…" menu.</summary>
public enum ToolbarOverflow
{
    /// <summary>When there's no room for it, in order of <c>Priority</c> (the default).</summary>
    Auto,

    /// <summary>Never: it stays in the bar, such as the main action.</summary>
    Never,

    /// <summary>Always: it's only in the menu, such as Print or Version history.</summary>
    Always,
}

/// <summary>
/// Anything that isn't a toolbar control, in a <see cref="Toolbar"/>: a search field, a
/// SegmentedControl, a Tooltip around a button. Give it a <see cref="LayoutItemBase.Width"/>, so the
/// widths without script allow for it, and <see cref="MenuContent"/> for what stands in for it in
/// the "…" menu; without that, it stays in the bar.
/// </summary>
/// <example>
/// <code>
/// &lt;ToolbarItem Width="Length.Rem(12)" Overflow="ToolbarOverflow.Never"&gt;
///     &lt;TextField Label="Search" HideLabel /&gt;
/// &lt;/ToolbarItem&gt;
/// </code>
/// </example>
public sealed class ToolbarItem : LayoutItemBase, IToolbarItem, IDisposable
{
    private readonly ToolbarSlot _slot;
    private string? _class;
    private string? _style;

    /// <summary>Creates the item.</summary>
    public ToolbarItem() => _slot = new ToolbarSlot(this, nameof(ToolbarItem));

    /// <summary>Where it sits. Defaults to <see cref="ToolbarPosition.Start"/>.</summary>
    [Parameter] public ToolbarPosition Position { get; set; }

    /// <summary>
    /// Which items stay longest when there's no room: higher stays longer. Defaults to 0. Items with
    /// the same priority move into the menu from the middle of the toolbar outward.
    /// </summary>
    [Parameter] public int Priority { get; set; }

    /// <summary>
    /// When it moves into the "…" menu: <see cref="ToolbarOverflow.Never"/> for the main action,
    /// <see cref="ToolbarOverflow.Always"/> for what only the menu needs. Defaults to <see cref="ToolbarOverflow.Auto"/>.
    /// It can change by breakpoint: <c>new() { Base = ToolbarOverflow.Always, Sm = ToolbarOverflow.Auto }</c> is in the
    /// menu on a phone, and in the bar from 640px up while there's room.
    /// </summary>
    [Parameter] public Responsive<ToolbarOverflow> Overflow { get; set; }

    /// <summary>What stands in for it in the "…" menu, such as <see cref="MenuItem"/>s. Without it, the item stays in the bar.</summary>
    [Parameter] public RenderFragment? MenuContent { get; set; }

    /// <summary>The content. Buttons in it are plain <see cref="Button"/>s, not toolbar buttons.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    [CascadingParameter] private ToolbarContext? Toolbar { get; set; }

    bool IToolbarItem.Separator => false;

    RenderFragment IToolbarItem.Bar => builder =>
    {
        builder.OpenElement(0, "div");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "class", _class);
        builder.AddAttribute(3, "style", _style);
        builder.AddContent(4, ChildContent);
        builder.CloseElement();
    };

    RenderFragment? IToolbarItem.Menu => MenuContent;

    double? IToolbarItem.Width => Pixels(Width.Base.Css);

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        (_class, _style) = BuildRootAttributes();
        _slot.Join(Toolbar);
    }

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add("ib-toolbar-item");
        base.BuildAttributes(classes, styles);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder) => _slot.Rendered();

    /// <summary>A CSS length in rem or px as pixels at a 16px root; null for anything else.</summary>
    internal static double? Pixels(string? css)
    {
        if (string.IsNullOrEmpty(css))
        {
            return null;
        }

        var (number, scale) = css.EndsWith("rem", StringComparison.Ordinal) ? (css[..^3], 16.0)
            : css.EndsWith("px", StringComparison.Ordinal) ? (css[..^2], 1.0)
            : (null, 0);
        return number is not null && double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value * scale : null;
    }

    /// <inheritdoc />
    public void Dispose() => _slot.Dispose();
}

/// <summary>
/// Text in a <see cref="Toolbar"/>, such as "3 selected" or a document's name. It stays in the bar,
/// sized from its text; give it a <see cref="LayoutItemBase.Width"/> to cut long text short with "…".
/// </summary>
/// <example>
/// <code>
/// &lt;ToolbarText&gt;3 selected&lt;/ToolbarText&gt;
/// &lt;ToolbarText Position="ToolbarPosition.Center" Width="Length.Rem(9)"&gt;Q3 report.pdf&lt;/ToolbarText&gt;
/// </code>
/// </example>
public sealed class ToolbarText : LayoutItemBase, IToolbarItem, IDisposable
{
    private readonly ToolbarSlot _slot;
    private string? _class;
    private string? _style;

    /// <summary>Creates the text.</summary>
    public ToolbarText() => _slot = new ToolbarSlot(this, nameof(ToolbarText));

    /// <summary>Where it sits. Defaults to <see cref="ToolbarPosition.Start"/>.</summary>
    [Parameter] public ToolbarPosition Position { get; set; }

    /// <summary>The text.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    [CascadingParameter] private ToolbarContext? Toolbar { get; set; }

    // The toolbar's settings, for its size.
    [CascadingParameter] private ButtonGroup? Defaults { get; set; }

    int IToolbarItem.Priority => 0;

    Responsive<ToolbarOverflow> IToolbarItem.Overflow => ToolbarOverflow.Never;

    bool IToolbarItem.Separator => false;

    RenderFragment IToolbarItem.Bar => builder =>
    {
        builder.OpenElement(0, "span");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "class", _class);
        builder.AddAttribute(3, "style", _style);
        builder.AddContent(4, ChildContent);
        builder.CloseElement();
    };

    RenderFragment? IToolbarItem.Menu => null;

    double? IToolbarItem.Width => ToolbarItem.Pixels(Width.Base.Css)
        ?? (ChildContent is not null && Code.ReadText(ChildContent) is { } text ? TextWidths.Of(text, Defaults?.DefaultSize ?? ControlSize.Sm) : null);

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        (_class, _style) = BuildRootAttributes();
        _slot.Join(Toolbar);
    }

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add("ib-toolbar-text");
        base.BuildAttributes(classes, styles);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder) => _slot.Rendered();

    /// <inheritdoc />
    public void Dispose() => _slot.Dispose();
}

/// <summary>A vertical line between groups of a <see cref="Toolbar"/>'s items; in its "…" menu, a divider.</summary>
public sealed class ToolbarSeparator : IzzyComponentBase, IToolbarItem, IDisposable
{
    private static readonly RenderFragment MenuDivider = builder =>
    {
        builder.OpenComponent<MenuDivider>(0);
        builder.CloseComponent();
    };

    private readonly ToolbarSlot _slot;

    /// <summary>Creates the separator.</summary>
    public ToolbarSeparator() => _slot = new ToolbarSlot(this, nameof(ToolbarSeparator));

    /// <summary>Where it sits. Defaults to <see cref="ToolbarPosition.Start"/>.</summary>
    [Parameter] public ToolbarPosition Position { get; set; }

    [CascadingParameter] private ToolbarContext? Toolbar { get; set; }

    int IToolbarItem.Priority => 0;

    Responsive<ToolbarOverflow> IToolbarItem.Overflow => ToolbarOverflow.Never;

    bool IToolbarItem.Separator => true;

    RenderFragment IToolbarItem.Bar => builder =>
    {
        var (cssClass, style) = BuildRootAttributes();
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "role", "separator");
        builder.AddAttribute(2, "aria-orientation", "vertical");
        builder.AddMultipleAttributes(3, AdditionalAttributes);
        builder.AddAttribute(4, "class", cssClass);
        builder.AddAttribute(5, "style", style);
        builder.CloseElement();
    };

    RenderFragment? IToolbarItem.Menu => MenuDivider;

    double? IToolbarItem.Width => 1;

    /// <inheritdoc />
    protected override void OnParametersSet() => _slot.Join(Toolbar);

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles) => classes.Add("ib-toolbar-divider");

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder) => _slot.Rendered();

    /// <inheritdoc />
    public void Dispose() => _slot.Dispose();
}

/// <summary>
/// Draws a <see cref="Toolbar"/>'s items at one position, each in a <c>div.ib-toolbar-unit</c>
/// marked with its id, which the CSS and the script show and hide. The center has a box of its own,
/// drawn only when an item is there.
/// </summary>
internal sealed class ToolbarOutlet : ComponentBase, IDisposable
{
    private Action? _render;

    /// <summary>The breakpoint below which an item floats instead (toolbar.css hides its units there); null for none.</summary>
    internal static string? FloatAttribute(IToolbarItem item) =>
        item.FloatBelow?.ToString().ToLowerInvariant();

    [Parameter, EditorRequired] public ToolbarContext Context { get; set; } = null!;

    [Parameter] public ToolbarPosition Position { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        _render = StateHasChanged;
        Context.AddOutlet(_render);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var items = Context.Items.Where(i => i.Item.Position == Position).ToList();
        var box = Position == ToolbarPosition.Center;
        if (box)
        {
            if (items.Count == 0)
            {
                return;
            }

            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "ib-toolbar-center");
        }

        foreach (var (item, id) in items)
        {
            builder.OpenElement(2, "div");
            builder.SetKey(item);
            builder.AddAttribute(3, "class", item.Separator ? "ib-toolbar-unit ib-toolbar-sep" : "ib-toolbar-unit");
            builder.AddAttribute(4, "data-ib-unit", id.ToString(CultureInfo.InvariantCulture));
            builder.AddAttribute(5, "data-ib-priority", item.Priority == 0 ? null : item.Priority.ToString(CultureInfo.InvariantCulture));
            // Its Overflow at the base width, then at each breakpoint it changes (data-ib-overflow-md), which the script reads.
            var overflows = item.Overflow.IsEmpty ? [] : item.Overflow.Entries.ToList();
            var baseOverflow = overflows.FirstOrDefault(o => o.Breakpoint is null).Value;
            builder.AddAttribute(6, "data-ib-overflow", baseOverflow == ToolbarOverflow.Auto ? null : baseOverflow.ToString().ToLowerInvariant());
            foreach (var (breakpoint, value) in overflows.Where(o => o.Breakpoint is not null))
            {
                builder.AddAttribute(10, $"data-ib-overflow-{breakpoint.ToString()!.ToLowerInvariant()}", value.ToString().ToLowerInvariant());
            }

            builder.AddAttribute(7, "data-ib-float", FloatAttribute(item));
            builder.AddAttribute(9, "data-ib-menu-below", item.Menu is null ? null : ToolbarOverflows.MenuBelow(item.Overflow)?.ToString().ToLowerInvariant());
            builder.AddContent(8, item.Bar);
            builder.CloseElement();
        }

        if (box)
        {
            builder.CloseElement();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_render is not null)
        {
            Context.RemoveOutlet(_render);
        }
    }
}

/// <summary>
/// The end of a <see cref="Toolbar"/>: its "…" menu, holding a copy of every item that can go
/// there, and the container queries that choose, without script, which copies show.
/// </summary>
internal sealed class ToolbarMore : ComponentBase
{
    [Parameter, EditorRequired] public ToolbarContext Context { get; set; } = null!;

    [Parameter] public string MoreLabel { get; set; } = "";

    [Parameter] public ControlSize Size { get; set; }

    [Parameter] public string? Nonce { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized() => Context.OnMenuChanged += StateHasChanged;

    /// <summary>How it moves, after what it can do: one with nothing to show in the menu stays.</summary>
    // On a wide screen, for the server's plan; one that's never in the menu at any width has no copy there.
    private static ToolbarOverflow Effective(IToolbarItem item) =>
        item.Separator || item.Menu is null || ToolbarOverflows.Never(item.Overflow) ? ToolbarOverflow.Never : ToolbarOverflows.Wide(item.Overflow);

    // Whether it has a copy in the menu: at some width it can be there (only on a phone, say, as an item
    // that's Always below Sm and Never above), though the wide screen's plan keeps it in the bar.
    private static bool InMenu(IToolbarItem item) =>
        item.Separator || (item.Menu is not null && !ToolbarOverflows.Never(item.Overflow));

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var items = Context.Items.ToList();


        // Nothing can move and no labels collapse: no "…" and no widths. (With CollapseLabels and
        // nothing to move, the "…" is there but never shows.)
        if (!Context.CollapseLabels && !items.Exists(i => !i.Item.Separator && InMenu(i.Item)))
        {
            return;
        }

        var plan = items.ConvertAll(i => new ToolbarPlan.Unit(i.Id, i.Item.Position, i.Item.Width ?? 0, i.Item.Priority, Effective(i.Item), i.Item.Separator, i.Item.CollapsedWidth));
        var moreWidth = Size switch { ControlSize.Sm => 32, ControlSize.Lg => 44, _ => 36 };
        var states = ToolbarPlan.States(plan, Context.Gap, moreWidth, Context.CollapseLabels);

        builder.OpenElement(0, "style");
        builder.AddAttribute(1, "nonce", Nonce);
        builder.AddMarkupContent(2, ToolbarPlan.Css(Context.RootId, states));
        builder.CloseElement();

        builder.OpenElement(3, "div");
        builder.AddAttribute(4, "class", "ib-toolbar-more");
        builder.OpenComponent<Menu>(5);
        builder.AddComponentParameter(7, nameof(Menu.Trigger), (RenderFragment<IReadOnlyDictionary<string, object>>)(attributes => t =>
        {
            t.OpenComponent<IconButton>(0);
            t.AddComponentParameter(1, nameof(IconButton.Icon), Lucide.Ellipsis);
            t.AddComponentParameter(2, nameof(IconButton.Label), MoreLabel);
            t.AddMultipleAttributes(3, attributes);
            t.CloseComponent();
        }));
        builder.AddComponentParameter(8, nameof(Menu.ChildContent), (RenderFragment)(m =>
        {
            // A copy of what can move; separators go with their neighbors.
            foreach (var (item, id) in items)
            {
                if (!InMenu(item))
                {
                    continue;
                }

                m.OpenElement(0, "div");
                m.SetKey(item);
                m.AddAttribute(1, "class", "ib-toolbar-menu-unit");
                m.AddAttribute(2, "data-ib-unit", id.ToString(CultureInfo.InvariantCulture));
                m.AddAttribute(3, "data-ib-float", ToolbarOutlet.FloatAttribute(item));
                m.AddContent(4, item.Menu);
                m.CloseElement();
            }
        }));
        builder.CloseComponent();
        builder.CloseElement();
    }
}

/// <summary>
/// The floating buttons of a <see cref="Toolbar"/>'s items that float below a width (<c>FloatBelow</c>), drawn
/// after the toolbar, outside it: a FloatingButton leaves the page's flow, so it isn't a toolbar control.
/// </summary>
internal sealed class ToolbarFloats : ComponentBase
{
    [Parameter, EditorRequired] public ToolbarContext Context { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnInitialized() => Context.OnMenuChanged += StateHasChanged;

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        // By the width they float below: one alone is a floating button; more share a speed dial, so
        // they never sit on each other in the corner.
        var groups = Context.Items
            .Where(i => i.Item.FloatBelow is not null && i.Item.Float is not null)
            .GroupBy(i => i.Item.FloatBelow!.Value)
            .ToList();
        if (groups.Count == 0)
        {
            return;
        }

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "ib-toolbar-floats");
        foreach (var group in groups)
        {
            var actions = group.Select(i => i.Item.Float!).ToList();
            builder.OpenRegion(2);
            if (actions.Count == 1)
            {
                var a = actions[0];
                builder.OpenComponent<FloatingButton>(0);
                builder.AddComponentParameter(1, nameof(FloatingButton.Icon), a.Icon);
                builder.AddComponentParameter(2, nameof(FloatingButton.Label), a.Label);
                builder.AddComponentParameter(3, nameof(FloatingButton.Href), a.Href);
                builder.AddComponentParameter(4, nameof(FloatingButton.Submit), a.Submit);
                builder.AddComponentParameter(5, nameof(FloatingButton.Name), a.Name);
                builder.AddComponentParameter(6, nameof(FloatingButton.Value), a.Value);
                builder.AddComponentParameter(7, nameof(FloatingButton.FormAction), a.FormAction);
                builder.AddComponentParameter(8, nameof(FloatingButton.Form), a.Form);
                builder.AddComponentParameter(9, nameof(FloatingButton.Disabled), a.Disabled);
                builder.AddComponentParameter(10, nameof(FloatingButton.HiddenFrom), group.Key);
                if (a.OnClick.HasDelegate)
                {
                    builder.AddComponentParameter(11, nameof(FloatingButton.OnClick), a.OnClick);
                }

                if (a.Attributes is not null)
                {
                    builder.AddMultipleAttributes(12, a.Attributes);
                }

                builder.CloseComponent();
            }
            else
            {
                builder.OpenComponent<SpeedDial>(20);
                builder.AddComponentParameter(21, nameof(SpeedDial.Label), Context.FloatLabel ?? "Actions");
                builder.AddComponentParameter(22, nameof(SpeedDial.HiddenFrom), group.Key);
                builder.AddComponentParameter(23, nameof(SpeedDial.ChildContent), (RenderFragment)(d =>
                {
                    foreach (var a in actions)
                    {
                        d.OpenComponent<SpeedDialAction>(0);
                        d.SetKey(a);
                        d.AddComponentParameter(1, nameof(SpeedDialAction.Icon), a.Icon);
                        d.AddComponentParameter(2, nameof(SpeedDialAction.Label), a.Label);
                        d.AddComponentParameter(3, nameof(SpeedDialAction.Href), a.Href);
                        d.AddComponentParameter(4, nameof(SpeedDialAction.Submit), a.Submit);
                        d.AddComponentParameter(5, nameof(SpeedDialAction.Name), a.Name);
                        d.AddComponentParameter(6, nameof(SpeedDialAction.Value), a.Value);
                        d.AddComponentParameter(7, nameof(SpeedDialAction.FormAction), a.FormAction);
                        d.AddComponentParameter(8, nameof(SpeedDialAction.Form), a.Form);
                        d.AddComponentParameter(9, nameof(SpeedDialAction.Disabled), a.Disabled);
                        if (a.OnClick.HasDelegate)
                        {
                            d.AddComponentParameter(10, nameof(SpeedDialAction.OnClick), a.OnClick);
                        }

                        d.CloseComponent();
                    }
                }));
                builder.CloseComponent();
            }

            builder.CloseRegion();
        }

        builder.CloseElement();
    }
}
