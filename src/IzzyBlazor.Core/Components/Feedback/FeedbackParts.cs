using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// Links or buttons after a <see cref="Banner"/>'s message, such as "Details". Write it directly in
/// the banner; the banner draws it after the message.
/// </summary>
/// <example><code>&lt;Banner Status="Status.Warning"&gt;Maintenance on Sunday. &lt;BannerActions&gt;&lt;Anchor Href="/status"&gt;Details&lt;/Anchor&gt;&lt;/BannerActions&gt;&lt;/Banner&gt;</code></example>
public sealed class BannerActions : ComponentPart
{
    internal override Type HostType => typeof(Banner);
}

/// <summary>
/// The buttons or links that fill an <see cref="EmptyState"/>, such as "New invoice" or "Clear
/// filters". Write it directly in the empty state; it's drawn last.
/// </summary>
/// <example><code>&lt;EmptyStateActions&gt;&lt;Button Href="/invoices/new"&gt;New invoice&lt;/Button&gt;&lt;/EmptyStateActions&gt;</code></example>
public sealed class EmptyStateActions : ComponentPart
{
    internal override Type HostType => typeof(EmptyState);
}
