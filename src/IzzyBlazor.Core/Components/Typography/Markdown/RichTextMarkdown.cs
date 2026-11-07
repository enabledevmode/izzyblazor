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
/// becomes the editor's alignment class.
/// </para>
/// <para>
/// Text that's still arriving, such as an assistant's reply as it streams, shows sensibly at
/// each step: an unclosed code block runs to the end, and unclosed emphasis shows its markers
/// until they close.
/// </para>
/// </remarks>
/// <seealso cref="RichText"/>
/// <seealso cref="RichTextSanitizer"/>
public static class RichTextMarkdown
{
    /// <summary>Returns <paramref name="markdown"/> as sanitized HTML.</summary>
    /// <param name="markdown">The Markdown. <see langword="null"/> gives an empty string.</param>
    /// <param name="topHeading">The highest heading level kept, 1 to 6. Higher headings move down to it, as the sanitizer's do. Defaults to 1.</param>
    public static string ToHtml(string? markdown, int topHeading = 1) =>
        string.IsNullOrEmpty(markdown) ? string.Empty : RichTextSanitizer.Sanitize(Render(markdown), topHeading);

    /// <summary>The HTML as the Markdown says, not yet sanitized: raw HTML passes through.</summary>
    internal static string Render(string markdown) => MdHtml.Render(MdBlocks.Parse(markdown));
}
