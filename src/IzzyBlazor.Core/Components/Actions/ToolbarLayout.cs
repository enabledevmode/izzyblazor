using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>Where a toolbar region sits.</summary>
internal enum ToolbarRegion
{
    Start,
    Center,
    End,
}

/// <summary>
/// What a control inside a <see cref="Toolbar"/> knows about where it is. At the top of a region
/// (no <see cref="Sink"/>) a control is a unit of its own; inside a unit (a ToolbarItem, a Tooltip,
/// a ButtonGroup) it reports to that. Overlays cascade <see cref="Blocked"/>, so the buttons in a
/// dialog or a menu panel aren't taken for toolbar items. Providers keep one instance per place,
/// since a control joins again when its scope object changes.
/// </summary>
internal sealed class ToolbarScope(ToolbarContext? context, ToolbarRegion region, IToolbarSink? sink, string? menuText)
{
    /// <summary>For content that isn't part of the toolbar: an overlay's panel, the "…" menu.</summary>
    public static ToolbarScope Blocked { get; } = new(null, ToolbarRegion.Start, null, null);

    public ToolbarContext? Context { get; } = context;

    public ToolbarRegion Region { get; } = region;

    public IToolbarSink? Sink { get; } = sink;

    /// <summary>A ToolbarItem's <c>MenuText</c>, for the control inside it.</summary>
    public string? MenuText { get; } = menuText;
}

/// <summary>Takes the menu forms and widths of the controls inside it.</summary>
internal interface IToolbarSink
{
    void Set(object owner, ToolbarEntry entry);

    void Remove(object owner);
}

/// <summary>
/// One control's part in the toolbar: what it becomes in the "…" menu (null: it can't go there)
/// and its estimated width in pixels at a 16px root (null: unknown).
/// </summary>
internal sealed record ToolbarEntry(RenderFragment? Menu, double? Width);

/// <summary>
/// What overflows into the "…" menu as one: a control at the top of a region, or a ToolbarItem.
/// It's drawn in the bar inside a <c>div.ib-toolbar-unit</c> and in the menu inside a
/// <c>div.ib-toolbar-menu-unit</c>, both marked with its id, which the CSS and the script show and hide.
/// </summary>
internal sealed class ToolbarUnit(ToolbarContext context, int id, ToolbarRegion region) : IToolbarSink
{
    private readonly List<(object Owner, ToolbarEntry Entry)> _entries = [];

    public int Id { get; } = id;

    public ToolbarRegion Region { get; } = region;

    public bool Separator { get; set; }

    public int Priority { get; set; }

    public ToolbarOverflow Overflow { get; set; }

    /// <summary>A ToolbarItem's own width, in pixels at a 16px root.</summary>
    public double? FixedWidth { get; set; }

    /// <summary>A ToolbarItem's <c>MenuContent</c>, in place of its controls' menu forms.</summary>
    public RenderFragment? MenuContent { get; set; }

    /// <summary>What it shows in the "…" menu, or null when it can't go there (it then stays in the bar).</summary>
    public RenderFragment? Menu => MenuContent ?? (_entries.Count > 0 && _entries.TrueForAll(e => e.Entry.Menu is not null)
        ? builder =>
        {
            foreach (var (_, entry) in _entries)
            {
                builder.AddContent(0, entry.Menu);
            }
        }
        : null);

    /// <summary>The estimated width, or null when a control in it has none.</summary>
    public double? Width => FixedWidth ?? (_entries.Count > 0 && _entries.TrueForAll(e => e.Entry.Width is not null)
        ? _entries.Sum(e => e.Entry.Width!.Value) + (_entries.Count - 1) * context.Gap
        : null);

    /// <summary>How it overflows, after what it can do: without a menu form it stays.</summary>
    public ToolbarOverflow EffectiveOverflow => Menu is null ? ToolbarOverflow.Never : Overflow;

    public void Set(object owner, ToolbarEntry entry)
    {
        var index = _entries.FindIndex(e => ReferenceEquals(e.Owner, owner));
        if (index >= 0)
        {
            _entries[index] = (owner, entry);
        }
        else
        {
            _entries.Add((owner, entry));
        }

        context.Changed();
    }

    public void Remove(object owner)
    {
        if (_entries.RemoveAll(e => ReferenceEquals(e.Owner, owner)) > 0)
        {
            context.Changed();
        }
    }
}

