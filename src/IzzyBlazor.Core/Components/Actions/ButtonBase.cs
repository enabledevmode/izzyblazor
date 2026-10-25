using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace IzzyBlazor;

/// <summary>
/// Shared look and content for <see cref="Button"/> and <see cref="IconButton"/>.
/// </summary>
public abstract class ButtonBase : LayoutItemBase
{
    /// <summary>
    /// Primary (the default) for the main action, Neutral for everything else, Danger for
    /// destructive actions, or a custom color (<c>Accent.Of("#0d9488")</c>,
    /// <c>Accent.Token("brand")</c>); text on a custom color is chosen for contrast. Emphasis
    /// comes from <see cref="Variant"/>.
    /// </summary>
    [Parameter] public Accent Accent { get; set; } = Accent.Primary;

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

    /// <summary>
    /// Makes it a toggle button, such as Bold in an editor: true shows it pressed, false not, and
    /// screen readers hear which (<c>aria-pressed</c>). With <c>@bind-Pressed</c> a click turns it
    /// on and off on interactive pages; on static pages the server decides, as with a submit button's
    /// <c>Name</c> and <c>Value</c>. Null (the default) is an ordinary button. Not for links.
    /// </summary>
    [Parameter] public bool? Pressed { get; set; }

    /// <summary>Raised with the new state when a click toggles <see cref="Pressed"/>, for <c>@bind-Pressed</c>. Interactive pages only.</summary>
    [Parameter, ModeSupport(Off = ModeLevel.None, Static = ModeLevel.None)]
    public EventCallback<bool> PressedChanged { get; set; }

    /// <summary>The label.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>The <see cref="ButtonGroup"/> this button is in, whose settings are its defaults.</summary>
    [CascadingParameter] internal ButtonGroup? Group { get; set; }

    /// <summary>The <see cref="Toolbar"/> it's directly in, where only toolbar buttons go.</summary>
    [CascadingParameter] internal ToolbarContext? Toolbar { get; set; }

    private bool _accentSet;
    private bool _variantSet;
    private bool _sizeSet;

    /// <summary>The accent in effect: this button's, else its group's, else Primary.</summary>
    protected Accent EffectiveAccent => !_accentSet && Group?.DefaultAccent is { } accent ? accent : Accent ?? Accent.Primary;

    /// <summary>The variant in effect: this button's, else its group's, else Solid.</summary>
    protected ButtonVariant EffectiveVariant => !_variantSet && Group?.DefaultVariant is { } variant ? variant : Variant;

    /// <summary>The size in effect: this button's, else its group's, else Md.</summary>
    protected ControlSize EffectiveSize => !_sizeSet && Group?.DefaultSize is { } size ? size : Size;

    /// <summary>Whether the content is a single icon with no visible text.</summary>
    protected virtual bool IconOnly => false;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // A group's settings apply only where the button doesn't set its own, so note which it sets.
        _accentSet = parameters.TryGetValue<Accent>(nameof(Accent), out _);
        _variantSet = parameters.TryGetValue<ButtonVariant>(nameof(Variant), out _);
        _sizeSet = parameters.TryGetValue<ControlSize>(nameof(Size), out _);
        var task = base.SetParametersAsync(parameters);
        ToolbarControl.Refuse(this, Toolbar, Group);
        return task;
    }

    /// <summary>The label as plain text, for the toolbar's width estimate; null when it isn't plain text.</summary>
    private protected virtual string? LabelText => ChildContent is null ? null : Code.ReadText(ChildContent);

    /// <summary>The <c>aria-pressed</c> value: null unless it's a toggle.</summary>
    private protected string? AriaPressed => Pressed is { } pressed ? (pressed ? "true" : "false") : null;

    /// <summary>Whether a click does anything in C#: toggles <see cref="Pressed"/> or runs <paramref name="onClick"/>.</summary>
    private protected bool Clickable(EventCallback<MouseEventArgs> onClick) => onClick.HasDelegate || (Pressed is not null && PressedChanged.HasDelegate);

    /// <summary>A click: toggles <see cref="Pressed"/>, then runs <paramref name="onClick"/>.</summary>
    private protected EventCallback<MouseEventArgs> Click(EventCallback<MouseEventArgs> onClick) =>
        Pressed is not null && PressedChanged.HasDelegate
            ? EventCallback.Factory.Create<MouseEventArgs>(this, async e =>
            {
                await PressedChanged.InvokeAsync(!Pressed.Value);
                await onClick.InvokeAsync(e);
            })
            : onClick;

    /// <summary>Refuses <see cref="Pressed"/> on a link, which has no pressed state.</summary>
    private protected void RefusePressedLink(string component)
    {
        if (Pressed is not null)
        {
            throw new InvalidOperationException($"{component} with Href is a link, which can't be pressed. Leave out Href for a toggle button.");
        }
    }

    /// <summary>Whether a link adds the external-link icon after its text.</summary>
    private protected virtual bool ExternalIcon => false;

    /// <summary>
    /// The width in pixels (at a 16px root), for a toolbar, from the size's padding and icon
    /// (button.css) and the label in Inter. Null when the label isn't plain text.
    /// </summary>
    private protected double? EstimateWidth() =>
        ButtonWidth.Estimate(EffectiveSize, IconOnly, ChildContent is not null, LabelText, Loading || StartIcon is not null, EndIcon is not null, ExternalIcon);

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add("ib-btn");
        base.BuildAttributes(classes, styles);

        // A custom color paints over the primary accent's custom properties.
        var accent = EffectiveAccent;
        classes.Add($"ib-btn-{accent.Role ?? "primary"}");
        classes.Add($"ib-btn-{Kebab(EffectiveVariant.ToString())}");
        classes.Add($"ib-btn-{Kebab(EffectiveSize.ToString())}", EffectiveSize != ControlSize.Md);
        classes.Add("ib-btn-icon-only", IconOnly);
        classes.Add("ib-btn-full", FullWidth);
        classes.Add("ib-btn-loading", Loading);
        classes.Add("ib-btn-toggle", Pressed is not null);
        if (accent.Custom is { } custom)
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
