using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// A screenshot or preview shown inside a browser window: a top bar with three dots and an
/// optional address, a border and rounded corners, as in a landing page's hero.
/// </summary>
/// <example>
/// <code>
/// &lt;BrowserFrame Address="app.example.com/orders" Shadow="Shadow.Lg"&gt;
///     &lt;Image Src="img/orders.png" Alt="The orders page, filtered to this week" /&gt;
/// &lt;/BrowserFrame&gt;
/// </code>
/// </example>
/// <remarks>
/// The content fills the window's width and the corners clip it, so an image needs no radius of its
/// own. The frame takes the theme's colors in light and dark.
/// </remarks>
/// <accessibility>
/// <list type="bullet">
/// <item>The top bar, dots and address included, has <c>aria-hidden="true"</c>: screen readers read only the content, so put what the screenshot shows in the image's <c>Alt</c>.</item>
/// <item>The address is text, not a field or a link.</item>
/// </list>
/// </accessibility>
/// <seealso cref="Image"/>
/// <seealso cref="Section"/>
[ModeSupport]
public class BrowserFrame : StyledComponentBase
{
    /// <summary>The address shown in the top bar, such as <c>app.example.com</c>. Without it, the bar shows only the dots.</summary>
    [Parameter] public string? Address { get; set; }

    /// <summary>What the window shows, usually an <see cref="Image"/>.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add("ib-browser-frame");
        base.BuildAttributes(classes, styles);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cssClass, style) = BuildRootAttributes();
        builder.OpenElement(0, "div");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "class", cssClass);
        builder.AddAttribute(3, "style", style);

        builder.OpenElement(4, "div");
        builder.AddAttribute(5, "class", "ib-browser-frame-bar");
        builder.AddAttribute(6, "aria-hidden", "true");
        builder.AddMarkupContent(7, "<span class=\"ib-browser-frame-dots\"><span></span><span></span><span></span></span>");
        if (!string.IsNullOrWhiteSpace(Address))
        {
            builder.OpenElement(8, "span");
            builder.AddAttribute(9, "class", "ib-browser-frame-address");
            builder.AddContent(10, Address);
            builder.CloseElement();
        }
        builder.CloseElement();

        builder.OpenElement(11, "div");
        builder.AddAttribute(12, "class", "ib-browser-frame-content");
        builder.AddContent(13, ChildContent);
        builder.CloseElement();

        builder.CloseElement();
    }
}
