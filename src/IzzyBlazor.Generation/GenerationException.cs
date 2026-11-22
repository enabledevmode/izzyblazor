namespace IzzyBlazor.Generation;

/// <summary>An item or its settings can't be generated: a bad manifest, an unknown setting, a value out of range.</summary>
public class GenerationException(string message) : Exception(message);

/// <summary>A Pro item, or a Pro choice of a free one, asked of an engine without the Pro catalog.</summary>
public sealed class ProRequiredException(string message) : GenerationException(message);

/// <summary>The item comes with a free account (<see cref="ItemTier.Account"/>), and its files aren't loaded.</summary>
public sealed class AccountRequiredException(string message) : GenerationException(message);
