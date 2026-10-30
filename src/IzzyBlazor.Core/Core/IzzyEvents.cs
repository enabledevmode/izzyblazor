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

    /// <summary>
    /// How many changes the browser has reported for this element, this one included. The
    /// component renders the last it has handled, so the script can tell a render that left
    /// before the user's latest change from one that has caught up. Set by IzzyBlazor's script;
    /// apps don't need it.
    /// </summary>
    public int Seq { get; set; }
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

/// <summary>
/// Data for the <c>ibmove</c> event, which IzzyBlazor's script raises when an item is dragged to a
/// new place in a reorderable list on an interactive page. The list applies it; apps use the
/// list's <c>OnMove</c> and <c>@bind-Items</c>.
/// </summary>
public sealed class IzzyMoveEventArgs : EventArgs
{
    /// <summary>The dragged item's key.</summary>
    public string? Key { get; set; }

    /// <summary>Where it was dropped: its index once it's there, among the items of its zone.</summary>
    public int ToIndex { get; set; }

    /// <summary>The zone it was dropped in, for a drop zone group; null in a single list.</summary>
    public string? ToZone { get; set; }

    /// <summary>
    /// How many drops the browser has reported for this list, this one included. The list renders
    /// the last it has handled, and the script holds the dragged item in place until that render
    /// arrives, so items don't jump back before moving. Set by IzzyBlazor's script.
    /// </summary>
    public int Seq { get; set; }
}

/// <summary>
/// Data for the <c>iblayout</c> event, which IzzyBlazor's script raises when a <c>DataGrid</c>'s
/// columns are rearranged on an interactive page. Apps use its <c>@bind-Layout</c>.
/// </summary>
public sealed class IzzyLayoutEventArgs : EventArgs
{
    /// <summary>The layout, as <see cref="DataGridLayout.ToJson"/> writes it; empty for none.</summary>
    public string? Layout { get; set; }
}

/// <summary>Registers IzzyBlazor's custom events with the Razor compiler.</summary>
[EventHandler("onibtoggle", typeof(IzzyToggleEventArgs), enableStopPropagation: true, enablePreventDefault: false)]
[EventHandler("onibmove", typeof(IzzyMoveEventArgs), enableStopPropagation: true, enablePreventDefault: false)]
[EventHandler("oniblayout", typeof(IzzyLayoutEventArgs), enableStopPropagation: true, enablePreventDefault: false)]
[EventHandler("onibscheme", typeof(IzzySchemeEventArgs), enableStopPropagation: true, enablePreventDefault: false)]
public static class EventHandlers
{
}
