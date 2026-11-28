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

    private string Value => Color.TokenName is { } name ? $"--ib-color-{name}" : Color.Css ?? "";

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
        builder.CloseElement();

        if (!string.IsNullOrWhiteSpace(Label))
        {
            builder.OpenElement(8, "span");
            builder.AddAttribute(9, "class", "ib-swatch-label");
            builder.AddContent(10, Label);
            builder.CloseElement();
        }

        builder.OpenElement(11, "span");
        builder.AddAttribute(12, "class", "ib-swatch-value");
        builder.AddContent(13, Value);
        builder.CloseElement();

        builder.CloseElement();
    }
}
