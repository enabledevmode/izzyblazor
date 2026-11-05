using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Sections;

namespace IzzyBlazor;

/// <summary>What an <see cref="AppShell"/> shares with its parts.</summary>
/// <param name="MainId">The main area's id, which the skip link targets.</param>
/// <param name="ContentWidth">The shell's <see cref="AppShell.ContentWidth"/>, if set.</param>
/// <param name="ScrollMode">The shell's <see cref="AppShell.ScrollMode"/>.</param>
public sealed record AppShellContext(string MainId, Length? ContentWidth = null, ShellScroll ScrollMode = ShellScroll.Page);

/// <summary>
/// Carries a <see cref="SiteNav"/>'s links to the shell's <see cref="AppSidebar"/>, which shows
/// them at its top below the SiteNav's breakpoint, so a phone has one menu, not two.
/// </summary>
internal sealed class ShellHeaderLinks
{
    /// <summary>The links, or null when the shell has no <see cref="SiteNav"/>.</summary>
    public RenderFragment? Links { get; private set; }

    /// <summary>Below this width the links show in the sidebar.</summary>
    public Breakpoint Breakpoint { get; private set; } = Breakpoint.Md;

    /// <summary>Raised when the links change, so the sidebar renders them.</summary>
    public event Action? Changed;

    internal void Set(RenderFragment? links, Breakpoint breakpoint)
    {
        Links = links;
        Breakpoint = breakpoint;
        Changed?.Invoke();
    }
}

/// <summary>
/// Carries the shell's <see cref="AppBrand"/> to its <see cref="AppSidebar"/>, whose drawer shows it
/// in its top row when the sidebar has no <see cref="SidebarHeader"/>.
/// </summary>
internal sealed class ShellBrand
{
    /// <summary>The first brand in the shell, or null.</summary>
    public AppBrand? Brand { get; private set; }

    /// <summary>Raised when the brand changes, so the sidebar renders it.</summary>
    public event Action? Changed;

    internal void Set(AppBrand brand)
    {
        // Only a new brand is news. The brand calls this on every render, and the sidebar re-renders
        // on Changed: a brand in the sidebar's own header would otherwise render it forever.
        if (Brand is not null)
        {
            return;
        }

        Brand = brand;
        Changed?.Invoke();
    }

    internal void Clear(AppBrand brand)
    {
        if (Brand == brand)
        {
            Brand = null;
            Changed?.Invoke();
        }
    }
}

/// <summary>
/// Where an <see cref="AppAside"/> written in a page goes: <see cref="AppMain"/> cascades it to the
/// page and renders its outlet just after the main area, so the page's aside takes the shell's
/// aside column.
/// </summary>
internal sealed class ShellAsideSlot
{
    /// <summary>The section the page's aside is rendered into.</summary>
    public object SectionId { get; } = new();
}

/// <summary>Shared plumbing for the parts of an <see cref="AppShell"/>.</summary>
public abstract class AppShellPart : IzzyComponentBase
{
    /// <summary>
    /// The background, such as <c>Color.Fill</c>. Text switches to the color's
    /// <c>on-</c> partner when it has one. Defaults to <c>Color.Surface</c>.
    /// </summary>
    [Parameter] public Color? Bg { get; set; }

    /// <summary>
    /// The color scheme of this part: <see cref="IzzyBlazor.ColorScheme.Dark"/> for a dark sidebar or
    /// header in a light app, <see cref="IzzyBlazor.ColorScheme.Inverse"/> for the opposite of the page.
    /// Everything inside, including its menus, follows it. Defaults to the page's scheme.
    /// </summary>
    [Parameter] public ColorScheme ColorScheme { get; set; } = ColorScheme.Auto;

    /// <summary>The content.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>The shell this part belongs to.</summary>
    [CascadingParameter] protected AppShellContext? Shell { get; set; }

    /// <summary>The part's name in custom properties: <c>header</c>, <c>sidebar</c>...</summary>
    protected abstract string Area { get; }

    /// <summary>
    /// Centers the part's content at <paramref name="width"/> (overriding the shell's
    /// <see cref="AppShell.ContentWidth"/>) by widening its side padding.
    /// </summary>
    protected static void AddContentWidth(ClassBuilder classes, StyleBuilder styles, Length? width)
    {
        if (width is { } w)
        {
            classes.Add("ib-shell-part-measured");
            styles.AddVar("shell-content-w", StyleValues.Length(w));
        }
    }

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add($"ib-shell-{Area}");
        if (ColorScheme.ClassName() is { } scheme)
        {
            classes.Add(scheme);
        }

        if (Bg is { } bg)
        {
            styles.AddVar($"shell-{Area}-bg", bg.Css);
            if (bg.ContrastText is { } text)
            {
                styles.AddVar($"shell-{Area}-fg", text.Css);
            }
        }
    }
}

