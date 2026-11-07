namespace IzzyBlazor;

/// <summary>
/// Turns Markdown into safe HTML: CommonMark, with GitHub's tables, task lists, strikethrough
/// and links from bare addresses, then sanitized as <see cref="RichTextSanitizer"/> does.
/// </summary>
/// <example>
/// <code>
/// var html = RichTextMarkdown.ToHtml("## Plan\n\n- [x] Draft\n- [ ] Review");
/// </code>
/// </example>
/// <remarks>
/// <para>
/// It follows CommonMark 0.31.2 and GitHub-flavored Markdown's extensions, checked against both
/// specs' examples. Named entities are HTML 4's set (<c>&amp;copy;</c>, <c>&amp;nbsp;</c>);
/// HTML5's longer list (<c>&amp;HilbertSpace;</c>) stays as text.
/// </para>
/// <para>
/// HTML written in the Markdown is read like any other HTML, and the sanitizer keeps only what a
/// <see cref="RichTextEditor"/> could make: scripts, styles, event handlers and
/// <c>javascript:</c> links go. A code block's language becomes its class
/// (<c>language-csharp</c>), so <see cref="RichText"/> highlights it. A table column's alignment
/// becomes the editor's alignment class. Link and image titles are dropped. A tag the sanitizer
/// wouldn't keep shows as text, so <c>List&lt;string&gt;</c> or <c>&lt;Button /&gt;</c> written
/// outside a code span reads as written instead of disappearing.
/// </para>
/// <para>
/// Text that's still arriving, such as an assistant's reply as it streams, shows sensibly at
/// each step: an unclosed code block runs to the end, and unclosed emphasis shows its markers
/// until they close. A chat entry that's still arriving also holds back a last line of only
/// <c>-</c> or <c>=</c>, which would turn the line above it into a heading until the next line
/// arrives.
/// </para>
/// </remarks>
/// <seealso cref="RichText"/>
/// <seealso cref="RichTextSanitizer"/>
public static class RichTextMarkdown
{
    /// <summary>Returns <paramref name="markdown"/> as sanitized HTML.</summary>
    /// <param name="markdown">The Markdown. <see langword="null"/> gives an empty string.</param>
    /// <param name="topHeading">The highest heading level kept, 1 to 6. Higher headings move down to it, as the sanitizer's do. Defaults to 1.</param>
    /// <param name="allowDataImages">Keep images whose source is a <c>data:</c> URL of a PNG, JPEG, GIF, WebP or AVIF image.</param>
    public static string ToHtml(string? markdown, int topHeading = 1, bool allowDataImages = false) =>
        string.IsNullOrEmpty(markdown) ? string.Empty
            : RichTextSanitizer.Sanitize(Render(markdown, display: true), labelTasks: true, topHeading, null, null, allowDataImages, customColors: false);

    /// <summary>
    /// The HTML as the Markdown says, not yet sanitized. As the spec has it, raw HTML passes
    /// through; for <paramref name="display"/>, tags the sanitizer wouldn't keep are text. While
    /// <paramref name="streaming"/>, a last line of only - or = (a heading's underline, or the
    /// start of a list or rule) waits for the next line.
    /// </summary>
    internal static string Render(string markdown, bool display = false, bool streaming = false)
    {
        if (streaming)
        {
            var lastBreak = markdown.LastIndexOf('\n');
            var last = markdown[(lastBreak + 1)..];
            if (last.Trim().Length > 0 && last.Trim().All(c => c is '-' or '=' or ' ' or '\t')) markdown = markdown[..(lastBreak + 1)];
        }

        return MdHtml.Render(MdBlocks.Parse(markdown), display);
    }
}
