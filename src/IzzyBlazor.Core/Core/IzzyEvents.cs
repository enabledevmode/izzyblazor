using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// Data for the <c>ibtoggle</c> event, which IzzyBlazor's script raises when a disclosure,
/// popover or dialog opens or closes in the browser.
/// </summary>
/// <remarks>
/// Opening and closing always happens in the browser, in every render mode. On interactive
/// pages this event tells C# what happened, so <c>@bind-Open</c> stays in step with the page.
/// </remarks>
public sealed class IzzyToggleEventArgs : EventArgs
{
    /// <summary>The id of the element that opened or closed.</summary>
    public string? Id { get; set; }

    /// <summary>Whether it is now open.</summary>
    public bool Open { get; set; }
}

/// <summary>
/// Data for the <c>ibscheme</c> event, which IzzyBlazor's script raises when a ColorSchemeSwitch
/// changes the page's color scheme, so <c>@bind-Value</c> follows on interactive pages.
/// </summary>
public sealed class IzzySchemeEventArgs : EventArgs
{
    /// <summary>The new scheme: <c>system</c>, <c>light</c> or <c>dark</c>.</summary>
    public string? Scheme { get; set; }
}

/// <summary>Registers IzzyBlazor's custom events with the Razor compiler.</summary>
[EventHandler("onibtoggle", typeof(IzzyToggleEventArgs), enableStopPropagation: true, enablePreventDefault: false)]
[EventHandler("onibscheme", typeof(IzzySchemeEventArgs), enableStopPropagation: true, enablePreventDefault: false)]
public static class EventHandlers
{
}
