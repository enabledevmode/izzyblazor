using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// A heading. <see cref="Level"/> sets the element (<c>h1</c> to <c>h6</c>) and a default
/// size; <c>Size</c> overrides the look without changing the document structure.
/// </summary>
/// <example>
/// <code>
/// &lt;Heading Level="1"&gt;Build a multi-step wizard&lt;/Heading&gt;
/// &lt;Heading Level="2" Size="new() { Base = TextSize.Xl2, Md = TextSize.Xl4 }"&gt;...&lt;/Heading&gt;
/// </code>
/// </example>
/// <remarks>
/// Defaults: bold, tight line height, balanced wrapping, no margins. Every default can be
/// overridden with the typography props.
/// </remarks>
public class Heading : TextBase
{
    /// <summary>The heading level, 1 to 6. Defaults to 2.</summary>
    [Parameter] public int Level { get; set; } = 2;

    /// <inheritdoc />
    protected override string BaseClass => "ib-heading";

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Level is < 1 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(Level), Level, "Heading Level must be 1 to 6.");
        }

        As = $"h{Level}";
    }

    /// <inheritdoc />
    protected override Responsive<TextSize> EffectiveSize => Size.IsEmpty ? DefaultSize(Level) : Size;

    private static TextSize DefaultSize(int level) => level switch
    {
        1 => TextSize.Xl4,
        2 => TextSize.Xl3,
        3 => TextSize.Xl2,
        4 => TextSize.Xl,
        5 => TextSize.Lg,
        _ => TextSize.Md,
    };
}
