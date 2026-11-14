using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// Text screen readers read and the screen doesn't show, such as "per month" for a price shown as
/// "$29 / month", or a word that finishes a sentence an icon starts. For a control's name, prefer
/// the control's own Label.
/// </summary>
/// <example>
/// <code>
/// &lt;TextBlock&gt;
///     $29&lt;span aria-hidden="true"&gt; / month&lt;/span&gt;
///     &lt;VisuallyHidden&gt; per month&lt;/VisuallyHidden&gt;
/// &lt;/TextBlock&gt;
/// </code>
/// </example>
/// <accessibility>
/// <list type="bullet">
/// <item>It renders a <c>span</c> (or the element <see cref="As"/> names) clipped to one pixel, so screen readers, search and translation tools read it.</item>
/// <item>With <see cref="Focusable"/>, it shows when it or anything in it has focus, as a skip link does. AppShell has its own skip link.</item>
/// </list>
/// </accessibility>
/// <seealso cref="TextBlock"/>
/// <seealso cref="Heading"/>
[ModeSupport]
public class VisuallyHidden : IzzyComponentBase
{
    /// <summary>The element: <c>span</c> by default, so it fits inside text; <c>div</c> or <c>h2</c> for a block or a hidden heading.</summary>
    [Parameter] public string As { get; set; } = "span";

    /// <summary>Shows it while it or anything in it has focus, such as a skip link.</summary>
    [Parameter] public bool Focusable { get; set; }

    /// <summary>The text, or the elements, to hide.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles) =>
        classes.Add(Focusable ? "ib-visually-hidden-focusable" : "ib-visually-hidden");

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
