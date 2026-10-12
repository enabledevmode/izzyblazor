using System.ComponentModel;
using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// What <see cref="Stack"/>, <see cref="Row"/> and <see cref="Column"/> share: a flexbox container
/// with gap, alignment and wrapping. Gap, Align and Justify take a single value or a breakpoint map.
/// </summary>
public abstract class FlexBase : Box
{
    /// <summary>Space between children, in spacing steps (0 to 16).</summary>
    [Parameter] public Responsive<int> Gap { get; set; }

    /// <summary>Cross-axis alignment. Defaults to stretch.</summary>
    [Parameter] public Responsive<Align> Align { get; set; }

    /// <summary>Main-axis distribution. Defaults to start.</summary>
    [Parameter] public Responsive<Justify> Justify { get; set; }

    /// <summary>Whether children wrap onto new lines: <c>&lt;Row Wrap&gt;</c>.</summary>
    [Parameter] public bool Wrap { get; set; }

    /// <summary>Renders as <c>inline-flex</c> so the container sits in a line of text.</summary>
    [Parameter] public bool Inline { get; set; }

    /// <summary>The layout direction to render. Empty means the stylesheet's default (vertical).</summary>
    protected abstract Responsive<Direction> LayoutDirection { get; }

    /// <inheritdoc />
    protected override string BaseClass => "ib-stack";

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        base.BuildAttributes(classes, styles);
        classes.Add("ib-stack-inline", Inline);
        ApplyResponsive(classes, styles, LayoutDirection, "ib-dir", "dir", StyleValues.Direction);
        ApplyResponsive(classes, styles, Gap, "ib-gap", "gap", StyleValues.Spacing);
        ApplyResponsive(classes, styles, Align, "ib-align", "align", StyleValues.Align);
        ApplyResponsive(classes, styles, Justify, "ib-justify", "justify", StyleValues.Justify);
        ApplyResponsive(classes, styles, Wrap ? true : default(Responsive<bool>), "ib-wrap", "wrap", StyleValues.Wrap);
    }
}

/// <summary>
/// The general flexbox container: vertical by default, any direction with <see cref="Direction"/>,
/// which can change per breakpoint. For a direction that never changes, <see cref="Row"/> and
/// <see cref="Column"/> say it in the name.
/// </summary>
/// <example>
/// <code>
/// &lt;Stack Direction="new() { Base = Direction.Column, Md = Direction.Row }" Gap="4" Align="Align.Center"&gt;
/// </code>
/// </example>
public class Stack : FlexBase
{
    /// <summary>Layout direction. Defaults to <see cref="IzzyBlazor.Direction.Column"/> (vertical).</summary>
    [Parameter] public Responsive<Direction> Direction { get; set; }

    /// <inheritdoc />
    protected override Responsive<Direction> LayoutDirection => Direction;
}

/// <summary>Lays children out horizontally. For a direction that changes by screen size, use <see cref="Stack"/>.</summary>
public class Row : FlexBase
{
    /// <summary>Not available: a Row is always horizontal. Use <see cref="Stack"/> for a direction that changes.</summary>
    /// <remarks>
    /// Declared only to turn a mistake into a build error. Without it, Direction would pass through
    /// as a meaningless HTML attribute and the layout would silently ignore it.
    /// </remarks>
    [Parameter]
    [Obsolete("A Row is always horizontal. For a direction that changes, use Stack with Direction.", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Responsive<Direction> Direction { get; set; }

    /// <inheritdoc />
    protected override Responsive<Direction> LayoutDirection => IzzyBlazor.Direction.Row;
}

/// <summary>Lays children out vertically. For a direction that changes by screen size, use <see cref="Stack"/>.</summary>
public class Column : FlexBase
{
    /// <summary>Not available: a Column is always vertical. Use <see cref="Stack"/> for a direction that changes.</summary>
    /// <remarks>
    /// Declared only to turn a mistake into a build error. Without it, Direction would pass through
    /// as a meaningless HTML attribute and the layout would silently ignore it.
    /// </remarks>
    [Parameter]
    [Obsolete("A Column is always vertical. For a direction that changes, use Stack with Direction.", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Responsive<Direction> Direction { get; set; }

    /// <inheritdoc />
    protected override Responsive<Direction> LayoutDirection => IzzyBlazor.Direction.Column;
}
