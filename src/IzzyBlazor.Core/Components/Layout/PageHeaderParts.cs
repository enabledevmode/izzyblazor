namespace IzzyBlazor;

/// <summary>
/// A picture before a <see cref="PageHeader"/>'s title on a record's page, such as a contact's
/// <see cref="Avatar"/> or a company's logo. Write it directly in the header; it's centered on the
/// title and description, and sits over them on a phone.
/// </summary>
/// <example>
/// <code>
/// &lt;PageHeaderMedia&gt;
///     &lt;Avatar Name="Olivia Chen" Size="AvatarSize.Lg" Decorative /&gt;
/// &lt;/PageHeaderMedia&gt;
/// </code>
/// </example>
public sealed class PageHeaderMedia : ComponentPart
{
    internal override Type HostType => typeof(PageHeader);
}

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
