using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// A spinning ring that says something is working. Sized in em like <see cref="Icon"/>, so it
/// matches the text beside it, and announced to screen readers as <see cref="Label"/>.
/// Pure CSS: it spins on static pages too.
/// </summary>
/// <example><code>&lt;Spinner /&gt;  &lt;Spinner Size="IconSize.Xl2" Color="Color.Primary" Label="Loading orders" /&gt;</code></example>
public class Spinner : LayoutItemBase
{
    /// <summary>Size, on the same scale as icons (relative to the text). Defaults to <see cref="IconSize.Md"/> (1em).</summary>
    [Parameter] public IconSize Size { get; set; } = IconSize.Md;

    /// <summary>The ring's color. Defaults to the text color.</summary>
    [Parameter] public Color? Color { get; set; }

    /// <summary>What screen readers hear. Defaults to "Loading".</summary>
    [Parameter] public string? Label { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add("ib-icon ib-spinner");
        base.BuildAttributes(classes, styles);

        classes.Add($"ib-icon-{Size.ToString().ToLowerInvariant()}", Size != IconSize.Md);
        if (Color is { } color)
        {
            styles.AddRaw($"color:{StyleValues.Color(color)}");
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cls, style) = BuildRootAttributes();
        builder.OpenElement(0, "span");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "class", cls);
        builder.AddAttribute(3, "style", style);
        builder.AddAttribute(4, "role", "status");
        builder.AddMarkupContent(5,
            "<svg class=\"ib-spinner-svg\" viewBox=\"0 0 24 24\" fill=\"none\" aria-hidden=\"true\" focusable=\"false\">" +
            "<circle class=\"ib-spinner-track\" cx=\"12\" cy=\"12\" r=\"9.5\"/>" +
            "<circle class=\"ib-spinner-arc\" cx=\"12\" cy=\"12\" r=\"9.5\"/></svg>");
        builder.OpenElement(6, "span");
        builder.AddAttribute(7, "class", "ib-visually-hidden");
        builder.AddContent(8, Label ?? TextDefaults.Loading);
        builder.CloseElement();
        builder.CloseElement();
    }
}
