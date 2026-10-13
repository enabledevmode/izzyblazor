using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>A tag's visual weight.</summary>
public enum TagVariant
{
    /// <summary>A light tint of the color (the default).</summary>
    Soft,

    /// <summary>Filled with the color.</summary>
    Solid,

    /// <summary>A colored border.</summary>
    Outline,
}

/// <summary>
/// A keyword, category or applied filter, larger than a <see cref="Badge"/> and optionally a link
/// or removable. Removing is a link on static pages (<see cref="RemoveHref"/>, such as the current
/// URL without that filter) or C# on interactive ones (<see cref="OnRemove"/>).
/// </summary>
/// <example>
/// <code>
/// &lt;Tag Label="Blazor" Href="/topics/blazor" /&gt;
/// &lt;Tag Label="Status: Paid" Status="Status.Success" RemoveHref="/invoices" OnRemove="ClearStatus" /&gt;
/// </code>
/// </example>
public class Tag : IzzyComponentBase
{
    /// <summary>The text. Also used in the remove button's name.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Custom content in place of <see cref="Label"/>.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>The color's meaning. Defaults to <see cref="IzzyBlazor.Status.Neutral"/>.</summary>
    [Parameter] public Status Status { get; set; } = Status.Neutral;

    /// <summary>A custom color in place of <see cref="Status"/>, such as <c>Color.Primary</c>.</summary>
    [Parameter] public Color? Color { get; set; }

    /// <summary>Visual weight. Defaults to <see cref="TagVariant.Soft"/>.</summary>
    [Parameter] public TagVariant Variant { get; set; } = TagVariant.Soft;

    /// <summary>Size: 20, 24 (the default) or 28px tall.</summary>
    [Parameter] public ControlSize Size { get; set; } = ControlSize.Md;

    /// <summary>An icon before the text.</summary>
    [Parameter] public IconData? StartIcon { get; set; }

    /// <summary>Makes the tag's text a link.</summary>
    [Parameter] public string? Href { get; set; }

    /// <summary>
    /// Adds a remove link to this address, such as the current page without this filter. Used on
    /// static pages, and on interactive ones when there's no <see cref="OnRemove"/>.
    /// </summary>
    [Parameter] public string? RemoveHref { get; set; }

    /// <summary>
    /// Adds a remove button that runs this on interactive pages. Static and prerendered pages need
    /// <see cref="RemoveHref"/> to show a remove control.
    /// </summary>
    [Parameter] public EventCallback OnRemove { get; set; }

    /// <summary>
    /// The remove control's name, before the label: "Remove Blazor". Defaults to "Remove". With only
    /// <see cref="ChildContent"/> and no <see cref="Label"/>, the name is just this, so give tags in a
    /// list a <see cref="Label"/> too.
    /// </summary>
    [Parameter] public string? RemoveLabel { get; set; }

    private bool RemoveButton => OnRemove.HasDelegate && RendererInfo.IsInteractive;

    private string RemoveName => string.IsNullOrWhiteSpace(Label) ? RemoveLabel ?? TextDefaults.Remove : $"{RemoveLabel ?? TextDefaults.Remove} {Label}";

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add("ib-tag");
        classes.Add($"ib-tag-{Status.ToString().ToLowerInvariant()}");
        classes.Add($"ib-tag-{Variant.ToString().ToLowerInvariant()}");
        classes.Add("ib-tag-sm", Size == ControlSize.Sm);
        classes.Add("ib-tag-lg", Size == ControlSize.Lg);
        classes.Add("ib-tag-removable", RemoveButton || RemoveHref is not null);
        if (Color is { } custom)
        {
            ColorOverride.Apply(styles, custom);
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cssClass, style) = BuildRootAttributes();
        builder.OpenElement(0, "span");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "class", cssClass);
        builder.AddAttribute(3, "style", style);

        if (Href is not null)
        {
            builder.OpenElement(4, "a");
            builder.AddAttribute(5, "class", "ib-tag-link");
            builder.AddAttribute(6, "href", Href);
            Content(builder);
            builder.CloseElement();
        }
        else
        {
            Content(builder);
        }

        if (RemoveButton)
        {
            builder.OpenElement(20, "button");
            builder.AddAttribute(21, "type", "button");
            builder.AddAttribute(22, "class", "ib-tag-remove");
            builder.AddAttribute(23, "aria-label", RemoveName);
            builder.AddAttribute(24, "onclick", OnRemove);
            RemoveIcon(builder);
            builder.CloseElement();
        }
        else if (RemoveHref is not null)
        {
            builder.OpenElement(30, "a");
            builder.AddAttribute(31, "class", "ib-tag-remove");
            builder.AddAttribute(32, "href", RemoveHref);
            builder.AddAttribute(33, "aria-label", RemoveName);
            RemoveIcon(builder);
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    private void Content(RenderTreeBuilder builder)
    {
        if (StartIcon is { } icon)
        {
            builder.OpenComponent<Icon>(40);
            builder.AddComponentParameter(41, nameof(Icon.Name), (IconData?)icon);
            builder.CloseComponent();
        }

        builder.OpenElement(42, "span");
        builder.AddAttribute(43, "class", "ib-tag-label");
        if (ChildContent is not null)
        {
            builder.AddContent(44, ChildContent);
        }
        else
        {
            builder.AddContent(45, Label);
        }

        builder.CloseElement();
    }

    private static void RemoveIcon(RenderTreeBuilder builder)
    {
        builder.OpenComponent<Icon>(50);
        builder.AddComponentParameter(51, nameof(Icon.Role), (IconRole?)IconRole.Close);
        builder.CloseComponent();
    }
}
