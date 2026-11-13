namespace IzzyBlazor;

/// <summary>
/// Buttons, links or a status at the end of a <see cref="PageHeader"/>, such as "New invoice" or a
/// badge. Write it directly in the header; it's drawn after the title and wraps under it on
/// narrow screens.
/// </summary>
/// <example>
/// <code>
/// &lt;PageHeaderActions&gt;
///     &lt;Button Href="/invoices/new"&gt;New invoice&lt;/Button&gt;
/// &lt;/PageHeaderActions&gt;
/// </code>
/// </example>
public sealed class PageHeaderActions : ComponentPart
{
    internal override Type HostType => typeof(PageHeader);
}
