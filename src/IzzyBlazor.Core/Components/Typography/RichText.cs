using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor;

/// <summary>
/// Shows saved rich text, such as a rich text editor's value, HTML from a CMS or Markdown:
/// sanitized, then styled as <see cref="Prose"/>.
/// </summary>
/// <example>
/// <code>
/// &lt;RichText Html="@post.Body" /&gt;
/// &lt;RichText Markdown="@readme" /&gt;
/// </code>
/// </example>
/// <remarks>
/// It runs <see cref="RichTextSanitizer"/> on <see cref="Html"/> every time it renders, so HTML
/// saved before you sanitized it, or written by another system, can't run script on the page.
/// What's kept is listed on <see cref="RichTextSanitizer"/>. A code block with a language
/// (<c>&lt;code class="language-csharp"&gt;</c>, as the editor writes it) is highlighted on the
/// server, as <see cref="Code"/> is, by the same highlighter; the saved HTML keeps only the class.
/// <see cref="Markdown"/> is turned into HTML by <see cref="RichTextMarkdown"/> (CommonMark, with
/// GitHub's tables, task lists and strikethrough), then sanitized the same way. For HTML you
/// wrote yourself and trust, use <see cref="Prose"/>.
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
public partial class RichText : Prose
{
    [Inject] private IServiceProvider Services { get; set; } = default!;

    [GeneratedRegex("<pre><code class=\"language-([a-z0-9+#-]+)\">([^<]*)</code></pre>")]
    private static partial Regex CodeBlock();

    /// <summary>The HTML to show. It's sanitized first.</summary>
    [Parameter] public string? Html { get; set; }

    /// <summary>Markdown to show instead of <see cref="Html"/>: turned into HTML, then sanitized.</summary>
    [Parameter] public string? Markdown { get; set; }

    /// <summary>The highest heading level shown, 1 to 6: higher headings move down to it. Defaults to 1; set the editor's <c>TopHeading</c> to match.</summary>
    [Parameter] public int TopHeading { get; set; } = 1;

    /// <summary>The fonts the text may use, as the editor's <c>Fonts</c>: others are dropped.</summary>
    [Parameter] public IReadOnlyList<string>? Fonts { get; set; }

    /// <summary>The fixed text sizes the text may use, as the editor's <c>FontSizes</c>: others are dropped.</summary>
    [Parameter] public IReadOnlyList<string>? FontSizes { get; set; }

    /// <summary>Keep images whose source is a <c>data:</c> URL of a PNG, JPEG, GIF, WebP or AVIF image.</summary>
    [Parameter] public bool AllowDataImages { get; set; }

    /// <summary>Keep the editor's custom colors: a text color or highlight as six hex digits, as the editor's <c>CustomColors</c>.</summary>
    [Parameter] public bool CustomColors { get; set; }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (!string.IsNullOrEmpty(Html) && !string.IsNullOrEmpty(Markdown))
        {
            throw new InvalidOperationException("RichText shows Html or Markdown, not both.");
        }

        var source = string.IsNullOrEmpty(Markdown) ? Html : RichTextMarkdown.Render(Markdown, display: true);
        var html = Highlight(RichTextSanitizer.Sanitize(source, labelTasks: true, TopHeading, Fonts, FontSizes, AllowDataImages, CustomColors));
        // The editor's content classes (alignment, color, size) and task lists have a sheet of their own.
        if (html.Contains("class=\"ib-", StringComparison.Ordinal) || html.Contains("ib-size-", StringComparison.Ordinal) || html.Contains("<input", StringComparison.Ordinal))
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

    // Code blocks with a language the highlighter knows, as tokens in ib-tok-* spans. The code
    // is the sanitized text, decoded and encoded again, so nothing in it becomes markup.
    private string Highlight(string html)
    {
        if (!html.Contains("<pre><code class=\"language-", StringComparison.Ordinal)) return html;
        var highlighter = Services.GetService<ICodeHighlighter>() ?? IzzyCodeHighlighter.Instance;
        return CodeBlock().Replace(html, match =>
        {
            var code = WebUtility.HtmlDecode(match.Groups[2].Value);
            if (!highlighter.TryHighlight(code, match.Groups[1].Value, out var tokens)) return match.Value;
            var output = new StringBuilder($"<pre><code class=\"language-{match.Groups[1].Value}\">");
            foreach (var token in tokens)
            {
                var text = WebUtility.HtmlEncode(token.Text);
                output.Append(token.Kind == CodeTokenKind.Text ? text : $"<span class=\"{Code.ClassOf(token.Kind)}\">{text}</span>");
            }

            return output.Append("</code></pre>").ToString();
        });
    }
}