/// <summary>The units of one <see cref="Toolbar"/>, in page order within each region.</summary>
internal sealed class ToolbarContext(string rootId)
{
    private readonly List<ToolbarUnit> _units = [];
    private int _nextId;

    public string RootId { get; } = rootId;

    /// <summary>The space between units, in pixels at a 16px root.</summary>
    public double Gap { get; set; } = 8;

    /// <summary>Re-renders the "…" menu and the toolbar's CSS. Set by the part that draws them.</summary>
    public Action? OnChanged { get; set; }

    /// <summary>The units, start region first, then center, then end, each in the order they joined.</summary>
    public IEnumerable<ToolbarUnit> Units => _units.OrderBy(u => u.Region);

    public ToolbarUnit Add(ToolbarRegion region)
    {
        var unit = new ToolbarUnit(this, ++_nextId, region);
        _units.Add(unit);
        Changed();
        return unit;
    }

    public void Remove(ToolbarUnit unit)
    {
        if (_units.Remove(unit))
        {
            Changed();
        }
    }

    public void Changed() => OnChanged?.Invoke();
}

/// <summary>
/// A control's membership in a toolbar. A control at the top of a region gets a unit of its own
/// (when it can draw the unit's wrapper); inside a unit it reports to it. Outside a toolbar it does nothing.
/// </summary>
internal sealed class ToolbarMember(object owner) : IDisposable
{
    private ToolbarScope? _scope;
    private ToolbarScope? _inner;
    private IToolbarSink? _sink;

    /// <summary>The unit this control is, when it's at the top of a region.</summary>
    public ToolbarUnit? Unit { get; private set; }

    /// <summary>Whether the control is in a toolbar at all.</summary>
    public bool Active => _sink is not null;

    /// <summary>
    /// Joins or updates. <paramref name="entry"/> is what this control reports (null: nothing, as
    /// for a wrapper such as Tooltip). <paramref name="canBeUnit"/> is false for controls that don't
    /// draw a unit's wrapper; at the top of a region they stay out of the toolbar's reckoning.
    /// </summary>
    public void Update(ToolbarScope? scope, ToolbarEntry? entry, bool canBeUnit = true, Action<ToolbarUnit>? configure = null)
    {
        if (!ReferenceEquals(scope, _scope))
        {
            Leave();
            _scope = scope;
            if (scope?.Context is { } context)
            {
                if (scope.Sink is not null)
                {
                    _sink = scope.Sink;
                }
                else if (canBeUnit)
                {
                    Unit = context.Add(scope.Region);
                    _sink = Unit;
                }
            }
        }

        if (_sink is null)
        {
            return;
        }

        if (Unit is not null)
        {
            configure?.Invoke(Unit);
        }

        if (entry is not null)
        {
            _sink.Set(owner, entry);
        }
        else
        {
            _scope!.Context!.Changed();
        }
    }

    /// <summary>
    /// The scope for the content inside this control: reporting to <paramref name="sink"/> (a
    /// ButtonGroup's buttons), else to this control's unit, else the same as its own.
    /// </summary>
    public ToolbarScope? Inner(IToolbarSink? sink = null, string? menuText = null)
    {
        if (_scope?.Context is not { } context)
        {
            return _scope;
        }

        var target = sink ?? Unit ?? _scope.Sink;
        var text = sink is not null ? null : menuText ?? _scope.MenuText;
        if (_inner is null || !ReferenceEquals(_inner.Sink, target) || _inner.MenuText != text)
        {
            _inner = new ToolbarScope(context, _scope.Region, target, text);
        }

        return _inner;
    }

