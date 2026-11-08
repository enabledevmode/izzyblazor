namespace IzzyBlazor;

/// <summary>Where a <see cref="SankeyChart{TItem}"/> puts each node across: the column it ends up in.</summary>
public enum SankeyAlign
{
    /// <summary>Each node as far left as its flows allow, and every node with no flows out in the last column (the default).</summary>
    Justify,

    /// <summary>Each node as far left as its flows allow: a flow that ends early ends in the middle.</summary>
    Start,

    /// <summary>Each node as far right as its flows allow: a flow that starts late starts in the middle.</summary>
    End,

    /// <summary>As <see cref="Start"/>, but a node with no flows in sits just before the nodes it flows to.</summary>
    Center,
}

/// <summary>How a <see cref="SankeyChart{TItem}"/> orders the nodes in each column.</summary>
public enum SankeyOrder
{
    /// <summary>Moved up and down to cross as few flows as it can (the default).</summary>
    Auto,

    /// <summary>In the order the items first name them, top to bottom: for categories with an order of their own.</summary>
    Data,
}

/// <summary>What a <see cref="SankeyChart{TItem}"/> colors each flow by.</summary>
public enum SankeyLinkColor
{
    /// <summary>The node it starts from, so a flow keeps its origin's color (the default).</summary>
    Source,

    /// <summary>The node it goes to, such as profit in green and costs in red.</summary>
    Target,

    /// <summary>A blend from the color of the node it starts from to the color of the one it goes to.</summary>
    Gradient,

    /// <summary>One gray for every flow, with the color only on the nodes: for diagrams with many nodes.</summary>
    Neutral,
}

/// <summary>A flow between two nodes, by index, with its value.</summary>
internal sealed record SankeyLinkInput(int Source, int Target, double Value);

/// <summary>
/// A node after layout: its column (0 to Columns − 1), its top and bottom in percent of the plot's
/// height, and its value (the larger of what flows in and out).
/// </summary>
internal sealed record SankeyNodeBox(int Index, int Column, double Top, double Bottom, double Value, double In, double Out);

/// <summary>
/// A flow after layout: its band's path in a 1000 × 1000 box (stretched to the plot), the columns
/// it joins, and where it meets each node, in percent of the plot's height.
/// </summary>
internal sealed record SankeyLinkBand(int Index, int Source, int Target, double Value, string Path, double SourceY, double TargetY, double Width);

internal sealed record SankeyModel(int Columns, IReadOnlyList<SankeyNodeBox> Nodes, IReadOnlyList<SankeyLinkBand> Links);

/// <summary>
/// Lays out a Sankey diagram: a port of d3-sankey (columns by longest path, node heights by value,
/// relaxation toward the flows' other ends, collisions pushed apart), on the server, in pixels of
/// the plot's height and then percent. Across, each column is a share of the width from 0 (the
/// first) to 1 (the last); the chart places node bars of a fixed width there with CSS.
/// </summary>
internal static class SankeyLayout
{
    private const int Iterations = 32;

    private sealed class Node(int index)
    {
        public int Index { get; } = index;
        public List<Link> Out { get; } = [];
        public List<Link> In { get; } = [];
        public double Value;
        public int Depth;
        public int Height;
        public int Column;
        public double Y0;
        public double Y1;
    }

    private sealed class Link(int index, Node source, Node target, double value)
    {
        public int Index { get; } = index;
        public Node Source { get; } = source;
        public Node Target { get; } = target;
        public double Value { get; } = value;
        public double Width;
        public double Y0;
        public double Y1;
    }

