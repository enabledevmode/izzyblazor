namespace IzzyBlazor;

/// <summary>
/// Content under the links in a <see cref="SiteNav"/>'s panel, shown below its breakpoint only:
/// social links, a color scheme switch, a call to action. Write it directly in the SiteNav.
/// </summary>
/// <example>
/// <code>
/// &lt;SiteNavFooter&gt;
///     &lt;Button Href="/signup" FullWidth&gt;Sign up&lt;/Button&gt;
/// &lt;/SiteNavFooter&gt;
/// </code>
/// </example>
public sealed class SiteNavFooter : ComponentPart
{
    internal override Type HostType => typeof(SiteNav);
}

/// <summary>
/// Content above the links in a <see cref="SiteNav"/>'s panel, shown below its breakpoint only: what
/// the header keeps for wide screens, such as a version tag or social links. In an
/// <see cref="AppShell"/> with a sidebar, it shows at the top of the sidebar instead, above the links.
/// Write it directly in the SiteNav.
/// </summary>
/// <example>
/// <code>
/// &lt;SiteNavHeader&gt;
///     &lt;Badge Variant="BadgeVariant.Outline" Pill&gt;v2.1&lt;/Badge&gt;
/// &lt;/SiteNavHeader&gt;
/// </code>
/// </example>
public sealed class SiteNavHeader : ComponentPart
{
    internal override Type HostType => typeof(SiteNav);
}
