using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// A <see cref="Button"/> in a <see cref="Toolbar"/> or a <see cref="ToolbarButtonGroup"/>. In the
/// "…" menu it's a menu item with the same link, form post and <c>OnClick</c>; a toggle
/// (<see cref="ButtonBase.Pressed"/>) is a checkbox item there.
/// </summary>
/// <example>
/// <code>
/// &lt;ToolbarButton StartIcon="Lucide.Share2" Priority="1"&gt;Share&lt;/ToolbarButton&gt;
/// &lt;ToolbarButton Position="ToolbarPosition.End" Overflow="ToolbarOverflow.Never" Accent="Accent.Primary" Variant="ButtonVariant.Solid"&gt;Publish&lt;/ToolbarButton&gt;
/// </code>
/// </example>
/// <seealso cref="Toolbar"/>
/// <seealso cref="ToolbarIconButton"/>
[ModeSupport(Notes = "OnClick runs only on interactive pages, from the bar or the menu; Submit and Href work everywhere.")]
public sealed class ToolbarButton : Button, IToolbarItem, IDisposable
{
    private readonly ToolbarControl _control;

    /// <summary>Creates the button.</summary>
    public ToolbarButton() => _control = new ToolbarControl(this, nameof(ToolbarButton));

    /// <summary>Where it sits. Defaults to <see cref="ToolbarPosition.Start"/>. Not in a group: set it on the group.</summary>
    [Parameter] public ToolbarPosition Position { get; set; }

    /// <summary>
    /// Which items stay longest when there's no room: higher stays longer. Defaults to 0. Items with
    /// the same priority move into the menu from the middle of the toolbar outward.
    /// </summary>
    [Parameter] public int Priority { get; set; }

    /// <summary>
    /// When it moves into the "…" menu: <see cref="ToolbarOverflow.Never"/> for the main action,
    /// <see cref="ToolbarOverflow.Always"/> for what only the menu needs. Defaults to <see cref="ToolbarOverflow.Auto"/>.
    /// </summary>
    [Parameter] public ToolbarOverflow Overflow { get; set; }

    bool IToolbarItem.Separator => false;

    RenderFragment IToolbarItem.Bar => builder => base.BuildRenderTree(builder);

    // A menu item can't reset a form, and one wired up by its attributes does more than a menu item can.
    RenderFragment? IToolbarItem.Menu => Reset || !ToolbarControl.CopiesToMenu(AdditionalAttributes) ? null : builder =>
    {
        builder.OpenComponent<MenuItem>(0);
        builder.AddComponentParameter(1, nameof(MenuItem.Href), Href);
        builder.AddComponentParameter(2, nameof(MenuItem.Submit), Submit);
        builder.AddComponentParameter(3, nameof(MenuItem.Name), Name);
        builder.AddComponentParameter(4, nameof(MenuItem.Value), Value);
        builder.AddComponentParameter(5, nameof(MenuItem.FormAction), FormAction);
        builder.AddComponentParameter(6, nameof(MenuItem.Form), Form);
        builder.AddComponentParameter(7, nameof(MenuItem.Icon), StartIcon);
        builder.AddComponentParameter(8, nameof(MenuItem.Disabled), Disabled || Loading);
        builder.AddComponentParameter(9, nameof(MenuItem.Accent), EffectiveAccent == Accent.Danger ? Accent.Danger : Accent.Neutral);
        if (OnClick.HasDelegate)
        {
            builder.AddComponentParameter(10, nameof(MenuItem.OnClick), OnClick);
        }

        // A toggle is a checkbox item there.
        builder.AddComponentParameter(14, nameof(MenuItem.Checked), Pressed);
        if (PressedChanged.HasDelegate)
        {
            builder.AddComponentParameter(15, nameof(MenuItem.CheckedChanged), PressedChanged);
        }

        if (External && Href is not null)
        {
            builder.AddAttribute(11, "target", "_blank");
            builder.AddAttribute(12, "rel", "noopener noreferrer");
        }

        ToolbarControl.CopyDownload(builder, AdditionalAttributes);
        builder.AddComponentParameter(13, nameof(MenuItem.ChildContent), ChildContent);
        builder.CloseComponent();
    };

    double? IToolbarItem.Width => EstimateWidth();

