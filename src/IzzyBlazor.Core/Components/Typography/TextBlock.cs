using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// Body text. (Named TextBlock because Razor reserves <c>&lt;text&gt;</c> inside code blocks.) Renders a <c>p</c> by default; set <c>As</c> to <c>span</c>, <c>small</c>,
/// <c>strong</c>, <c>label</c> and so on.
/// </summary>
/// <example>
/// <code>
/// &lt;TextBlock Size="TextSize.Lg" Color="Color.Muted" Wrap="TextWrap.Pretty"&gt;...&lt;/TextBlock&gt;
/// &lt;TextBlock As="span" Size="TextSize.Xs" Weight="FontWeight.Semibold" Transform="TextTransform.Uppercase" LetterSpacing="LetterSpacing.Wider"&gt;New&lt;/TextBlock&gt;
/// </code>
/// </example>
/// <accessibility>
/// <list type="bullet">
/// <item>It renders a <c>p</c>, so text styled to look like a heading is still a paragraph to screen readers; use <see cref="Heading"/> for a real one.</item>
/// <item>Weight and size are only visual: for emphasis screen readers can hear, set <c>As</c> to <c>strong</c> or <c>em</c>.</item>
/// </list>
/// </accessibility>
/// <seealso cref="Heading"/>
/// <seealso cref="Prose"/>
/// <seealso cref="Code"/>
[ModeSupport]
public class TextBlock : TextBase
{
    /// <summary>Creates a TextBlock that renders a paragraph.</summary>
    public TextBlock() => As = "p";

    /// <inheritdoc />
    protected override string BaseClass => "ib-text";
}
