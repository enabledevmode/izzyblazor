using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>When an item of a <see cref="Toolbar"/> moves into its "…" menu.</summary>
public enum ToolbarOverflow
{
    /// <summary>When there's no room for it, in order of <see cref="ToolbarItem.Priority"/> (the default).</summary>
    Auto,

    /// <summary>Never: it stays in the bar, such as the main action.</summary>
    Never,

    /// <summary>Always: it's only in the menu, such as Print or Version history.</summary>
    Always,
}

/// <summary>The middle of a <see cref="Toolbar"/>, kept centered between the start and the end.</summary>
public sealed class ToolbarCenter : ComponentPart
{
    internal override Type HostType => typeof(Toolbar);
}

/// <summary>The end of a <see cref="Toolbar"/> (the right in left-to-right text), before its "…" menu.</summary>
public sealed class ToolbarEnd : ComponentPart
{
    internal override Type HostType => typeof(Toolbar);
}

/// <summary>
/// Settings for the control inside it, which moves into the toolbar's "…" menu as one: a
/// <see cref="Button"/>, <see cref="IconButton"/>, <see cref="ButtonGroup"/> or <see cref="Menu"/>.
/// Also a box for anything else, such as a search field, with <see cref="MenuContent"/> for what
/// stands in for it in the menu.
/// </summary>
/// <example>
/// <code>
/// &lt;ToolbarItem Priority="1"&gt;&lt;Button StartIcon="Lucide.Link"&gt;Link&lt;/Button&gt;&lt;/ToolbarItem&gt;
/// &lt;ToolbarItem Overflow="ToolbarOverflow.Never"&gt;&lt;Button Accent="Accent.Primary" Variant="ButtonVariant.Solid"&gt;Publish&lt;/Button&gt;&lt;/ToolbarItem&gt;
/// &lt;ToolbarItem Width="Length.Rem(12)"&gt;&lt;TextField Label="Search" HideLabel /&gt;&lt;/ToolbarItem&gt;
/// </code>
/// </example>
public sealed class ToolbarItem : LayoutItemBase, IDisposable
{
    private readonly ToolbarMember _member;

    /// <summary>Creates the item.</summary>
    public ToolbarItem() => _member = new ToolbarMember(this);

    /// <summary>
    /// Which items stay longest when there's no room: higher stays longer. Defaults to 0. Items with
    /// the same priority move into the menu from the middle of the toolbar outward.
    /// </summary>
    [Parameter] public int Priority { get; set; }

    /// <summary>When it moves into the "…" menu. Defaults to <see cref="ToolbarOverflow.Auto"/>.</summary>
    [Parameter] public ToolbarOverflow Overflow { get; set; }

    /// <summary>The text of the control in the menu, in place of its label, such as "Bold" for a "B" button.</summary>
    [Parameter] public string? MenuText { get; set; }

    /// <summary>
    /// What stands in for this item in the "…" menu, such as <see cref="MenuItem"/>s. Without it,
    /// content that isn't a Button, IconButton, ButtonGroup or Menu stays in the bar.
    /// </summary>
    [Parameter] public RenderFragment? MenuContent { get; set; }

    /// <summary>The control, or other content.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    [CascadingParameter] private ToolbarScope? Scope { get; set; }

    private string? _class;
    private string? _style;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Scope?.Context is null && !ReferenceEquals(Scope, ToolbarScope.Blocked))
        {
            throw new InvalidOperationException("ToolbarItem must be inside a Toolbar.");
        }

        (_class, _style) = BuildRootAttributes();
        _member.Update(Scope, entry: null, configure: unit =>
        {
            unit.Priority = Priority;
            unit.Overflow = Overflow;
            unit.MenuContent = MenuContent;
            unit.FixedWidth = Pixels(Width.Base.Css);
        });
    }

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add("ib-toolbar-item");
        base.BuildAttributes(classes, styles);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<ToolbarScope?>>(0);
        builder.AddComponentParameter(1, "Value", _member.Inner(menuText: MenuText));
        builder.AddComponentParameter(2, "ChildContent", _member.Wrap(ChildContent ?? (_ => { }), _class, _style, AdditionalAttributes));
        builder.CloseComponent();
    }

    /// <summary>A CSS length in rem or px as pixels at a 16px root; null for anything else.</summary>
    private static double? Pixels(string? css)
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
    public void Dispose() => _member.Dispose();
}

/// <summary>A vertical line between groups of a <see cref="Toolbar"/>'s items; in its "…" menu, a divider.</summary>
public sealed class ToolbarSeparator : IzzyComponentBase, IDisposable
{
    private readonly ToolbarMember _member;

