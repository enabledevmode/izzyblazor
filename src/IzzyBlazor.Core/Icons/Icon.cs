using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// Renders an icon as inline SVG on the server: no font or script to load, works on static
/// pages, and follows the text color and size around it.
/// </summary>
/// <example>
/// <code>
/// &lt;Icon Name="Lucide.House" /&gt;
/// &lt;Icon Name="Lucide.TriangleAlert" Size="IconSize.Lg" Color="Color.WarningText" Label="Warning" /&gt;
/// &lt;Icon Role="IconRole.Close" /&gt;
/// </code>
/// </example>
/// <remarks>
/// Without a <see cref="Label"/> the icon is decorative and hidden from screen readers, which
/// is right when visible text sits next to it. Give icons that stand alone a label.
/// </remarks>
public class Icon : IzzyComponentBase
{
    /// <summary>The icon to show, such as <c>Lucide.House</c>. Set this or <see cref="Role"/>.</summary>
    [Parameter] public IconData? Name { get; set; }

    /// <summary>A purpose such as <see cref="IconRole.Close"/>, resolved through the current icon set. Set this or <see cref="Name"/>.</summary>
    [Parameter] public IconRole? Role { get; set; }

    /// <summary>Size relative to the surrounding text. Defaults to the text size (1em).</summary>
    [Parameter] public IconSize Size { get; set; } = IconSize.Md;

    /// <summary>Color. Defaults to the surrounding text color.</summary>
    [Parameter] public Responsive<Color> Color { get; set; }

    /// <summary>
    /// Accessible name. When set, the icon is announced as an image with this label;
    /// when not, it is decorative and hidden from assistive technology.
    /// </summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Overrides the stroke width of outlined icons (Lucide's default is 2).</summary>
    [Parameter] public double? StrokeWidth { get; set; }

    /// <summary>Rotates continuously, for loading indicators. Respects reduced-motion settings.</summary>
    [Parameter] public bool Spin { get; set; }

    private IconData _icon;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        _icon = (Name, Role) switch
        {
            ({ } name, null) => name,
            (null, { } role) => IconSet.Current.Get(role),
            (null, null) => throw new InvalidOperationException("Icon needs either Name or Role."),
            _ => throw new InvalidOperationException("Icon takes Name or Role, not both."),
        };
    }

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add("ib-icon");
        classes.Add($"ib-icon-{Size.ToString().ToLowerInvariant()}", Size != IconSize.Md);
        classes.Add("ib-icon-spin", Spin);
        ApplyResponsive(classes, styles, Color, "ib-fg", "fg", StyleValues.Color);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cssClass, style) = BuildRootAttributes();
        var stroked = _icon.Style == IconStyle.Stroke;
        var labelled = !string.IsNullOrWhiteSpace(Label);

        builder.OpenElement(0, "svg");
        builder.AddAttribute(1, "xmlns", "http://www.w3.org/2000/svg");
        builder.AddAttribute(2, "viewBox", _icon.ViewBox);
        builder.AddAttribute(3, "fill", stroked ? "none" : "currentColor");
        if (stroked)
        {
            builder.AddAttribute(4, "stroke", "currentColor");
            builder.AddAttribute(5, "stroke-width", (StrokeWidth ?? _icon.StrokeWidth).ToString(CultureInfo.InvariantCulture));
            builder.AddAttribute(6, "stroke-linecap", "round");
            builder.AddAttribute(7, "stroke-linejoin", "round");
        }

        if (labelled)
        {
            builder.AddAttribute(8, "role", "img");
            builder.AddAttribute(9, "aria-label", Label);
        }
        else
        {
            builder.AddAttribute(10, "aria-hidden", "true");
        }

        builder.AddAttribute(11, "focusable", "false");
        builder.AddMultipleAttributes(12, AdditionalAttributes);
        builder.AddAttribute(13, "class", cssClass);
        builder.AddAttribute(14, "style", style);
        builder.AddMarkupContent(15, _icon.Body);
        builder.CloseElement();
    }
}