    // With CollapseLabels, one with a start icon (and no end icon) shows only that icon, unless it
    // never moves into the menu (in a group, the group's Overflow).
    private bool Collapses => ToolbarControl.Of(Toolbar, Group)?.CollapseLabels == true
        && ((Group as ToolbarButtonGroup)?.Overflow ?? Overflow) != ToolbarOverflow.Never
        && StartIcon is not null && ChildContent is not null && EndIcon is null && !ExternalIcon;

    double? IToolbarItem.CollapsedWidth => Collapses ? ButtonWidth.Estimate(EffectiveSize, iconOnly: true, hasLabel: false, null, startIcon: true, endIcon: false) : EstimateWidth();

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        base.BuildAttributes(classes, styles);
        classes.Add("ib-toolbar-collapse ib-toolbar-collapse-square", Collapses);
    }

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        var task = base.SetParametersAsync(parameters);
        _control.Place(Toolbar, Group);
        return task;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder) => _control.Render(builder, base.BuildRenderTree);

    /// <inheritdoc />
    public void Dispose() => _control.Dispose();
}

/// <summary>
/// An <see cref="IconButton"/> in a <see cref="Toolbar"/> or a <see cref="ToolbarButtonGroup"/>. In
/// the "…" menu it's a menu item with its icon, its <c>Label</c> as text, and the same link, form
/// post and <c>OnClick</c>; a toggle (<see cref="ButtonBase.Pressed"/>) is a checkbox item there.
/// </summary>
/// <example>
/// <code>
/// &lt;ToolbarIconButton Icon="Lucide.Printer" Label="Print" /&gt;
/// </code>
/// </example>
/// <seealso cref="Toolbar"/>
/// <seealso cref="ToolbarButton"/>
[ModeSupport(Notes = "OnClick runs only on interactive pages, from the bar or the menu; Submit and Href work everywhere.")]
public sealed class ToolbarIconButton : IconButton, IToolbarItem, IDisposable
{
    private readonly ToolbarControl _control;

    /// <summary>Creates the button.</summary>
    public ToolbarIconButton() => _control = new ToolbarControl(this, nameof(ToolbarIconButton));

    /// <summary>Where it sits. Defaults to <see cref="ToolbarPosition.Start"/>. Not in a group: set it on the group.</summary>
    [Parameter] public ToolbarPosition Position { get; set; }

    /// <summary>
    /// Which items stay longest when there's no room: higher stays longer. Defaults to 0. Items with
    /// the same priority move into the menu from the middle of the toolbar outward.
    /// </summary>
    [Parameter] public int Priority { get; set; }

    /// <summary>
    /// When it moves into the "…" menu: <see cref="ToolbarOverflow.Never"/> for the main action,
    /// <see cref="ToolbarOverflow.Always"/> for what only the menu needs. Defaults to <see cref="ToolbarOverflow.Auto"/>.
    /// </summary>
    [Parameter] public ToolbarOverflow Overflow { get; set; }

    bool IToolbarItem.Separator => false;

    RenderFragment IToolbarItem.Bar => builder => base.BuildRenderTree(builder);

    RenderFragment? IToolbarItem.Menu => Reset || !ToolbarControl.CopiesToMenu(AdditionalAttributes) ? null : builder =>
    {
        builder.OpenComponent<MenuItem>(0);
        builder.AddComponentParameter(1, nameof(MenuItem.Href), Href);
        builder.AddComponentParameter(2, nameof(MenuItem.Submit), Submit);
        builder.AddComponentParameter(3, nameof(MenuItem.Name), Name);
        builder.AddComponentParameter(4, nameof(MenuItem.Value), Value);
        builder.AddComponentParameter(5, nameof(MenuItem.FormAction), FormAction);
        builder.AddComponentParameter(6, nameof(MenuItem.Form), Form);
        builder.AddComponentParameter(7, nameof(MenuItem.Icon), (IconData?)Icon);
        builder.AddComponentParameter(8, nameof(MenuItem.Disabled), Disabled || Loading);
        builder.AddComponentParameter(9, nameof(MenuItem.Accent), EffectiveAccent == Accent.Danger ? Accent.Danger : Accent.Neutral);
        if (OnClick.HasDelegate)
        {
            builder.AddComponentParameter(10, nameof(MenuItem.OnClick), OnClick);
        }

        // A toggle is a checkbox item there.
        builder.AddComponentParameter(14, nameof(MenuItem.Checked), Pressed);
        if (PressedChanged.HasDelegate)
        {
            builder.AddComponentParameter(15, nameof(MenuItem.CheckedChanged), PressedChanged);
        }

        ToolbarControl.CopyDownload(builder, AdditionalAttributes);
        builder.AddComponentParameter(11, nameof(MenuItem.ChildContent), (RenderFragment)(b => b.AddContent(0, Label)));
        builder.CloseComponent();
    };

