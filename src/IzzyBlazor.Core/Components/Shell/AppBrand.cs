using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// The app or site name in an <see cref="AppHeader"/> (or a sidebar's header), with an icon or
/// logo before it, usually linking home. It stays on one line: on a narrow screen the name is cut
/// short with an ellipsis rather than wrapping or pushing the header's buttons off.
/// </summary>
/// <example>
/// <code>
/// &lt;AppHeader&gt;
///     &lt;SidebarToggle /&gt;
///     &lt;AppBrand Href="/" Icon="Tabler.Bolt" Label="Enable Dev Mode" /&gt;
///     &lt;Box Grow /&gt;
///     ...
/// &lt;/AppHeader&gt;
/// </code>
/// </example>
public class AppBrand : IzzyComponentBase
{
    /// <summary>The name, shown and used as the link's name.</summary>
    [Parameter, EditorRequired] public string Label { get; set; } = "";

    /// <summary>Where it links, usually <c>"/"</c>. Unset: plain text, not a link.</summary>
    [Parameter] public string? Href { get; set; }

    /// <summary>An icon before the name, in the accent color.</summary>
    [Parameter] public IconData? Icon { get; set; }

    /// <summary>A logo before the name, such as an <c>img</c> or inline SVG; takes the place of <see cref="Icon"/>.</summary>
    [Parameter] public RenderFragment? Logo { get; set; }

    /// <summary>
    /// Shows only the icon or logo, keeping the name for screen readers: for a logo that already
    /// spells the name.
    /// </summary>
    [Parameter] public bool HideLabel { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles) =>
        classes.Add("ib-shell-brand");

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cssClass, style) = BuildRootAttributes();
        builder.OpenElement(0, Href is null ? "span" : "a");
        builder.AddAttribute(1, "href", Href);
        builder.AddMultipleAttributes(2, AdditionalAttributes);
        builder.AddAttribute(3, "class", cssClass);
        builder.AddAttribute(4, "style", style);

        if (Logo is not null)
        {
            builder.OpenElement(5, "span");
            builder.AddAttribute(6, "class", "ib-shell-brand-logo");
            builder.AddAttribute(7, "aria-hidden", "true");
            builder.AddContent(8, Logo);
            builder.CloseElement();
        }
        else if (Icon is not null)
        {
            builder.OpenComponent<Icon>(9);
            builder.AddComponentParameter(10, nameof(IzzyBlazor.Icon.Name), Icon);
            builder.AddComponentParameter(11, nameof(IzzyBlazor.Icon.Size), IconSize.Lg);
            builder.AddComponentParameter(12, nameof(IzzyBlazor.Icon.Class), "ib-shell-brand-icon");
            builder.CloseComponent();
        }

        builder.OpenElement(13, "span");
        builder.AddAttribute(14, "class", HideLabel ? "ib-visually-hidden" : "ib-shell-brand-label");
        builder.AddContent(15, Label);
        builder.CloseElement();

        builder.CloseElement();
    }
}
