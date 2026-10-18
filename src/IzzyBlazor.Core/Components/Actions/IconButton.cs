using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace IzzyBlazor;

/// <summary>
/// A square button showing one icon. <see cref="Label"/> is required: it becomes the
/// accessible name and the hover tooltip, so an icon-only button is never unlabeled.
/// </summary>
/// <example>
/// <code>
/// &lt;IconButton Icon="Lucide.Pencil" Label="Edit" Variant="ButtonVariant.Ghost" Accent="Accent.Neutral" /&gt;
/// &lt;IconButton Icon="Lucide.ExternalLink" Label="Open the docs" Href="https://example.com/docs" /&gt;
/// </code>
/// </example>
public class IconButton : ButtonBase
{
    /// <summary>The icon.</summary>
    [Parameter, EditorRequired] public IconData Icon { get; set; }

    /// <summary>What the button does, for screen readers and the tooltip. Required.</summary>
    [Parameter, EditorRequired] public string Label { get; set; } = "";

    /// <summary>Renders a link to this URL instead of a button.</summary>
    [Parameter] public string? Href { get; set; }

    /// <summary>Submits the form it's in, when it's a button (not a link), like <c>type="submit"</c>.</summary>
    [Parameter] public bool Submit { get; set; }

    /// <summary>Resets the form it's in, when it's a button, like <c>type="reset"</c>. Can't be combined with <see cref="Submit"/>.</summary>
    [Parameter] public bool Reset { get; set; }

    /// <summary>Runs on click on interactive pages.</summary>
    [Parameter] public EventCallback<MouseEventArgs> OnClick { get; set; }

    /// <inheritdoc />
    protected override bool IconOnly => true;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (string.IsNullOrWhiteSpace(Label))
        {
            throw new InvalidOperationException("IconButton needs a Label: it is the button's accessible name.");
        }

        if (string.IsNullOrEmpty(Icon.Body))
        {
            throw new InvalidOperationException("IconButton needs an Icon, such as Icon=\"Lucide.Pencil\".");
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cssClass, style) = BuildRootAttributes();
        var isLink = Href is not null;

        builder.OpenElement(0, isLink ? "a" : "button");
        if (isLink)
        {
            // Like LinkButton: an inactive link has no href, so it keeps role="link" for its name.
            var inactive = Disabled || Loading;
            builder.AddAttribute(1, "href", inactive ? null : Href);
            builder.AddAttribute(2, "aria-disabled", inactive ? "true" : null);
            builder.AddAttribute(15, "role", inactive ? "link" : null);
        }
        else
        {
            builder.AddAttribute(3, "type", Loading ? "button" : ButtonTypes.Of(Submit, Reset, nameof(IconButton)));
            builder.AddAttribute(4, "disabled", Disabled);
            if (OnClick.HasDelegate && !Loading)
            {
                builder.AddAttribute(5, "onclick", OnClick);
            }
        }

        builder.AddAttribute(6, "aria-label", Label);
        builder.AddAttribute(7, "title", Label);
        if (Loading)
        {
            builder.AddAttribute(8, "aria-busy", "true");
        }

        builder.AddMultipleAttributes(9, AdditionalAttributes);
        builder.AddAttribute(10, "class", cssClass);
        builder.AddAttribute(11, "style", style);

        builder.OpenComponent<IzzyBlazor.Icon>(12);
        builder.AddComponentParameter(13, nameof(IzzyBlazor.Icon.Name), (IconData?)(Loading ? IconSet.Current.Get(IconRole.Loading) : Icon));
        builder.AddComponentParameter(14, nameof(IzzyBlazor.Icon.Spin), Loading);
        builder.CloseComponent();
        builder.CloseElement();
    }
}