    double? IToolbarItem.Width => EstimateWidth();

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        var task = base.SetParametersAsync(parameters);
        _control.Place(Toolbar, Group);
        return task;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder) => _control.Render(builder, base.BuildRenderTree);

    /// <inheritdoc />
    public void Dispose() => _control.Dispose();
}

/// <summary>
/// A <see cref="ButtonGroup"/> in a <see cref="Toolbar"/>, holding <see cref="ToolbarButton"/>s and
/// <see cref="ToolbarIconButton"/>s. It moves into the "…" menu as one, as a group named by its
/// <c>Label</c>.
/// </summary>
/// <example>
/// <code>
/// &lt;ToolbarButtonGroup Label="History"&gt;
///     &lt;ToolbarIconButton Icon="Lucide.Undo2" Label="Undo" /&gt;
///     &lt;ToolbarIconButton Icon="Lucide.Redo2" Label="Redo" /&gt;
/// &lt;/ToolbarButtonGroup&gt;
/// </code>
/// </example>
/// <seealso cref="Toolbar"/>
[ModeSupport]
public sealed class ToolbarButtonGroup : ButtonGroup, IToolbarItem, IDisposable
{
    private readonly ToolbarSlot _slot;
    private readonly List<IToolbarItem> _buttons = [];

    /// <summary>Creates the group.</summary>
    public ToolbarButtonGroup() => _slot = new ToolbarSlot(this, nameof(ToolbarButtonGroup));

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
    /// </summary>
    [Parameter] public ToolbarOverflow Overflow { get; set; }

    bool IToolbarItem.Separator => false;

    RenderFragment IToolbarItem.Bar => builder => base.BuildRenderTree(builder);

    // Its buttons as a labeled group, when every one can go in the menu.
    RenderFragment? IToolbarItem.Menu
    {
        get
        {
            var menus = _buttons.ConvertAll(b => b.Menu);
            if (menus.Count == 0 || menus.Exists(m => m is null))
            {
                return null;
            }

            RenderFragment items = builder =>
            {
                foreach (var menu in menus)
                {
                    builder.AddContent(0, menu);
                }
            };
            return string.IsNullOrWhiteSpace(Label) ? items : builder =>
            {
                builder.OpenComponent<MenuGroup>(0);
                builder.AddComponentParameter(1, nameof(MenuGroup.Label), Label);
                builder.AddComponentParameter(2, nameof(MenuGroup.ChildContent), items);
                builder.CloseComponent();
            };
        }
    }

    double? IToolbarItem.Width => Sum(_buttons.ConvertAll(b => b.Width));

    double? IToolbarItem.CollapsedWidth => Sum(_buttons.ConvertAll(b => b.CollapsedWidth));

    // Joined buttons share a 1px border; spaced ones are 8px apart.
    private double? Sum(List<double?> widths) => widths.Count > 0 && widths.TrueForAll(w => w is not null)
        ? widths.Sum(w => w!.Value) + (widths.Count - 1) * (Attached ? -1 : 8)
        : null;

    /// <summary>Adds or updates one of its buttons, which the toolbar draws inside it.</summary>
    internal void Set(IToolbarItem button)
    {
        if (!_buttons.Contains(button))
        {
            _buttons.Add(button);
        }

        _slot.MenuChanged();
    }

    internal void Remove(IToolbarItem button)
    {
        if (_buttons.Remove(button))
        {
            _slot.MenuChanged();
        }
    }

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        var task = base.SetParametersAsync(parameters);
        _slot.Join(Toolbar);
        return task;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder) => _slot.Rendered();

    /// <inheritdoc />
    public void Dispose() => _slot.Dispose();
}

