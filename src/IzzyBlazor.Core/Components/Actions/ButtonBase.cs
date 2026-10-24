using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// Shared look and content for <see cref="Button"/> and <see cref="IconButton"/>.
/// </summary>
public abstract class ButtonBase : LayoutItemBase, IDisposable
{
    /// <summary>Creates the button.</summary>
    protected ButtonBase() => ToolbarPart = new ToolbarMember(this);

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

    /// <summary>The label.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>The <see cref="ButtonGroup"/> this button is in, whose settings are its defaults.</summary>
    [CascadingParameter] internal ButtonGroup? Group { get; set; }

    /// <summary>Where it is in a <see cref="Toolbar"/>, if it's in one.</summary>
    [CascadingParameter] internal ToolbarScope? ToolbarScope { get; set; }

    /// <summary>Its part in a toolbar: a unit of its own at the top of a region, or reporting to the unit it's in.</summary>
    private protected ToolbarMember ToolbarPart { get; }

    /// <summary>
    /// Whether it draws a toolbar unit's wrapper (Button and IconButton do), so it can move into
    /// the "…" menu on its own; others count only inside a ButtonGroup or ToolbarItem.
    /// </summary>
    private protected virtual bool WrapsInToolbar => false;

    /// <summary>What it becomes in a toolbar's "…" menu (with <paramref name="text"/> in place of its label), or null when it can't go there.</summary>
    private protected virtual RenderFragment? ToolbarMenuItem(string? text) => null;

    /// <summary>
    /// Whether its other attributes are only names and ids, which a menu copy can do without. One
    /// wired to something by its attributes (a popover's trigger, an event handler) can't be copied.
    /// </summary>
    private protected bool CopiesToMenu => AdditionalAttributes is null
        || AdditionalAttributes.Keys.All(k => k is "id" or "title" || k.StartsWith("aria-", StringComparison.Ordinal));

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

        // After the parameters are set and before the render (which waits for the batch), so a
        // button that's a toolbar unit draws its wrapper.
        ToolbarPart.Update(ToolbarScope, new ToolbarEntry(ToolbarMenuItem(ToolbarScope?.MenuText), EstimateWidth()), WrapsInToolbar);
        return task;
    }

    /// <summary>The label as plain text, for the toolbar's width estimate; null when it isn't plain text.</summary>
    private protected virtual string? LabelText => ChildContent is null ? null : Code.ReadText(ChildContent);

    /// <summary>Whether a link adds the external-link icon after its text.</summary>
    private protected virtual bool ExternalIcon => false;

    /// <summary>
    /// The width in pixels (at a 16px root), from the size's padding and icon (button.css), and the
    /// label in Inter (<see cref="TextWidths"/>). Null when the label isn't plain text.
    /// </summary>
    private double? EstimateWidth()
    {
        var size = EffectiveSize;
        var (height, padding, icon, font) = size switch
        {
            ControlSize.Sm => (32.0, 12.0, 16.0, 13.0),
            ControlSize.Lg => (44.0, 20.0, 24.0, 16.0),
            _ => (36.0, 16.0, 20.0, 14.0),
        };
        if (IconOnly)
        {
            return height;
        }

        var text = LabelText;
        if (ChildContent is not null && text is null)
        {
            return null;
        }

        var parts = new List<double>();
        if (Loading || StartIcon is not null)
        {
            parts.Add(icon);
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            parts.Add(TextWidths.Of(text, size));
        }

        if (EndIcon is not null)
        {
            parts.Add(icon);
        }
        else if (ExternalIcon)
        {
            parts.Add(font < 16 ? 12 : 14);
        }

        // Padding and the 1px border on each side, and a gap of half the font size between parts.
        return 2 * padding + 2 + parts.Sum() + Math.Max(0, parts.Count - 1) * font / 2;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        ToolbarPart.Dispose();
        GC.SuppressFinalize(this);
    }

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
