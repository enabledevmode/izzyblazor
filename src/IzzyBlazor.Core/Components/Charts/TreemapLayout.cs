namespace IzzyBlazor;

/// <summary>What a <see cref="TreemapChart{TItem}"/> colors its tiles by, when they aren't colored by a value (<c>ColorValue</c>).</summary>
public enum TreemapColors
{
    /// <summary>By group when the chart has groups, by tile when it hasn't (the default).</summary>
    Auto,

    /// <summary>One color per group, every tile in it the same: the groups read as wholes.</summary>
    Group,

    /// <summary>A color per tile, in the items' order, the palette starting again in each group: tells neighbors apart.</summary>
    Item,
}

/// <summary>A box as percentages of its parent: left, top, width and height, 0 to 100.</summary>
internal readonly record struct TreemapBox(double X, double Y, double W, double H);

/// <summary>A tile: the item it draws, its group, its value and its box in its group's body, wide and narrow.</summary>
internal sealed record TreemapTile(int Item, int Group, double Value, TreemapBox Wide, TreemapBox Narrow);

/// <summary>A group: its total, its box in the plot (wide and narrow), and its tiles, largest first.</summary>
internal sealed record TreemapGroup(int Index, double Total, TreemapBox Wide, TreemapBox Narrow, IReadOnlyList<TreemapTile> Tiles);

internal sealed class TreemapModel
{
    /// <summary>The groups, largest first; one with no header when the chart has no groups.</summary>
    public List<TreemapGroup> Groups { get; } = [];

    public double Total { get; set; }

    /// <summary>Every tile, group by group, largest first: the order the keys visit them.</summary>
    public IEnumerable<TreemapTile> Tiles => Groups.SelectMany(g => g.Tiles);
}

/// <summary>
/// Lays a treemap out squarified (d3-hierarchy's <c>treemapSquarify</c>, at the golden ratio)
/// for a plot of a known shape: the chart keeps that shape at every width, so the boxes, in
/// percent, stay as square as they were laid out. Laid out twice: wide (the chart's aspect
/// ratio) and narrow (square, for a phone). Groups take the plot largest first, then each
/// group's tiles take its body, the group less its header strip.
/// </summary>
internal static class TreemapLayout
{
    /// <summary>The widths each layout is worked out at, in px: the header's share of a group depends on it.</summary>
    internal const double WidePx = 720, NarrowPx = 360, HeaderPx = 22;

    private static readonly double Phi = (1 + Math.Sqrt(5)) / 2;

    /// <param name="items">Each item's group (0 when there are none) and value; values that aren't positive and finite are left out.</param>
    /// <param name="groups">How many groups there are (1 with none).</param>
    /// <param name="headers">Whether groups have a header strip.</param>
    /// <param name="ratio">The wide layout's width over its height.</param>
    public static TreemapModel Build(IReadOnlyList<(int Group, double Value)> items, int groups, bool headers, double ratio)
    {
        var members = Enumerable.Range(0, groups).Select(_ => new List<(int Item, double Value)>()).ToArray();
        for (var i = 0; i < items.Count; i++)
        {
            var (g, v) = items[i];
            if (v > 0 && double.IsFinite(v)) members[g].Add((i, v));
        }

        // Largest first; equal values keep the items' order.
        var order = Enumerable.Range(0, groups)
            .Where(g => members[g].Count > 0)
            .Select(g => (Group: g, Total: members[g].Sum(m => m.Value)))
            .OrderByDescending(g => g.Total)
            .ToList();
        foreach (var m in members) m.Sort((a, b) => b.Value != a.Value ? b.Value.CompareTo(a.Value) : a.Item.CompareTo(b.Item));

        var model = new TreemapModel { Total = order.Sum(g => g.Total) };
        var wide = Squarify(order.Select(g => g.Total).ToList(), WidePx, WidePx / ratio);
        var narrow = Squarify(order.Select(g => g.Total).ToList(), NarrowPx, NarrowPx);
        for (var k = 0; k < order.Count; k++)
        {
            var (g, total) = order[k];
            var values = members[g].Select(m => m.Value).ToList();
            var w = Body(wide[k], headers);
            var n = Body(narrow[k], headers);
            var tilesWide = Squarify(values, w.W, w.H);
            var tilesNarrow = Squarify(values, n.W, n.H);
            var tiles = members[g].Select((m, j) => new TreemapTile(m.Item, g, m.Value, Percent(tilesWide[j], w.W, w.H), Percent(tilesNarrow[j], n.W, n.H))).ToList();
            model.Groups.Add(new TreemapGroup(g, total, Percent(wide[k], WidePx, WidePx / ratio), Percent(narrow[k], NarrowPx, NarrowPx), tiles));
        }

        return model;
    }