/// <summary>
/// A <see cref="Menu"/> in a <see cref="Toolbar"/>. In the "…" menu it's a submenu with the same
/// items, named by its <c>Label</c> (a menu with a custom <c>Trigger</c> and no Label stays in the bar).
/// </summary>
/// <example>
/// <code>
/// &lt;ToolbarMenu Label="Export" StartIcon="Lucide.Download"&gt;
///     &lt;MenuItem Href="/export.csv"&gt;CSV&lt;/MenuItem&gt;
/// &lt;/ToolbarMenu&gt;
/// </code>
/// </example>
/// <seealso cref="Toolbar"/>
[ModeSupport(NoScript = ModeLevel.Partial, NoScriptNotes = "With JavaScript off, the menu opens and closes, but arrow keys and type-ahead don't move between items.")]
public sealed class ToolbarMenu : Menu, IToolbarItem, IDisposable
{
    private readonly ToolbarSlot _slot;

    /// <summary>Creates the menu.</summary>
    public ToolbarMenu() => _slot = new ToolbarSlot(this, nameof(ToolbarMenu));

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
    /// </summary>
    [Parameter] public ToolbarOverflow Overflow { get; set; }

    bool IToolbarItem.Separator => false;

    RenderFragment IToolbarItem.Bar => builder => base.BuildRenderTree(builder);

    RenderFragment? IToolbarItem.Menu => string.IsNullOrWhiteSpace(Label) ? null : builder =>
    {
        builder.OpenComponent<MenuSub>(0);
        builder.AddComponentParameter(1, nameof(MenuSub.Label), Label);
        builder.AddComponentParameter(2, nameof(MenuSub.Icon), StartIcon);
        builder.AddComponentParameter(3, nameof(MenuSub.ChildContent), ChildContent);
        builder.CloseComponent();
    };

    // The default trigger: its label, start icon and chevron.
    double? IToolbarItem.Width => Trigger is not null ? null
        : ButtonWidth.Estimate(TriggerSize, iconOnly: false, hasLabel: true, Label, startIcon: StartIcon is not null, endIcon: true);

    // With CollapseLabels, a default trigger with a start icon keeps the icon and the chevron,
    // unless it never moves into the menu.
    private bool Collapses => Toolbar?.CollapseLabels == true && Overflow != ToolbarOverflow.Never && Trigger is null && StartIcon is not null;

    double? IToolbarItem.CollapsedWidth => Collapses
        ? ButtonWidth.Estimate(TriggerSize, iconOnly: false, hasLabel: false, null, startIcon: true, endIcon: true)
        : ((IToolbarItem)this).Width;

    private protected override string? TriggerClass => Collapses ? "ib-toolbar-collapse" : null;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        var task = base.SetParametersAsync(parameters);
        _slot.Join(Toolbar);
        return task;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder) => _slot.Rendered();

    /// <inheritdoc />
    public void Dispose() => _slot.Dispose();
}

/// <summary>
/// A <see cref="SplitButton"/> in a <see cref="Toolbar"/>. In the "…" menu it's a submenu named by its
/// <c>MenuLabel</c>, with the main action as its first item, then the same items; with
/// <c>MenuDisabled</c> it's just the main action.
/// </summary>
/// <example>
/// <code>
/// &lt;ToolbarSplitButton Label="Save" StartIcon="Lucide.Save" Submit MenuLabel="More save options" Priority="1"&gt;
///     &lt;MenuItem Submit Name="intent" Value="draft"&gt;Save as draft&lt;/MenuItem&gt;
/// &lt;/ToolbarSplitButton&gt;
/// </code>
/// </example>
/// <seealso cref="Toolbar"/>
/// <seealso cref="ToolbarMenu"/>
[ModeSupport(NoScript = ModeLevel.Partial, Notes = "OnClick runs only on interactive pages, from the bar or the menu; Submit and Href work everywhere.", NoScriptNotes = "With JavaScript off, the menu opens and closes, but Alt+Down and the arrow keys don't work.")]
public sealed class ToolbarSplitButton : SplitButton, IToolbarItem, IDisposable
{
    private readonly ToolbarSlot _slot;

    /// <summary>Creates the button.</summary>
    public ToolbarSplitButton() => _slot = new ToolbarSlot(this, nameof(ToolbarSplitButton));

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
    /// </summary>
    [Parameter] public ToolbarOverflow Overflow { get; set; }

    bool IToolbarItem.Separator => false;

    RenderFragment IToolbarItem.Bar => builder => base.BuildRenderTree(builder);

