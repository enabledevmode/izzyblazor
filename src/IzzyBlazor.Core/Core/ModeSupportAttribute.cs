namespace IzzyBlazor;

/// <summary>
/// How well a component, or one of its parameters or methods, works in each of the two ways a page
/// can run: <see cref="Static"/> (static server-side rendering, with Blazor's and IzzyBlazor's
/// scripts) and <see cref="Interactive"/> (Server, WebAssembly or Auto). It also records what still
/// works with neither script (<see cref="NoScript"/>): the plain HTML every page starts as, before
/// the scripts load or while an interactive page prerenders. It changes nothing at run time; the
/// documentation site and tools read it. It isn't inherited: a component derived from another
/// (DialogClose from Button) states its own.
/// </summary>
/// <example>
/// <code>
/// [ModeSupport(NoScript = ModeLevel.Partial, NoScriptNotes = "Without script it's a native select.")]
/// public partial class Combobox&lt;TValue&gt; { }
///
/// [ModeSupport(Static = ModeLevel.None, NoScript = ModeLevel.None)]
/// [Parameter] public EventCallback OnClick { get; set; }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class ModeSupportAttribute : Attribute
{
    /// <summary>On a static page (no circuit, no WebAssembly), with Blazor's and IzzyBlazor's scripts. Defaults to <see cref="ModeLevel.Full"/>.</summary>
    public ModeLevel Static { get; set; } = ModeLevel.Full;

    /// <summary>On an interactive page (Server, WebAssembly or Auto). Defaults to <see cref="ModeLevel.Full"/>.</summary>
    public ModeLevel Interactive { get; set; } = ModeLevel.Full;

    /// <summary>
    /// With neither script: the plain HTML a page shows before its scripts load, if they fail to,
    /// or with JavaScript off. Not a mode the documentation offers; it keeps the fallback checked.
    /// Defaults to <see cref="ModeLevel.Full"/>.
    /// </summary>
    public ModeLevel NoScript { get; set; } = ModeLevel.Full;

    /// <summary>What's missing in <see cref="Static"/> or <see cref="Interactive"/>, in a sentence.</summary>
    public string? Notes { get; set; }

    /// <summary>What's missing with no script (<see cref="NoScript"/>), in a sentence.</summary>
    public string? NoScriptNotes { get; set; }
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
