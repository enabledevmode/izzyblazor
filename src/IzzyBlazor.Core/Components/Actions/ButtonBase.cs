using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// Shared look and content for <see cref="Button"/>, <see cref="LinkButton"/> and
/// <see cref="IconButton"/>.
/// </summary>
public abstract class ButtonBase : IzzyComponentBase
{
    /// <summary>
    /// Primary (the default) for the main action, Neutral for everything else, Danger for
    /// destructive actions. Emphasis comes from <see cref="Variant"/>.
    /// </summary>
    [Parameter] public Accent Accent { get; set; } = Accent.Primary;

    /// <summary>
    /// A custom color in place of <see cref="Accent"/>, such as <c>Color.Token("brand-teal")</c>
    /// or <c>Color.Of("#0d9488")</c>. Text on it is chosen for contrast: the token's <c>on-</c>
    /// partner, or black or white for a hex color.
    /// </summary>
    [Parameter] public Color? Color { get; set; }

    /// <summary>Visual weight. Defaults to <see cref="ButtonVariant.Solid"/>.</summary>
    [Parameter] public ButtonVariant Variant { get; set; } = ButtonVariant.Solid;

    /// <summary>Height. Defaults to <see cref="ControlSize.Md"/>.</summary>
    [Parameter] public ControlSize Size { get; set; } = ControlSize.Md;

    /// <summary>An icon before the text.</summary>
    [Parameter] public IconData? StartIcon { get; set; }

    /// <summary>An icon after the text.</summary>
    [Parameter] public IconData? EndIcon { get; set; }

    /// <summary>Stretches to the full width of its container.</summary>
    [Parameter] public bool FullWidth { get; set; }

    /// <summary>Shows a spinner in place of the start icon and blocks further clicks.</summary>
    [Parameter] public bool Loading { get; set; }

    /// <summary>Read to screen readers while <see cref="Loading"/>. Defaults to "Loading".</summary>
    [Parameter] public string? LoadingLabel { get; set; }

    /// <summary>Disables the button.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>The label.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>The <see cref="ButtonGroup"/> this button is in, whose settings are its defaults.</summary>
    [CascadingParameter] public ButtonGroup? Group { get; set; }

    private bool _accentSet;
    private bool _variantSet;
    private bool _sizeSet;

    /// <summary>The accent in effect: this button's, else its group's, else Primary.</summary>
    protected Accent EffectiveAccent => !_accentSet && Group?.Accent is { } accent ? accent : Accent;

    /// <summary>The variant in effect: this button's, else its group's, else Solid.</summary>
    protected ButtonVariant EffectiveVariant => !_variantSet && Group?.Variant is { } variant ? variant : Variant;

    /// <summary>The size in effect: this button's, else its group's, else Md.</summary>
    protected ControlSize EffectiveSize => !_sizeSet && Group?.Size is { } size ? size : Size;

    /// <summary>Whether the content is a single icon with no visible text.</summary>
    protected virtual bool IconOnly => false;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // A group's settings apply only where the button doesn't set its own, so note which it sets.
        _accentSet = parameters.TryGetValue<Accent>(nameof(Accent), out _);
        _variantSet = parameters.TryGetValue<ButtonVariant>(nameof(Variant), out _);
        _sizeSet = parameters.TryGetValue<ControlSize>(nameof(Size), out _);
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add("ib-btn");
        classes.Add($"ib-btn-{Kebab(EffectiveAccent.ToString())}");
        classes.Add($"ib-btn-{Kebab(EffectiveVariant.ToString())}");
        classes.Add($"ib-btn-{Kebab(EffectiveSize.ToString())}", EffectiveSize != ControlSize.Md);
        classes.Add("ib-btn-icon-only", IconOnly);
        classes.Add("ib-btn-full", FullWidth);
        classes.Add("ib-btn-loading", Loading);
        if (Color is { } custom)
        {
            ColorOverride.Apply(styles, custom);
        }
    }

    /// <summary>Renders the start icon (or spinner), the label and the end icon.</summary>
    protected void RenderContent(RenderTreeBuilder builder)
    {
        if (Loading)
        {
            builder.AddContent(0, IconFragment(IconSet.Current.Get(IconRole.Loading), spin: true));
            builder.OpenElement(6, "span");
            builder.AddAttribute(7, "class", "ib-visually-hidden");
            builder.AddContent(8, LoadingLabel ?? TextDefaults.Loading);
            builder.CloseElement();
        }
        else if (StartIcon is { } start)
        {
            builder.AddContent(1, IconFragment(start, spin: false));
        }

        if (ChildContent is not null)
        {
            builder.OpenElement(2, "span");
            builder.AddAttribute(3, "class", "ib-btn-label");
            builder.AddContent(4, ChildContent);
            builder.CloseElement();
        }

        if (EndIcon is { } end && !IconOnly)
        {
            builder.AddContent(5, IconFragment(end, spin: false));
        }
    }

    private static RenderFragment IconFragment(IconData icon, bool spin) => builder =>
    {
        builder.OpenComponent<Icon>(0);
        builder.AddComponentParameter(1, nameof(Icon.Name), (IconData?)icon);
        builder.AddComponentParameter(2, nameof(Icon.Spin), spin);
        builder.CloseComponent();
    };

    private static string Kebab(string name) =>
        string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) ? "-" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
}