/// <summary>The top bar of an <see cref="AppShell"/>: brand, <see cref="SidebarToggle"/>, actions.</summary>
public class AppHeader : AppShellPart
{
    /// <summary>Whether it stays at the top while the page scrolls. Defaults to <see cref="BarPosition.Sticky"/>.</summary>
    [Parameter] public BarPosition Position { get; set; } = BarPosition.Sticky;

    /// <summary>
    /// The width the header's content is centered at, such as <c>Length.Rem(64)</c>; the bar itself
    /// stays full width. Overrides the shell's <see cref="AppShell.ContentWidth"/>.
    /// </summary>
    [Parameter] public Length? ContentWidth { get; set; }

    /// <inheritdoc />
    protected override string Area => "header";

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        base.BuildAttributes(classes, styles);
        classes.Add("ib-shell-header-sticky", Position == BarPosition.Sticky);
        AddContentWidth(classes, styles, ContentWidth);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cssClass, style) = BuildRootAttributes();
        builder.OpenElement(0, "header");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "class", cssClass);
        builder.AddAttribute(3, "style", style);
        builder.AddContent(4, ChildContent);
        builder.CloseElement();
    }
}

/// <summary>The main content of an <see cref="AppShell"/>, usually <c>@Body</c>. The skip link targets it.</summary>
public class AppMain : AppShellPart
{
    /// <summary>
    /// The element id. Defaults to the shell's <see cref="AppShell.MainId"/>; inside a shell, set
    /// it there instead (a different one throws), so the skip link keeps pointing here.
    /// </summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>
    /// The width the content is centered at, such as <c>Length.Prose</c>, with at least the
    /// page gutter at the sides. Overrides the shell's <see cref="AppShell.ContentWidth"/>.
    /// A <see cref="Section"/> inside still spans the whole main area.
    /// </summary>
    [Parameter] public Length? ContentWidth { get; set; }

    /// <inheritdoc />
    protected override string Area => "main";

    // The shell's page outline, passed on to the headings in the main area only.
    [CascadingParameter] private PageOutline? Outline { get; set; }

    // Where an AppAside in the page goes; one per main area.
    private readonly ShellAsideSlot _asideSlot = new();

    // The content goes in a centered column (a div inside main), so main itself stays full width
    // and full-bleed sections can measure it as a container.
    private bool Measured => ContentWidth is not null || Shell?.ContentWidth is not null;

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        base.BuildAttributes(classes, styles);
        if (ContentWidth is { } width)
        {
            styles.AddVar("shell-content-w", StyleValues.Length(width));
        }

        classes.Add("ib-shell-main-measured", Measured);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var id = Id ?? (AdditionalAttributes?.TryGetValue("id", out var attribute) == true ? Convert.ToString(attribute, System.Globalization.CultureInfo.InvariantCulture) : null);
        if (id is not null && Shell is not null && id != Shell.MainId)
        {
            throw new InvalidOperationException($"AppMain's Id ({id}) differs from its AppShell's MainId ({Shell.MainId}), which the skip link points to. Set MainId on the AppShell instead.");
        }

        var (cssClass, style) = BuildRootAttributes();
        builder.OpenElement(0, "main");
        builder.AddAttribute(1, "id", Id ?? Shell?.MainId ?? "ib-main");
        // With ScrollMode.Main it scrolls on its own, so the keyboard needs to reach it even when
        // nothing in it takes focus.
        builder.AddAttribute(2, "tabindex", Shell?.ScrollMode == ShellScroll.Main ? "0" : "-1");
        builder.AddMultipleAttributes(3, AdditionalAttributes);
        builder.AddAttribute(4, "class", cssClass);
        builder.AddAttribute(5, "style", style);
        if (Measured)
        {
            builder.OpenElement(6, "div");
            builder.AddAttribute(7, "class", "ib-shell-main-column");
            builder.AddContent(8, Content);
            builder.CloseElement();
        }
        else
        {
            builder.AddContent(9, Content);
        }

        builder.CloseElement();

