namespace IzzyBlazor;

/// <summary>
/// Names the parameters that matter most when setting a component up, so tools show them first: an
/// editor's properties form opens on them, and the rest wait behind "more". It changes nothing at run
/// time; the documentation site and tools read it. Required parameters (<c>[EditorRequired]</c>) count
/// without being named and come first; then those a shared base names (<c>FieldBase</c>: <c>Label</c> and
/// <c>Hint</c>); then the component's own, in the order written. Eight in all at most. Every component
/// with options of its own states its choice, even none (<c>[ImportantParameters]</c>). It isn't
/// inherited from a component's own bases: a component derived from another (DialogClose from Button)
/// states its own.
/// </summary>
/// <example>
/// <code>
/// [ImportantParameters(nameof(Columns), nameof(Layout), nameof(ConfirmLeave))]
/// public class Form&lt;TModel&gt; { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ImportantParametersAttribute(params string[] names) : Attribute
{
    /// <summary>The parameters' names, most important first.</summary>
    public IReadOnlyList<string> Names { get; } = names;
}
