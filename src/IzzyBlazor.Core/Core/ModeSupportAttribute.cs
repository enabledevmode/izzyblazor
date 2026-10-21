namespace IzzyBlazor;

/// <summary>
/// How well a component, or one of its parameters, works in each of the three ways a page can run:
/// with JavaScript off, on a static page with IzzyBlazor's script, and on an interactive page. It
/// changes nothing at run time; the documentation site and tools read it.
/// </summary>
/// <example>
/// <code>
/// [ModeSupport(Off = ModeLevel.Partial, Notes = "Without script it's a native select.")]
/// public partial class Combobox&lt;TValue&gt; { }
///
/// [ModeSupport(Off = ModeLevel.None, Static = ModeLevel.None)]
/// [Parameter] public EventCallback OnClick { get; set; }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class ModeSupportAttribute : Attribute
{
    /// <summary>With JavaScript off. Defaults to <see cref="ModeLevel.Full"/>.</summary>
    public ModeLevel Off { get; set; } = ModeLevel.Full;

    /// <summary>On a static page, with IzzyBlazor's script running. Defaults to <see cref="ModeLevel.Full"/>.</summary>
    public ModeLevel Static { get; set; } = ModeLevel.Full;

    /// <summary>On an interactive page (Server, WebAssembly or Auto). Defaults to <see cref="ModeLevel.Full"/>.</summary>
    public ModeLevel Live { get; set; } = ModeLevel.Full;

    /// <summary>What's missing where support is partial or absent, in a sentence.</summary>
    public string? Notes { get; set; }
}

/// <summary>How much of a component or parameter works in a mode (see <see cref="ModeSupportAttribute"/>).</summary>
public enum ModeLevel
{
    /// <summary>Everything works.</summary>
    Full,

    /// <summary>It works, with less: see the notes.</summary>
    Partial,

    /// <summary>It does nothing in this mode.</summary>
    None,
}
