namespace IzzyBlazor;

/// <summary>
/// Names a parameter a component inherits but doesn't take, so the documentation site and tools
/// leave it off the component's pages. The component still has to refuse it at run time (Heading
/// throws when given <c>As</c>, since <c>Level</c> sets its element); this only records that it does.
/// </summary>
/// <example>
/// <code>
/// [RefusesParameter(nameof(As))]
/// public class Heading : TextBase { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class RefusesParameterAttribute(string name) : Attribute
{
    /// <summary>The inherited parameter's name.</summary>
    public string Name { get; } = name;
}
