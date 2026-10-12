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
public class TextBlock : TextBase
{
    /// <summary>Creates a TextBlock that renders a paragraph.</summary>
    public TextBlock() => As = "p";

    /// <inheritdoc />
    protected override string BaseClass => "ib-text";
}
