using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor;

/// <summary>
/// What <see cref="RichText"/> and <see cref="Markdown"/> share: sanitizing, highlighting code
/// blocks and moving headings down, then styling the result as <see cref="Prose"/>. Not a
/// component of its own.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract partial class RichContentBase : Prose
{
    private protected RichContentBase()
    {
    }

    [Inject] private IServiceProvider Services { get; set; } = default!;

    [GeneratedRegex("<pre><code class=\"language-([a-z0-9+#-]+)\">([^<]*)</code></pre>")]
    private static partial Regex CodeBlock();

    /// <summary>The highest heading level shown, 1 to 6: higher headings move down to it. Defaults to 1.</summary>
    [Parameter] public int TopHeading { get; set; } = 1;

    /// <summary>Keep images whose source is a <c>data:</c> URL of a PNG, JPEG, GIF, WebP or AVIF image.</summary>
    [Parameter] public bool AllowDataImages { get; set; }

    // The HTML to sanitize and show.
    private protected abstract string? Source { get; }

    private protected virtual IReadOnlyList<string>? AllowedFonts => null;

    private protected virtual IReadOnlyList<string>? AllowedFontSizes => null;

    private protected virtual bool AllowCustomColors => false;

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var html = Highlight(RichTextSanitizer.Sanitize(Source, labelTasks: true, TopHeading, AllowedFonts, AllowedFontSizes, AllowDataImages, AllowCustomColors));
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