        // An AppAside in the page renders here, a sibling of main inside the shell, so it takes the
        // aside column and comes after the main area in reading order.
        if (Shell is not null)
        {
            builder.OpenComponent<SectionOutlet>(10);
            builder.AddComponentParameter(11, nameof(SectionOutlet.SectionId), _asideSlot.SectionId);
            builder.CloseComponent();
        }
    }

    private RenderFragment? Content => Shell is null ? Outlined : builder =>
    {
        builder.OpenComponent<CascadingValue<ShellAsideSlot>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<ShellAsideSlot>.Value), _asideSlot);
        builder.AddComponentParameter(2, nameof(CascadingValue<ShellAsideSlot>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<ShellAsideSlot>.ChildContent), Outlined);
        builder.CloseComponent();
    };

    private RenderFragment? Outlined => Outline is null ? ChildContent : builder =>
    {
        builder.OpenComponent<CascadingValue<PageOutline>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<PageOutline>.Name), PageOutline.MainCascade);
        builder.AddComponentParameter(2, nameof(CascadingValue<PageOutline>.Value), Outline);
        builder.AddComponentParameter(3, nameof(CascadingValue<PageOutline>.IsFixed), true);
        builder.AddComponentParameter(4, nameof(CascadingValue<PageOutline>.ChildContent), ChildContent);
        builder.CloseComponent();
    };
}

/// <summary>
/// An optional side column of an <see cref="AppShell"/>, on the side opposite the sidebar: an
/// outline, or a panel about what the main area shows, such as the selected row's details. Its
/// content stays in view while the page scrolls, and scrolls on its own when it's taller.
/// </summary>
/// <remarks>
/// Put it in the layout for every page, or in a page for that page only: an aside written inside
/// <see cref="AppMain"/> moves to the shell's aside column, and a page without one leaves no column.
/// A page's aside takes the place of the layout's while that page is shown; give the two different
/// <see cref="Id"/>s if both are drawers.
/// </remarks>
[ModeSupport(NoScript = ModeLevel.Partial, NoScriptNotes = "With JavaScript off, a drawer (AsideSmallScreen.Drawer) doesn't close on Escape or move focus, and the scroll box isn't a tab stop.")]
public class AppAside : AppShellPart, IDisposable
{
    /// <summary>The width, such as <c>Length.Px(420)</c> for a detail panel. Defaults to 15rem.</summary>
    [Parameter] public Length Width { get; set; } = Length.Rem(15);

    /// <summary>Shown beside the main area from this breakpoint up. Defaults to <see cref="Breakpoint.Lg"/>.</summary>
    [Parameter] public Breakpoint Breakpoint { get; set; } = Breakpoint.Lg;

    /// <summary>
    /// What it does below <see cref="Breakpoint"/>: <see cref="AsideSmallScreen.Hidden"/> (the default)
    /// hides it, <see cref="AsideSmallScreen.Below"/> puts it after the main area, full width, and
    /// <see cref="AsideSmallScreen.Drawer"/> makes it a drawer opened by an <see cref="AsideToggle"/>.
    /// All three work with JavaScript off.
    /// </summary>
    [Parameter] public AsideSmallScreen SmallScreen { get; set; } = AsideSmallScreen.Hidden;

    /// <summary>
    /// The space inside, on the spacing scale, per breakpoint if you like:
    /// <c>new() { Base = 4, Md = 6 }</c>. Defaults to 4 (1rem).
    /// </summary>
    [Parameter] public Responsive<int> Padding { get; set; }

    /// <summary>
    /// The landmark's name for screen readers, such as "Details". Set it when the page has another
    /// <c>aside</c>, or when what's in it isn't clear from its first words.
    /// </summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>
    /// The element id, which <see cref="AsideToggle"/> and links to the panel point at (an <c>id</c>
    /// attribute sets it too). Defaults to <c>ib-aside</c>.
    /// </summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>The drawer's close button name (<see cref="AsideSmallScreen.Drawer"/>). Defaults to "Close panel".</summary>
    [Parameter] public string? CloseLabel { get; set; }

    [CascadingParameter] private ShellAsideSlot? Slot { get; set; }

    [Inject] private NavigationManager Navigation { get; set; } = null!;

    private bool _subscribed;

    /// <inheritdoc />
    protected override string Area => "aside";

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        base.BuildAttributes(classes, styles);
        ApplyResponsive(classes, styles, Padding, "ib-shell-aside-pad", "asidepad", StyleValues.Spacing);
        // Outside a shell (a page interactive on its own, in a static layout: neither the section
        // nor the shell's cascade crosses into it), it's a block where it's written, at every width.
        if (Shell is null)
        {
            classes.Add("ib-shell-aside-alone");
            return;
        }

        classes.Add($"ib-shell-aside-bp-{Breakpoint.Name()}");
        classes.Add("ib-shell-aside-page", Slot is not null);
        classes.Add(SmallScreen switch
        {
            AsideSmallScreen.Below => "ib-shell-aside-below",
            AsideSmallScreen.Drawer => "ib-shell-aside-drawer",
            _ => "ib-shell-aside-hide",
        });
        styles.AddVar("aside-w", StyleValues.Length(Width));
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // A drawer's close links carry the page's address, kept current as it changes.
        if (SmallScreen == AsideSmallScreen.Drawer && Shell is not null && !_subscribed)
        {
            _subscribed = true;
            Navigation.LocationChanged += OnLocationChanged;
        }

