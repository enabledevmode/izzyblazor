using IzzyBlazor.Site.Components.Parts;
using IzzyBlazor.Site.Services;
using Microsoft.AspNetCore.Components.Endpoints;

namespace IzzyBlazor.Site.Components;

/// <summary>
/// Marks a page that has the Off · Static · Live switch (ModeSwitch): its examples render in the
/// picked mode, and in Off, App sends it no Blazor and no IzzyBlazor script. Every other page
/// renders its examples in Static and keeps its script, so picking Off for the examples doesn't
/// turn the rest of the site's script off on pages with no switch to turn it back on. A site test
/// checks that a page has a switch exactly when it follows one.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class FollowsModeSwitchAttribute : Attribute
{
    /// <summary>
    /// The mode a request's page shows its examples in: the switch's (<see cref="ModeSwitch.Current(HttpContext?)"/>)
    /// on a page that follows it, else Static. A component page follows it when it has previews
    /// (<see cref="Docs.ComponentPage.FollowsSwitch"/>): examples of its own, in the page or, for a
    /// whole page such as an AppShell, in a frame, or demos its hand-written Overview shows.
    /// </summary>
    public static string PageMode(HttpContext? context, Registry registry)
    {
        var page = context?.GetEndpoint()?.Metadata.GetMetadata<ComponentTypeMetadata>()?.Type;
        if (page is null || !page.IsDefined(typeof(FollowsModeSwitchAttribute), inherit: false))
        {
            return "static";
        }

        if (page == typeof(Docs.ComponentPage))
        {
            var component = registry.ById(context!.GetRouteValue("Id") as string ?? "");
            if (component is null || !Docs.ComponentPage.FollowsSwitch(component))
            {
                return "static";
            }
        }

        return ModeSwitch.Current(context);
    }
}
