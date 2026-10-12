using IzzyBlazor;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers IzzyBlazor's services for a server app.</summary>
public static class IzzyBlazorServiceCollectionExtensions
{
    /// <summary>
    /// Adds IzzyBlazor's services: <see cref="ToastService"/>, plus the server pieces that use the
    /// request: toasts carried over a redirect (in a short-lived cookie) and AppSidebar's remembered
    /// collapsed state. In a WebAssembly (<c>.Client</c>) project, call <c>AddIzzyBlazorCore()</c> instead.
    /// </summary>
    public static IServiceCollection AddIzzyBlazor(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddIzzyBlazorCore();
        services.AddHttpContextAccessor();
        services.TryAddScoped<IToastCarrier, CookieToastCarrier>();
        services.TryAddScoped<IRequestCookies, HttpRequestCookies>();
        return services;
    }
}
