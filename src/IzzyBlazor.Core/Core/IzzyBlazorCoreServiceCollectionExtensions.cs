using IzzyBlazor;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers IzzyBlazor.Core's services.</summary>
public static class IzzyBlazorCoreServiceCollectionExtensions
{
    /// <summary>
    /// Adds the services that work in any render mode: <see cref="ToastService"/> and <see cref="DialogService"/>. Call this in a
    /// WebAssembly (<c>.Client</c>) project; server projects call <c>AddIzzyBlazor()</c>, which
    /// includes it. Components that need no services work without either.
    /// </summary>
    public static IServiceCollection AddIzzyBlazorCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (!services.Any(d => d.ServiceType == typeof(ToastService)))
        {
            services.AddScoped<ToastService>();
        }

        if (!services.Any(d => d.ServiceType == typeof(DialogService)))
        {
            services.AddScoped<DialogService>();
        }

        if (!services.Any(d => d.ServiceType == typeof(StyleNonce)))
        {
            services.AddScoped<StyleNonce>();
        }

        return services;
    }
}
