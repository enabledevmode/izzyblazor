using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace IzzyBlazor;

/// <summary>
/// One action of a <see cref="SpeedDial"/>: a round icon with its label beside it, or as its
/// tooltip. Like a <see cref="MenuItem"/>, it's a link (<see cref="Href"/>), a form post
/// (<see cref="Submit"/>) or C# (<see cref="OnClick"/>), so it acts on static pages too.
/// </summary>
/// <example><code>&lt;SpeedDialAction Icon="Lucide.FileText" Label="Invoice" Href="invoices/new" /&gt;</code></example>
/// <seealso cref="SpeedDial"/>
[ModeSupport(Notes = "OnClick runs only on interactive pages; Href and Submit work everywhere.")]
public sealed class SpeedDialAction : ComponentBase
{
    /// <summary>The icon, such as <c>Lucide.FileText</c>.</summary>
    [Parameter, EditorRequired] public IconData Icon { get; set; }

    /// <summary>What it does, such as "Invoice": its name, shown beside it or as its tooltip.</summary>
    [Parameter, EditorRequired] public string Label { get; set; } = "";

    /// <summary>Makes it a link to this address.</summary>
    [Parameter] public string? Href { get; set; }

    /// <summary>Makes it a submit button for the form around the speed dial, or the one named by <see cref="Form"/>.</summary>
    [Parameter] public bool Submit { get; set; }

    /// <summary>The name a submit action posts with its <see cref="Value"/>.</summary>
    [Parameter] public string? Name { get; set; }

    /// <summary>The value a submit action posts under its <see cref="Name"/>.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>An address a submit action posts to in place of its form's.</summary>
    [Parameter] public string? FormAction { get; set; }

    /// <summary>The id of the form a submit action belongs to.</summary>
    [Parameter] public string? Form { get; set; }

    /// <summary>Turns it off.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Runs on click on interactive pages (a link still navigates). Has no effect on static pages.</summary>
    [Parameter, ModeSupport(NoScript = ModeLevel.None, Static = ModeLevel.None)]
    public EventCallback<MouseEventArgs> OnClick { get; set; }

    [CascadingParameter] private SpeedDial? Dial { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Dial is null)
        {
            throw new InvalidOperationException("SpeedDialAction goes inside a SpeedDial.");
        }

        if (string.IsNullOrWhiteSpace(Label))
        {
            throw new InvalidOperationException("SpeedDialAction needs a Label: it names the action.");
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<MenuItem>(0);
        builder.AddComponentParameter(1, nameof(MenuItem.Icon), (IconData?)Icon);
        builder.AddComponentParameter(2, nameof(MenuItem.Href), Href);
        builder.AddComponentParameter(3, nameof(MenuItem.Submit), Submit);
        builder.AddComponentParameter(4, nameof(MenuItem.Name), Name);
        builder.AddComponentParameter(5, nameof(MenuItem.Value), Value);
        builder.AddComponentParameter(6, nameof(MenuItem.FormAction), FormAction);
        builder.AddComponentParameter(7, nameof(MenuItem.Form), Form);
        builder.AddComponentParameter(8, nameof(MenuItem.Disabled), Disabled);
        if (OnClick.HasDelegate)
        {
            builder.AddComponentParameter(9, nameof(MenuItem.OnClick), OnClick);
        }

        builder.AddComponentParameter(10, nameof(MenuItem.Class), "ib-sd-action");
        if (Dial?.IconsOnly == true)
        {
            builder.AddComponentParameter(11, "title", Label);
        }

        builder.AddComponentParameter(12, nameof(MenuItem.ChildContent), (RenderFragment)(b => b.AddContent(0, Label)));
        builder.CloseComponent();
    }
}
