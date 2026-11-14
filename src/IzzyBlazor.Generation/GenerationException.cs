namespace IzzyBlazor.Generation;

/// <summary>An item or its settings can't be generated: a bad manifest, an unknown setting, a value out of range.</summary>
public class GenerationException(string message) : Exception(message);

/// <summary>A Pro item, or a Pro choice of a free one, asked of an engine without the Pro catalogue.</summary>
public sealed class ProRequiredException(string message) : GenerationException(message);
