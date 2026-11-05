using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace IzzyBlazor;

/// <summary>
/// A floating action button: fixed to a bottom corner of the window, raised and large, where it
/// stays as the page scrolls. For the page's main action, such as adding an item; one per page. An
/// icon alone by default, named by <see cref="Label"/>; <see cref="ShowLabel"/> shows the label
/// beside it. In an <see cref="AppShell"/> it sits above a sticky footer.
/// </summary>
/// <example><code>&lt;FloatingButton Icon="Lucide.Plus" Label="New invoice" Href="invoices/new" /&gt;</code></example>
/// <accessibility>
/// <list type="bullet">
/// <item>It renders a native <c>button</c>, or an <c>a</c> with <see cref="Href"/>, named by <see cref="Label"/>; with only the icon showing, the label is its <c>aria-label</c> and, with script, its tooltip.</item>
/// <item>It comes after the page's content in the tab order, where it's written: put it at the end of the page's markup.</item>
/// </list>
/// </accessibility>
/// <seealso cref="Button"/>
/// <seealso cref="IconButton"/>
[ModeSupport]
public class FloatingButton : IzzyComponentBase
{
    /// <summary>The icon, such as <c>Lucide.Plus</c>.</summary>
    [Parameter, EditorRequired] public IconData Icon { get; set; }

    /// <summary>What it does, such as "New invoice": its accessible name, and its text with <see cref="ShowLabel"/>.</summary>
    [Parameter, EditorRequired] public string Label { get; set; } = "";

    /// <summary>Shows <see cref="Label"/> beside the icon, for a main action an icon alone wouldn't name.</summary>
    [Parameter] public bool ShowLabel { get; set; }

    /// <summary>Where it sits. Defaults to <see cref="FloatPosition.BottomEnd"/>.</summary>
    [Parameter] public FloatPosition Position { get; set; } = FloatPosition.BottomEnd;

    /// <summary>Its color. Defaults to <see cref="Accent.Primary"/>.</summary>
    [Parameter] public Accent Accent { get; set; } = Accent.Primary;

    /// <summary>Its size. Defaults to <see cref="ControlSize.Lg"/>.</summary>
    [Parameter] public ControlSize Size { get; set; } = ControlSize.Lg;

    /// <summary>Makes it a link to this address.</summary>
    [Parameter] public string? Href { get; set; }

    /// <summary>Makes it a submit button for the form around it, or the one named by <see cref="Form"/>.</summary>
    [Parameter] public bool Submit { get; set; }

    /// <summary>The name a submit button posts with its <see cref="Value"/>.</summary>
    [Parameter] public string? Name { get; set; }

    /// <summary>The value a submit button posts under its <see cref="Name"/>.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>An address a submit button posts to in place of its form's.</summary>
    [Parameter] public string? FormAction { get; set; }

    /// <summary>The id of the form a submit button belongs to, when it's outside it.</summary>
    [Parameter] public string? Form { get; set; }

    /// <summary>Turns it off.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Runs on click on interactive pages (a link still navigates). Has no effect on static pages.</summary>
    [Parameter, ModeSupport(NoScript = ModeLevel.None, Static = ModeLevel.None)]
    public EventCallback<MouseEventArgs> OnClick { get; set; }

    private FloatSpot _spot = new(FloatPosition.BottomEnd);

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (string.IsNullOrWhiteSpace(Label))
        {
            throw new InvalidOperationException("FloatingButton needs a Label: it names the button, such as \"New invoice\".");
        }

        if (_spot.Position != Position)
        {
            _spot = new FloatSpot(Position);
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<FloatSpot>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<FloatSpot>.Value), _spot);
        builder.AddComponentParameter(2, nameof(CascadingValue<FloatSpot>.ChildContent), (RenderFragment)RenderButton);
        builder.CloseComponent();
    }

    private void RenderButton(RenderTreeBuilder builder)
    {
        if (ShowLabel)
        {
            builder.OpenComponent<Button>(0);
            builder.AddComponentParameter(1, nameof(Button.StartIcon), (IconData?)Icon);
            builder.AddComponentParameter(2, nameof(Button.ChildContent), (RenderFragment)(b => b.AddContent(0, Label)));
        }
        else
        {
            builder.OpenComponent<IconButton>(0);
            builder.AddComponentParameter(1, nameof(IconButton.Icon), Icon);
            builder.AddComponentParameter(2, nameof(IconButton.Label), Label);
        }

        builder.AddComponentParameter(3, nameof(ButtonBase.Accent), Accent);
        builder.AddComponentParameter(4, nameof(ButtonBase.Size), Size);
        builder.AddComponentParameter(5, nameof(ButtonBase.Disabled), Disabled);
        if (Href is not null)
        {
            builder.AddComponentParameter(6, nameof(Button.Href), Href);
        }

        if (Submit)
        {
            builder.AddComponentParameter(7, nameof(Button.Submit), true);
        }

        if (Name is not null)
        {
            builder.AddComponentParameter(8, nameof(Button.Name), Name);
        }

        if (Value is not null)
        {
            builder.AddComponentParameter(9, nameof(Button.Value), Value);
        }

        if (FormAction is not null)
        {
            builder.AddComponentParameter(10, nameof(Button.FormAction), FormAction);
        }

        if (Form is not null)
        {
            builder.AddComponentParameter(11, nameof(Button.Form), Form);
        }

        if (OnClick.HasDelegate)
        {
            builder.AddComponentParameter(12, nameof(Button.OnClick), OnClick);
        }

        builder.AddComponentParameter(13, nameof(Class), Class);
        builder.AddComponentParameter(14, nameof(Style), Style);
        builder.AddComponentParameter(15, nameof(HiddenFrom), HiddenFrom);
        builder.AddComponentParameter(16, nameof(VisibleFrom), VisibleFrom);
        builder.AddMultipleAttributes(17, AdditionalAttributes);
        builder.CloseComponent();
    }
}

/// <summary>Where the <see cref="FloatingButton"/> around a button puts it.</summary>
internal sealed record FloatSpot(FloatPosition Position);
