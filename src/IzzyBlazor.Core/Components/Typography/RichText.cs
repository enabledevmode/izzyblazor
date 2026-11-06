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
/// <item>Heading levels, alt text and link text come from the HTML. When the page has its own heading 1 above the text, set <see cref="TopHeading"/> to 2 so the text's headings sit under it.</item>
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

    /// <summary>The highest heading level shown, 1 to 6: higher headings move down to it. Defaults to 1; set the editor's <c>TopHeading</c> to match.</summary>
    [Parameter] public int TopHeading { get; set; } = 1;

    /// <summary>The fonts the text may use, as the editor's <c>Fonts</c>: others are dropped.</summary>
    [Parameter] public IReadOnlyList<string>? Fonts { get; set; }

    /// <summary>The fixed text sizes the text may use, as the editor's <c>FontSizes</c>: others are dropped.</summary>
    [Parameter] public IReadOnlyList<string>? FontSizes { get; set; }

    /// <summary>Keep images whose source is a <c>data:</c> URL of a PNG, JPEG, GIF, WebP or AVIF image.</summary>
    [Parameter] public bool AllowDataImages { get; set; }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var html = RichTextSanitizer.Sanitize(Html, TopHeading, Fonts, FontSizes, AllowDataImages);
        // The editor's content classes (alignment, color, size) have a sheet of their own.
        if (html.Contains("class=\"ib-", StringComparison.Ordinal) || html.Contains("ib-size-", StringComparison.Ordinal))
        {
            builder.OpenElement(5, "link");
            builder.AddAttribute(6, "rel", "stylesheet");
            builder.AddAttribute(7, "href", Assets["_content/IzzyBlazor.Core/rich-content.css"]);
            builder.CloseElement();
        }

        var (cssClass, style) = BuildRootAttributes();
        builder.OpenElement(0, ValidateTag(As));
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "class", cssClass);
        builder.AddAttribute(3, "style", style);
        builder.AddContent(4, new MarkupString(html));
        builder.CloseElement();
    }
}
