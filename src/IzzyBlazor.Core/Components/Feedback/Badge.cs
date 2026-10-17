using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>A badge's visual weight.</summary>
public enum BadgeVariant
{
    /// <summary>A light tint of the color (the default).</summary>
    Soft,

    /// <summary>Filled with the color.</summary>
    Solid,

    /// <summary>A colored border.</summary>
    Outline,
}

/// <summary>Badge sizes.</summary>
public enum BadgeSize
{
    /// <summary>Compact, for counts in navigation.</summary>
    Sm,

    /// <summary>The default.</summary>
    Md,
}

/// <summary>
/// A small label for a status, category or count.
/// </summary>
/// <example>
/// <code>
/// &lt;Badge Status="Status.Success"&gt;Published&lt;/Badge&gt;
/// &lt;Badge Status="Status.Danger" Variant="BadgeVariant.Solid" Pill&gt;3&lt;/Badge&gt;
/// &lt;Badge Status="Status.Warning" Dot&gt;Pending review&lt;/Badge&gt;
/// &lt;Badge Accent="Accent.Primary" Variant="BadgeVariant.Solid" Pill&gt;12&lt;/Badge&gt;
/// </code>
/// </example>
public class Badge : LayoutItemBase
{
    /// <summary>What the badge means. Defaults to <see cref="IzzyBlazor.Status.Neutral"/>.</summary>
    [Parameter] public Status Status { get; set; } = Status.Neutral;

    /// <summary>
    /// A color in place of <see cref="Status"/>'s, such as <c>Accent.Primary</c> for a
    /// brand-colored count, <c>Accent.Of("#0d9488")</c> or <c>Accent.Token("brand")</c>.
    /// </summary>
    [Parameter] public Accent? Accent { get; set; }

    /// <summary>Visual weight. Defaults to <see cref="BadgeVariant.Soft"/>.</summary>
    [Parameter] public BadgeVariant Variant { get; set; } = BadgeVariant.Soft;

    /// <summary>Size. Defaults to <see cref="BadgeSize.Md"/>.</summary>
    [Parameter] public BadgeSize Size { get; set; } = BadgeSize.Md;

    /// <summary>Fully rounded ends, for counts.</summary>
    [Parameter] public bool Pill { get; set; }

    /// <summary>A colored dot before the text, for statuses.</summary>
    [Parameter] public bool Dot { get; set; }

    /// <summary>An icon before the text.</summary>
    [Parameter] public IconData? StartIcon { get; set; }

    /// <summary>The text.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add("ib-badge");
        classes.Add($"ib-badge-{Status.ToString().ToLowerInvariant()}");
        classes.Add($"ib-badge-{Variant.ToString().ToLowerInvariant()}");
        classes.Add("ib-badge-sm", Size == BadgeSize.Sm);
        classes.Add("ib-badge-pill", Pill);
        if (Accent is { } accent)
        {
            ColorOverride.Apply(styles, accent.ToColor());
        }

        base.BuildAttributes(classes, styles);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cssClass, style) = BuildRootAttributes();
        builder.OpenElement(0, "span");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "class", cssClass);
        builder.AddAttribute(3, "style", style);
        if (Dot)
        {
            builder.OpenElement(4, "span");
            builder.AddAttribute(5, "class", "ib-badge-dot");
            builder.AddAttribute(6, "aria-hidden", "true");
            builder.CloseElement();
        }
        else if (StartIcon is { } icon)
        {
            builder.OpenComponent<Icon>(7);
            builder.AddComponentParameter(8, nameof(Icon.Name), (IconData?)icon);
            builder.CloseComponent();
        }

        if (ChildContent is not null)
        {
            builder.OpenElement(9, "span");
            builder.AddAttribute(10, "class", "ib-badge-label");
            builder.AddContent(11, ChildContent);
            builder.CloseElement();
        }

        builder.CloseElement();
    }
}
