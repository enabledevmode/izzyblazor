using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// Shows saved rich text, such as a rich text editor's value or HTML from a CMS: sanitized, then
/// styled as <see cref="Prose"/>.
/// </summary>
/// <remarks>
/// It runs <see cref="RichTextSanitizer"/> on <see cref="Html"/> every time it renders, so HTML
/// saved before you sanitized it, or written by another system, can't run script on the page.
/// What's kept is listed on <see cref="RichTextSanitizer"/>. A code block with a language
/// (<c>&lt;code class="language-csharp"&gt;</c>, as the editor writes it) is highlighted on the
/// server, as <see cref="Code"/> is, by the same highlighter; the saved HTML keeps only the class.
/// For Markdown, use <see cref="Markdown"/>. For HTML you wrote yourself and trust, use
/// <see cref="Prose"/>.
/// </remarks>
/// <accessibility>
/// <list type="bullet">
/// <item>Heading levels, alt text and link text come from the HTML. When the page has its own heading 1 above the text, set <see cref="RichContentBase.TopHeading"/> to 2 so the text's headings sit under it.</item>
/// <item>Links are underlined, not marked by color alone.</item>
/// </list>
/// </accessibility>
/// <seealso cref="Markdown"/>
/// <seealso cref="Prose"/>
/// <seealso cref="RichTextSanitizer"/>
[ModeSupport]
public class RichText : RichContentBase
{
    /// <summary>The HTML to show. It's sanitized first.</summary>
    [Parameter] public string? Html { get; set; }

    /// <summary>The fonts the text may use, as the editor's <c>Fonts</c>: others are dropped.</summary>
    [Parameter] public IReadOnlyList<string>? Fonts { get; set; }

    /// <summary>The fixed text sizes the text may use, as the editor's <c>FontSizes</c>: others are dropped.</summary>
    [Parameter] public IReadOnlyList<string>? FontSizes { get; set; }

    /// <summary>Keep the editor's custom colors: a text color or highlight as six hex digits, as the editor's <c>CustomColors</c>.</summary>
    [Parameter] public bool CustomColors { get; set; }

    private protected override string? Source => Html;

    private protected override IReadOnlyList<string>? AllowedFonts => Fonts;

    private protected override IReadOnlyList<string>? AllowedFontSizes => FontSizes;

    private protected override bool AllowCustomColors => CustomColors;
}
