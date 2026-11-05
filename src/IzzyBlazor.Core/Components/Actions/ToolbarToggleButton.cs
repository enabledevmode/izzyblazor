using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// A <see cref="ToggleButton"/> in a <see cref="Toolbar"/> or a <see cref="ToolbarButtonGroup"/>,
/// such as bold or show marks in an editor. In the "…" menu it's a checkbox item that toggles the
/// same value.
/// </summary>
/// <example>
/// <code>
/// &lt;ToolbarButtonGroup Label="Text style"&gt;
///     &lt;ToolbarToggleButton @bind-Value="_bold" Icon="Lucide.Bold" Label="Bold" IconOnly /&gt;
///     &lt;ToolbarToggleButton @bind-Value="_italic" Icon="Lucide.Italic" Label="Italic" IconOnly /&gt;
/// &lt;/ToolbarButtonGroup&gt;
/// </code>
/// </example>
/// <seealso cref="Toolbar"/>
/// <seealso cref="ToggleButton"/>
[ModeSupport(Notes = "In the bar it toggles in every mode. In the \"…\" menu, choosing it changes the value only on interactive pages.")]
public sealed class ToolbarToggleButton : ToggleButton, IToolbarItem, IDisposable
{
    private readonly ToolbarControl _control;

    /// <summary>Creates the toggle.</summary>
    public ToolbarToggleButton() => _control = new ToolbarControl(this, nameof(ToolbarToggleButton));

    /// <summary>Where it sits. Defaults to <see cref="ToolbarPosition.Start"/>. Not in a group: set it on the group.</summary>
    [Parameter] public ToolbarPosition Position { get; set; }

    /// <summary>
    /// Which items stay longest when there's no room: higher stays longer. Defaults to 0. Items with
    /// the same priority move into the menu from the middle of the toolbar outward.
    /// </summary>
    [Parameter] public int Priority { get; set; }

    /// <summary>
    /// When it moves into the "…" menu: <see cref="ToolbarOverflow.Never"/> to keep it in the bar,
    /// <see cref="ToolbarOverflow.Always"/> for what only the menu needs. Defaults to <see cref="ToolbarOverflow.Auto"/>.
    /// </summary>
    [Parameter] public ToolbarOverflow Overflow { get; set; }

    bool IToolbarItem.Separator => false;

    RenderFragment IToolbarItem.Bar => builder => base.BuildRenderTree(builder);

    // A checkbox item with the same value; one wired up by its attributes stays in the bar.
    RenderFragment? IToolbarItem.Menu => !ToolbarControl.CopiesToMenu(AdditionalAttributes) ? null : builder =>
    {
        builder.OpenComponent<MenuItem>(0);
        builder.AddComponentParameter(1, nameof(MenuItem.Icon), Icon);
        builder.AddComponentParameter(2, nameof(MenuItem.Disabled), IsDisabled);
        builder.AddComponentParameter(3, nameof(MenuItem.Checked), (bool?)CurrentValue);
        builder.AddComponentParameter(4, nameof(MenuItem.CheckedChanged), EventCallback.Factory.Create<bool>(this, value => CurrentValue = value));
        builder.AddComponentParameter(5, nameof(MenuItem.ChildContent), ChildContent ?? (RenderFragment)(b => b.AddContent(0, ResolvedLabel)));
        builder.CloseComponent();
    };

    double? IToolbarItem.Width => ButtonWidth.Estimate(EffectiveSize, IconOnly, !IconOnly, IconOnly ? null : ChildContent is null ? ResolvedLabel : Code.ReadText(ChildContent), Icon is not null, endIcon: false);

    // With CollapseLabels, one with an icon shows only the icon, unless it never moves into the menu.
    private bool Collapses => ToolbarControl.Of(Toolbar, Group)?.CollapseLabels == true
        && ((Group as ToolbarButtonGroup)?.Overflow ?? Overflow) != ToolbarOverflow.Never
        && Icon is not null && !IconOnly;

    double? IToolbarItem.CollapsedWidth => Collapses
        ? ButtonWidth.Estimate(EffectiveSize, iconOnly: true, hasLabel: false, null, startIcon: true, endIcon: false)
        : ((IToolbarItem)this).Width;

    /// <inheritdoc />
    private protected override string? ExtraClass => Collapses ? "ib-toolbar-collapse ib-toolbar-collapse-square" : null;

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
