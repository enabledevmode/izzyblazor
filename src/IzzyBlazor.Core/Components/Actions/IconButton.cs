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
/// <accessibility>
/// <list type="bullet">
/// <item>It renders a native <c>button</c>, or an <c>a</c> with <see cref="Href"/>, so Enter, Space and focus work as the browser defines them.</item>
/// <item><see cref="Label"/> is required and becomes its <c>aria-label</c>; the icon itself is hidden from screen readers.</item>
/// <item>The tooltip is the native <c>title</c>, so it shows on mouse hover, not on keyboard focus. Inside a <see cref="Tooltip"/> it has none, and the Tooltip shows on hover and focus.</item>
/// <item>A loading button (not a link) keeps focus and sets <c>aria-busy</c> and <c>aria-disabled</c>; screen readers hear <see cref="ButtonBase.LoadingLabel"/> before the label ("Loading Delete").</item>
/// <item>A disabled or loading link loses its <c>href</c>, keeps <c>role="link"</c> and sets <c>aria-disabled</c>.</item>
/// </list>
/// </accessibility>
/// <seealso cref="Button"/>
/// <seealso cref="ButtonGroup"/>
/// <seealso cref="Tooltip"/>
/// <seealso cref="CopyButton"/>
[ModeSupport]
public class IconButton : ButtonBase
{
    /// <summary>The icon.</summary>
    [Parameter, EditorRequired] public IconData Icon { get; set; }

    /// <summary>What the button does, for screen readers and the tooltip. Required.</summary>
    [Parameter, EditorRequired] public string Label { get; set; } = "";

    [CascadingParameter] private TooltipScope? InTooltip { get; set; }

    /// <summary>Renders a link to this URL instead of a button.</summary>
    [Parameter] public string? Href { get; set; }

    /// <summary>Submits the form it's in (or <see cref="Form"/>), when it's a button (not a link), like <c>type="submit"</c>.</summary>
    [Parameter] public bool Submit { get; set; }

    /// <summary>Resets the form it's in, when it's a button, like <c>type="reset"</c>. Can't be combined with <see cref="Submit"/>.</summary>
    [Parameter] public bool Reset { get; set; }

    /// <summary>
    /// Posted with the form as <c>Name=Value</c> when this button submits it, so one handler can tell
    /// which button was pressed, such as which row's Delete. Not <c>action</c>, <c>method</c> or
    /// <c>target</c>: a control with one of those names hides the form's own property, and Blazor's
    /// enhanced form handling then posts to the wrong address.
    /// </summary>
    [Parameter] public string? Name { get; set; }

    /// <summary>The value posted with <see cref="Name"/>.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Submits to this URL instead of the form's own action (native <c>formaction</c>).</summary>
    [Parameter] public string? FormAction { get; set; }

    /// <summary>The id of a form elsewhere on the page that this button submits (native <c>form</c>).</summary>
    [Parameter] public string? Form { get; set; }

    /// <summary>Runs on click on interactive pages (a link still navigates).</summary>
    [Parameter, ModeSupport(Off = ModeLevel.None, Static = ModeLevel.None)]
    public EventCallback<MouseEventArgs> OnClick { get; set; }

    /// <inheritdoc />
    protected override bool IconOnly => true;

    /// <inheritdoc />
    private protected override string? LabelText => Label;

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
    protected override void BuildRenderTree(RenderTreeBuilder builder) => RenderButton(builder);

    private void RenderButton(RenderTreeBuilder builder)
    {
        var (cssClass, style) = BuildRootAttributes();
        var isLink = Href is not null;

        builder.OpenElement(0, isLink ? "a" : "button");
        if (isLink)
        {
            if (Submit || Reset || Name is not null || Value is not null || FormAction is not null || Form is not null)
            {
                throw new InvalidOperationException("IconButton with Href is a link, so Submit, Reset, Name, Value, FormAction and Form don't apply. Leave out Href for a button.");
            }

            // Like Button with Href: an inactive link has no href, so it keeps role="link" for its name.
            var inactive = Disabled || Loading;
            builder.AddAttribute(1, "href", inactive ? null : Href);
            builder.AddAttribute(2, "aria-disabled", inactive ? "true" : null);
            builder.AddAttribute(15, "role", inactive ? "link" : null);
            if (OnClick.HasDelegate && !inactive)
            {
                builder.AddAttribute(16, "onclick", OnClick);
            }
        }
        else
        {
            var type = ButtonTypes.Of(Submit, Reset, nameof(IconButton));
            builder.AddAttribute(3, "type", Loading ? "button" : type);
            builder.AddAttribute(4, "disabled", Disabled);
            builder.AddAttribute(18, "name", Name);
            builder.AddAttribute(19, "value", Value);
            builder.AddAttribute(20, "formaction", FormAction);
            builder.AddAttribute(21, "form", Form);
            if (OnClick.HasDelegate && !Loading)
            {
                builder.AddAttribute(5, "onclick", OnClick);
            }
        }

        // Loading reads like Button's: "Loading Delete" (LoadingLabel, then the label).
        builder.AddAttribute(6, "aria-label", Loading ? $"{LoadingLabel ?? TextDefaults.Loading} {Label}" : Label);
        // The native tooltip, unless a Tooltip around it shows one.
        if (InTooltip is null)
        {
            builder.AddAttribute(7, "title", Label);
        }

        if (Loading)
        {
            builder.AddAttribute(8, "aria-busy", "true");
            if (!isLink)
            {
                builder.AddAttribute(17, "aria-disabled", "true");
            }
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
