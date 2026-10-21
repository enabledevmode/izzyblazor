using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Routing;

namespace IzzyBlazor;

/// <summary>
/// A heading. <see cref="Level"/> sets the element (<c>h1</c> to <c>h6</c>) and a default
/// size; <c>Size</c> overrides the look without changing the document structure.
/// </summary>
/// <example>
/// <code>
/// &lt;Heading Level="1"&gt;Build a multi-step wizard&lt;/Heading&gt;
/// &lt;Heading Level="2" Size="new() { Base = TextSize.Xl2, Md = TextSize.Xl4 }"&gt;...&lt;/Heading&gt;
/// </code>
/// </example>
/// <remarks>
/// Defaults: bold, tight line height, balanced wrapping, no margins. Every default can be
/// overridden with the typography props.
/// <para>
/// In an <see cref="AppMain"/> on a page with an <see cref="OnThisPage"/>, a heading gets an id
/// from its text (unless it sets one) and is listed there; headings at the levels it lists also
/// become links to themselves, with a "#" shown on hover, so a section can be linked.
/// </para>
/// </remarks>
[ModeSupport]
public class Heading : TextBase, IDisposable
{
    /// <summary>The heading level, 1 to 6. Defaults to 2.</summary>
    [Parameter] public int Level { get; set; } = 2;

    /// <summary>
    /// Lists the heading in the page's <see cref="OnThisPage"/> (when its level is one it lists).
    /// Defaults to true; false leaves it out and gives it no anchor link.
    /// </summary>
    [Parameter] public bool Outline { get; set; } = true;

    /// <summary>
    /// Makes the heading a link to itself, with a "#" on hover, so a section can be linked. By
    /// default, headings listed in an <see cref="OnThisPage"/> are; true adds the link to any heading
    /// with an id, false removes it. A heading whose content holds a link or button of its own
    /// (an <c>a</c>, <c>button</c>, <see cref="IzzyBlazor.Anchor"/> or button component) never gets
    /// one, since links can't nest. A heading holding a component that isn't one of IzzyBlazor's
    /// plain ones (<see cref="Icon"/>, <see cref="Badge"/>, <see cref="Kbd"/>, <see cref="TextBlock"/>)
    /// gets one only with true, since it might render a link. Set it as <c>Anchor="true"</c>:
    /// the attribute needs a value.
    /// </summary>
    [Parameter] public bool? Anchor { get; set; }

    [CascadingParameter(Name = PageOutline.MainCascade)] private PageOutline? PageOutline { get; set; }

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private OutlineEntry? _entry;
    private PageOutline? _subscribed;
    private bool _hasControl;
    private bool _mayHaveControl;
    private bool _watchingLocation;

    /// <inheritdoc />
    protected override string BaseClass => "ib-heading";

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Level is < 1 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(Level), Level, "Heading Level must be 1 to 6.");
        }

        As = $"h{Level}";

        var content = IzzyBlazor.PageOutline.ReadText(ChildContent);
        var text = content.Text;
        _hasControl = content.HasControl;
        _mayHaveControl = content.MayHaveControl;
        var outline = Outline ? PageOutline : null;
        if (_subscribed != outline)
        {
            Unregister();
            if (outline is not null)
            {
                outline.ReadersChanged += OnReadersChanged;
                _subscribed = outline;
            }
        }

        if (outline is null)
        {
            return;
        }

        if (_entry is null)
        {
            _entry = outline.Add(Level, text, ExplicitId, OnReadersChanged);
        }
        else
        {
            outline.Update(_entry, Level, text, ExplicitId);
        }
    }

    private string? ExplicitId => AdditionalAttributes?.TryGetValue("id", out var id) == true ? Convert.ToString(id, System.Globalization.CultureInfo.InvariantCulture) : null;

    // The id the heading renders with: its own, or the outline's once something reads the outline.
    private string? ElementId => ExplicitId ?? (_entry is not null && _subscribed!.Active ? _entry.Id : null);

    private bool Anchored(string? id) =>
        id is not null && !_hasControl && (Anchor ?? (!_mayHaveControl && _entry is not null && _subscribed!.Active && _subscribed.Lists(Level)));

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cssClass, style) = BuildRootAttributes();
        var id = ElementId;
        var anchored = Anchored(id);
        if (anchored && !_watchingLocation)
        {
            // The link carries the page's path and query, which change on interactive pages.
            Navigation.LocationChanged += OnLocationChanged;
            _watchingLocation = true;
        }

        builder.OpenElement(0, ValidateTag(As));
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "id", id);
        builder.AddAttribute(3, "data-ib-outline", !Outline && id is not null ? "false" : null);
        builder.AddAttribute(4, "class", cssClass);
        builder.AddAttribute(5, "style", style);
        if (anchored)
        {
            builder.OpenElement(6, "a");
            builder.AddAttribute(7, "class", "ib-heading-anchor");
            builder.AddAttribute(8, "href", PageLinks.ToFragment(Navigation, id!));
            builder.AddContent(9, ChildContent);
            builder.CloseElement();
        }
        else
        {
            builder.AddContent(10, ChildContent);
        }

        builder.CloseElement();
    }

    private void OnReadersChanged() => _ = InvokeAsync(StateHasChanged);

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => _ = InvokeAsync(StateHasChanged);

    private void Unregister()
    {
        if (_subscribed is null)
        {
            return;
        }

        _subscribed.ReadersChanged -= OnReadersChanged;
        if (_entry is not null)
        {
            _subscribed.Remove(_entry);
            _entry = null;
        }

        _subscribed = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Unregister();
        if (_watchingLocation)
        {
            Navigation.LocationChanged -= OnLocationChanged;
        }

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    protected override Responsive<TextSize> EffectiveSize => Size.IsEmpty ? DefaultSize(Level) : Size;

    private static TextSize DefaultSize(int level) => level switch
    {
        1 => TextSize.Xl4,
        2 => TextSize.Xl3,
        3 => TextSize.Xl2,
        4 => TextSize.Xl,
        5 => TextSize.Lg,
        _ => TextSize.Md,
    };
}
