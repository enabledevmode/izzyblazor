namespace IzzyBlazor.Generation;

/// <summary>An item or its settings can't be generated: a bad manifest, an unknown setting, a value out of range.</summary>
public sealed class GenerationException(string message) : Exception(message);