    /// <summary>
    /// Draws <paramref name="content"/>, inside the unit's wrapper when this control is a unit. A
    /// control with a box of its own (ToolbarItem) passes its class, style and attributes, and gets
    /// the box whether or not it's a unit.
    /// </summary>
    public RenderFragment Wrap(RenderFragment content, string? cssClass = null, string? style = null, IReadOnlyDictionary<string, object>? attributes = null) => builder =>
    {
        if (Unit is null && cssClass is null)
        {
            builder.AddContent(0, content);
            return;
        }

        builder.OpenElement(1, "div");
        builder.AddMultipleAttributes(2, attributes);
        builder.AddAttribute(3, "class", Unit is null ? cssClass : cssClass is null ? "ib-toolbar-unit" : $"ib-toolbar-unit {cssClass}");
        builder.AddAttribute(4, "style", style);
        if (Unit is { } unit)
        {
            builder.AddAttribute(5, "data-ib-unit", unit.Id.ToString(CultureInfo.InvariantCulture));
            builder.AddAttribute(6, "data-ib-priority", unit.Priority == 0 ? null : unit.Priority.ToString(CultureInfo.InvariantCulture));
            builder.AddAttribute(7, "data-ib-overflow", unit.Overflow == ToolbarOverflow.Auto ? null : unit.Overflow.ToString().ToLowerInvariant());
        }

        builder.AddContent(8, content);
        builder.CloseElement();
    };

    private void Leave()
    {
        if (Unit is not null)
        {
            _scope!.Context!.Remove(Unit);
        }
        else
        {
            _sink?.Remove(owner);
        }

        Unit = null;
        _sink = null;
        _inner = null;
    }

    public void Dispose() => Leave();
}

/// <summary>
/// Works out which units fit at every toolbar width: the same reckoning the script makes from
/// measured widths (<c>Scripts/elements/toolbar.ts</c>; keep the two in step).
/// </summary>
internal static class ToolbarPlan
{
    /// <summary>A unit as the plan sees it.</summary>
    public sealed record Unit(int Id, ToolbarRegion Region, double Width, int Priority, ToolbarOverflow Overflow, bool Separator);

    /// <summary>
    /// The toolbar's state from <see cref="Min"/> (inclusive) to <see cref="Max"/> (exclusive) pixels
    /// wide: the units hidden from the bar, those shown in the menu, and whether the "…" shows.
    /// </summary>
    public sealed record State(double Min, double Max, IReadOnlySet<int> BarHidden, IReadOnlySet<int> MenuShown, bool More);

    /// <summary>
    /// The order units leave the bar: lowest priority first; ties from the middle outward (the start
    /// region from its end, then the center from its end, then the end region from its start).
    /// </summary>
    public static List<Unit> DropOrder(IReadOnlyList<Unit> units)
    {
        var droppable = units.Where(u => !u.Separator && u.Overflow == ToolbarOverflow.Auto).ToList();
        int Rank(Unit u)
        {
            var region = droppable.Where(d => d.Region == u.Region).ToList();
            var index = region.IndexOf(u);
            var before = droppable.Count(d => d.Region < u.Region);
            return before + (u.Region == ToolbarRegion.End ? index : region.Count - 1 - index);
        }

        return [.. droppable.OrderBy(u => u.Priority).ThenBy(Rank)];
    }

    /// <summary>Every state the toolbar can be in, widest first. Empty ranges are left out.</summary>
    public static List<State> States(IReadOnlyList<Unit> units, double gap, double moreWidth)
    {
        var order = DropOrder(units);
        var always = units.Any(u => !u.Separator && u.Overflow == ToolbarOverflow.Always);
        var states = new List<State>();
        var max = double.PositiveInfinity;
        for (var k = 0; k <= order.Count; k++)
        {
            var dropped = order.Take(k).Select(u => u.Id).ToHashSet();
            var (barHidden, menuShown, width) = Layout(units, dropped, gap, moreWidth, more: k > 0 || always);
            var min = k == order.Count ? double.NegativeInfinity : width;
            if (min < max)
            {
                states.Add(new State(min, max, barHidden, menuShown, k > 0 || always));
            }

            max = Math.Min(max, width);
        }

        return states;
    }

