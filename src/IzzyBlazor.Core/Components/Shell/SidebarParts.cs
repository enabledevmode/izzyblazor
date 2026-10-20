using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>Where a <see cref="SidebarPart"/> is drawn.</summary>
internal enum SidebarPartKind
{
    Header,
    Footer,
}

/// <summary>What an <see cref="AppSidebar"/> cascades to its parts.</summary>
internal sealed class SidebarContext(AppSidebar sidebar)
{
    public AppSidebar Sidebar { get; } = sidebar;
}

/// <summary>
/// A part of an <see cref="AppSidebar"/> that the sidebar draws in its own place, wherever it's
/// written directly in the sidebar. It's drawn at the sidebar's level, so a form or cascading value
/// it's written inside doesn't reach its content.
/// </summary>
public abstract class SidebarPart : ComponentBase, IDisposable
{
    private bool _added;

    private protected SidebarPart()
    {
    }

    /// <summary>The part's content.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    [CascadingParameter] private SidebarContext? Context { get; set; }

    internal abstract SidebarPartKind Kind { get; }

    /// <inheritdoc />
    protected sealed override void OnParametersSet()
    {
        if (Context is null)
        {
            throw new InvalidOperationException($"{GetType().Name} must be inside an AppSidebar.");
        }

        if (_added)
        {
            Context.Sidebar.PartChanged(this);
        }
        else
        {
            _added = true;
            Context.Sidebar.AddPart(this);
        }
    }

    /// <inheritdoc />
    protected sealed override void BuildRenderTree(RenderTreeBuilder builder)
    {
        // Drawn by the sidebar, not here.
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_added)
        {
            Context?.Sidebar.RemovePart(this);
        }

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Pinned to the top of an <see cref="AppSidebar"/>, above the scrolling content: a brand or
/// workspace switcher. In the drawer it shares the top bar with the close button. In the icon rail
/// an <see cref="AppBrand"/>'s name hides by itself; mark other text with
/// <c>class="ib-collapsed-hide"</c>. Write it directly in the sidebar.
/// </summary>
/// <example><code>&lt;SidebarHeader&gt;&lt;AppBrand Href="/" Icon="Lucide.Zap" Label="Acme" /&gt;&lt;/SidebarHeader&gt;</code></example>
public sealed class SidebarHeader : SidebarPart
{
    internal override SidebarPartKind Kind => SidebarPartKind.Header;
}

/// <summary>
/// Pinned to the bottom of an <see cref="AppSidebar"/>, below the scrolling content: an account menu
/// or settings. Write it directly in the sidebar.
/// </summary>
/// <example><code>&lt;SidebarFooter&gt;&lt;Menu ...&gt;...&lt;/Menu&gt;&lt;/SidebarFooter&gt;</code></example>
public sealed class SidebarFooter : SidebarPart
{
    internal override SidebarPartKind Kind => SidebarPartKind.Footer;
}

/// <summary>Draws a part's content where its host puts it; re-rendered alone when the content changes.</summary>
internal sealed class PartOutlet : ComponentBase
{
    [Parameter] public Func<RenderFragment?>? Content { get; set; }

    public void Refresh() => StateHasChanged();

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, Content?.Invoke());
}
