using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// One of a <see cref="Toolbar"/>'s items: a ToolbarButton, ToolbarItem, ToolbarSeparator and so on.
/// The toolbar draws it twice, in the bar and (when it has a <see cref="Menu"/>) in the "…" menu,
/// and the item itself draws nothing where it's written. Read when drawn, so always current.
/// </summary>
internal interface IToolbarItem
{
    ToolbarPosition Position { get; }

    int Priority { get; }

    /// <summary>When it moves into the "…" menu. One with no <see cref="Menu"/> stays in the bar whatever this says.</summary>
    ToolbarOverflow Overflow { get; }

    bool Separator { get; }

    /// <summary>What it shows in the bar.</summary>
    RenderFragment Bar { get; }

    /// <summary>What it shows in the "…" menu, or null when it can't go there (it then stays in the bar).</summary>
    RenderFragment? Menu { get; }

    /// <summary>The estimated width in pixels at a 16px root, or null when unknown.</summary>
    double? Width { get; }
}

/// <summary>
/// The items of one <see cref="Toolbar"/>, in the order they joined (page order, unless a component
/// of the app's adds one later), and the parts that draw them: an outlet per position and the "…" menu.
/// </summary>
internal sealed class ToolbarContext(string rootId)
{
    private readonly List<(IToolbarItem Item, int Id)> _items = [];
    private readonly List<Action> _outlets = [];
    private int _nextId;

    /// <summary>
    /// For content that isn't at the top of a toolbar: a toolbar's own outlets and menu, an
    /// overlay's panel. Toolbar controls there refuse to render, and other controls are allowed.
    /// </summary>
    public static ToolbarContext Outside { get; } = new("");

    public string RootId { get; } = rootId;

    /// <summary>The space between units, in pixels at a 16px root.</summary>
    public double Gap { get; set; } = 8;

    /// <summary>Re-renders the "…" menu and the toolbar's CSS. Set by the part that draws them.</summary>
    public Action? OnMenuChanged { get; set; }

    /// <summary>The items with their ids, start first, then center, then end, each in the order they joined.</summary>
    public IEnumerable<(IToolbarItem Item, int Id)> Items => _items.OrderBy(i => i.Item.Position);

    public bool Has(ToolbarPosition position) => _items.Exists(i => i.Item.Position == position);

    public void Add(IToolbarItem item)
    {
        _items.Add((item, ++_nextId));
        Refresh();
    }

    public void Remove(IToolbarItem item)
    {
        if (_items.RemoveAll(i => ReferenceEquals(i.Item, item)) > 0)
        {
            Refresh();
        }
    }

    public void AddOutlet(Action render) => _outlets.Add(render);

    public void RemoveOutlet(Action render) => _outlets.Remove(render);

    /// <summary>Draws the bar and the menu again: an item changed.</summary>
    public void Refresh()
    {
        foreach (var render in _outlets.ToList())
        {
            render();
        }

        MenuChanged();
    }

    /// <summary>
    /// Draws the menu and the CSS again, not the bar: a control inside a unit (a ToolbarButtonGroup's
    /// button) changed while the bar drew it.
    /// </summary>
    public void MenuChanged() => OnMenuChanged?.Invoke();
}

/// <summary>An item's place in its toolbar: joins on its first parameters, leaves when disposed.</summary>
internal sealed class ToolbarSlot(IToolbarItem item, string component) : IDisposable
{
    private ToolbarContext? _context;

    /// <summary>Joins <paramref name="context"/>, the one a toolbar's content gets; refuses anywhere else.</summary>
    public void Join(ToolbarContext? context)
    {
        if (context is null || ReferenceEquals(context, ToolbarContext.Outside))
        {
            throw new InvalidOperationException($"{component} must be directly inside a Toolbar.");
        }

        if (!ReferenceEquals(context, _context))
        {
            _context?.Remove(item);
            _context = context;
            context.Add(item);
        }
    }

    /// <summary>
    /// Called where the item would render: the toolbar draws it instead, so the bar and the menu
    /// follow every change (new parameters, or a state change of its own, such as a Menu opening).
    /// </summary>
    public void Rendered() => _context?.Refresh();

    /// <summary>The menu copy changed, not the bar: a ToolbarButtonGroup's button, which the bar is drawing.</summary>
    public void MenuChanged() => _context?.MenuChanged();

    public void Dispose()
    {
        _context?.Remove(item);
        _context = null;
    }
}

/// <summary>The estimated width of a button, from button.css and the label in Inter (<see cref="TextWidths"/>).</summary>
internal static class ButtonWidth
{
    /// <summary>
    /// In pixels at a 16px root. <paramref name="text"/> is the label as plain text; null with
    /// <paramref name="hasLabel"/> means a label that isn't plain text, whose width is unknown.
    /// </summary>
    public static double? Estimate(ControlSize size, bool iconOnly, bool hasLabel, string? text, bool startIcon, bool endIcon, bool externalIcon = false)
    {
        var (height, padding, icon, font) = size switch
        {
            ControlSize.Sm => (32.0, 12.0, 16.0, 13.0),
            ControlSize.Lg => (44.0, 20.0, 24.0, 16.0),
            _ => (36.0, 16.0, 20.0, 14.0),
        };
        if (iconOnly)
        {
            return height;
        }

        if (hasLabel && text is null)
        {
            return null;
        }

        var parts = new List<double>();
        if (startIcon)
        {
            parts.Add(icon);
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            parts.Add(TextWidths.Of(text, size));
        }

        if (endIcon)
        {
            parts.Add(icon);
        }
        else if (externalIcon)
        {
            parts.Add(font < 16 ? 12 : 14);
        }

        // Padding and the 1px border on each side, and a gap of half the font size between parts.
        return 2 * padding + 2 + parts.Sum() + Math.Max(0, parts.Count - 1) * font / 2;
    }
}

