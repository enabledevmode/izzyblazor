namespace IzzyBlazor;

/// <summary>How an icon's shapes are painted.</summary>
public enum IconStyle
{
    /// <summary>Outlined shapes drawn with <c>stroke</c> (Lucide, Tabler, Heroicons outline).</summary>
    Stroke,

    /// <summary>Solid shapes drawn with <c>fill</c> (Bootstrap Icons, Fluent, Material).</summary>
    Fill,
}

/// <summary>
/// One icon from any icon set: its SVG view box and inner markup. The neutral format every
/// set converts to, so <see cref="Icon"/> renders them all the same way.
/// </summary>
/// <param name="ViewBox">The SVG <c>viewBox</c>, such as <c>0 0 24 24</c>.</param>
/// <param name="Body">
/// The SVG's inner markup: paths, circles, rects, groups, gradients. <see cref="Icon"/> writes it into
/// the page as markup, so it's checked first: anything that can run script or load something (script,
/// foreignObject, use, image, links, animation, event handlers, href) throws. Don't build icons from
/// user input anyway.
/// </param>
/// <param name="Style">Whether the shapes are stroked or filled.</param>
/// <param name="StrokeWidth">Default stroke width for <see cref="IconStyle.Stroke"/> icons.</param>
/// <param name="Name">The icon's name in its set, such as <c>arrow-right</c>, for debugging and lookup.</param>
public readonly record struct IconData(string ViewBox, string Body, IconStyle Style, double StrokeWidth = 2, string Name = "");

/// <summary>Icon sizes, relative to the surrounding text so icons line up with labels.</summary>
public enum IconSize
{
    /// <summary>0.75em.</summary>
    Xs,

    /// <summary>0.875em.</summary>
    Sm,

    /// <summary>1em, the size of the surrounding text (the default).</summary>
    Md,

    /// <summary>1.25em.</summary>
    Lg,

    /// <summary>1.5em.</summary>
    Xl,

    /// <summary>2em.</summary>
    Xl2,

    /// <summary>3em.</summary>
    Xl3,
}
