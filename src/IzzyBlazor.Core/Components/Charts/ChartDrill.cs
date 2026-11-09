using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// One level of a chart's drill-down, such as years, then months, then days: in a
/// <see cref="Chart{TItem}"/>, in order from the top. The chart groups its items by the level's
/// <see cref="Argument"/>, adds up each series by its <c>Aggregate</c>, and links each bar or
/// point to the next level down; a breadcrumb above the plot goes back up. The level is in the
/// page's address (<see cref="Chart{TItem}.Drill"/>), so it works with no script, the browser's
/// Back goes up a level, and a level can be bookmarked.
/// <para>
/// <c>&lt;ChartDrillLevel Argument="s =&gt; s.Date.Year" Name="Year" /&gt;</c>
/// </para>
/// </summary>
/// <typeparam name="TItem">The chart's items.</typeparam>
[ModeSupport]
public sealed class ChartDrillLevel<TItem> : ComponentBase, IDisposable
{
    private Expression<Func<TItem, object?>>? _last;

    [CascadingParameter] private ChartContext<TItem>? Context { get; set; }

    [CascadingParameter] private ChartDrillLevels<TItem>? Host { get; set; }

    // What the level groups by, as last seen: the chart draws again only when it changes.
    private string? _seen;

    /// <summary>What the level groups the items by, such as <c>s =&gt; s.Date.Year</c>: names, numbers or dates.</summary>
    [Parameter, EditorRequired] public Expression<Func<TItem, object?>> Argument { get; set; } = default!;

    /// <summary>What the level's arguments are, such as "Year", for the breadcrumb and the hints ("by year"). Defaults to the property's name in words.</summary>
    [Parameter] public string? Name { get; set; }

    internal Func<TItem, object?> Of { get; private set; } = default!;

    internal string DisplayName { get; private set; } = "";

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Context is null && Host is null) throw new InvalidOperationException("ChartDrillLevel goes inside a Chart, PieChart, FunnelChart or TreemapChart, one for each level from the top.");
        if (Context is not null) Context.Register(this);
        else Host!.Register(this);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Argument is null) throw new InvalidOperationException("ChartDrillLevel needs an Argument, what the level groups by, such as s => s.Date.Year.");
        if (!ReferenceEquals(Argument, _last))
        {
            _last = Argument;
            Of = Argument.Compile();
        }

        DisplayName = Name ?? ChartMembers.Name(Argument) ?? "";
        if (Context is not null)
        {
            Context.Seen(this);
            return;
        }

        var seen = $"{Argument}|{DisplayName}";
        if (seen == _seen) return;
        var first = _seen is null;
        _seen = seen;
        if (!first) Host!.Changed();
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Context?.Unregister(this);
        Host?.Unregister(this);
    }
}

