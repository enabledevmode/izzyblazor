namespace IzzyBlazor;

/// <summary>What a <see cref="Menu"/> shares with its items.</summary>
/// <param name="Id">The menu panel's id, which button items use to close it.</param>
/// <param name="Inline">The panel is drawn on the page (<see cref="Menu.Inline"/>), so items are plain links and buttons, with no menu roles.</param>
public sealed record MenuContext(string Id, bool Inline = false);
