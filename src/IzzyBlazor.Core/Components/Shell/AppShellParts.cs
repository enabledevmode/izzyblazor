using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

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
    }

    private RenderFragment? Content => Outline is null ? ChildContent : builder =>
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
/// An optional side column of an <see cref="AppShell"/>, such as a table of contents, on the
/// side opposite the sidebar. Its content stays in view while the page scrolls.
/// </summary>
public class AppAside : AppShellPart
{
    /// <summary>The width. Defaults to 15rem.</summary>
    [Parameter] public Length Width { get; set; } = Length.Rem(15);

    /// <summary>Shown from this breakpoint up. Defaults to <see cref="Breakpoint.Lg"/>.</summary>
    [Parameter] public Breakpoint Breakpoint { get; set; } = Breakpoint.Lg;

    /// <inheritdoc />
    protected override string Area => "aside";

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        base.BuildAttributes(classes, styles);
        classes.Add($"ib-shell-aside-bp-{Breakpoint.Name()}");
        styles.AddVar("aside-w", StyleValues.Length(Width));
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cssClass, style) = BuildRootAttributes();
        builder.OpenElement(0, "aside");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "class", cssClass);
        builder.AddAttribute(3, "style", style);
        builder.OpenElement(4, "div");
        builder.AddAttribute(5, "class", "ib-shell-aside-inner");
        builder.AddContent(6, ChildContent);
        builder.CloseElement();
        builder.CloseElement();
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
