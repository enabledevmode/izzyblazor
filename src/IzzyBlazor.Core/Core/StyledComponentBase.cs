using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// Base class for layout and surface components. Adds the surface props (padding, background,
/// text color, radius, shadow, borders, heights, overflow) to the item props of
/// <see cref="LayoutItemBase"/>. Every prop takes a single value or a breakpoint map, except the
/// on/off switches.
/// </summary>
/// <example>
/// <code>
/// &lt;Box Padding="4" Bg="Color.SurfaceContainer" Radius="Radius.Md"&gt;
/// &lt;Box Padding="new() { Base = 4, Md = 10 }"&gt;
/// </code>
/// </example>
/// <remarks>
/// Each prop that is set adds a modifier class (<c>ib-px</c>, <c>ib-px-md</c>...) and a
/// non-inheriting custom property. Props that are not set add nothing, so they never
/// override a component's own defaults.
/// </remarks>
public abstract class StyledComponentBase : LayoutItemBase
{
    /// <summary>
    /// The color scheme inside: <see cref="IzzyBlazor.ColorScheme.Dark"/> for a dark band on a light
    /// page, <see cref="IzzyBlazor.ColorScheme.Inverse"/> for the opposite of the page. It also sets
    /// the surface background and text color, which <see cref="Bg"/> and <see cref="Color"/> override.
    /// </summary>
    [Parameter] public ColorScheme ColorScheme { get; set; } = ColorScheme.Auto;

    /// <summary>Padding on all sides, in spacing steps (0 to 16; one step = 4px).</summary>
    [Parameter] public Responsive<int> Padding { get; set; }

    /// <summary>Horizontal padding. When set, replaces <see cref="Padding"/> on this axis.</summary>
    [Parameter] public Responsive<int> PaddingX { get; set; }

    /// <summary>Vertical padding. When set, replaces <see cref="Padding"/> on this axis.</summary>
    [Parameter] public Responsive<int> PaddingY { get; set; }

    /// <summary>Top padding. When set, replaces <see cref="PaddingY"/> and <see cref="Padding"/> on this side.</summary>
    [Parameter] public Responsive<int> PaddingTop { get; set; }

    /// <summary>Bottom padding. When set, replaces <see cref="PaddingY"/> and <see cref="Padding"/> on this side.</summary>
    [Parameter] public Responsive<int> PaddingBottom { get; set; }

    /// <summary>Padding at the start (left in left-to-right text, right in right-to-left). Replaces <see cref="PaddingX"/> and <see cref="Padding"/> on this side.</summary>
    [Parameter] public Responsive<int> PaddingStart { get; set; }

    /// <summary>Padding at the end (right in left-to-right text, left in right-to-left). Replaces <see cref="PaddingX"/> and <see cref="Padding"/> on this side.</summary>
    [Parameter] public Responsive<int> PaddingEnd { get; set; }

    /// <summary>Background color.</summary>
    [Parameter] public Responsive<Color> Bg { get; set; }

    /// <summary>
    /// Text color. When not set and <see cref="Bg"/> is a theme color, the matching contrast color is
    /// used automatically (<c>Bg="Color.Primary"</c> gets <c>Color.OnPrimary</c> text).
    /// </summary>
    [Parameter] public Responsive<Color> Color { get; set; }

    /// <summary>Corner radius.</summary>
    [Parameter] public Responsive<Radius> Radius { get; set; }

    /// <summary>Elevation.</summary>
    [Parameter] public Responsive<Shadow> Shadow { get; set; }

    /// <summary>Minimum height.</summary>
    [Parameter] public Responsive<Length> MinHeight { get; set; }

    /// <summary>Height.</summary>
    [Parameter] public Responsive<Length> Height { get; set; }

    /// <summary>Maximum height; pair it with <see cref="Overflow"/> to scroll what doesn't fit.</summary>
    [Parameter] public Responsive<Length> MaxHeight { get; set; }

    /// <summary>What happens to content that doesn't fit: <see cref="IzzyBlazor.Overflow.Auto"/> to scroll it.</summary>
    [Parameter] public Responsive<Overflow> Overflow { get; set; }

    /// <summary>
    /// A border on every side, in whole pixels (0 to 16), in <see cref="BorderColor"/> (the border
    /// token by default) and <see cref="BorderStyle"/> (solid by default). Like padding: <see cref="BorderX"/> and <see cref="BorderY"/> override an
    /// axis, the single sides override that. Sides you don't set keep the component's own border.
    /// </summary>
    [Parameter] public Responsive<int> Border { get; set; }

    /// <summary>Borders at the start and end. Replaces <see cref="Border"/> on this axis.</summary>
    [Parameter] public Responsive<int> BorderX { get; set; }

    /// <summary>Borders at the top and bottom, such as a band with lines above and below. Replaces <see cref="Border"/> on this axis.</summary>
    [Parameter] public Responsive<int> BorderY { get; set; }

