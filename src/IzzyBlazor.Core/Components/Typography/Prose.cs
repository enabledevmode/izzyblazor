using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>Text size for <see cref="Prose"/>.</summary>
public enum ProseSize
{
    /// <summary>14px text, for asides and dense pages.</summary>
    Sm,

    /// <summary>16px text (the default).</summary>
    Md,

    /// <summary>18px text, for long reading.</summary>
    Lg,
}

/// <summary>
/// Styles plain HTML inside it, such as Markdown output or a CMS field: headings, paragraphs,
/// links, lists, quotes, code, tables, images and rules, in the library's type and colors.
/// </summary>
/// <example>
/// <code>
/// &lt;Prose&gt;@((MarkupString)html)&lt;/Prose&gt;
/// </code>
/// </example>
/// <remarks>
/// The rules have the lowest weight (<c>:where()</c>), so library components inside keep their
/// own look, and your own classes win. Only render HTML you trust or have sanitized.
/// </remarks>
[ModeSupport]
public class Prose : Box
{
    /// <summary>Text size. Defaults to <see cref="ProseSize.Md"/>.</summary>
    [Parameter] public ProseSize Size { get; set; } = ProseSize.Md;

    /// <inheritdoc />
    protected override string BaseClass => "ib-prose";

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        base.BuildAttributes(classes, styles);
        classes.Add(Size switch
        {
            ProseSize.Sm => "ib-prose-sm",
            ProseSize.Lg => "ib-prose-lg",
            _ => null,
        });
    }
}
