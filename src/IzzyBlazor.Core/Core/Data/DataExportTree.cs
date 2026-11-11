using System.Linq.Expressions;

namespace IzzyBlazor;

/// <summary>
/// Rows that form a tree, for an export of a <c>TreeList</c>: give it to <c>DataExport.WriteExcelAsync</c>
/// or <c>WriteCsvAsync</c> as <c>tree:</c>, with the key and parent key as properties:
/// <c>DataExportTree&lt;Employee&gt;.Of(e =&gt; e.Id, e =&gt; e.ManagerId)</c>. The rows come in the tree's
/// order, each under its parent and sorted among its siblings. While filtering, the rows above a
/// match come too, and with the tree list's <c>FilterBranch</c> the rows under one. Excel shows each
/// row's level as an outline, so the rows fold, and CSV indents the first column. To write a row's
/// total of its branch, as a column's <c>RollUp</c> shows it, add <see cref="RollUp"/>. The whole
/// tree is read into memory to put it in order.
/// </summary>
/// <typeparam name="TItem">The type of the rows.</typeparam>
public sealed class DataExportTree<TItem>
{
    private const int MaxDepth = 64;

    private readonly Func<IReadOnlyList<TItem>, HashSet<TItem>?, bool, IEnumerable<(TItem Item, int Level)>> _arrange;

    private DataExportTree(Func<IReadOnlyList<TItem>, HashSet<TItem>?, bool, IEnumerable<(TItem Item, int Level)>> arrange) => _arrange = arrange;

    /// <summary>The fields whose value a row with rows under it replaces with its branch's total.</summary>
    internal IReadOnlyList<(string Key, DataAggregate Type)> RollUps { get; private init; } = [];

    /// <summary>
    /// Writes a row with rows under it with the total of its branch in this field, in place of its own
    /// value: the sum, average, lowest, highest or count of the rows at the ends of the branch, as
    /// the column's <c>RollUp</c> shows it. While filtering, of the rows written.
    /// </summary>
    /// <param name="key">The field's key, as the column's.</param>
    /// <param name="type">The total: Sum, Avg, Min, Max or Count.</param>
    public DataExportTree<TItem> RollUp(string key, DataAggregate type)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (type == DataAggregate.Custom)
        {
            throw new ArgumentException("A roll-up can't be Custom: use Sum, Avg, Min, Max or Count.", nameof(type));
        }

        return new DataExportTree<TItem>(_arrange) { RollUps = [.. RollUps, (key, type)] };
    }

    // Each row with rows under it takes its branch's totals, over the rows with nothing under them.
    // The rows are in the tree's order, each with its level last.
    internal static void ApplyRollUps(List<object?[]> rows, IReadOnlyList<(int Column, DataAggregate Type)> rollUps)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            var level = (int)rows[i][^1]!;
            var leaves = new List<object?[]>();
            for (var j = i + 1; j < rows.Count && (int)rows[j][^1]! > level; j++)
            {
                if (j + 1 == rows.Count || (int)rows[j + 1][^1]! <= (int)rows[j][^1]!)
                {
                    leaves.Add(rows[j]);
                }
            }

            if (leaves.Count == 0)
            {
                continue;
            }

            foreach (var (column, type) in rollUps)
            {
                rows[i][column] = Total(leaves.Select(r => r[column]).ToList(), type);
            }
        }
    }

    private static object? Total(List<object?> values, DataAggregate type)
    {
        var present = values.Where(v => v is not null).ToList();
        if (type == DataAggregate.Count)
        {
            return values.Count;
        }

        if (present.Count == 0)
        {
            return null;
        }

        return type switch
        {
            DataAggregate.Sum => present.Sum(v => Convert.ToDecimal(v, System.Globalization.CultureInfo.InvariantCulture)),
            DataAggregate.Avg => present.Average(v => Convert.ToDecimal(v, System.Globalization.CultureInfo.InvariantCulture)),
            DataAggregate.Min => present.Min(Comparer<object?>.Default),
            _ => present.Max(Comparer<object?>.Default),
        };
    }

    /// <summary>A tree by each row's key and its parent's key, such as <c>e =&gt; e.Id, e =&gt; e.ManagerId</c>. A row whose parent isn't a row is a top row.</summary>
    public static DataExportTree<TItem> Of<TKey, TParent>(Expression<Func<TItem, TKey>> key, Expression<Func<TItem, TParent>> parent)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(parent);
        var keyOf = key.Compile();
        var parentOf = parent.Compile();
        return new DataExportTree<TItem>((rows, matches, branch) => Arrange(rows, matches, branch, keyOf, row => parentOf(row) is { } p ? (TKey)(object)p : default));
    }

    // The rows in the tree's order, with their levels: sorted rows stay sorted among their siblings.
    internal IEnumerable<(TItem Item, int Level)> Order(IReadOnlyList<TItem> sorted, HashSet<TItem>? matches, bool branch) => _arrange(sorted, matches, branch);

    private static IEnumerable<(TItem Item, int Level)> Arrange<TKey>(IReadOnlyList<TItem> rows, HashSet<TItem>? matches, bool branch, Func<TItem, TKey> keyOf, Func<TItem, TKey?> parentOf)
        where TKey : notnull
    {
        var byKey = new Dictionary<TKey, TItem>();
        foreach (var row in rows)
        {
            byKey.TryAdd(keyOf(row), row);
        }

        var roots = new List<TItem>();
        var children = new Dictionary<TKey, List<TItem>>();
        foreach (var row in rows)
        {
            if (parentOf(row) is { } parent && byKey.ContainsKey(parent) && !EqualityComparer<TKey>.Default.Equals(parent, keyOf(row)))
            {
                (children.TryGetValue(parent, out var list) ? list : children[parent] = []).Add(row);
            }
            else
            {
                roots.Add(row);
            }
        }

        // While filtering: the matches, the rows above them and, for a branch, the rows under them.
        HashSet<TItem>? kept = null;
        if (matches is not null)
        {
            kept = [.. matches];
            foreach (var match in matches)
            {
                for (var (up, depth) = (parentOf(match), 0); up is not null && depth < MaxDepth && byKey.TryGetValue(up, out var above) && kept.Add(above); up = parentOf(above), depth++)
                {
                }

                if (branch)
                {
                    var stack = new Stack<(TItem Row, int Depth)>([(match, 0)]);
                    while (stack.TryPop(out var at))
                    {
                        foreach (var child in at.Depth < MaxDepth && children.TryGetValue(keyOf(at.Row), out var list) ? list : [])
                        {
                            if (kept.Add(child))
                            {
                                stack.Push((child, at.Depth + 1));
                            }
                        }
                    }
                }
            }
        }

        var seen = new HashSet<TKey>();
        var order = new Stack<(TItem Row, int Level)>(roots.AsEnumerable().Reverse().Select(r => (r, 0)));
        while (order.TryPop(out var at))
        {
            if ((kept is not null && !kept.Contains(at.Row)) || !seen.Add(keyOf(at.Row)))
            {
                continue;
            }

            yield return at;
            if (at.Level < MaxDepth && children.TryGetValue(keyOf(at.Row), out var list))
            {
                for (var i = list.Count - 1; i >= 0; i--)
                {
                    order.Push((list[i], at.Level + 1));
                }
            }
        }
    }
}
