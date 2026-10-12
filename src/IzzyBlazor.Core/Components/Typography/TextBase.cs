using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// Shared typography props for <see cref="TextBlock"/> and <see cref="Heading"/>, so common
/// text styling never needs CSS. Also inherits every style prop from <see cref="Box"/>
/// (padding, margin, color, background...).
/// </summary>
/// <remarks>
/// Text components reset the browser's default margins; space them with a Stack's
/// <c>Gap</c> or the <c>Margin</c> props.
/// </remarks>
public abstract class TextBase : Box
{
    /// <summary>Font size. Responsive.</summary>
    [Parameter] public Responsive<TextSize> Size { get; set; }

    /// <summary>Font weight.</summary>
    [Parameter] public FontWeight? Weight { get; set; }

    /// <summary>Font family.</summary>
    [Parameter] public FontFamily? Family { get; set; }

    /// <summary>Horizontal alignment. Responsive.</summary>
    [Parameter] public Responsive<TextAlign> Align { get; set; }

    /// <summary>Line height.</summary>
    [Parameter] public LineHeight? LineHeight { get; set; }

    /// <summary>Letter spacing.</summary>
    [Parameter] public LetterSpacing? LetterSpacing { get; set; }

    /// <summary>Letter case.</summary>
    [Parameter] public TextTransform? Transform { get; set; }

    /// <summary>Underline or strike-through.</summary>
    [Parameter] public TextDecoration? Decoration { get; set; }

    /// <summary>Italic.</summary>
    [Parameter] public bool Italic { get; set; }

    /// <summary>How lines break: normal, never, balanced or pretty.</summary>
    [Parameter] public TextWrap? Wrap { get; set; }

    /// <summary>Keep to one line and end with an ellipsis when it does not fit.</summary>
    [Parameter] public bool Truncate { get; set; }

    /// <summary>Show at most this many lines, ending with an ellipsis.</summary>
    [Parameter] public int? LineClamp { get; set; }

    /// <summary>Equal-width digits, so numbers line up in columns and do not jitter when they change.</summary>
    [Parameter] public bool TabularNums { get; set; }

    /// <summary>The size to render: <see cref="Size"/>, or a component default when it is not set.</summary>
    protected virtual Responsive<TextSize> EffectiveSize => Size;

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        base.BuildAttributes(classes, styles);
        ApplyResponsive(classes, styles, EffectiveSize, "ib-fs", "fs", StyleValues.TextSize);
        ApplyResponsive(classes, styles, Align, "ib-ta", "ta", StyleValues.TextAlign);

        if (Weight is { } weight) classes.Add($"ib-fw-{Kebab(weight)}");
        if (Family is { } family) classes.Add($"ib-ff-{Kebab(family)}");
        if (LineHeight is { } leading) classes.Add($"ib-lh-{Kebab(leading)}");
        if (LetterSpacing is { } tracking) classes.Add($"ib-ls-{Kebab(tracking)}");
        if (Transform is { } transform) classes.Add($"ib-tt-{Kebab(transform)}");
        if (Decoration is { } decoration) classes.Add($"ib-td-{Kebab(decoration)}");
        if (Wrap is { } wrap) classes.Add($"ib-tw-{Kebab(wrap)}");
        classes.Add("ib-italic", Italic);
        classes.Add("ib-truncate", Truncate);
        classes.Add("ib-tabular", TabularNums);

        if (LineClamp is { } lines)
        {
            if (lines < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(LineClamp), lines, "LineClamp must be at least 1.");
            }

            classes.Add("ib-clamp");
            styles.AddVar("clamp", lines.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    /// <summary><c>LineThrough</c> becomes <c>line-through</c>, <c>NoWrap</c> becomes <c>no-wrap</c>.</summary>
    private static string Kebab<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        var name = value.ToString();
        var sb = new System.Text.StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0)
            {
                sb.Append('-');
            }

            sb.Append(char.ToLowerInvariant(name[i]));
        }

        return sb.ToString();
    }
}