/// <summary>A drill-down level's key in the address: an argument as invariant text.</summary>
internal static class ChartDrillKey
{
    public static string Of(object? value) => value switch
    {
        null => "",
        DateTime d => d.TimeOfDay == TimeSpan.Zero ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : d.ToString("s", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTimeOffset d => d.ToString("s", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };
}

/// <summary>
/// The levels a chart other than Chart (PieChart, FunnelChart, TreemapChart) takes from its
/// <see cref="ChartDrillLevel{TItem}"/>s, in the order they're declared. It asks the chart to
/// draw again only when a level comes, goes or changes what it groups by.
/// </summary>
internal sealed class ChartDrillLevels<TItem>(Action changed)
{
    private readonly List<ChartDrillLevel<TItem>> _levels = [];

    public IReadOnlyList<ChartDrillLevel<TItem>> Levels => _levels;

    public void Register(ChartDrillLevel<TItem> level)
    {
        _levels.Add(level);
        changed();
    }

    public void Changed() => changed();

    public void Unregister(ChartDrillLevel<TItem> level)
    {
        if (_levels.Remove(level)) changed();
    }
}

/// <summary>A chart at a drill-down level: the items under the path, grouped by the level's argument.</summary>
internal sealed record ChartDrillState<TItem>(
    int Depth,
    IReadOnlyList<string> Path,
    IReadOnlyList<string> Crumbs,
    IReadOnlyList<string> Up,
    IReadOnlyList<TItem> Rows,
    int[] GroupOf,
    IReadOnlyList<object?> Arguments,
    IReadOnlyList<TItem> Firsts,
    IReadOnlyList<string?>? Hrefs,
    string? Hint,
    string? Said,
    string Key)
{
    /// <summary>Each group's values added up from its rows' (null rows left out; Count counts the rest).</summary>
    public double?[] Total(IReadOnlyList<double?> rows, DataAggregate how)
    {
        var values = new double?[Arguments.Count];
        for (var g = 0; g < values.Length; g++)
        {
            var own = new List<double>();
            for (var i = 0; i < GroupOf.Length; i++)
            {
                if (GroupOf[i] == g && rows[i] is { } v) own.Add(v);
            }

            values[g] = how switch
            {
                DataAggregate.Count => own.Count,
                _ when own.Count == 0 => null,
                DataAggregate.Avg => own.Average(),
                DataAggregate.Min => own.Min(),
                DataAggregate.Max => own.Max(),
                _ => own.Sum(),
            };
        }

        return values;
    }
}

/// <summary>Drill-down for every chart: the path in the address, the level's groups, the links.</summary>
internal static class ChartDrill
{
    /// <summary>The query value that holds a chart's path: Drill, or "drill".</summary>
    public static string KeyOf(string? drill) => string.IsNullOrWhiteSpace(drill) ? "drill" : drill;

    /// <summary>
    /// The level in the address: the items under the path chosen so far (an unknown key stops
    /// there), grouped by the level's argument in the order the groups first appear.
    /// </summary>
    public static ChartDrillState<TItem> At<TItem>(IReadOnlyList<TItem> items, IReadOnlyList<ChartDrillLevel<TItem>> levels, NavigationManager navigation, string key, IReadOnlyDictionary<string, object>? attributes, IzzyText text)
    {
        var asked = PathOf(navigation, key);
        var anchor = attributes?.TryGetValue("id", out var id) == true ? id?.ToString() : null;
        IReadOnlyList<TItem> rows = items;
        var crumbs = new List<string>();
        var depth = 0;
        while (depth < levels.Count - 1 && depth < asked.Count)
        {
            var level = levels[depth];
            var step = asked[depth];
            var next = rows.Where(r => ChartDrillKey.Of(level.Of(r)) == step).ToList();
            if (next.Count == 0) break;
            crumbs.Add(ChartLayout.ArgumentText(level.Of(next[0]), null, CultureInfo.CurrentCulture));
            rows = next;
            depth++;
        }

        var path = asked.Take(depth).ToList();
        var at = levels[depth];
        var index = new Dictionary<string, int>();
        var arguments = new List<object?>();
        var firsts = new List<TItem>();
        var keys = new List<string>();
        var groupOf = new int[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            var value = at.Of(rows[i]);
            var k = ChartDrillKey.Of(value);
            if (!index.TryGetValue(k, out var g))
            {
                index[k] = g = arguments.Count;
                arguments.Add(value);
                firsts.Add(rows[i]);
                keys.Add(k);
            }

            groupOf[i] = g;
        }

        var deeper = depth < levels.Count - 1;
        var culture = CultureInfo.CurrentCulture;
        var hrefs = deeper ? keys.Select(k => (string?)Href(navigation, key, path.Append(k), anchor)).ToList() : null;
        var up = Enumerable.Range(0, depth).Select(d => Href(navigation, key, path.Take(d), anchor)).ToList();
        var hint = deeper ? string.Format(culture, text.ChartDrillHint, levels[depth + 1].DisplayName.ToLower(culture)) : null;
        var said = depth > 0 ? string.Format(culture, text.ChartDrilled, string.Join(", ", crumbs), at.DisplayName.ToLower(culture), arguments.Count) : null;
        return new ChartDrillState<TItem>(depth, path, crumbs, up, rows, groupOf, arguments, firsts, hrefs, hint, said, key);
    }

    /// <summary>The path in the address: each value of the chart's query key, in order.</summary>
    public static List<string> PathOf(NavigationManager navigation, string key)
    {
        var query = new Uri(navigation.Uri).Query.TrimStart('?');
        return query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .Where(p => Uri.UnescapeDataString(p[0].Replace('+', ' ')) == key)
            .Select(p => p.Length > 1 ? Uri.UnescapeDataString(p[1].Replace('+', ' ')) : "")
            .ToList();
    }

    /// <summary>This page with the chart's path set (none: the top), back at the chart when it has an id.</summary>
    public static string Href(NavigationManager navigation, string key, IEnumerable<string> path, string? anchor)
    {
        var keys = path.ToArray();
        var uri = navigation.GetUriWithQueryParameters(new Dictionary<string, object?> { [key] = keys.Length == 0 ? null : keys });
        var hash = uri.IndexOf('#');
        if (hash >= 0) uri = uri[..hash];
        return anchor is { Length: > 0 } ? $"{uri}#{anchor}" : uri;
    }

    /// <summary>The breadcrumb above a drilled chart: the levels above as links, this one as text.</summary>
    public static RenderFragment Crumbs<TItem>(ChartDrillState<TItem> state, string top) => builder =>
    {
        builder.OpenComponent<Breadcrumb>(0);
        builder.AddComponentParameter(1, "Class", "ib-chart-crumbs");
        builder.AddComponentParameter(2, nameof(Breadcrumb.ChildContent), (RenderFragment)(b =>
        {
            for (var c = 0; c <= state.Depth; c++)
            {
                b.OpenComponent<BreadcrumbItem>(0);
                b.SetKey(c);
                if (c < state.Depth) b.AddComponentParameter(1, nameof(BreadcrumbItem.Href), state.Up[c]);
                var label = c == 0 ? top : state.Crumbs[c - 1];
                b.AddComponentParameter(2, nameof(BreadcrumbItem.ChildContent), (RenderFragment)(t => t.AddContent(0, label)));
                b.CloseComponent();
            }
        }));
        builder.CloseComponent();
    };
}
