using System.ComponentModel;
using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// What <see cref="Tabs"/> and <see cref="TabNav"/> share: the strip's name, look and size, and how a
/// tab's icon, text and badge draw. Not a component of its own.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class TabsBase : LayoutItemBase
{
    private protected TabsBase()
    {
    }

    /// <summary>The tab strip's name for screen readers, such as "Account settings".</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>How the tabs look. Defaults to <see cref="TabsVariant.Line"/>.</summary>
    [Parameter] public TabsVariant Variant { get; set; } = TabsVariant.Line;

    /// <summary>Tab height and text size, shared with buttons and fields. Defaults to <see cref="ControlSize.Md"/>.</summary>
    [Parameter] public ControlSize Size { get; set; } = ControlSize.Md;

    /// <summary>Stretches the tabs to share the full width (or, vertical, the list's width).</summary>
    [Parameter] public bool FullWidth { get; set; }

    /// <summary>The element id. Generated when not set.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>The tabs.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add("ib-tabs");
        classes.Add($"ib-tabs-{Variant.Name()}");
        classes.Add("ib-tabs-sm", Size == ControlSize.Sm);
        classes.Add("ib-tabs-lg", Size == ControlSize.Lg);
        classes.Add("ib-tabs-fitted", FullWidth);
        base.BuildAttributes(classes, styles);
    }

    /// <summary>A tab's icon, text and badge, then <paramref name="after"/>.</summary>
    private protected static RenderFragment TabContent(IconData? icon, string label, string? badge, RenderFragment? after = null) => builder =>
    {
        if (icon is { } shown)
        {
            builder.OpenComponent<Icon>(0);
            builder.AddComponentParameter(1, nameof(Icon.Name), (IconData?)shown);
            builder.CloseComponent();
        }

        builder.OpenElement(2, "span");
        builder.AddAttribute(3, "class", "ib-tab-text");
        builder.AddContent(4, label);
        builder.CloseElement();
        if (badge is not null)
        {
            builder.OpenComponent<Badge>(5);
            builder.AddComponentParameter(6, nameof(Badge.Size), BadgeSize.Sm);
            builder.AddComponentParameter(7, nameof(Badge.Pill), true);
            builder.AddComponentParameter(8, nameof(Badge.ChildContent), (RenderFragment)(b => b.AddContent(0, badge)));
            builder.CloseComponent();
        }

        builder.AddContent(9, after);
    };
}