    // A group's body in px: the group less its header, where the header shows (treemap.css hides it
    // in a group under 3rem tall, so a short group's tiles take all of it).
    private static (double W, double H) Body(TreemapBox group, bool headers) =>
        (group.W, headers && group.H >= 48 ? group.H - HeaderPx : group.H);

    private static TreemapBox Percent(TreemapBox px, double w, double h) =>
        w <= 0 || h <= 0 ? new(0, 0, 0, 0) : new(px.X / w * 100, px.Y / h * 100, px.W / w * 100, px.H / h * 100);

    /// <summary>
    /// Boxes in px for <paramref name="values"/> (largest first) filling a w × h box, in order:
    /// rows of tiles along the shorter side, each row as long as keeps its tiles nearest square.
    /// </summary>
    internal static List<TreemapBox> Squarify(IReadOnlyList<double> values, double w, double h)
    {
        var boxes = new List<TreemapBox>(values.Count);
        double x0 = 0, y0 = 0, x1 = w, y1 = h;
        var value = values.Sum();
        var n = values.Count;
        var i0 = 0;
        while (i0 < n)
        {
            var dx = x1 - x0;
            var dy = y1 - y0;
            var i1 = i0;

            // The row's first tile, then as many more as make its worst tile squarer.
            double sum;
            do sum = values[i1++]; while (sum == 0 && i1 < n);
            var min = sum;
            var max = sum;
            var alpha = Math.Max(dy / dx, dx / dy) / (value * Phi);
            var beta = sum * sum * alpha;
            var worst = Math.Max(max / beta, beta / min);
            for (; i1 < n; i1++)
            {
                var v = values[i1];
                sum += v;
                min = Math.Min(min, v);
                max = Math.Max(max, v);
                beta = sum * sum * alpha;
                var next = Math.Max(max / beta, beta / min);
                if (next > worst)
                {
                    sum -= v;
                    break;
                }

                worst = next;
            }

            // A row across the top of a tall box, or down the side of a wide one.
            if (dx < dy)
            {
                var yb = value > 0 ? y0 + dy * sum / value : y1;
                Row(values, i0, i1, x0, y0, x1, yb, across: true, boxes);
                y0 = yb;
            }
            else
            {
                var xb = value > 0 ? x0 + dx * sum / value : x1;
                Row(values, i0, i1, x0, y0, xb, y1, across: false, boxes);
                x0 = xb;
            }

            value -= sum;
            i0 = i1;
        }

        return boxes;
    }

    // Tiles i0 to i1 side by side in a box, each as wide (across) or tall as its share.
    private static void Row(IReadOnlyList<double> values, int i0, int i1, double x0, double y0, double x1, double y1, bool across, List<TreemapBox> boxes)
    {
        var sum = 0.0;
        for (var i = i0; i < i1; i++) sum += values[i];
        var at = across ? x0 : y0;
        var span = across ? x1 - x0 : y1 - y0;
        for (var i = i0; i < i1; i++)
        {
            var size = sum > 0 ? span * values[i] / sum : 0;
            boxes.Add(across ? new TreemapBox(at, y0, size, y1 - y0) : new TreemapBox(x0, at, x1 - x0, size));
            at += size;
        }
    }
}
