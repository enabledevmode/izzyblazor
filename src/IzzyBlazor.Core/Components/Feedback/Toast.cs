using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// One toast drawn on the page, as ToastRegion draws it: a picture of it for a theme preview, a
/// mockup or docs. To show people a toast, use <see cref="ToastService"/> and a
/// <see cref="ToastRegion"/>, which place, announce and close them.
/// </summary>
/// <accessibility>
/// <list type="bullet">
/// <item>It renders an <see cref="Alert"/> that isn't announced, as it's part of the page from the start, not news.</item>
/// <item>The close control is the Alert's, named "Dismiss" (<see cref="DismissLabel"/>), and it closes the toast.</item>
/// </list>
/// </accessibility>
/// <seealso cref="ToastRegion"/>
/// <seealso cref="Alert"/>
[ModeSupport]
[ImportantParameters(nameof(Status), nameof(Title))]
public class Toast : LayoutItemBase
{
    /// <summary>What the message means; sets the icon. Defaults to <see cref="IzzyBlazor.Status.Info"/>.</summary>
    [Parameter] public Status Status { get; set; } = Status.Info;

    /// <summary>The bold first line.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>The message, and any <see cref="AlertActions"/> written last in it.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>How it's drawn. Defaults to <see cref="AlertVariant.Outline"/>, a raised card with a colored icon, as ToastRegion's are.</summary>
    [Parameter] public AlertVariant Variant { get; set; } = AlertVariant.Outline;

    /// <summary>Shows the close button. Defaults to true, as every toast in a ToastRegion has one.</summary>
    [Parameter] public bool Dismissible { get; set; } = true;

    /// <summary>The close button's accessible name. Defaults to "Dismiss".</summary>
    [Parameter] public string? DismissLabel { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add("ib-toast");
        classes.Add("ib-toast-inline");
        base.BuildAttributes(classes, styles);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cssClass, style) = BuildRootAttributes();
        builder.OpenComponent<Alert>(0);
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddComponentParameter(2, nameof(Alert.Status), Status);
        builder.AddComponentParameter(3, nameof(Alert.Variant), Variant);
        builder.AddComponentParameter(4, nameof(Alert.Title), Title);
        builder.AddComponentParameter(5, nameof(Alert.Dismissible), Dismissible);
        builder.AddComponentParameter(6, nameof(Alert.DismissLabel), DismissLabel);
        builder.AddComponentParameter(7, nameof(Alert.Announce), false);
        builder.AddComponentParameter(8, nameof(Alert.Class), cssClass);
        builder.AddComponentParameter(9, nameof(Alert.Style), style);
        builder.AddComponentParameter(10, nameof(Alert.ChildContent), ChildContent);
        builder.CloseComponent();
    }
}
