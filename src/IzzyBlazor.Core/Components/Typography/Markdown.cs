using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// Shows Markdown, such as release notes or a chat reply: turned into HTML on the server,
/// sanitized, then styled as <see cref="Prose"/>.
/// </summary>
/// <remarks>
/// <see cref="RichTextMarkdown"/> reads <see cref="Text"/> as CommonMark, with GitHub's tables,
/// task lists, strikethrough and links from bare URLs, and <see cref="RichTextSanitizer"/> cleans
/// the result, so HTML written in the Markdown can't run script on the page. A fenced code block
/// with a language is highlighted on the server, as <see cref="Code"/> is. For saved HTML, use
/// <see cref="RichText"/>.
/// </remarks>
/// <accessibility>
/// <list type="bullet">
/// <item>Heading levels, alt text and link text come from the Markdown. When the page has its own heading 1 above the text, set <see cref="RichContentBase.TopHeading"/> to 2 so the text's headings sit under it.</item>
/// <item>Links are underlined, not marked by color alone.</item>
/// </list>
/// </accessibility>
/// <seealso cref="RichText"/>
/// <seealso cref="RichTextMarkdown"/>
[ModeSupport]
public class Markdown : RichContentBase
{
    /// <summary>The Markdown to show.</summary>
    [Parameter] public string? Text { get; set; }

    private protected override string? Source => string.IsNullOrEmpty(Text) ? null : RichTextMarkdown.Render(Text, display: true);
}
