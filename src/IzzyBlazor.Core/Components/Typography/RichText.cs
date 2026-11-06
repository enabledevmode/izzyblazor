using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// Shows saved rich text, such as a rich text editor's value or HTML from a CMS: sanitized, then
/// styled as <see cref="Prose"/>.
/// </summary>
/// <example>
/// <code>
/// &lt;RichText Html="@post.Body" /&gt;
/// </code>
/// </example>
/// <remarks>
/// It runs <see cref="RichTextSanitizer"/> on <see cref="Html"/> every time it renders, so HTML
/// saved before you sanitized it, or written by another system, can't run script on the page.
/// What's kept is listed on <see cref="RichTextSanitizer"/>. For HTML you wrote yourself and
/// trust, such as rendered Markdown with its own styling, use <see cref="Prose"/>.
/// </remarks>
/// <accessibility>
/// <list type="bullet">
/// <item>Heading levels, alt text and link text come from the HTML. The sanitizer turns a heading 1 into a 2, since the page has its own.</item>
/// <item>Links are underlined, not marked by color alone.</item>
/// </list>
/// </accessibility>
/// <seealso cref="Prose"/>
/// <seealso cref="RichTextSanitizer"/>
[ModeSupport]
public class RichText : Prose
{
    /// <summary>The HTML to show. It's sanitized first.</summary>
    [Parameter] public string? Html { get; set; }

    /// <summary>Keep images whose source is a <c>data:</c> URL of a PNG, JPEG, GIF, WebP or AVIF image.</summary>
    [Parameter] public bool AllowDataImages { get; set; }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cssClass, style) = BuildRootAttributes();
        builder.OpenElement(0, ValidateTag(As));
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "class", cssClass);
        builder.AddAttribute(3, "style", style);
        builder.AddContent(4, new MarkupString(RichTextSanitizer.Sanitize(Html, AllowDataImages)));
        builder.CloseElement();
    }
}
