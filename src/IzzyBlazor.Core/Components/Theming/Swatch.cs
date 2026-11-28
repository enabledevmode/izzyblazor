using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// A color shown with its name: a theme token such as <c>Color.PrimarySoft</c>, one of your own
/// (<c>Color.Token("promo")</c>) or a raw CSS color. For a page of tokens, a theme preview or a brand
/// kit. A chip as wide as its place, then the name and the variable under it. The chip has an edge,
/// so a color that matches the page still shows.
/// </summary>
/// <accessibility>
/// <list type="bullet">
/// <item>It renders a <c>span</c>: the chip is decorative (<c>aria-hidden</c>), and the label is text, so screen readers read the name.</item>
/// <item>The color itself isn't spoken, so name it in <see cref="Label"/> when its value matters, as in "Promo, #7c3aed".</item>
/// <item>With <see cref="TextColor"/>, the chip's "Aa" is decorative too: the variables under the name say which pair it is.</item>
/// </list>
/// </accessibility>
/// <seealso cref="ColorPicker"/>
/// <seealso cref="ThemeScope"/>
[ModeSupport]
[ImportantParameters(nameof(Label))]
public class Swatch : LayoutItemBase
{
    /// <summary>The color, such as <c>Color.Primary</c>, <c>Color.Token("promo")</c> or <c>Color.Of("#0f766e")</c>.</summary>
    [Parameter, EditorRequired] public Color Color { get; set; }

    /// <summary>
    /// The color's name, such as "Brand soft", over its variable (<c>--ib-color-primary-soft</c>) or a
    /// raw color's CSS. Without it, the variable alone.
    /// </summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>
    /// A text color to show on the chip, as "Aa": for a pair such as <c>Color.SuccessSoft</c> with
    /// <c>Color.SuccessText</c>. Its variable shows under the chip's.
    /// </summary>
    [Parameter] public Color? TextColor { get; set; }

    private static string ValueOf(Color color) => color.TokenName is { } name ? $"--ib-color-{name}" : color.Css ?? "";

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Color.Css is null)
        {
            throw new InvalidOperationException("Swatch needs a Color, such as Color=\"Color.Primary\".");
        }

        base.OnParametersSet();
    }

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add("ib-swatch");
        base.BuildAttributes(classes, styles);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cssClass, style) = BuildRootAttributes();
        builder.OpenElement(0, "span");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "class", cssClass);
        builder.AddAttribute(3, "style", style);

        builder.OpenElement(4, "span");
        builder.AddAttribute(5, "class", "ib-swatch-chip");
        builder.AddAttribute(6, "style", $"background-color: {Color.Css}");
        builder.AddAttribute(7, "aria-hidden", "true");
        if (TextColor is { Css: { } text })
        {
            builder.OpenElement(8, "span");
            builder.AddAttribute(9, "class", "ib-swatch-text");
            builder.AddAttribute(10, "style", $"color: {text}");
            builder.AddContent(11, "Aa");
            builder.CloseElement();
        }

        builder.CloseElement();

        if (!string.IsNullOrWhiteSpace(Label))
        {
            builder.OpenElement(12, "span");
            builder.AddAttribute(13, "class", "ib-swatch-label");
            builder.AddContent(14, Label);
            builder.CloseElement();
        }

        builder.OpenElement(15, "span");
        builder.AddAttribute(16, "class", "ib-swatch-value");
        builder.AddContent(17, ValueOf(Color));
        builder.CloseElement();

        if (TextColor is { Css: not null } textColor)
        {
            builder.OpenElement(18, "span");
            builder.AddAttribute(19, "class", "ib-swatch-value");
            builder.AddContent(20, ValueOf(textColor));
            builder.CloseElement();
        }

        builder.CloseElement();
    }
}
