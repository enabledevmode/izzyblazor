using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// A region of a component, written among the component's content and drawn by the component in
/// its own place, such as <see cref="CardHeader"/> or <see cref="SidebarFooter"/>. Because it's
/// drawn at the component's level, a form or cascading value it's written inside doesn't reach its
/// content. (Parts drawn where they're written, such as <see cref="DialogFooter"/>, are ordinary
/// components instead.)
/// </summary>
public abstract class ComponentPart : ComponentBase, IDisposable
{
    private PartScope? _scope;
    private bool _added;

    private protected ComponentPart()
    {
    }

    /// <summary>The part's content.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    [CascadingParameter] private PartScope? Scope { get; set; }

    /// <summary>The component that draws this part.</summary>
    internal abstract Type HostType { get; }

    /// <inheritdoc />
    protected sealed override void OnParametersSet()
    {
        if (!_added)
        {
            var host = HostType.Name;
            _scope = Scope?.Find(HostType)
                ?? throw new InvalidOperationException($"{GetType().Name} must be inside {("AEIOU".Contains(host[0]) ? "an" : "a")} {host}.");
            _added = true;
            _scope.Add(this);
        }
        else
        {
            _scope!.Changed(this);
        }
    }

    /// <inheritdoc />
    protected sealed override void BuildRenderTree(RenderTreeBuilder builder)
    {
        // Drawn by the host, not here.
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_added)
        {
            _scope!.Remove(this);
        }

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// The parts a host component holds, cascaded to its content. Scopes chain to the one above, so a
/// part finds its own host past others: a <see cref="DialogHeader"/> in an accordion in a dialog.
/// </summary>
internal sealed class PartScope
{
    private readonly object? _host;
    private readonly Action _render;
    private readonly List<ComponentPart> _parts = [];
    private readonly Dictionary<ComponentPart, PartOutlet> _outlets = [];

    /// <summary>Creates a host's scope. <paramref name="render"/> re-renders the host.</summary>
    public PartScope(IComponent host, PartScope? parent, Action render)
    {
        _host = host;
        Parent = parent;
        _render = render;
    }

    private PartScope()
    {
        _render = () => { };
    }

    /// <summary>
    /// Takes any part and draws none: for content rendered a second time, such as a SiteNav's links
    /// in the sidebar, so its parts aren't added twice or refused.
    /// </summary>
    public static PartScope Discard { get; } = new();

    public PartScope? Parent { get; }

    private bool Discards => _host is null;

    public PartScope? Find(Type hostType)
    {
        for (var scope = this; scope is not null; scope = scope.Parent)
        {
            if (scope.Discards || hostType.IsInstanceOfType(scope._host))
            {
                return scope;
            }
        }

        return null;
    }

    // Adding or removing a part re-renders the host; a change to a part's content re-renders only
    // its outlet, so the host's content (which holds the part) isn't rendered again.
    public void Add(ComponentPart part)
    {
        if (Discards)
        {
            return;
        }

        _parts.Add(part);
        _render();
    }

    public void Remove(ComponentPart part)
    {
        _outlets.Remove(part);
        if (_parts.Remove(part))
        {
            _render();
        }
    }

    public void Changed(ComponentPart part)
    {
        if (_outlets.TryGetValue(part, out var outlet))
        {
            outlet.Refresh();
        }
    }

    /// <summary>
    /// The part of this kind to draw: the last. While one replaces another (@if/else), the new one is
    /// added before the old one is removed, so both are briefly listed.
    /// </summary>
    public T? Shown<T>() where T : ComponentPart => (T?)_parts.FindLast(p => p is T);

    /// <summary>Draws a part's content. Keyed by the part, so a part swapped in gets its own outlet.</summary>
    public RenderFragment Outlet(ComponentPart part) => builder =>
    {
        builder.OpenComponent<PartOutlet>(0);
        builder.SetKey(part);
        builder.AddComponentParameter(1, nameof(PartOutlet.Content), (Func<RenderFragment?>)(() => part.ChildContent));
        builder.AddComponentReferenceCapture(2, outlet => _outlets[part] = (PartOutlet)outlet);
        builder.CloseComponent();
    };
}

/// <summary>Draws a part's content where its host puts it; re-rendered alone when the content changes.</summary>
internal sealed class PartOutlet : ComponentBase
{
    [Parameter] public Func<RenderFragment?>? Content { get; set; }

    public void Refresh() => StateHasChanged();

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, Content?.Invoke());
}