    /// <summary>The top border. Replaces <see cref="BorderY"/> and <see cref="Border"/> on this side.</summary>
    [Parameter] public Responsive<int> BorderTop { get; set; }

    /// <summary>The bottom border. Replaces <see cref="BorderY"/> and <see cref="Border"/> on this side.</summary>
    [Parameter] public Responsive<int> BorderBottom { get; set; }

    /// <summary>The border at the start (left in left-to-right text). Replaces <see cref="BorderX"/> and <see cref="Border"/> on this side.</summary>
    [Parameter] public Responsive<int> BorderStart { get; set; }

    /// <summary>The border at the end (right in left-to-right text). Replaces <see cref="BorderX"/> and <see cref="Border"/> on this side.</summary>
    [Parameter] public Responsive<int> BorderEnd { get; set; }

    /// <summary>The border color, for borders set here or the component's own. Defaults to <see cref="IzzyBlazor.Color.Border"/>.</summary>
    [Parameter] public Responsive<Color> BorderColor { get; set; }

    /// <summary>
    /// How borders set here are drawn (<see cref="IzzyBlazor.BorderStyle.Dashed"/>,
    /// <see cref="IzzyBlazor.BorderStyle.Dotted"/>), and the own border of a <see cref="Card"/> or a bordered
    /// <see cref="EmptyState"/>. Solid by default (dashed on an EmptyState).
    /// </summary>
    [Parameter] public Responsive<BorderStyle> BorderStyle { get; set; }

    /// <inheritdoc />
    private protected override void BuildLayoutAttributes(ClassBuilder classes, StyleBuilder styles, bool itemProps)
    {
        // Item props (margins, widths, flex) only where the parent lays this element out; surface
        // props always. The order matches the output before the two sets were split.
        if (ColorScheme.ClassName() is { } scheme)
        {
            classes.Add(scheme);
        }

        ApplyAxis(classes, styles, Either(PaddingX, Padding), PaddingStart, PaddingEnd, "px", "ps", "pe", StyleValues.Spacing);
        ApplyAxis(classes, styles, Either(PaddingY, Padding), PaddingTop, PaddingBottom, "py", "pt", "pb", StyleValues.Spacing);
        if (itemProps)
        {
            LayoutItemProps.ApplyMargins(classes, styles, this);
        }

        ApplyResponsive(classes, styles, Bg, "ib-bg", "bg", StyleValues.Color);
        var textColor = Color.IsEmpty ? Bg.Select(bg => bg.ContrastText) : Color;
        ApplyResponsive(classes, styles, textColor, "ib-fg", "fg", StyleValues.Color);
        ApplyResponsive(classes, styles, Radius, "ib-radius", "rounded", StyleValues.Radius);
        ApplyResponsive(classes, styles, Shadow, "ib-shadow", "elevation", StyleValues.Shadow);
        if (itemProps)
        {
            ApplyResponsive(classes, styles, Width, "ib-w", "w", StyleValues.Length);
            ApplyResponsive(classes, styles, MaxWidth, "ib-maxw", "maxw", StyleValues.Length);
        }

        ApplyResponsive(classes, styles, MinHeight, "ib-minh", "minh", StyleValues.Length);
        if (itemProps)
        {
            ApplyResponsive(classes, styles, MinWidth, "ib-minw", "minw", StyleValues.Length);
        }

        ApplyResponsive(classes, styles, Height, "ib-h", "h", StyleValues.Length);
        ApplyResponsive(classes, styles, MaxHeight, "ib-maxh", "maxh", StyleValues.Length);
        ApplyResponsive(classes, styles, Overflow, "ib-ov", "ov", StyleValues.Overflow);
        ApplyAxis(classes, styles, Either(BorderX, Border), BorderStart, BorderEnd, "bx", "bs", "be", StyleValues.BorderWidth);
        ApplyAxis(classes, styles, Either(BorderY, Border), BorderTop, BorderBottom, "by", "bt", "bb", StyleValues.BorderWidth);
        ApplyResponsive(classes, styles, BorderColor, "ib-bc", "bc", StyleValues.Color);
        ApplyResponsive(classes, styles, BorderStyle, "ib-bst", "bst", StyleValues.BorderStyle);
        if (itemProps)
        {
            LayoutItemProps.ApplyFlex(classes, styles, this);
            LayoutItemProps.ApplyGrid(classes, styles, this);
        }
    }

    private static void ApplyAxis(ClassBuilder classes, StyleBuilder styles, Responsive<int> axis, Responsive<int> start, Responsive<int> end,
        string axisName, string startName, string endName, Func<int, string> format) =>
        LayoutItemProps.ApplyAxis(classes, styles, axis, start, end, axisName, startName, endName, format);

    private static Responsive<int> Either(Responsive<int> specific, Responsive<int> general) =>
        LayoutItemProps.Either(specific, general);
}
