using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// Pinned to the top of an <see cref="AppSidebar"/>, above the scrolling content: a brand or
/// workspace switcher. In the drawer it shares the top bar with the close button. In the icon rail
/// an <see cref="AppBrand"/>'s name hides by itself; mark other text with
/// <c>class="ib-collapsed-hide"</c>. Write it directly in the sidebar.
/// </summary>
/// <example><code>&lt;SidebarHeader&gt;&lt;AppBrand Href="/" Icon="Lucide.Zap" Label="Acme" /&gt;&lt;/SidebarHeader&gt;</code></example>
public sealed class SidebarHeader : ComponentPart
{
    internal override Type HostType => typeof(AppSidebar);
}

/// <summary>
/// Pinned to the bottom of an <see cref="AppSidebar"/>, below the scrolling content: an account menu
/// or settings. Write it directly in the sidebar.
/// </summary>
/// <example><code>&lt;SidebarFooter&gt;&lt;Menu ...&gt;...&lt;/Menu&gt;&lt;/SidebarFooter&gt;</code></example>
public sealed class SidebarFooter : ComponentPart
{
    internal override Type HostType => typeof(AppSidebar);
}

/// <summary>
/// An <see cref="AppSidebar"/>'s scrolling content with layout props: a column with <c>Gap</c>,
/// <c>Align</c> and <c>Justify</c>, like <see cref="Stack"/>. Optional: content written straight in
/// the sidebar is its body too.
/// </summary>
/// <example><code>&lt;SidebarBody Gap="4"&gt;&lt;NavMenu&gt;...&lt;/NavMenu&gt;&lt;NavMenu&gt;...&lt;/NavMenu&gt;&lt;/SidebarBody&gt;</code></example>
public class SidebarBody : FlexBase
{
    /// <inheritdoc />
    protected override Responsive<Direction> LayoutDirection => default;

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        base.BuildAttributes(classes, styles);
        classes.Add("ib-shell-sidebar-content");
    }
}
