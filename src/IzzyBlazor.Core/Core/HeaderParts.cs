using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor;

/// <summary>
/// What a <see cref="Tile"/> or <see cref="Card"/> tells what's in it. For a chart: which of its
/// buttons (<c>reset</c>, <c>zoom</c>, <c>pause</c>) are in its header, as ChartResetZoom,
/// ChartZoom and ChartPause with no <c>For</c>; the chart hides its own of those (they stay, as
/// what a press is passed to) and, when none of its own are left to show, its row of buttons. For
/// a DataGrid or Scheduler: the toolbar parts in its header (<see cref="PartLink"/>).
/// </summary>
internal sealed class HeaderHost
{
    private readonly HashSet<string> _moved = [];
    private readonly Dictionary<Type, PartLink> _parts = [];

    /// <summary>
    /// The id of the tile's or card's title, which the buttons add to their names ("Download as
    /// PNG, Revenue"), so two charts' buttons on a page are told apart. Null without a title.
    /// </summary>
    public string? TitleId { get; set; }
    private readonly List<Action> _charts = [];

    public bool Moved(string kind) => _moved.Contains(kind);

    public void Add(string kind)
    {
        if (_moved.Add(kind))
        {
            // A button drawn after the chart (in a Card's actions, or a later render): it draws again.
            foreach (var render in _charts.ToArray())
            {
                render();
            }
        }
    }

    public void Listen(Action render) => _charts.Add(render);

    public void Unlisten(Action render) => _charts.Remove(render);

    /// <summary>The link between the one component of a kind in it (a DataGrid's <see cref="IDataGridToolbar"/>) and its parts in the header.</summary>
    public PartLink Parts(Type kind) => _parts.TryGetValue(kind, out var link) ? link : _parts[kind] = new PartLink();
}

/// <summary>
/// A component's toolbar parts placed outside it, such as a DataGridSearch in a tile's header:
/// the component they draw from (the first to claim the link), which parts it leaves out of its
/// own toolbar, and the parts to draw again when it draws.
/// </summary>
internal sealed class PartLink
{
    private readonly Dictionary<Type, int> _placed = [];
    private readonly List<Action> _parts = [];
    private Action? _redrawOwner;

    /// <summary>The component the parts draw from, or null until it draws.</summary>
    public object? Target { get; private set; }

    /// <summary>Whether any part is placed outside.</summary>
    public bool HasParts => _placed.Count > 0;

    /// <summary>Whether a part of this type is placed outside, so the component leaves out its own.</summary>
    public bool IsPlaced(Type part) => _placed.ContainsKey(part);

    /// <summary>Takes the link for <paramref name="target"/>, unless another component has it. Its parts draw again.</summary>
    public bool Claim(object target, Action redraw)
    {
        if (Target is not null)
        {
            return ReferenceEquals(Target, target);
        }

        Target = target;
        _redrawOwner = redraw;
        Drew();
        return true;
    }

    /// <summary>Lets go of the link, when the component leaves; its parts draw nothing.</summary>
    public void Release(object target)
    {
        if (ReferenceEquals(Target, target))
        {
            Target = null;
            _redrawOwner = null;
            Drew();
        }
    }

    /// <summary>A part of <paramref name="part"/>'s type is placed outside: the component draws again without its own.</summary>
    public void Place(Type part, Action redraw)
    {
        _parts.Add(redraw);
        _placed[part] = _placed.GetValueOrDefault(part) + 1;
        if (_placed[part] == 1)
        {
            _redrawOwner?.Invoke();
        }
    }

    /// <summary>The part has left: the component draws its own again once none of the type is left.</summary>
    public void Unplace(Type part, Action redraw)
    {
        _parts.Remove(redraw);
        if (_placed.TryGetValue(part, out var count))
        {
            if (count > 1)
            {
                _placed[part] = count - 1;
            }
            else
            {
                _placed.Remove(part);
                _redrawOwner?.Invoke();
            }
        }
    }

    /// <summary>The component drew: its parts outside draw again, with what it now has.</summary>
    public void Drew()
    {
        foreach (var redraw in _parts.ToArray())
        {
            redraw();
        }
    }
}

/// <summary>
/// The page's links from an <c>id</c> to the component that has it, for parts that name their
/// component with <c>For</c>. Registered by <c>AddIzzyBlazorCore()</c>.
/// </summary>
internal sealed class PartLinks
{
    private readonly Dictionary<(Type, string), PartLink> _links = [];

    public PartLink Of(Type kind, string id) => _links.TryGetValue((kind, id), out var link) ? link : _links[(kind, id)] = new PartLink();

    /// <summary>The page's links, or null when the app didn't call <c>AddIzzyBlazor()</c>.</summary>
    public static PartLinks? From(IServiceProvider services) => services.GetService<PartLinks>();

    /// <summary>The link for a part's <c>For</c>; throws, saying what to add, when the app has no links.</summary>
    public static PartLink For(IServiceProvider services, Type kind, string id, string part) =>
        From(services)?.Of(kind, id) ?? throw new InvalidOperationException($"{part} For=\"{id}\" needs IzzyBlazor's services: call builder.Services.AddIzzyBlazor() (AddIzzyBlazorCore() in a WebAssembly project).");

    /// <summary>The <c>id</c> a component was given, from its attributes.</summary>
    public static string? IdOf(IReadOnlyDictionary<string, object>? attributes) =>
        attributes?.TryGetValue("id", out var id) == true && id?.ToString() is { Length: > 0 } text ? text : null;
}

/// <summary>How a toolbar part outside its component draws.</summary>
internal static class OutsideParts
{
    /// <summary>
    /// <paramref name="bar"/> in an element that names its component (<c>data-ib-grid</c> with the
    /// grid's id), for the component's script; with <paramref name="defaults"/>, its buttons look as in
    /// the component's own toolbar. With no <paramref name="id"/>, the part is in the component's
    /// toolbar and needs neither.
    /// </summary>
    public static RenderFragment Wrap(string? id, string attribute, ButtonGroup? defaults, RenderFragment bar)
    {
        if (id is null)
        {
            return bar;
        }

        RenderFragment named = b =>
        {
            b.OpenElement(0, "div");
            b.AddAttribute(1, "class", "ib-part");
            b.AddAttribute(2, attribute, id);
            b.AddContent(3, bar);
            b.CloseElement();
        };
        return defaults is null ? named : b =>
        {
            b.OpenComponent<CascadingValue<ButtonGroup>>(0);
            b.AddComponentParameter(1, nameof(CascadingValue<ButtonGroup>.Value), defaults);
            b.AddComponentParameter(2, nameof(CascadingValue<ButtonGroup>.IsFixed), true);
            b.AddComponentParameter(3, nameof(CascadingValue<ButtonGroup>.ChildContent), named);
            b.CloseComponent();
        };
    }
}
