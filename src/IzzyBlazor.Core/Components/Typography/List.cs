using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>What a <see cref="List"/> shares with its items.</summary>
/// <param name="Icon">The icon every item shows in place of a bullet, if any.</param>
/// <param name="IconColor">The icons' color.</param>
public sealed record ListContext(IconData? Icon, Color? IconColor);

/// <summary>
/// A list of <see cref="ListItem"/>s you write out: bulleted, numbered or with icons, spaced with
/// <see cref="Gap"/>, or rows with a muted line and actions at the end, such as a settings page.
/// <c>&lt;List Icon="Lucide.Check" IconColor="Color.SuccessText"&gt;</c> makes a check list. For
/// items that come from data, use <see cref="ListView{TItem}"/>.
/// </summary>
/// <example>
/// <code>
/// &lt;List Icon="Lucide.Check" IconColor="Color.SuccessText"&gt;
///     &lt;ListItem&gt;Unlimited projects&lt;/ListItem&gt;
///     &lt;ListItem&gt;Priority support&lt;/ListItem&gt;
/// &lt;/List&gt;
///
/// &lt;List Ordered&gt;
///     &lt;ListItem&gt;Install the package.&lt;/ListItem&gt;
///     &lt;ListItem&gt;Add IzzyTheme to App.razor.&lt;/ListItem&gt;
/// &lt;/List&gt;
///
/// &lt;List Plain Dividers Gap="4"&gt;
///     &lt;ListItem Icon="Lucide.KeyRound" Description="Last changed 3 months ago."&gt;
///         Password
///         &lt;ListItemActions&gt;
///             &lt;Button Href="/account/password" Variant="ButtonVariant.Outline"&gt;Change&lt;/Button&gt;
///         &lt;/ListItemActions&gt;
///     &lt;/ListItem&gt;
/// &lt;/List&gt;
/// </code>
/// </example>
/// <accessibility>
/// <list type="bullet">
/// <item>It renders a <c>ul</c>, or an <c>ol</c> when <see cref="Ordered"/>, of <c>li</c> items, so screen readers announce a list and its count.</item>
/// <item>Lists without bullets (<see cref="Plain"/> or an <see cref="Icon"/>) keep <c>role="list"</c>, which Safari otherwise drops from a list with no markers.</item>
/// <item>Item icons are decorative and hidden from screen readers, so the item's text must carry the meaning, such as "Included" or "Not included".</item>
/// <item>An item's actions follow its text in reading order; name each so it makes sense beside the item's text, such as "Change" after "Password".</item>
/// </list>
/// </accessibility>
/// <seealso cref="ListView{TItem}"/>
/// <seealso cref="DescriptionList"/>
/// <seealso cref="Prose"/>
/// <seealso cref="Timeline"/>
[ModeSupport]
public class List : Box
{
    /// <summary>Creates a list, rendered as a <c>ul</c>.</summary>
    public List() => As = "ul";

    /// <summary>A numbered list (<c>ol</c>).</summary>
    [Parameter] public bool Ordered { get; set; }

    /// <summary>No bullets or numbers, and no indent.</summary>
    [Parameter] public bool Plain { get; set; }

    /// <summary>An icon in place of each bullet, such as <c>Lucide.Check</c>. An item's own icon wins.</summary>
    [Parameter] public IconData? Icon { get; set; }

    /// <summary>The icons' color. Defaults to the text color.</summary>
    [Parameter] public Color? IconColor { get; set; }

    /// <summary>Space between items, on the spacing scale. Defaults to 2 (8px).</summary>
    [Parameter] public Responsive<int> Gap { get; set; } = 2;

    /// <summary>
    /// A line between items, in the middle of the <see cref="Gap"/>, as in a list of settings.
    /// </summary>
    [Parameter] public bool Dividers { get; set; }

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
        classes.Add("ib-list-dividers", Dividers);
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

/// <summary>
/// An item in a <see cref="List"/>: a <c>li</c>, with the list's icon or its own. With a
/// <see cref="Description"/> or <see cref="ListItemActions"/>, it's a row: the icon, the text with
/// a muted line under it, and the actions at the end, which wrap under the text when it's narrow.
/// </summary>
public class ListItem : IzzyComponentBase
{
    /// <summary>The content: the item's text, and any <see cref="ListItemActions"/>.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>An icon for this item, in place of the list's.</summary>
    [Parameter] public IconData? Icon { get; set; }

    /// <summary>This item's icon color, in place of the list's.</summary>
    [Parameter] public Color? IconColor { get; set; }

    /// <summary>A muted line under the item's text: its state or what it does.</summary>
    [Parameter] public string? Description { get; set; }

    /// <summary>The list this item belongs to.</summary>
    [CascadingParameter] public ListContext? List { get; set; }

    [CascadingParameter] private PartScope? OuterScope { get; set; }

    private PartScope _scope = null!;

    private IconData? ResolvedIcon => Icon ?? List?.Icon;

    /// <inheritdoc />
    protected override void OnInitialized() => _scope = new PartScope(this, OuterScope, StateHasChanged);

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add("ib-list-item");
        classes.Add("ib-list-item-icon", ResolvedIcon is not null);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var actions = _scope.Shown<ListItemActions>();
        var row = Description is not null || actions is not null;
        var (cssClass, style) = BuildRootAttributes();
        builder.OpenElement(0, "li");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "class", row ? $"{cssClass} ib-list-item-row" : cssClass);
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
        }

        var wrap = row || ResolvedIcon is not null;
        if (wrap)
        {
            builder.OpenElement(8, "div");
            builder.AddAttribute(9, "class", "ib-list-content");
        }

        builder.OpenComponent<CascadingValue<PartScope>>(10);
        builder.AddComponentParameter(11, "Value", _scope);
        builder.AddComponentParameter(12, "IsFixed", true);
        builder.AddComponentParameter(13, "ChildContent", ChildContent);
        builder.CloseComponent();
        if (Description is not null)
        {
            builder.OpenElement(14, "p");
            builder.AddAttribute(15, "class", "ib-list-description");
            builder.AddContent(16, Description);
            builder.CloseElement();
        }

        if (wrap)
        {
            builder.CloseElement();
        }

        if (actions is not null)
        {
            builder.OpenElement(17, "div");
            builder.AddAttribute(18, "class", "ib-list-actions");
            builder.AddContent(19, _scope.Outlet(actions));
            builder.CloseElement();
        }

        builder.CloseElement();
    }
}

/// <summary>
/// Buttons, links, a switch or a badge at the end of a <see cref="ListItem"/>. Write it directly in
/// the item, after its text; it's drawn at the item's end and wraps under the text when it's
/// narrow.
/// </summary>
/// <example>
/// <code>
/// &lt;ListItemActions&gt;
///     &lt;Button Href="/account/password" Variant="ButtonVariant.Outline"&gt;Change&lt;/Button&gt;
/// &lt;/ListItemActions&gt;
/// </code>
/// </example>
public sealed class ListItemActions : ComponentPart
{
    internal override Type HostType => typeof(ListItem);
}
