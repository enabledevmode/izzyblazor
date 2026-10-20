namespace IzzyBlazor;

/// <summary>
/// Content under the links in a <see cref="SiteNav"/>'s panel, shown below its breakpoint only:
/// social links, a color scheme switch, a call to action. Write it directly in the SiteNav.
/// </summary>
/// <example><code>&lt;SiteNavFooter&gt;&lt;Button Href="/signup" FullWidth&gt;Sign up&lt;/Button&gt;&lt;/SiteNavFooter&gt;</code></example>
public sealed class SiteNavFooter : ComponentPart
{
    internal override Type HostType => typeof(SiteNav);
}
