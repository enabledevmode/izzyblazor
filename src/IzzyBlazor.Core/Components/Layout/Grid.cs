using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// A CSS grid. Columns come from, in order of precedence:
/// <list type="bullet">
/// <item><see cref="Template"/>: your own column sizes, such as <c>240px 1fr</c>, per breakpoint.</item>
/// <item><see cref="Columns"/>: a number of equal columns, per breakpoint.</item>
/// <item><see cref="MinChildWidth"/>: as many columns as fit, with no breakpoints.</item>
/// </list>
/// </summary>
/// <example>
/// <code>
/// &lt;Grid Columns="new() { Base = 1, Sm = 2, Lg = 3 }" Gap="4"&gt;...&lt;/Grid&gt;
/// &lt;Grid MinChildWidth="16.Rem" Gap="4"&gt;...&lt;/Grid&gt;
/// &lt;Grid Template='new() { Base = "1fr", Md = "240px 1fr" }' Gap="6"&gt;...&lt;/Grid&gt;
/// </code>
/// </example>
public class Grid : Box
{
    /// <summary>The number of equal columns (1 to 12), per breakpoint.</summary>
    [Parameter] public Responsive<int> Columns { get; set; }

    /// <summary>
    /// Column sizes as CSS <c>grid-template-columns</c>, per breakpoint: <c>"240px 1fr"</c> for a
    /// fixed side column, <c>"2fr 1fr"</c>, <c>"auto 1fr auto"</c>. Wins over <see cref="Columns"/>.
    /// </summary>
    [Parameter] public Responsive<string> Template { get; set; }

    /// <summary>The smallest a column may be; the grid fits as many as there is room for. Used when neither <see cref="Template"/> nor <see cref="Columns"/> is set.</summary>
    [Parameter] public Length? MinChildWidth { get; set; }

    /// <summary>
    /// With <see cref="MinChildWidth"/>: when there are fewer items than fit in a row, stretch them to
    /// fill it. By default they keep their size and the rest of the row stays empty, so every item
    /// is the same width.
    /// </summary>
    [Parameter] public bool Stretch { get; set; }

    /// <summary>Space between cells, in spacing steps (0 to 16).</summary>
    [Parameter] public Responsive<int> Gap { get; set; }

    /// <summary>How items line up in their cells, block direction. Defaults to stretch.</summary>
    [Parameter] public Responsive<Align> Align { get; set; }

    /// <inheritdoc />
    protected override string BaseClass => "ib-grid";

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        base.BuildAttributes(classes, styles);
        if (!Template.IsEmpty)
        {
            ApplyResponsive(classes, styles, Template, "ib-cols", "cols", value => StyleValues.Raw(value, "grid template"));
        }
        else if (!Columns.IsEmpty)
        {
            ApplyResponsive(classes, styles, Columns, "ib-cols", "cols", StyleValues.GridColumns);
        }
        else if (MinChildWidth is { } min)
        {
            ApplyResponsive(classes, styles, (Responsive<Length>)min, "ib-cols", "cols", width => StyleValues.GridAutoColumns(width, Stretch));
        }

        ApplyResponsive(classes, styles, Gap, "ib-gap", "gap", StyleValues.Spacing);
        ApplyResponsive(classes, styles, Align, "ib-align", "align", StyleValues.Align);
    }
}

/// <summary>
/// A cell of a <see cref="Grid"/> that spans several rows, or columns. Any component takes
/// <c>ColSpan</c>; GridItem adds <see cref="RowSpan"/>.
/// </summary>
/// <example><code>&lt;GridItem ColSpan="new() { Base = 1, Lg = 2 }"&gt;...&lt;/GridItem&gt;</code></example>
public class GridItem : Box
{
    /// <summary>How many rows it spans, per breakpoint.</summary>
    [Parameter] public Responsive<int> RowSpan { get; set; }

    /// <inheritdoc />
    protected override string BaseClass => "ib-grid-item";

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        base.BuildAttributes(classes, styles);
        ApplyResponsive(classes, styles, RowSpan, "ib-rowspan", "rowspan", StyleValues.Span);
    }
}

/// <summary>How wide a <see cref="Container"/> gets.</summary>
public enum ContainerSize
{
    /// <summary>40rem: forms and short text.</summary>
    Sm,

    /// <summary>48rem: articles.</summary>
    Md,

    /// <summary>64rem: most pages.</summary>
    Lg,

    /// <summary>80rem: wide dashboards (the default).</summary>
    Xl,

    /// <summary>No limit; only the side padding.</summary>
    Full,
}

/// <summary>Centers content at a readable width, with side padding that grows on larger screens.</summary>
/// <example><code>&lt;Container Size="ContainerSize.Lg"&gt;...&lt;/Container&gt;</code></example>
public class Container : Box
{
    /// <summary>The widest it gets. Defaults to <see cref="ContainerSize.Xl"/>.</summary>
    [Parameter] public ContainerSize Size { get; set; } = ContainerSize.Xl;

    /// <inheritdoc />
    protected override string BaseClass => "ib-container";

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        base.BuildAttributes(classes, styles);
        classes.Add($"ib-container-{Size.ToString().ToLowerInvariant()}");
    }
}

/// <summary>Centers its children both ways, such as an icon in a square or an empty state in a panel.</summary>
public class Center : Box
{
    /// <summary>Renders as <c>inline-flex</c>, to sit in a line of text.</summary>
    [Parameter] public bool Inline { get; set; }

    /// <inheritdoc />
    protected override string BaseClass => "ib-center";

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        base.BuildAttributes(classes, styles);
        classes.Add("ib-center-inline", Inline);
    }
}

/// <summary>Takes up the free space in a <see cref="Stack"/> or <see cref="Row"/>, pushing what follows to the end.</summary>
/// <example><code>&lt;Row&gt;&lt;Logo /&gt;&lt;Spacer /&gt;&lt;Button&gt;Sign in&lt;/Button&gt;&lt;/Row&gt;</code></example>
public class Spacer : Box
{
    /// <inheritdoc />
    protected override string BaseClass => "ib-spacer";
}

/// <summary>
/// Keeps its content at a fixed shape, such as 16:9 for a video or 1:1 for an avatar. Images
/// and videos inside fill it and are cropped to fit.
/// </summary>
/// <example><code>&lt;AspectRatio Ratio="16 / 9.0"&gt;&lt;img src="..." alt="..." /&gt;&lt;/AspectRatio&gt;</code></example>
public class AspectRatio : Box
{
    /// <summary>Width divided by height. Defaults to 16:9.</summary>
    [Parameter] public double Ratio { get; set; } = 16 / 9.0;

    /// <inheritdoc />
    protected override string BaseClass => "ib-ratio";

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        if (!(Ratio > 0) || double.IsInfinity(Ratio))
        {
            throw new ArgumentOutOfRangeException(nameof(Ratio), Ratio, "The ratio must be a positive number, such as 16 / 9.0.");
        }

        base.BuildAttributes(classes, styles);
        styles.AddVar("ratio", Ratio.ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture));
    }
}
