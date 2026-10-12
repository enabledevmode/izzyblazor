using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// A shimmering placeholder in the shape of content that's still loading: lines of text, a
/// block (an image, a card) or a circle (an avatar). Hidden from screen readers; announce the
/// loading with a <see cref="Spinner"/> or the region's <c>aria-busy</c>.
/// </summary>
/// <example>
/// <code>
/// &lt;Skeleton Lines="3" /&gt;
/// &lt;Skeleton Circle Width="Length.Rem(3)" /&gt;
/// &lt;Skeleton Height="Length.Rem(10)" /&gt;
/// &lt;Skeleton Loaded="_order is not null" Lines="2"&gt;@_order.Summary&lt;/Skeleton&gt;
/// </code>
/// </example>
public class Skeleton : StyledComponentBase
{
    /// <summary>Lines of text to stand in for. The last of several is shorter. Defaults to 1.</summary>
    [Parameter] public int Lines { get; set; } = 1;

    /// <summary>A block of this height instead of text lines, such as an image or a card.</summary>
    [Parameter] public Length? Height { get; set; }

    /// <summary>A circle, such as an avatar. Its size is <c>Width</c> (2.5rem by default).</summary>
    [Parameter] public bool Circle { get; set; }

    /// <summary>When true, shows <see cref="ChildContent"/> in place of the placeholder.</summary>
    [Parameter] public bool Loaded { get; set; }

    /// <summary>The real content, shown once <see cref="Loaded"/>.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private string BaseClass => Circle ? "ib-skeleton ib-skeleton-circle" : Height is not null ? "ib-skeleton ib-skeleton-block" : "ib-skeleton-text";

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add(BaseClass);
        base.BuildAttributes(classes, styles);
        if (Height is { } height && !Circle)
        {
            styles.AddVar("skeleton-h", StyleValues.Length(height));
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (Loaded)
        {
            builder.AddContent(0, ChildContent);
            return;
        }

        var (cls, style) = BuildRootAttributes();
        builder.OpenElement(1, "div");
        builder.AddMultipleAttributes(2, AdditionalAttributes);
        builder.AddAttribute(3, "class", cls);
        builder.AddAttribute(4, "style", style);
        builder.AddAttribute(5, "aria-hidden", "true");
        if (!Circle && Height is null)
        {
            for (var i = 0; i < Math.Max(1, Lines); i++)
            {
                builder.OpenElement(6, "span");
                builder.AddAttribute(7, "class", "ib-skeleton ib-skeleton-line");
                builder.CloseElement();
            }
        }

        builder.CloseElement();
    }
}