    // One wired up by its other attributes does more than a menu item can.
    RenderFragment? IToolbarItem.Menu => !ToolbarControl.CopiesToMenu(AdditionalAttributes) ? null
        : MenuDisabled && !Disabled && !Loading ? MainItem
        : builder =>
        {
            builder.OpenComponent<MenuSub>(0);
            builder.AddComponentParameter(1, nameof(MenuSub.Label), ToggleLabel);
            builder.AddComponentParameter(2, nameof(MenuSub.Icon), StartIcon);
            builder.AddComponentParameter(3, nameof(MenuSub.Disabled), Disabled || Loading);
            builder.AddComponentParameter(4, nameof(MenuSub.ChildContent), (RenderFragment)(sub =>
            {
                // A submenu's trigger only opens, so the main action goes first in it.
                sub.AddContent(0, MainItem);
                sub.AddContent(1, ChildContent);
            }));
            builder.CloseComponent();
        };

    private RenderFragment MainItem => builder =>
    {
        builder.OpenComponent<MenuItem>(0);
        builder.AddComponentParameter(1, nameof(MenuItem.Href), Href);
        builder.AddComponentParameter(2, nameof(MenuItem.Submit), Submit);
        builder.AddComponentParameter(3, nameof(MenuItem.Name), Name);
        builder.AddComponentParameter(4, nameof(MenuItem.Value), Value);
        builder.AddComponentParameter(5, nameof(MenuItem.FormAction), FormAction);
        builder.AddComponentParameter(6, nameof(MenuItem.Form), Form);
        builder.AddComponentParameter(7, nameof(MenuItem.Icon), StartIcon);
        builder.AddComponentParameter(8, nameof(MenuItem.Disabled), Disabled || Loading || MainDisabled);
        builder.AddComponentParameter(9, nameof(MenuItem.Accent), (Accent ?? Group?.DefaultAccent) == Accent.Danger ? Accent.Danger : Accent.Neutral);
        if (OnClick.HasDelegate)
        {
            builder.AddComponentParameter(10, nameof(MenuItem.OnClick), OnClick);
        }

        if (External && Href is not null)
        {
            builder.AddAttribute(11, "target", "_blank");
            builder.AddAttribute(12, "rel", "noopener noreferrer");
        }

        builder.AddComponentParameter(13, nameof(MenuItem.ChildContent), (RenderFragment)(b => b.AddContent(0, Label)));
        ToolbarControl.CopyDownload(builder, AdditionalAttributes);
        builder.CloseComponent();
    };

    double? IToolbarItem.Width => EstimateWidth(collapsed: false);

    // With CollapseLabels, one with a start icon shows only that icon and the arrow, unless it never
    // moves into the menu.
    private bool Collapses => Toolbar?.CollapseLabels == true && Overflow != ToolbarOverflow.Never
        && StartIcon is not null && !IconOnly && !(External && Href is not null);

    double? IToolbarItem.CollapsedWidth => EstimateWidth(Collapses);

    private protected override string? MainClass => Collapses ? "ib-toolbar-collapse ib-toolbar-collapse-square" : null;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        var task = base.SetParametersAsync(parameters);
        _slot.Join(Toolbar);
        return task;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder) => _slot.Rendered();

    /// <inheritdoc />
    public void Dispose() => _slot.Dispose();
}

/// <summary>
/// The main button of a <see cref="SplitButton"/>: a <see cref="Button"/> that with
/// <see cref="IconOnlyName"/> shows only its icon, named by that, as an <see cref="IconButton"/> is.
/// </summary>
internal sealed class SplitButtonMain : Button
{
    /// <summary>The name when it shows only its icon; null with its text showing.</summary>
    [Parameter] public string? IconOnlyName { get; set; }

    /// <inheritdoc />
    protected override bool IconOnly => IconOnlyName is not null;

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, object>>? ExtraAttributes
    {
        get
        {
            var attributes = new List<KeyValuePair<string, object>>();

            // A loading link has no href. It keeps focus and its tab stop, as a loading button does.
            if (Loading && Href is not null)
            {
                attributes.Add(new("tabindex", "0"));
            }

            if (IconOnlyName is not null)
            {
                // An aria-label replaces the content, so it says what the hidden texts would.
                var name = Loading ? $"{LoadingLabel ?? TextDefaults.Loading} {IconOnlyName}" : IconOnlyName;
                name = External && Href is not null ? $"{name} {NewTabLabel ?? TextDefaults.OpensInNewTab}" : name;
                attributes.Add(new("aria-label", name));
                attributes.Add(new("title", IconOnlyName));
            }

            return attributes;
        }
    }
}

