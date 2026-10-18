using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// Base class for components that sit in a layout: adds the item props, which say how the
/// component sits in its parent (margins, growing and shrinking in a Stack, widths, alignment).
/// Every prop takes a single value or a breakpoint map, except the on/off switches <see cref="Grow"/>
/// and <see cref="Shrink"/>.
/// </summary>
/// <example>
/// <code>
/// &lt;Row Gap="2"&gt;
///     &lt;TextField @bind-Value="Query" Grow /&gt;
///     &lt;Button MarginStart="2"&gt;Search&lt;/Button&gt;
/// &lt;/Row&gt;
/// </code>
/// </example>
/// <remarks>
/// The props go on the component's outermost element, the one its parent lays out, even when
/// <c>Class</c> and <c>Style</c> go on an inner element (see <see cref="BuildOuterAttributes"/>). Props that
/// are not set add nothing, so they never override a component's own defaults.
/// <see cref="StyledComponentBase"/> adds the surface props (padding, background, borders...)
/// for boxes and containers.
/// </remarks>
public abstract class LayoutItemBase : IzzyComponentBase, ILayoutItem
{
    /// <summary>Margin on all sides, in spacing steps (-16 to 16; one step = 4px).</summary>
    [Parameter] public Responsive<int> Margin { get; set; }

    /// <summary>Horizontal margin. When set, replaces <see cref="Margin"/> on this axis.</summary>
    [Parameter] public Responsive<int> MarginX { get; set; }

    /// <summary>Vertical margin. When set, replaces <see cref="Margin"/> on this axis.</summary>
    [Parameter] public Responsive<int> MarginY { get; set; }

    /// <summary>Top margin. When set, replaces <see cref="MarginY"/> and <see cref="Margin"/> on this side.</summary>
    [Parameter] public Responsive<int> MarginTop { get; set; }

    /// <summary>Bottom margin. When set, replaces <see cref="MarginY"/> and <see cref="Margin"/> on this side.</summary>
    [Parameter] public Responsive<int> MarginBottom { get; set; }

    /// <summary>Margin at the start (left in left-to-right text, right in right-to-left). Replaces <see cref="MarginX"/> and <see cref="Margin"/> on this side.</summary>
    [Parameter] public Responsive<int> MarginStart { get; set; }

    /// <summary>Margin at the end (right in left-to-right text, left in right-to-left). Replaces <see cref="MarginX"/> and <see cref="Margin"/> on this side.</summary>
    [Parameter] public Responsive<int> MarginEnd { get; set; }

    /// <summary>Width.</summary>
    [Parameter] public Responsive<Length> Width { get; set; }

    /// <summary>Minimum width.</summary>
    [Parameter] public Responsive<Length> MinWidth { get; set; }

    /// <summary>Maximum width.</summary>
    [Parameter] public Responsive<Length> MaxWidth { get; set; }

    /// <summary>Whether to grow into free space inside a Stack, Row or Column: <c>&lt;Box Grow&gt;</c>.</summary>
    [Parameter] public bool Grow { get; set; }

    /// <summary>Whether to shrink when space is short inside a Stack, Row or Column. Defaults to true; <c>Shrink="false"</c> keeps the size.</summary>
    [Parameter] public bool Shrink { get; set; } = true;

    /// <summary>Starting size inside a Stack, before growing or shrinking.</summary>
    [Parameter] public Responsive<Length> Basis { get; set; }

    /// <summary>
    /// Alignment across the parent's direction, overriding the parent's <c>Align</c> for this
    /// component only, such as a button lined up with the bottom of labeled fields in a Row.
    /// </summary>
    [Parameter] public Responsive<Align> AlignSelf { get; set; }

    /// <summary>
    /// How many columns it spans in a <see cref="Form"/> or <see cref="Fieldset"/> with
    /// <c>Columns</c>, or a <see cref="Grid"/>; <see cref="GridSpan.Full"/> for the whole row. Never
    /// more than the columns shown, so a form that narrows to one column needs no breakpoints.
    /// </summary>
    [Parameter] public Responsive<int> ColSpan { get; set; }

    /// <summary>
    /// Starts a new row in a <see cref="Form"/> or <see cref="Fieldset"/> with <c>Columns</c>, or a
    /// <see cref="Grid"/>, leaving the rest of the row before it empty.
    /// </summary>
    [Parameter] public bool NewRow { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        base.BuildAttributes(classes, styles);
        BuildLayoutAttributes(classes, styles, itemProps: VisibilityOnRoot);
    }

    /// <summary>
    /// For components whose <see cref="IzzyComponentBase.Class"/> and <see cref="IzzyComponentBase.Style"/>
    /// go on an inner element (<see cref="IzzyComponentBase.VisibilityOnRoot"/> is false, as in a menu's
    /// panel): the outer element's class and style, with the item props and the visibility classes.
    /// The outer element is the one the parent lays out.
    /// </summary>
    /// <param name="cssClass">The outer element's own classes.</param>
    /// <param name="style">The outer element's own style, if any. Returned as is when no item prop is set.</param>
    protected (string Class, string? Style) BuildOuterAttributes(string cssClass, string? style = null)
    {
        var classes = new ClassBuilder().Add(cssClass);
        var styles = new StyleBuilder();
        LayoutItemProps.Apply(classes, styles, this);
        classes.Add(VisibilityClasses);
        var vars = styles.Build();
        var outerStyle = vars is null ? style
            : string.IsNullOrEmpty(style) ? vars
            : $"{style.TrimEnd(';')};{vars}";
        return (classes.Build()!, outerStyle);
    }

    /// <summary>
    /// Adds the style-prop classes and custom properties. <see cref="StyledComponentBase"/>
    /// overrides it to interleave the surface props.
    /// </summary>
    /// <param name="classes">The root's classes.</param>
    /// <param name="styles">The root's style.</param>
    /// <param name="itemProps">
    /// Whether the item props go on this element: false when <see cref="IzzyComponentBase.VisibilityOnRoot"/>
    /// is false, as they go on the outer element (<see cref="BuildOuterAttributes"/>). Surface props
    /// always go on the element <c>Class</c> goes on.
    /// </param>
    private protected virtual void BuildLayoutAttributes(ClassBuilder classes, StyleBuilder styles, bool itemProps)
    {
        if (itemProps)
        {
            LayoutItemProps.Apply(classes, styles, this);
        }
    }
}
