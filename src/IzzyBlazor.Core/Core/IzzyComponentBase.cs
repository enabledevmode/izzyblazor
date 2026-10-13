using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// Base class for every IzzyBlazor component: user classes and styles, pass-through
/// attributes, and render-mode awareness.
/// </summary>
/// <remarks>
/// Components never set a render mode. They render complete HTML on every render
/// and check <see cref="IsInteractive"/> only to decide whether C# events can fire.
/// </remarks>
public abstract partial class IzzyComponentBase : ComponentBase
{
    /// <summary>Extra CSS classes, merged after the component's own classes.</summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>Inline style, appended after the component's generated custom properties.</summary>
    [Parameter]
    public string? Style { get; set; }

    /// <summary>
    /// Any other attributes (<c>id</c>, <c>data-*</c>, <c>aria-*</c>, event handlers), passed to the root element.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>Hides the component from this breakpoint up, such as a menu button only phones need.</summary>
    [Parameter]
    public Breakpoint? HiddenFrom { get; set; }

    /// <summary>Shows the component only from this breakpoint up, such as header links for wide screens.</summary>
    [Parameter]
    public Breakpoint? VisibleFrom { get; set; }

    /// <summary>
    /// Whether this render can run C# event handlers. <c>false</c> under static SSR,
    /// where behavior must come from HTML, CSS or IzzyBlazor's custom elements.
    /// </summary>
    protected bool IsInteractive => RendererInfo.IsInteractive;

    [Inject] private IServiceProvider IzzyServices { get; set; } = default!;

    /// <summary>
    /// The default text for labels and messages: the app's registered <see cref="IzzyText"/>, or
    /// English. A component's own text parameters win over it.
    /// </summary>
    protected IzzyText TextDefaults => IzzyServices.GetService(typeof(IzzyText)) as IzzyText ?? IzzyText.English;

    /// <summary>
    /// Adds the component's own classes and custom properties. Called on every render.
    /// Overrides add their base class first, then call <c>base.BuildAttributes</c>.
    /// </summary>
    protected virtual void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
    }

    /// <summary>
    /// The root element's <c>class</c> and <c>style</c> values: the component's own, then
    /// the user's <see cref="Class"/> and <see cref="Style"/>. Write them after
    /// <see cref="AdditionalAttributes"/> so the component's values win.
    /// </summary>
    protected (string? Class, string? Style) BuildRootAttributes()
    {
        var classes = new ClassBuilder();
        var styles = new StyleBuilder();
        BuildAttributes(classes, styles);
        if (VisibilityOnRoot)
        {
            classes.Add(VisibilityClasses);
        }

        classes.Add(Class);
        styles.AddRaw(Style);
        return (classes.Build(), styles.Build());
    }

    /// <summary>
    /// Whether <see cref="BuildRootAttributes"/> adds the <see cref="HiddenFrom"/> and
    /// <see cref="VisibleFrom"/> classes. Components whose <see cref="Class"/> goes on an inner
    /// element (such as a menu's panel) return false and put <see cref="VisibilityClasses"/>
    /// on their outer element themselves.
    /// </summary>
    protected virtual bool VisibilityOnRoot => true;

    /// <summary>The classes for <see cref="HiddenFrom"/> and <see cref="VisibleFrom"/>, or null.</summary>
    protected string? VisibilityClasses
    {
        get
        {
            var hidden = HiddenFrom is { } h ? $"ib-hidden-from-{h.Name()}" : null;
            var visible = VisibleFrom is { } v ? $"ib-visible-from-{v.Name()}" : null;
            return hidden is null ? visible : visible is null ? hidden : $"{hidden} {visible}";
        }
    }

    /// <summary>Checks a tag name passed to an <c>As</c> parameter.</summary>
    protected static string ValidateTag(string tag) =>
        TagName().IsMatch(tag)
            ? tag
            : throw new ArgumentException($"\"{tag}\" is not a valid element name for As.");

    /// <summary>
    /// For each value in a responsive parameter, adds the modifier class (<c>ib-px</c>,
    /// <c>ib-px-md</c>...) and its custom property (<c>--ib-px</c>, <c>--ib-px-md</c>...).
    /// </summary>
    /// <remarks>
    /// Classes are added only for values that are set, so a value given only at
    /// <c>Md</c> leaves the component's default in place below 768px.
    /// </remarks>
    protected static void ApplyResponsive<T>(
        ClassBuilder classes,
        StyleBuilder styles,
        Responsive<T> value,
        string cssClass,
        string varName,
        Func<T, string> map)
    {
        foreach (var (breakpoint, _) in value.Entries)
        {
            classes.Add(breakpoint is { } bp ? $"{cssClass}-{bp.Name()}" : cssClass);
        }

        styles.AddVars(varName, value, map);
    }

    [GeneratedRegex("^[a-z][a-z0-9-]*$")]
    private static partial Regex TagName();
}
