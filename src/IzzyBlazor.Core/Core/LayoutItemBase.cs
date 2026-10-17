using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// Base class for components that sit in a layout: adds the item props, which say how the
/// component sits in its parent (margins, growing and shrinking in a Stack, widths, alignment).
/// Every prop takes a single value or a breakpoint map.
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
/// The props go on the component's outermost element, the one its parent lays out. Props that
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

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        base.BuildAttributes(classes, styles);
        BuildLayoutAttributes(classes, styles);
    }

    /// <summary>
    /// Adds the style-prop classes and custom properties. <see cref="StyledComponentBase"/>
    /// overrides it to interleave the surface props.
    /// </summary>
    private protected virtual void BuildLayoutAttributes(ClassBuilder classes, StyleBuilder styles) =>
        LayoutItemProps.Apply(classes, styles, this);
}