    /// <summary>What's hidden and shown with <paramref name="dropped"/> in the menu, and the width the bar then needs.</summary>
    private static (HashSet<int> BarHidden, HashSet<int> MenuShown, double Width) Layout(
        IReadOnlyList<Unit> units, HashSet<int> dropped, double gap, double moreWidth, bool more)
    {
        bool InMenu(Unit u) => !u.Separator && (dropped.Contains(u.Id) || u.Overflow == ToolbarOverflow.Always);

        var barHidden = units.Where(InMenu).Select(u => u.Id).ToHashSet();
        var menuShown = barHidden.ToHashSet();

        // A separator shows between two shown units of its region, never twice in a row; in the
        // menu, between two units shown there.
        foreach (var region in units.GroupBy(u => u.Region))
        {
            HideStraySeparators([.. region], u => !barHidden.Contains(u.Id), id => barHidden.Add(id));
        }

        foreach (var separator in units.Where(u => u.Separator))
        {
            if (MenuSeparatorShows(units, separator, menuShown))
            {
                menuShown.Add(separator.Id);
            }
        }

        double RegionWidth(ToolbarRegion region)
        {
            var shown = units.Where(u => u.Region == region && !barHidden.Contains(u.Id)).ToList();
            return shown.Sum(u => u.Width) + Math.Max(0, shown.Count - 1) * gap;
        }

        var start = RegionWidth(ToolbarRegion.Start);
        var end = RegionWidth(ToolbarRegion.End);
        if (more)
        {
            end += (end > 0 ? gap : 0) + moreWidth;
        }

        var hasCenter = units.Any(u => u.Region == ToolbarRegion.Center);
        var width = hasCenter
            ? 2 * Math.Max(start, end) + RegionWidth(ToolbarRegion.Center) + 2 * gap
            : start + gap + end;
        return (barHidden, menuShown, width);
    }

    private static void HideStraySeparators(IReadOnlyList<Unit> units, Func<Unit, bool> shown, Action<int> hide)
    {
        var afterItem = false;
        for (var i = 0; i < units.Count; i++)
        {
            var unit = units[i];
            if (!unit.Separator)
            {
                afterItem |= shown(unit);
                continue;
            }

            var itemAfter = units.Skip(i + 1).TakeWhile(u => u.Region == unit.Region).Any(u => !u.Separator && shown(u));
            if (!afterItem || !itemAfter)
            {
                hide(unit.Id);
            }
            else
            {
                afterItem = false;
            }
        }
    }

    // In the menu a separator shows when units in the menu come before and after it in its region,
    // and no other shown separator stands between it and the last of them.
    private static bool MenuSeparatorShows(IReadOnlyList<Unit> units, Unit separator, HashSet<int> menuShown)
    {
        var region = units.Where(u => u.Region == separator.Region).ToList();
        var index = region.IndexOf(separator);
        var before = region.Take(index).LastOrDefault(u => !u.Separator && menuShown.Contains(u.Id));
        var after = region.Skip(index + 1).Any(u => !u.Separator && menuShown.Contains(u.Id));
        if (before is null || !after)
        {
            return false;
        }

        // The first separator after that unit is the one that shows.
        return region.Skip(region.IndexOf(before) + 1).First(u => u.Separator) == separator;
    }

    /// <summary>The container queries for a toolbar without script, scoped to its id.</summary>
    public static string Css(string rootId, IReadOnlyList<State> states)
    {
        static string Rem(double px) => (px / 16).ToString("0.###", CultureInfo.InvariantCulture) + "rem";
        static string Ids(IEnumerable<int> ids) => string.Join(",", ids.Order().Select(id => $"[data-ib-unit=\"{id}\"]"));

        var root = $"#{rootId}:not([data-ib-measured])";
        var css = new StringBuilder("@layer izzy.components{");
        foreach (var state in states)
        {
            var query = (double.IsNegativeInfinity(state.Min), double.IsPositiveInfinity(state.Max)) switch
            {
                (true, true) => null,
                (true, false) => $"(width < {Rem(state.Max)})",
                (false, true) => $"(width >= {Rem(state.Min)})",
                _ => $"(width >= {Rem(state.Min)}) and (width < {Rem(state.Max)})",
            };

            var rules = new StringBuilder();
            if (state.BarHidden.Count > 0)
            {
                rules.Append(CultureInfo.InvariantCulture, $"{root} .ib-toolbar-unit:is({Ids(state.BarHidden)}){{display:none}}");
            }

            if (state.MenuShown.Count > 0)
            {
                rules.Append(CultureInfo.InvariantCulture, $"{root} .ib-toolbar-menu-unit:is({Ids(state.MenuShown)}){{display:contents}}");
            }

            if (state.More)
            {
                rules.Append(CultureInfo.InvariantCulture, $"{root} .ib-toolbar-more{{display:flex}}");
            }

            if (rules.Length == 0)
            {
                continue;
            }

            css.Append(query is null ? rules.ToString() : $"@container {query}{{{rules}}}");
        }

        return css.Append('}').ToString();
    }
}
