using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// The top of a section of a landing or marketing page: an optional eyebrow, a heading and a
/// sentence under it, centered over the section's content by default. For the top of an app
/// page, with actions at the end, use <see cref="PageHeader"/>.
/// </summary>
/// <example>
/// <code>
/// &lt;SectionHeader Title="Everything you need" Description="The parts every app needs." /&gt;
/// </code>
/// </example>
/// <accessibility>
/// <list type="bullet">
/// <item><see cref="Title"/> is a heading, an <c>h2</c> by default, so the page's outline goes h1, then a heading per section.</item>
/// <item>Give the section a name with <c>aria-labelledby</c> pointing at <see cref="TitleId"/>.</item>
/// <item>The eyebrow is a tag before the heading, read before it.</item>
/// </list>
/// </accessibility>
/// <seealso cref="Section"/>
/// <seealso cref="PageHeader"/>
/// <seealso cref="Heading"/>
[ModeSupport]
[ImportantParameters(nameof(Description), nameof(Eyebrow), nameof(Align), nameof(TitleLevel), nameof(Size))]
public class SectionHeader : StyledComponentBase
{
    /// <summary>The heading, such as "Everything you need to run your work".</summary>
    [Parameter, EditorRequired] public string Title { get; set; } = "";

    /// <summary>The heading level of <see cref="Title"/>, 1 to 6. Defaults to 2, a section under the page's h1.</summary>
    [Parameter] public int TitleLevel { get; set; } = 2;

    /// <summary>The heading's <c>id</c>, for the section's <c>aria-labelledby</c>.</summary>
    [Parameter] public string? TitleId { get; set; }

    /// <summary>A muted sentence under the heading: what the section is about.</summary>
    [Parameter] public string? Description { get; set; }

    /// <summary>A short pill above the heading in the accent color, such as "Pricing". Leave it out on most sections: a pill on every one is noise.</summary>
    [Parameter] public string? Eyebrow { get; set; }

    /// <summary>
    /// Where the text sits, per breakpoint. Defaults to <see cref="TextAlign.Center"/>. Over
    /// content that starts at the edge on phones, such as a list of features, use
    /// <c>new() { Base = TextAlign.Start, Md = TextAlign.Center }</c>.
    /// </summary>
    [Parameter] public Responsive<TextAlign> Align { get; set; } = TextAlign.Center;

    /// <summary>
    /// The heading and sentence size: <see cref="ControlSize.Sm"/> (20px and 16px) for a section
    /// in a card, <see cref="ControlSize.Md"/> (36px and 18px, 30px and 16px on phones; the
    /// default), <see cref="ControlSize.Lg"/> (48px, 36px on phones) for a page made of one section.
    /// </summary>
    [Parameter] public ControlSize Size { get; set; } = ControlSize.Md;

    /// <summary>Anything under the sentence, such as a link or buttons.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add("ib-section-header");
        var (title, text) = Size switch
        {
            ControlSize.Sm => (StyleValues.TextSize(TextSize.Xl), StyleValues.TextSize(TextSize.Md)),
            ControlSize.Lg => (StyleValues.FluidTextSize(TextSize.Xl4, TextSize.Xl5), StyleValues.FluidTextSize(TextSize.Md, TextSize.Lg)),
            _ => (StyleValues.FluidTextSize(TextSize.Xl3, TextSize.Xl4), StyleValues.FluidTextSize(TextSize.Md, TextSize.Lg)),
        };
        styles.AddVar("sh-title", title);
        styles.AddVar("sh-text", text);
        ApplyResponsive(classes, styles, Align, "ib-ta", "ta", StyleValues.TextAlign);
        ApplyResponsive(classes, styles, Align.Select<Align>(a => a switch
        {
            TextAlign.Center => IzzyBlazor.Align.Center,
            TextAlign.End => IzzyBlazor.Align.End,
            _ => IzzyBlazor.Align.Start,
        }), "ib-align", "align", StyleValues.Align);
        base.BuildAttributes(classes, styles);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cssClass, style) = BuildRootAttributes();
        builder.OpenElement(0, "div");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "class", cssClass);
        builder.AddAttribute(3, "style", style);
        if (!string.IsNullOrEmpty(Eyebrow))
        {
            builder.OpenComponent<Tag>(4);
            builder.AddComponentParameter(5, nameof(Tag.Label), Eyebrow);
            builder.AddComponentParameter(6, nameof(Tag.Accent), (Accent?)Accent.Primary);
            builder.AddComponentParameter(7, nameof(Tag.Pill), true);
            builder.AddComponentParameter(8, nameof(Tag.MarginBottom), (Responsive<int>)1);
            builder.CloseComponent();
        }
        builder.OpenElement(17, $"h{Math.Clamp(TitleLevel, 1, 6)}");
        builder.AddAttribute(18, "class", "ib-section-header-title");
        builder.AddAttribute(19, "id", TitleId);
        builder.AddContent(20, Title);
        builder.CloseElement();
        if (!string.IsNullOrEmpty(Description))
        {
            builder.OpenElement(21, "p");
            builder.AddAttribute(22, "class", "ib-section-header-description");
            builder.AddContent(23, Description);
            builder.CloseElement();
        }
        builder.AddContent(24, ChildContent);
        builder.CloseElement();
    }
}