    /// <summary>
    /// Lays out <paramref name="nodeCount"/> nodes and their links in a plot <paramref name="height"/>
    /// pixels tall, with at least <paramref name="padding"/> pixels between nodes in a column.
    /// A loop (a node reached again from itself) throws, naming the flow that closes it.
    /// </summary>
    public static SankeyModel Build(int nodeCount, IReadOnlyList<SankeyLinkInput> input, IReadOnlyList<string> names, SankeyAlign align = SankeyAlign.Justify, SankeyOrder order = SankeyOrder.Auto, double height = 288, double padding = 8)
    {
        var nodes = Enumerable.Range(0, nodeCount).Select(i => new Node(i)).ToArray();
        var links = new List<Link>();
        foreach (var l in input)
        {
            var link = new Link(links.Count, nodes[l.Source], nodes[l.Target], l.Value);
            links.Add(link);
            link.Source.Out.Add(link);
            link.Target.In.Add(link);
        }

        if (nodeCount == 0)
        {
            return new SankeyModel(0, [], []);
        }

        FindLoop(nodes, names);
        foreach (var node in nodes)
        {
            node.Value = Math.Max(node.In.Sum(l => l.Value), node.Out.Sum(l => l.Value));
        }

        Depths(nodes);
        var columns = Columns(nodes, align, order);
        var py = Math.Min(padding, height / Math.Max(1, columns.Max(c => c.Count) - 1));
        Initialize(columns, height, py);
        for (var i = 0; i < Iterations; i++)
        {
            var alpha = Math.Pow(0.99, i);
            var beta = Math.Max(1 - alpha, (i + 1.0) / Iterations);
            RelaxRightToLeft(columns, alpha, beta, height, py, order);
            RelaxLeftToRight(columns, alpha, beta, height, py, order);
        }

        // Where each flow meets its nodes, top to bottom in the order of the nodes at its other end.
        foreach (var node in nodes)
        {
            node.Out.Sort((a, b) => a.Target.Y0.CompareTo(b.Target.Y0) is var c && c != 0 ? c : a.Index - b.Index);
            node.In.Sort((a, b) => a.Source.Y0.CompareTo(b.Source.Y0) is var c && c != 0 ? c : a.Index - b.Index);
        }

        foreach (var node in nodes)
        {
            var y0 = node.Y0;
            var y1 = node.Y0;
            foreach (var link in node.Out)
            {
                link.Y0 = y0 + link.Width / 2;
                y0 += link.Width;
            }

            foreach (var link in node.In)
            {
                link.Y1 = y1 + link.Width / 2;
                y1 += link.Width;
            }
        }

        var last = Math.Max(1, columns.Count - 1);
        double Pct(double px) => px / height * 100;
        var boxes = nodes.Select(n => new SankeyNodeBox(n.Index, n.Column, Pct(n.Y0), Pct(n.Y1), n.Value, n.In.Sum(l => l.Value), n.Out.Sum(l => l.Value))).ToArray();
        var bands = links.Select(l =>
        {
            var x0 = 1000.0 * l.Source.Column / last;
            var x1 = 1000.0 * l.Target.Column / last;
            return new SankeyLinkBand(l.Index, l.Source.Index, l.Target.Index, l.Value, Band(x0, x1, l.Y0 / height * 1000, l.Y1 / height * 1000, l.Width / height * 1000), Pct(l.Y0), Pct(l.Y1), Pct(l.Width));
        }).ToArray();
        return new SankeyModel(columns.Count, boxes, bands);
    }

    // A band from (x0, y0) to (x1, y1), as wide as w: two cubic curves with level ends, half-way
    // handles (d3's sankeyLinkHorizontal, filled in place of a thick stroke).
    private static string Band(double x0, double x1, double y0, double y1, double w)
    {
        var xm = (x0 + x1) / 2;
        var h = w / 2;
        return $"M{N(x0)},{N(y0 - h)}C{N(xm)},{N(y0 - h)} {N(xm)},{N(y1 - h)} {N(x1)},{N(y1 - h)}V{N(y1 + h)}C{N(xm)},{N(y1 + h)} {N(xm)},{N(y0 + h)} {N(x0)},{N(y0 + h)}Z";
    }

