using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>How a <see cref="Anchor"/> is underlined.</summary>
public enum AnchorUnderline
{
    /// <summary>Always underlined (the default): clearest in running text.</summary>
    Always,

    /// <summary>Underlined on hover, for links whose color and place already say "link".</summary>
    Hover,

    /// <summary>Never underlined.</summary>
    None,
}

/// <summary>
/// A text link in the accent color. <see cref="External"/> opens it in a new tab, with an icon
/// and a note for screen readers.
/// </summary>
/// <example>
/// <code>
/// Read the &lt;Anchor Href="/docs"&gt;docs&lt;/Anchor&gt;.
/// &lt;Anchor Href="https://github.com/..." External&gt;GitHub&lt;/Anchor&gt;
/// </code>
/// </example>
public class Anchor : LayoutItemBase
{
    /// <summary>Where it goes.</summary>
    [Parameter, EditorRequired] public string Href { get; set; } = "";

    /// <summary>Opens in a new tab (<c>target="_blank" rel="noopener noreferrer"</c>), with an icon after the text.</summary>
    [Parameter] public bool External { get; set; }

    /// <summary>
    /// Leaves out an external link's icon, for repeated links where it would be noise (a list of
    /// lessons, say). Screen readers still hear <see cref="NewTabLabel"/>.
    /// </summary>
    [Parameter] public bool HideExternalIcon { get; set; }

    /// <summary>When it's underlined. Defaults to <see cref="AnchorUnderline.Always"/>.</summary>
    [Parameter] public AnchorUnderline Underline { get; set; } = AnchorUnderline.Always;

    /// <summary>Uses the surrounding text color instead of the accent, still underlined, for links in muted text or on colored backgrounds.</summary>
    [Parameter] public bool Inherit { get; set; }

    /// <summary>Read to screen readers after an external link's text. Defaults to "(opens in a new tab)".</summary>
    [Parameter] public string? NewTabLabel { get; set; }

    /// <summary>The link text.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add("ib-anchor");
        classes.Add("ib-anchor-hover", Underline == AnchorUnderline.Hover);
        classes.Add("ib-anchor-plain", Underline == AnchorUnderline.None);
        classes.Add("ib-anchor-inherit", Inherit);
        base.BuildAttributes(classes, styles);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cssClass, style) = BuildRootAttributes();
        builder.OpenElement(0, "a");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "href", Href);
        builder.AddAttribute(3, "class", cssClass);
        builder.AddAttribute(4, "style", style);
        if (External)
        {
            builder.AddAttribute(5, "target", "_blank");
            builder.AddAttribute(6, "rel", "noopener noreferrer");
        }

        builder.AddContent(7, ChildContent);
        if (External)
        {
            if (!HideExternalIcon)
            {
                builder.OpenComponent<Icon>(8);
                builder.AddComponentParameter(9, nameof(Icon.Role), IconRole.ExternalLink);
                builder.AddComponentParameter(10, nameof(Icon.Class), "ib-anchor-icon");
                builder.CloseComponent();
            }

            builder.OpenElement(11, "span");
            builder.AddAttribute(12, "class", "ib-visually-hidden");
            builder.AddContent(13, $" {NewTabLabel ?? TextDefaults.OpensInNewTab}");
            builder.CloseElement();
        }

        builder.CloseElement();
    }
}
