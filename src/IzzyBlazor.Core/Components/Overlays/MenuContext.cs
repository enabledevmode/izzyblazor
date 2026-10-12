namespace IzzyBlazor;

/// <summary>What a <see cref="Menu"/> shares with its items.</summary>
/// <param name="Id">The menu panel's id, which button items use to close it.</param>
public sealed record MenuContext(string Id);
