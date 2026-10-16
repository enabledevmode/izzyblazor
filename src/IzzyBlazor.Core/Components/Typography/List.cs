using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>What a <see cref="List"/> shares with its items.</summary>
/// <param name="Icon">The icon every item shows in place of a bullet, if any.</param>
/// <param name="IconColor">The icons' color.</param>
public sealed record ListContext(IconData? Icon, Color? IconColor);

/// <summary>
/// A bulleted, numbered or icon list of <see cref="ListItem"/>s, spaced with <see cref="Gap"/>.
/// <c>&lt;List Icon="Hugeicons.Check" IconColor="Color.SuccessText"&gt;</c> makes a check list.
/// </summary>
/// <remarks>
/// Lists without bullets keep <c>role="list"</c>, which Safari otherwise drops from a list with no
/// markers, so screen readers still announce the count.
/// </remarks>
public class List : Box
{
    /// <summary>Creates a list, rendered as a <c>ul</c>.</summary>
    public List() => As = "ul";

    /// <summary>A numbered list (<c>ol</c>).</summary>
    [Parameter] public bool Ordered { get; set; }

    /// <summary>No bullets or numbers, and no indent.</summary>
    [Parameter] public bool Plain { get; set; }

    /// <summary>An icon in place of each bullet, such as <c>Hugeicons.Check</c>. An item's own icon wins.</summary>
    [Parameter] public IconData? Icon { get; set; }

    /// <summary>The icons' color. Defaults to the text color.</summary>
    [Parameter] public Color? IconColor { get; set; }

    /// <summary>Space between items, on the spacing scale. Defaults to 2 (8px).</summary>
    [Parameter] public Responsive<int> Gap { get; set; } = 2;

    /// <inheritdoc />
    protected override string BaseClass => "ib-list";

    private ListContext _context = new(null, null);

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        base.BuildAttributes(classes, styles);
        classes.Add("ib-list-ordered", Ordered && !Plain && Icon is null);
        classes.Add("ib-list-plain", Plain || Icon is not null);
        classes.Add("ib-list-icons", Icon is not null);
        ApplyResponsive(classes, styles, Gap, "ib-gap", "gap", StyleValues.Spacing);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        if (!Equals(_context.Icon, Icon) || !Equals(_context.IconColor, IconColor))
        {
            _context = new ListContext(Icon, IconColor);
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cssClass, style) = BuildRootAttributes();
        builder.OpenElement(0, Ordered ? "ol" : ValidateTag(As));
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "class", cssClass);
        builder.AddAttribute(3, "style", style);
        if (Plain || Icon is not null)
        {
            builder.AddAttribute(4, "role", "list");
        }

        builder.OpenComponent<CascadingValue<ListContext>>(5);
        builder.AddComponentParameter(6, "Value", _context);
        builder.AddComponentParameter(7, "ChildContent", ChildContent);
        builder.CloseComponent();
        builder.CloseElement();
    }
}

/// <summary>An item in a <see cref="List"/>: a <c>li</c>, with the list's icon or its own.</summary>
public class ListItem : IzzyComponentBase
{
    /// <summary>The content.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>An icon for this item, in place of the list's.</summary>
    [Parameter] public IconData? Icon { get; set; }

    /// <summary>This item's icon color, in place of the list's.</summary>
    [Parameter] public Color? IconColor { get; set; }

    /// <summary>The list this item belongs to.</summary>
    [CascadingParameter] public ListContext? List { get; set; }

    private IconData? ResolvedIcon => Icon ?? List?.Icon;

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add("ib-list-item");
        classes.Add("ib-list-item-icon", ResolvedIcon is not null);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cssClass, style) = BuildRootAttributes();
        builder.OpenElement(0, "li");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "class", cssClass);
        builder.AddAttribute(3, "style", style);
        if (ResolvedIcon is { } icon)
        {
            builder.OpenComponent<Icon>(4);
            builder.AddComponentParameter(5, nameof(IzzyBlazor.Icon.Name), icon);
            builder.AddComponentParameter(6, nameof(IzzyBlazor.Icon.Class), "ib-list-icon");
            if ((IconColor ?? List?.IconColor) is { } color)
            {
                builder.AddComponentParameter(7, nameof(IzzyBlazor.Icon.Color), (Responsive<Color>)color);
            }

            builder.CloseComponent();
            builder.OpenElement(8, "div");
            builder.AddAttribute(9, "class", "ib-list-content");
            builder.AddContent(10, ChildContent);
            builder.CloseElement();
        }
        else
        {
            builder.AddContent(11, ChildContent);
        }

        builder.CloseElement();
    }
}
