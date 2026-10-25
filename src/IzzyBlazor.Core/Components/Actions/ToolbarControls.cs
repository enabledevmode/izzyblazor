using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// A <see cref="Button"/> in a <see cref="Toolbar"/> or a <see cref="ToolbarButtonGroup"/>. In the
/// "…" menu it's a menu item with the same link, form post and <c>OnClick</c>.
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

        if (External && Href is not null)
        {
            builder.AddAttribute(11, "target", "_blank");
            builder.AddAttribute(12, "rel", "noopener noreferrer");
        }

        builder.AddComponentParameter(13, nameof(MenuItem.ChildContent), ChildContent);
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
/// An <see cref="IconButton"/> in a <see cref="Toolbar"/> or a <see cref="ToolbarButtonGroup"/>. In
/// the "…" menu it's a menu item with its icon, its <c>Label</c> as text, and the same link, form
/// post and <c>OnClick</c>.
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

    // Joined buttons share a 1px border; spaced ones are 8px apart.
    double? IToolbarItem.Width
    {
        get
        {
            var widths = _buttons.ConvertAll(b => b.Width);
            return widths.Count > 0 && widths.TrueForAll(w => w is not null)
                ? widths.Sum(w => w!.Value) + (widths.Count - 1) * (Attached ? -1 : 8)
                : null;
        }
    }

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
[ModeSupport(Off = ModeLevel.Partial, Notes = "With JavaScript off, the menu opens and closes, but arrow keys and type-ahead don't move between items.")]
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

    /// <summary>
    /// Whether its other attributes are only names and ids, which a menu copy can do without. One
    /// wired to something by its attributes (a popover's trigger, an event handler) can't be copied.
    /// </summary>
    public static bool CopiesToMenu(IReadOnlyDictionary<string, object>? attributes) => attributes is null
        || attributes.Keys.All(k => k is "id" or "title" || k.StartsWith("aria-", StringComparison.Ordinal));

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
