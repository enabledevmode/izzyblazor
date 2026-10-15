using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// Base class for layout and surface components. Adds a small, token-based set of
/// style props. Every prop takes a single value or a breakpoint map.
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
public abstract class StyledComponentBase : IzzyComponentBase
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

    /// <summary>Margin on all sides, in spacing steps (-16 to 16).</summary>
    [Parameter] public Responsive<int> Margin { get; set; }

    /// <summary>Horizontal margin. When set, replaces <see cref="Margin"/> on this axis.</summary>
    [Parameter] public Responsive<int> MarginX { get; set; }

    /// <summary>Vertical margin. When set, replaces <see cref="Margin"/> on this axis.</summary>
    [Parameter] public Responsive<int> MarginY { get; set; }

    /// <summary>Top padding. When set, replaces <see cref="PaddingY"/> and <see cref="Padding"/> on this side.</summary>
    [Parameter] public Responsive<int> PaddingTop { get; set; }

    /// <summary>Bottom padding. When set, replaces <see cref="PaddingY"/> and <see cref="Padding"/> on this side.</summary>
    [Parameter] public Responsive<int> PaddingBottom { get; set; }

    /// <summary>Padding at the start (left in left-to-right text, right in right-to-left). Replaces <see cref="PaddingX"/> and <see cref="Padding"/> on this side.</summary>
    [Parameter] public Responsive<int> PaddingStart { get; set; }

    /// <summary>Padding at the end (right in left-to-right text, left in right-to-left). Replaces <see cref="PaddingX"/> and <see cref="Padding"/> on this side.</summary>
    [Parameter] public Responsive<int> PaddingEnd { get; set; }

    /// <summary>Top margin. When set, replaces <see cref="MarginY"/> and <see cref="Margin"/> on this side.</summary>
    [Parameter] public Responsive<int> MarginTop { get; set; }

    /// <summary>Bottom margin. When set, replaces <see cref="MarginY"/> and <see cref="Margin"/> on this side.</summary>
    [Parameter] public Responsive<int> MarginBottom { get; set; }

    /// <summary>Margin at the start (left in left-to-right text, right in right-to-left). Replaces <see cref="MarginX"/> and <see cref="Margin"/> on this side.</summary>
    [Parameter] public Responsive<int> MarginStart { get; set; }

    /// <summary>Margin at the end (right in left-to-right text, left in right-to-left). Replaces <see cref="MarginX"/> and <see cref="Margin"/> on this side.</summary>
    [Parameter] public Responsive<int> MarginEnd { get; set; }

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

    /// <summary>Width.</summary>
    [Parameter] public Responsive<Length> Width { get; set; }

    /// <summary>Maximum width.</summary>
    [Parameter] public Responsive<Length> MaxWidth { get; set; }

    /// <summary>Minimum height.</summary>
    [Parameter] public Responsive<Length> MinHeight { get; set; }

    /// <summary>Minimum width.</summary>
    [Parameter] public Responsive<Length> MinWidth { get; set; }

    /// <summary>Height.</summary>
    [Parameter] public Responsive<Length> Height { get; set; }

    /// <summary>Maximum height; pair it with <see cref="Overflow"/> to scroll what doesn't fit.</summary>
    [Parameter] public Responsive<Length> MaxHeight { get; set; }

    /// <summary>What happens to content that doesn't fit: <see cref="IzzyBlazor.Overflow.Auto"/> to scroll it.</summary>
    [Parameter] public Responsive<Overflow> Overflow { get; set; }

    /// <summary>
    /// A border on every side, in whole pixels (0 to 16), in <see cref="BorderColor"/> (the border
    /// token by default). Like padding: <see cref="BorderX"/> and <see cref="BorderY"/> override an
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

    /// <summary>Whether to grow into free space inside a Stack, Row or Column: <c>&lt;Box Grow&gt;</c>.</summary>
    [Parameter] public bool Grow { get; set; }

    /// <summary>Whether to shrink when space is short inside a Stack, Row or Column. Defaults to true; <c>Shrink="false"</c> keeps the size.</summary>
    [Parameter] public bool Shrink { get; set; } = true;

    /// <summary>Starting size inside a Stack, before growing or shrinking.</summary>
    [Parameter] public Responsive<Length> Basis { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        base.BuildAttributes(classes, styles);
        if (ColorScheme.ClassName() is { } scheme)
        {
            classes.Add(scheme);
        }

        ApplyAxis(classes, styles, Either(PaddingX, Padding), PaddingStart, PaddingEnd, "px", "ps", "pe", StyleValues.Spacing);
        ApplyAxis(classes, styles, Either(PaddingY, Padding), PaddingTop, PaddingBottom, "py", "pt", "pb", StyleValues.Spacing);
        ApplyAxis(classes, styles, Either(MarginX, Margin), MarginStart, MarginEnd, "mx", "ms", "me", StyleValues.Margin);
        ApplyAxis(classes, styles, Either(MarginY, Margin), MarginTop, MarginBottom, "my", "mt", "mb", StyleValues.Margin);
        ApplyResponsive(classes, styles, Bg, "ib-bg", "bg", StyleValues.Color);
        var textColor = Color.IsEmpty ? Bg.Select(bg => bg.ContrastText) : Color;
        ApplyResponsive(classes, styles, textColor, "ib-fg", "fg", StyleValues.Color);
        ApplyResponsive(classes, styles, Radius, "ib-radius", "rounded", StyleValues.Radius);
        ApplyResponsive(classes, styles, Shadow, "ib-shadow", "elevation", StyleValues.Shadow);
        ApplyResponsive(classes, styles, Width, "ib-w", "w", StyleValues.Length);
        ApplyResponsive(classes, styles, MaxWidth, "ib-maxw", "maxw", StyleValues.Length);
        ApplyResponsive(classes, styles, MinHeight, "ib-minh", "minh", StyleValues.Length);
        ApplyResponsive(classes, styles, MinWidth, "ib-minw", "minw", StyleValues.Length);
        ApplyResponsive(classes, styles, Height, "ib-h", "h", StyleValues.Length);
        ApplyResponsive(classes, styles, MaxHeight, "ib-maxh", "maxh", StyleValues.Length);
        ApplyResponsive(classes, styles, Overflow, "ib-ov", "ov", StyleValues.Overflow);
        ApplyAxis(classes, styles, Either(BorderX, Border), BorderStart, BorderEnd, "bx", "bs", "be", StyleValues.BorderWidth);
        ApplyAxis(classes, styles, Either(BorderY, Border), BorderTop, BorderBottom, "by", "bt", "bb", StyleValues.BorderWidth);
        ApplyResponsive(classes, styles, BorderColor, "ib-bc", "bc", StyleValues.Color);
        // On/off switches are plain bools (so <Box Grow> works); only a change from the default adds CSS.
        ApplyResponsive(classes, styles, Grow ? true : default(Responsive<bool>), "ib-grow", "grow", StyleValues.Factor);
        ApplyResponsive(classes, styles, Shrink ? default(Responsive<bool>) : false, "ib-shrink", "shrink", StyleValues.Factor);
        ApplyResponsive(classes, styles, Basis, "ib-basis", "basis", StyleValues.Length);
    }

    // With no side set, one axis class (ib-px). With either side set, a class per side, each from
    // its own value or the axis value: an axis class at a larger breakpoint would otherwise
    // override a side set at a smaller one.
    private static void ApplyAxis(ClassBuilder classes, StyleBuilder styles, Responsive<int> axis, Responsive<int> start, Responsive<int> end,
        string axisName, string startName, string endName, Func<int, string> format)
    {
        if (start.IsEmpty && end.IsEmpty)
        {
            ApplyResponsive(classes, styles, axis, "ib-" + axisName, axisName, format);
            return;
        }

        ApplyResponsive(classes, styles, Either(start, axis), "ib-" + startName, startName, format);
        ApplyResponsive(classes, styles, Either(end, axis), "ib-" + endName, endName, format);
    }

    private static Responsive<int> Either(Responsive<int> axis, Responsive<int> all) => axis.IsEmpty ? all : axis;
}