        base.OnParametersSet();
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => _ = InvokeAsync(StateHasChanged);

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        // Written in a page: rendered through AppMain's outlet, into the shell's aside column.
        if (Slot is not null)
        {
            builder.OpenComponent<SectionContent>(0);
            builder.AddComponentParameter(1, nameof(SectionContent.SectionId), Slot.SectionId);
            builder.AddComponentParameter(2, nameof(SectionContent.ChildContent), (RenderFragment)RenderAside);
            builder.CloseComponent();
        }
        else
        {
            RenderAside(builder);
        }
    }

    private void RenderAside(RenderTreeBuilder builder)
    {
        var drawer = SmallScreen == AsideSmallScreen.Drawer && Shell is not null;
        // "#ib-shell-closed" matches no element: it clears :target without scrolling to the top.
        var closed = drawer ? PageLinks.ToFragment(Navigation, "ib-shell-closed") : null;
        var (cssClass, style) = BuildRootAttributes();
        var id = Id ?? "ib-aside";
        if (drawer)
        {
            builder.OpenElement(10, "a");
            builder.AddAttribute(11, "class", "ib-shell-backdrop ib-shell-aside-backdrop");
            builder.AddAttribute(12, "href", closed);
            builder.AddAttribute(13, "tabindex", "-1");
            builder.AddAttribute(14, "aria-hidden", "true");
            builder.CloseElement();
        }

        builder.OpenElement(20, "aside");
        builder.AddMultipleAttributes(21, AdditionalAttributes);
        builder.AddAttribute(22, "id", id);
        if (Label is not null)
        {
            builder.AddAttribute(23, "aria-label", Label);
        }

        builder.AddAttribute(24, "class", cssClass);
        builder.AddAttribute(25, "style", style);
        if (drawer)
        {
            // The drawer's top bar, shown below the breakpoint only.
            builder.OpenElement(30, "div");
            builder.AddAttribute(31, "class", "ib-shell-aside-close");
            builder.OpenElement(32, "a");
            builder.AddAttribute(33, "class", "ib-btn ib-btn-neutral ib-btn-ghost ib-btn-icon-only");
            builder.AddAttribute(34, "href", closed);
            builder.AddAttribute(35, "aria-label", CloseLabel ?? TextDefaults.ClosePanel);
            builder.AddAttribute(37, "data-ib-shell-close", true);
            builder.OpenComponent<Icon>(38);
            builder.AddComponentParameter(39, nameof(Icon.Role), IconRole.Close);
            builder.CloseComponent();
            builder.CloseElement();
            builder.CloseElement();
        }

        builder.OpenElement(40, "div");
        builder.AddAttribute(41, "class", "ib-shell-aside-inner");
        // Headings in a page's aside aren't the page's own: OnThisPage lists the main area's only.
        builder.OpenComponent<CascadingValue<PageOutline>>(42);
        builder.AddComponentParameter(43, nameof(CascadingValue<PageOutline>.Name), PageOutline.MainCascade);
        builder.AddComponentParameter(44, nameof(CascadingValue<PageOutline>.Value), null);
        builder.AddComponentParameter(45, nameof(CascadingValue<PageOutline>.IsFixed), true);
        builder.AddComponentParameter(46, nameof(CascadingValue<PageOutline>.ChildContent), ChildContent);
        builder.CloseComponent();
        builder.CloseElement();
        builder.CloseElement();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_subscribed)
        {
            Navigation.LocationChanged -= OnLocationChanged;
        }
    }
}

/// <summary>The bottom bar of an <see cref="AppShell"/>.</summary>
public class AppFooter : AppShellPart
{
    /// <summary>Whether it stays at the bottom while the page scrolls. Defaults to <see cref="BarPosition.Static"/>.</summary>
    [Parameter] public BarPosition Position { get; set; } = BarPosition.Static;

    /// <summary>
    /// The width the footer's content is centered at; the bar itself stays full width. Overrides
    /// the shell's <see cref="AppShell.ContentWidth"/>.
    /// </summary>
    [Parameter] public Length? ContentWidth { get; set; }

    /// <inheritdoc />
    protected override string Area => "footer";

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        base.BuildAttributes(classes, styles);
        classes.Add("ib-shell-footer-sticky", Position == BarPosition.Sticky);
        AddContentWidth(classes, styles, ContentWidth);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cssClass, style) = BuildRootAttributes();
        builder.OpenElement(0, "footer");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "class", cssClass);
        builder.AddAttribute(3, "style", style);
        builder.AddContent(4, ChildContent);
        builder.CloseElement();
    }
}