/// <summary>
/// A toolbar button's place: an item of the toolbar at its top, or one of a
/// <see cref="ToolbarButtonGroup"/>'s buttons, which the group's copy draws where it's written.
/// </summary>
internal sealed class ToolbarControl(IToolbarItem button, string component) : IDisposable
{
    private readonly ToolbarSlot _slot = new(button, component);
    private ToolbarButtonGroup? _group;

    public void Place(ToolbarContext? toolbar, ButtonGroup? group)
    {
        if (group is ToolbarButtonGroup buttons)
        {
            if (button.Position != ToolbarPosition.Start || button.Priority != 0 || button.Overflow != ToolbarOverflow.Auto)
            {
                throw new InvalidOperationException($"A {component} in a ToolbarButtonGroup can't take Position, Priority or Overflow: set them on the group.");
            }

            if (!ReferenceEquals(buttons, _group))
            {
                _group?.Remove(button);
                _group = buttons;
            }

            buttons.Set(button);
            return;
        }

        _slot.Join(toolbar);
    }

    /// <summary>In a group the button draws itself; at the top of the toolbar, the toolbar draws it.</summary>
    public void Render(RenderTreeBuilder builder, Action<RenderTreeBuilder> render)
    {
        if (_group is not null)
        {
            render(builder);
        }
        else
        {
            _slot.Rendered();
        }
    }

    /// <summary>The toolbar a button is in: the one around it, or for a group's button, the group's.</summary>
    public static ToolbarContext? Of(ToolbarContext? toolbar, ButtonGroup? group) => group is ToolbarButtonGroup buttons ? buttons.Toolbar : toolbar;

    /// <summary>
    /// Whether its other attributes are only names, ids and a link's download, which a menu copy can keep or do without. One
    /// wired to something by its attributes (a popover's trigger, an event handler) can't be copied.
    /// </summary>
    public static bool CopiesToMenu(IReadOnlyDictionary<string, object>? attributes) => attributes is null
        || attributes.Keys.All(k => k is "id" or "title" or "download" || k.StartsWith("aria-", StringComparison.Ordinal));

    /// <summary>A link's download stays one in the menu, or it would navigate instead.</summary>
    public static void CopyDownload(RenderTreeBuilder builder, IReadOnlyDictionary<string, object>? attributes)
    {
        if (attributes?.TryGetValue("download", out var download) == true)
        {
            builder.AddAttribute(16, "download", download);
        }
    }

    /// <summary>
    /// Refuses a control written straight in a toolbar or a <see cref="ToolbarButtonGroup"/>, where
    /// only toolbar items go: the toolbar couldn't place it or copy it into the "…" menu.
    /// </summary>
    public static void Refuse(object control, ToolbarContext? toolbar, ButtonGroup? group = null)
    {
        if (control is IToolbarItem)
        {
            return;
        }

        var name = control.GetType().Name;
        var instead = control.GetType() == typeof(Button) ? "ToolbarButton"
            : control.GetType() == typeof(IconButton) ? "ToolbarIconButton"
            : control.GetType() == typeof(ButtonGroup) ? "ToolbarButtonGroup"
            : control.GetType() == typeof(Menu) ? "ToolbarMenu"
            : control.GetType() == typeof(SplitButton) ? "ToolbarSplitButton"
            : null;
        if (toolbar is not null && !ReferenceEquals(toolbar, ToolbarContext.Outside))
        {
            throw new InvalidOperationException(instead is null
                ? $"{name} can't go directly in a Toolbar: put it in a ToolbarItem."
                : $"{name} can't go directly in a Toolbar: use {instead}, or put it in a ToolbarItem.");
        }

        if (group is ToolbarButtonGroup)
        {
            throw new InvalidOperationException($"{name} can't go in a ToolbarButtonGroup: use ToolbarButton or ToolbarIconButton.");
        }
    }

    public void Dispose()
    {
        _group?.Remove(button);
        _group = null;
        _slot.Dispose();
    }
}
