using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// A line between pieces of content: an <c>hr</c>, a vertical separator in a <see cref="Row"/>,
/// or a line with a label in the middle ("or").
/// </summary>
/// <example>
/// <code>
/// &lt;Divider /&gt;
/// &lt;Divider Label="or" /&gt;
/// &lt;Row&gt;...&lt;Divider Orientation="Orientation.Vertical" /&gt;...&lt;/Row&gt;
/// </code>
/// </example>
/// <accessibility>
/// <list type="bullet">
/// <item>A plain horizontal divider is an <c>hr</c>; a vertical one is a <c>div</c> with <c>role="separator"</c> and <c>aria-orientation="vertical"</c>.</item>
/// <item>A divider with a <see cref="Label"/> is read as its text ("or"), with no separator role, since a separator's content isn't read.</item>
/// </list>
/// </accessibility>
/// <seealso cref="Stack"/>
/// <seealso cref="Row"/>
/// <seealso cref="Spacer"/>
[ModeSupport]
public class Divider : StyledComponentBase
{
    /// <summary>Horizontal (the default) or vertical, for use in a row.</summary>
    [Parameter] public Orientation Orientation { get; set; } = Orientation.Horizontal;

    /// <summary>Text in the middle of a horizontal line, such as "or".</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>A dashed line.</summary>
    [Parameter] public bool Dashed { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add("ib-divider");
        classes.Add("ib-divider-vertical", Orientation == Orientation.Vertical);
        classes.Add("ib-divider-label", Label is not null && Orientation == Orientation.Horizontal);
        classes.Add("ib-divider-dashed", Dashed);
        base.BuildAttributes(classes, styles);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cssClass, style) = BuildRootAttributes();
        if (Orientation == Orientation.Horizontal && Label is null)
        {
            builder.OpenElement(0, "hr");
            builder.AddMultipleAttributes(1, AdditionalAttributes);
            builder.AddAttribute(2, "class", cssClass);
            builder.AddAttribute(3, "style", style);
            builder.CloseElement();
            return;
        }

        builder.OpenElement(10, "div");
        builder.AddMultipleAttributes(11, AdditionalAttributes);
        // A labeled line is its text: a separator's content isn't read, so it has no role.
        var labeled = Orientation == Orientation.Horizontal && Label is not null;
        builder.AddAttribute(12, "role", labeled ? null : "separator");
        builder.AddAttribute(13, "aria-orientation", Orientation == Orientation.Vertical ? "vertical" : null);
        builder.AddAttribute(14, "class", cssClass);
        builder.AddAttribute(15, "style", style);
        if (labeled)
        {
            builder.OpenElement(16, "span");
            builder.AddAttribute(17, "class", "ib-divider-text");
            builder.AddContent(18, Label);
            builder.CloseElement();
        }

        builder.CloseElement();
    }
}