    private static string N(double v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    // A depth-first walk; an edge back to a node still on the path closes a loop.
    private static void FindLoop(Node[] nodes, IReadOnlyList<string> names)
    {
        var state = new byte[nodes.Length];
        foreach (var start in nodes)
        {
            if (state[start.Index] != 0) continue;
            var stack = new Stack<(Node Node, int Next)>();
            stack.Push((start, 0));
            state[start.Index] = 1;
            while (stack.Count > 0)
            {
                var (node, next) = stack.Pop();
                if (next < node.Out.Count)
                {
                    stack.Push((node, next + 1));
                    var target = node.Out[next].Target;
                    if (state[target.Index] == 1)
                    {
                        throw new InvalidOperationException(
                            $"A SankeyChart's flows can't loop: \"{names[node.Index]}\" to \"{names[target.Index]}\" leads back to where it started. Leave that flow out, or name the second visit differently, such as \"{names[target.Index]} (returned)\".");
                    }

                    if (state[target.Index] == 0)
                    {
                        state[target.Index] = 1;
                        stack.Push((target, 0));
                    }
                }
                else
                {
                    state[node.Index] = 2;
                }
            }
        }
    }

    // Depth: the longest path from a node with no flows in. Height: the longest path to one with no flows out.
    private static void Depths(Node[] nodes)
    {
        var current = nodes.ToHashSet();
        for (var x = 0; current.Count > 0; x++)
        {
            var next = new HashSet<Node>();
            foreach (var node in current)
            {
                node.Depth = x;
                foreach (var link in node.Out) next.Add(link.Target);
            }

            current = next;
        }

        current = nodes.ToHashSet();
        for (var x = 0; current.Count > 0; x++)
        {
            var next = new HashSet<Node>();
            foreach (var node in current)
            {
                node.Height = x;
                foreach (var link in node.In) next.Add(link.Source);
            }

            current = next;
        }
    }

    private static List<List<Node>> Columns(Node[] nodes, SankeyAlign align, SankeyOrder order)
    {
        var count = nodes.Max(n => n.Depth) + 1;
        foreach (var node in nodes)
        {
            var column = align switch
            {
                SankeyAlign.Start => node.Depth,
                SankeyAlign.End => count - 1 - node.Height,
                SankeyAlign.Center => node.In.Count > 0 ? node.Depth : node.Out.Count > 0 ? node.Out.Min(l => l.Target.Depth) - 1 : 0,
                _ => node.Out.Count > 0 ? node.Depth : count - 1,
            };
            node.Column = Math.Clamp(column, 0, count - 1);
        }

        var columns = Enumerable.Range(0, count).Select(_ => new List<Node>()).ToList();
        foreach (var node in nodes) columns[node.Column].Add(node);
        return columns.Where(c => c.Count > 0).Select((c, i) =>
        {
            foreach (var node in c) node.Column = i;
            return c;
        }).ToList();
    }

    private static void Initialize(List<List<Node>> columns, double height, double py)
    {
        var ky = columns.Min(c => (height - (c.Count - 1) * py) / Math.Max(1e-9, c.Sum(n => n.Value)));
        foreach (var column in columns)
        {
            var y = 0.0;
            foreach (var node in column)
            {
                node.Y0 = y;
                node.Y1 = y + node.Value * ky;
                y = node.Y1 + py;
                foreach (var link in node.Out) link.Width = link.Value * ky;
            }

            // The column's spare height, spread between its nodes.
            var gap = (height - y + py) / (column.Count + 1);
            for (var i = 0; i < column.Count; i++)
            {
                column[i].Y0 += gap * (i + 1);
                column[i].Y1 += gap * (i + 1);
            }

            foreach (var node in column) SortLinks(node);
        }
    }

    // Each node toward the middle of the flows into it, weighted by value and how far they come.
    private static void RelaxLeftToRight(List<List<Node>> columns, double alpha, double beta, double height, double py, SankeyOrder order)
    {
        for (var i = 1; i < columns.Count; i++)
        {
            var column = columns[i];
            foreach (var target in column)
            {
                double y = 0, w = 0;
                foreach (var link in target.In)
                {
                    var v = link.Value * (target.Column - link.Source.Column);
                    y += TargetTop(link.Source, target, py) * v;
                    w += v;
                }

                if (!(w > 0)) continue;
                var dy = (y / w - target.Y0) * alpha;
                target.Y0 += dy;
                target.Y1 += dy;
                SortNeighborLinks(target);
            }

            if (order == SankeyOrder.Auto) column.Sort((a, b) => a.Y0.CompareTo(b.Y0));
            Resolve(column, beta, height, py);
        }
    }

    // Each node toward the middle of the flows out of it.
    private static void RelaxRightToLeft(List<List<Node>> columns, double alpha, double beta, double height, double py, SankeyOrder order)
    {
        for (var i = columns.Count - 2; i >= 0; i--)
        {
            var column = columns[i];
            foreach (var source in column)
            {
                double y = 0, w = 0;
                foreach (var link in source.Out)
                {
                    var v = link.Value * (link.Target.Column - source.Column);
                    y += SourceTop(source, link.Target, py) * v;
                    w += v;
                }

                if (!(w > 0)) continue;
                var dy = (y / w - source.Y0) * alpha;
                source.Y0 += dy;
                source.Y1 += dy;
                SortNeighborLinks(source);
            }

            if (order == SankeyOrder.Auto) column.Sort((a, b) => a.Y0.CompareTo(b.Y0));
            Resolve(column, beta, height, py);
        }
    }

    // Pushes overlapping nodes apart from the middle one out, then back inside the plot.
    private static void Resolve(List<Node> nodes, double alpha, double height, double py)
    {
        var i = nodes.Count >> 1;
        var subject = nodes[i];
        Up(nodes, subject.Y0 - py, i - 1, alpha, py);
        Down(nodes, subject.Y1 + py, i + 1, alpha, py);
        Up(nodes, height, nodes.Count - 1, alpha, py);
        Down(nodes, 0, 0, alpha, py);
    }

    private static void Down(List<Node> nodes, double y, int i, double alpha, double py)
    {
        for (; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var dy = (y - node.Y0) * alpha;
            if (dy > 1e-6)
            {
                node.Y0 += dy;
                node.Y1 += dy;
            }

            y = node.Y1 + py;
        }
    }

    private static void Up(List<Node> nodes, double y, int i, double alpha, double py)
    {
        for (; i >= 0; i--)
        {
            var node = nodes[i];
            var dy = (node.Y1 - y) * alpha;
            if (dy > 1e-6)
            {
                node.Y0 -= dy;
                node.Y1 -= dy;
            }

            y = node.Y0 - py;
        }
    }

    private static void SortLinks(Node node)
    {
        node.Out.Sort((a, b) => a.Target.Y0.CompareTo(b.Target.Y0) is var c && c != 0 ? c : a.Index - b.Index);
        node.In.Sort((a, b) => a.Source.Y0.CompareTo(b.Source.Y0) is var c && c != 0 ? c : a.Index - b.Index);
    }

    private static void SortNeighborLinks(Node node)
    {
        foreach (var link in node.In) SortLinks(link.Source);
        foreach (var link in node.Out) SortLinks(link.Target);
    }

    // The target top that would make the flow from source to target level.
    private static double TargetTop(Node source, Node target, double py)
    {
        var y = source.Y0 - (source.Out.Count - 1) * py / 2;
        foreach (var link in source.Out)
        {
            if (link.Target == target) break;
            y += link.Width + py;
        }

        foreach (var link in target.In)
        {
            if (link.Source == source) break;
            y -= link.Width;
        }

        return y;
    }

    // The source top that would make the flow from source to target level.
    private static double SourceTop(Node source, Node target, double py)
    {
        var y = target.Y0 - (target.In.Count - 1) * py / 2;
        foreach (var link in target.In)
        {
            if (link.Source == source) break;
            y += link.Width + py;
        }

        foreach (var link in source.Out)
        {
            if (link.Target == target) break;
            y -= link.Width;
        }

        return y;
    }
}
