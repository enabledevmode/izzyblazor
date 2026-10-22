using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// The base surface. Renders one element with the style props applied; every
/// other layout primitive is a Box with defaults.
/// </summary>
/// <example>
/// <code>
/// &lt;Box As="section" Padding="6 md:10" Bg="surface-container" Radius="md"&gt;...&lt;/Box&gt;
/// </code>
/// </example>
/// <accessibility>
/// <list type="bullet">
/// <item>It renders a <c>div</c>; <see cref="As"/> makes it another element, such as <c>nav</c> or <c>ul</c>, so a landmark or list needs no extra wrapper.</item>
/// <item>Attributes it doesn't take as parameters, such as <c>role</c> or <c>aria-label</c>, go on the element.</item>
/// </list>
/// </accessibility>
/// <seealso cref="Stack"/>
/// <seealso cref="Grid"/>
/// <seealso cref="Section"/>
/// <seealso cref="Container"/>
[ModeSupport]
public class Box : StyledComponentBase
{
    /// <summary>The element to render: <c>div</c> on a Box; components built on it set their own (List a <c>ul</c>, Section a <c>section</c>).</summary>
    [Parameter] public string As { get; set; } = "div";

    /// <summary>The content.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>The component's base class, such as <c>ib-box</c>.</summary>
    protected virtual string BaseClass => "ib-box";

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add(BaseClass);
        base.BuildAttributes(classes, styles);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cssClass, style) = BuildRootAttributes();
        builder.OpenElement(0, ValidateTag(As));
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "class", cssClass);
        builder.AddAttribute(3, "style", style);
        builder.AddContent(4, ChildContent);
        builder.CloseElement();
    }
}
