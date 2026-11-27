using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.Http;

namespace IzzyBlazor;

/// <summary>
/// Gives <see cref="IdGenerator"/> a counter for each request that renders a page statically, so
/// the page's ids are the same every time it renders. Not for a request that a script makes for a
/// part of a page (the <c>X-Izzy-Part</c> header), since the part joins a page whose count may
/// differ, nor for an interactive render (a circuit's hub has no page).
/// </summary>
internal static class StaticRenderIds
{
    // Reads the same request as any other accessor: HttpContextAccessor holds it per async flow.
    private static readonly HttpContextAccessor Http = new();
    private static readonly object Key = new();

    public const string PartHeader = "X-Izzy-Part";

    public static IdCounter? Current()
    {
        if (Http.HttpContext is not { } http
            || http.GetEndpoint()?.Metadata.GetMetadata<ComponentTypeMetadata>() is null
            || http.Request.Headers.ContainsKey(PartHeader))
        {
            return null;
        }

        if (http.Items.TryGetValue(Key, out var found) && found is IdCounter counter)
        {
            return counter;
        }

        counter = new IdCounter();
        http.Items[Key] = counter;
        return counter;
    }
}