/// <summary>
/// Works out which units fit at every toolbar width: the same reckoning the script makes from
/// measured widths (<c>Scripts/elements/toolbar.ts</c>; keep the two in step).
/// </summary>
internal static class ToolbarPlan
{
    /// <summary>A unit as the plan sees it.</summary>
    public sealed record Unit(int Id, ToolbarPosition Region, double Width, int Priority, ToolbarOverflow Overflow, bool Separator);

    /// <summary>
    /// The toolbar's state from <see cref="Min"/> (inclusive) to <see cref="Max"/> (exclusive) pixels
    /// wide: the units hidden from the bar, those shown in the menu, and whether the "…" shows.
    /// </summary>
    public sealed record State(double Min, double Max, IReadOnlySet<int> BarHidden, IReadOnlySet<int> MenuShown, bool More);

    /// <summary>
    /// The order units leave the bar: lowest priority first; ties from the middle outward (the start
    /// region from its end, then the center from its end, then the end region from its start). With
    /// a center the wider side sets the width, so a tie goes from whichever side is wider then.
    /// </summary>
    public static List<Unit> DropOrder(IReadOnlyList<Unit> units, double gap, double moreWidth)
    {
        var droppable = units.Where(u => !u.Separator && u.Overflow == ToolbarOverflow.Auto).ToList();
        int Rank(Unit u)
        {
            var region = droppable.Where(d => d.Region == u.Region).ToList();
            var index = region.IndexOf(u);
            var before = droppable.Count(d => d.Region < u.Region);
            return before + (u.Region == ToolbarPosition.End ? index : region.Count - 1 - index);
        }

        var ranked = droppable.OrderBy(u => u.Priority).ThenBy(Rank).ToList();
        if (!units.Any(u => u.Region == ToolbarPosition.Center))
        {
            return ranked;
        }

        var order = new List<Unit>();
        while (ranked.Count > 0)
        {
            // Once one has gone, the "…" shows at the end.
            var (_, _, _, start, end) = Layout(units, [.. order.Select(u => u.Id)], gap, moreWidth, more: true);
            var side = start >= end ? ToolbarPosition.Start : ToolbarPosition.End;
            var next = ranked.Find(u => u.Priority == ranked[0].Priority && u.Region == side) ?? ranked[0];
            order.Add(next);
            ranked.Remove(next);
        }

        return order;
    }

    /// <summary>Every state the toolbar can be in, widest first. Empty ranges are left out.</summary>
    public static List<State> States(IReadOnlyList<Unit> units, double gap, double moreWidth)
    {
        var order = DropOrder(units, gap, moreWidth);
        var always = units.Any(u => !u.Separator && u.Overflow == ToolbarOverflow.Always);
        var states = new List<State>();
        var max = double.PositiveInfinity;
        for (var k = 0; k <= order.Count; k++)
        {
            var dropped = order.Take(k).Select(u => u.Id).ToHashSet();
            var (barHidden, menuShown, width, _, _) = Layout(units, dropped, gap, moreWidth, more: k > 0 || always);
            var min = k == order.Count ? double.NegativeInfinity : width;
            if (min < max)
            {
                states.Add(new State(min, max, barHidden, menuShown, k > 0 || always));
            }

            max = Math.Min(max, width);
        }

        return states;
    }

    /// <summary>What's hidden and shown with <paramref name="dropped"/> in the menu, the width the bar then needs, and its start's and end's.</summary>
    private static (HashSet<int> BarHidden, HashSet<int> MenuShown, double Width, double Start, double End) Layout(
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

        double RegionWidth(ToolbarPosition region)
        {
            var shown = units.Where(u => u.Region == region && !barHidden.Contains(u.Id)).ToList();
            return shown.Sum(u => u.Width) + Math.Max(0, shown.Count - 1) * gap;
        }

        var start = RegionWidth(ToolbarPosition.Start);
        var end = RegionWidth(ToolbarPosition.End);
        if (more)
        {
            end += (end > 0 ? gap : 0) + moreWidth;
        }

        var hasCenter = units.Any(u => u.Region == ToolbarPosition.Center);
        var width = hasCenter
            ? 2 * Math.Max(start, end) + RegionWidth(ToolbarPosition.Center) + 2 * gap
            : start + gap + end;
        return (barHidden, menuShown, width, start, end);
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

        // Off once the script has measured, and while it measures.
        var root = $"#{rootId}:not([data-ib-measured], [data-ib-measuring])";
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
