using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>Vertical padding for a <see cref="Section"/>.</summary>
public enum SectionSpacing
{
    /// <summary>No padding: the content sets its own.</summary>
    None,

    /// <summary>32px.</summary>
    Sm,

    /// <summary>48px, 64px from 768px up (the default).</summary>
    Md,

    /// <summary>64px, 96px from 768px up, for a hero or a closing call to action.</summary>
    Lg,
}

/// <summary>
/// A band of a page, such as a hero, a feature row or a newsletter sign-up: a <c>section</c> with
/// vertical padding and an optional background. Inside an <see cref="AppMain"/> with a
/// <c>ContentWidth</c>, it spans the whole main area (full bleed) while its content stays on the
/// column, lined up with the rest of the page.
/// </summary>
/// <example>
/// <code>
/// &lt;Section Bg="Color.SurfaceContainerLow"&gt;
///     &lt;Heading Level="2"&gt;Get the newsletter&lt;/Heading&gt; ...
/// &lt;/Section&gt;
/// </code>
/// </example>
/// <remarks>
/// It works out the bleed from the main area and the column, so put it at the page's top level or
/// in containers without side padding of their own. It works with no script.
/// </remarks>
public class Section : Box
{
    /// <summary>Creates a section, rendered as a <c>section</c> element.</summary>
    public Section() => As = "section";

    /// <summary>Vertical padding. Defaults to <see cref="SectionSpacing.Md"/>.</summary>
    [Parameter] public SectionSpacing Spacing { get; set; } = SectionSpacing.Md;

    /// <summary>
    /// Spans the whole main area when the page has a content width. Defaults to true; set false
    /// for a band that stays on the column (rounded, for instance).
    /// </summary>
    [Parameter] public bool FullBleed { get; set; } = true;

    /// <inheritdoc />
    protected override string BaseClass => "ib-section";

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        base.BuildAttributes(classes, styles);
        classes.Add(Spacing switch
        {
            SectionSpacing.None => null,
            SectionSpacing.Sm => "ib-section-sm",
            SectionSpacing.Lg => "ib-section-lg",
            _ => "ib-section-md",
        });
        classes.Add("ib-section-bleed", FullBleed);
    }
}