    /// <summary>Creates the separator.</summary>
    public ToolbarSeparator() => _member = new ToolbarMember(this);

    [CascadingParameter] private ToolbarScope? Scope { get; set; }

    private static readonly RenderFragment MenuDivider = builder =>
    {
        builder.OpenComponent<MenuDivider>(0);
        builder.CloseComponent();
    };

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Scope?.Context is null && !ReferenceEquals(Scope, ToolbarScope.Blocked))
        {
            throw new InvalidOperationException("ToolbarSeparator must be inside a Toolbar.");
        }

        _member.Update(Scope, new ToolbarEntry(MenuDivider, 1), configure: unit => unit.Separator = true);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cssClass, style) = BuildRootAttributes();
        builder.AddContent(0, _member.Wrap(b =>
        {
            b.OpenElement(0, "div");
            b.AddAttribute(1, "role", "separator");
            b.AddAttribute(2, "aria-orientation", "vertical");
            b.AddMultipleAttributes(3, AdditionalAttributes);
            b.AddAttribute(4, "class", cssClass);
            b.AddAttribute(5, "style", style);
            b.CloseElement();
        }, "ib-toolbar-sep"));
    }

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles) => classes.Add("ib-toolbar-divider");

    /// <inheritdoc />
    public void Dispose() => _member.Dispose();
}

/// <summary>
/// The end of a <see cref="Toolbar"/>: its "…" menu, holding a copy of every unit that can go
/// there, and the container queries that choose, without script, which copies show.
/// </summary>
internal sealed class ToolbarMore : ComponentBase
{
    [Parameter, EditorRequired] public ToolbarContext Context { get; set; } = null!;

    [Parameter] public string MoreLabel { get; set; } = "";

    [Parameter] public ControlSize Size { get; set; }

    [Parameter] public string? Nonce { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized() => Context.OnChanged = StateHasChanged;

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var units = Context.Units.ToList();
        if (!units.Exists(u => !u.Separator && u.EffectiveOverflow != ToolbarOverflow.Never))
        {
            return;
        }

        var plan = units.Select(u => new ToolbarPlan.Unit(u.Id, u.Region, u.Width ?? 0, u.Priority, u.Separator ? ToolbarOverflow.Never : u.EffectiveOverflow, u.Separator)).ToList();
        var moreWidth = Size switch { ControlSize.Sm => 32, ControlSize.Lg => 44, _ => 36 };
        var states = ToolbarPlan.States(plan, Context.Gap, moreWidth);

        builder.OpenElement(0, "style");
        builder.AddAttribute(1, "nonce", Nonce);
        builder.AddMarkupContent(2, ToolbarPlan.Css(Context.RootId, states));
        builder.CloseElement();

        builder.OpenElement(3, "div");
        builder.AddAttribute(4, "class", "ib-toolbar-more");
        builder.OpenComponent<CascadingValue<ToolbarScope?>>(6);
        builder.AddComponentParameter(7, "Value", ToolbarScope.Blocked);
        builder.AddComponentParameter(8, "IsFixed", true);
        builder.AddComponentParameter(9, "ChildContent", (RenderFragment)(b =>
        {
            b.OpenComponent<Menu>(0);
            b.AddComponentParameter(1, nameof(Menu.Placement), Placement.BottomEnd);
            b.AddComponentParameter(2, nameof(Menu.Trigger), (RenderFragment<IReadOnlyDictionary<string, object>>)(attributes => t =>
            {
                t.OpenComponent<IconButton>(0);
                t.AddComponentParameter(1, nameof(IconButton.Icon), Lucide.Ellipsis);
                t.AddComponentParameter(2, nameof(IconButton.Label), MoreLabel);
                t.AddMultipleAttributes(3, attributes);
                t.CloseComponent();
            }));
            b.AddComponentParameter(3, nameof(Menu.ChildContent), (RenderFragment)(m =>
            {
                foreach (var unit in units)
                {
                    if (unit.Menu is not { } menu)
                    {
                        continue;
                    }

                    m.OpenElement(0, "div");
                    m.SetKey(unit);
                    m.AddAttribute(1, "class", "ib-toolbar-menu-unit");
                    m.AddAttribute(2, "data-ib-unit", unit.Id.ToString(CultureInfo.InvariantCulture));
                    m.AddContent(4, menu);
                    m.CloseElement();
                }
            }));
            b.CloseComponent();
        }));
        builder.CloseComponent();
        builder.CloseElement();
    }
}
