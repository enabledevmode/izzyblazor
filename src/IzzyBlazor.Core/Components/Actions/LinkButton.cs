using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// A link (<c>a</c>) that looks like a button. Use it for navigation, such as "Start the
/// project"; use <see cref="Button"/> for actions.
/// </summary>
/// <example>
/// <code>
/// &lt;LinkButton Href="/courses/wizard" EndIcon="Hugeicons.ArrowRight02"&gt;Start the project&lt;/LinkButton&gt;
/// &lt;LinkButton Href="https://github.com/..." External Variant="ButtonVariant.Outline"&gt;Get the repo&lt;/LinkButton&gt;
/// </code>
/// </example>
public class LinkButton : ButtonBase
{
    /// <summary>The destination.</summary>
    [Parameter, EditorRequired] public string Href { get; set; } = "";

    /// <summary>Opens in a new tab with <c>rel="noopener noreferrer"</c> and shows an external-link icon.</summary>
    [Parameter] public bool External { get; set; }

    /// <summary>
    /// Leaves out an external link's icon, for repeated links where it would be noise. Screen
    /// readers still hear <see cref="NewTabLabel"/>.
    /// </summary>
    [Parameter] public bool HideExternalIcon { get; set; }

    /// <summary>Read to screen readers after an external link's text. Defaults to "(opens in a new tab)".</summary>
    [Parameter] public string? NewTabLabel { get; set; }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cssClass, style) = BuildRootAttributes();
        var inactive = Disabled || Loading;

        builder.OpenElement(0, "a");
        if (inactive)
        {
            // A link with no href cannot be followed or focused by Tab.
            builder.AddAttribute(1, "role", "link");
            builder.AddAttribute(2, "aria-disabled", "true");
        }
        else
        {
            builder.AddAttribute(3, "href", Href);
        }

        if (Loading)
        {
            builder.AddAttribute(4, "aria-busy", "true");
        }

        if (External)
        {
            builder.AddAttribute(5, "target", "_blank");
            builder.AddAttribute(6, "rel", "noopener noreferrer");
        }

        builder.AddMultipleAttributes(7, AdditionalAttributes);
        builder.AddAttribute(8, "class", cssClass);
        builder.AddAttribute(9, "style", style);
        builder.OpenRegion(10);
        RenderContent(builder);
        builder.CloseRegion();

        if (External)
        {
            if (EndIcon is null && !HideExternalIcon)
            {
                builder.OpenComponent<Icon>(11);
                builder.AddComponentParameter(12, nameof(Icon.Role), (IconRole?)IconRole.ExternalLink);
                builder.AddComponentParameter(13, nameof(Icon.Size), IconSize.Sm);
                builder.CloseComponent();
            }

            builder.OpenElement(14, "span");
            builder.AddAttribute(15, "class", "ib-visually-hidden");
            builder.AddContent(16, $" {NewTabLabel ?? TextDefaults.OpensInNewTab}");
            builder.CloseElement();
        }

        builder.CloseElement();
    }
}
