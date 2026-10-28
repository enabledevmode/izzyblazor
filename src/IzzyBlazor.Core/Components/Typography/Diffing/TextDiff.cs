namespace IzzyBlazor;

/// <summary>What a line of a diff is: in both texts, or in one of them only.</summary>
public enum DiffKind
{
    /// <summary>In both texts.</summary>
    Same,

    /// <summary>In the old text only.</summary>
    Removed,

    /// <summary>In the new text only.</summary>
    Added,
}

/// <summary>A line of a diff.</summary>
/// <param name="Kind">Which texts have it.</param>
/// <param name="OldNumber">Its number in the old text, from 1. Null for a line added.</param>
/// <param name="NewNumber">Its number in the new text, from 1. Null for a line removed.</param>
/// <param name="Text">The line, without its end.</param>
public sealed record DiffLine(DiffKind Kind, int? OldNumber, int? NewNumber, string Text);

/// <summary>
/// The lines two texts differ by: the fewest lines removed and added that make one text the other, by Myers's
/// algorithm, the lines both texts start and end with set aside first. Its work grows with the length of the rest times
/// the lines that differ, so texts that differ by more than a set number of lines aren't compared. <see cref="Diff"/>
/// draws the result.
/// </summary>
public static class TextDiff
{
    /// <summary>The most lines removed and added <see cref="Compare"/> looks for unless told otherwise.</summary>
    public const int DefaultMaxChanges = 1000;

    /// <summary>
    /// The lines of a text: split at each <c>\n</c>, <c>\r\n</c> or <c>\r</c>. A text that ends with a line end has no
    /// line after it, and an empty text has none.
    /// </summary>
    public static IReadOnlyList<string> Lines(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            return [];
        }

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        return lines[^1].Length == 0 ? lines[..^1] : lines;
    }

    /// <summary>Every line of the two texts, in order: each line both have, and those only one has, removed before added.</summary>
    /// <param name="oldText">The old text.</param>
    /// <param name="newText">The new text.</param>
    /// <param name="maxChanges">The most lines removed and added to look for.</param>
    /// <returns>The lines. Null when the texts differ by more than <paramref name="maxChanges"/> lines.</returns>
    public static IReadOnlyList<DiffLine>? Compare(string oldText, string newText, int maxChanges = DefaultMaxChanges)
    {
        ArgumentNullException.ThrowIfNull(oldText);
        ArgumentNullException.ThrowIfNull(newText);
        ArgumentOutOfRangeException.ThrowIfNegative(maxChanges);
        var (a, b) = (Lines(oldText), Lines(newText));
        if (Edits(a, b, maxChanges) is not { } edits)
        {
            return null;
        }

        var lines = new List<DiffLine>(edits.Count);
        foreach (var (kind, x, y) in edits)
        {
            lines.Add(kind switch
            {
                DiffKind.Same => new DiffLine(kind, x + 1, y + 1, b[y]),
                DiffKind.Removed => new DiffLine(kind, x + 1, null, a[x]),
                _ => new DiffLine(kind, null, y + 1, b[y]),
            });
        }

        return lines;
    }

    /// <summary>
    /// The steps that make <paramref name="a"/> into <paramref name="b"/>, in order, each with its index in a (Same,
    /// Removed) and in b (Same, Added); removed before added. Null when more than <paramref name="max"/> items are
    /// removed and added.
    /// </summary>
    internal static List<(DiffKind Kind, int A, int B)>? Edits(IReadOnlyList<string> a, IReadOnlyList<string> b, int max)
    {
        // The items both start and end with are the same items, and cost the search nothing.
        var head = 0;
        while (head < a.Count && head < b.Count && a[head] == b[head])
        {
            head++;
        }

        var tail = 0;
        while (tail < a.Count - head && tail < b.Count - head && a[a.Count - 1 - tail] == b[b.Count - 1 - tail])
        {
            tail++;
        }

        if (Middle(a, b, head, a.Count - tail, head, b.Count - tail, max) is not { } middle)
        {
            return null;
        }

        var edits = new List<(DiffKind, int, int)>(head + middle.Count + tail);
        for (var index = 0; index < head; index++)
        {
            edits.Add((DiffKind.Same, index, index));
        }

        edits.AddRange(middle);
        for (var index = tail; index > 0; index--)
        {
            edits.Add((DiffKind.Same, a.Count - index, b.Count - index));
        }

        return edits;
    }

    // The steps for a[aStart..aEnd) and b[bStart..bEnd): Myers's greedy search for the furthest point on each diagonal,
    // edit by edit, keeping each edit's points to walk back from the end. Null when more than max edits are needed.
    private static List<(DiffKind, int, int)>? Middle(IReadOnlyList<string> a, IReadOnlyList<string> b, int aStart, int aEnd, int bStart, int bEnd, int max)
    {
        var (n, m) = (aEnd - aStart, bEnd - bStart);
        if (n + m > max && (n == 0 || m == 0))
        {
            return null;
        }

        max = Math.Min(n + m, max);
        var offset = max + 1;
        var furthest = new int[(2 * max) + 3];
        var trace = new List<int[]>();
        for (var edits = 0; edits <= max; edits++)
        {
            // The diagonals the next edit reads from, each edit's few: a whole array an edit would be 8 MB at the most.
            trace.Add(furthest[(offset - edits - 1)..(offset + edits + 2)]);
            for (var diagonal = -edits; diagonal <= edits; diagonal += 2)
            {
                // Down (an item of b added) from the diagonal above, or right (an item of a removed) from the one below.
                var x = diagonal == -edits || (diagonal != edits && furthest[offset + diagonal - 1] < furthest[offset + diagonal + 1])
                    ? furthest[offset + diagonal + 1]
                    : furthest[offset + diagonal - 1] + 1;
                var y = x - diagonal;
                while (x < n && y < m && a[aStart + x] == b[bStart + y])
                {
                    (x, y) = (x + 1, y + 1);
                }

                furthest[offset + diagonal] = x;
                if (x >= n && y >= m)
                {
                    return Walk(trace, edits, n, m, aStart, bStart);
                }
            }
        }

        return null;
    }

    // Walks back from the end through each edit's points, and gives the steps in order. trace[edit] holds diagonals
    // -edit - 1 to edit + 1 as they were before that edit, the diagonal k at k + edit + 1.
    private static List<(DiffKind, int, int)> Walk(List<int[]> trace, int edits, int n, int m, int aStart, int bStart)
    {
        var steps = new List<(DiffKind, int, int)>();
        var (x, y) = (n, m);
        for (var edit = edits; edit > 0; edit--)
        {
            var before = trace[edit];
            var diagonal = x - y;
            var down = diagonal == -edit || (diagonal != edit && before[diagonal + edit] < before[diagonal + edit + 2]);
            var previous = down ? diagonal + 1 : diagonal - 1;
            var (px, py) = (before[previous + edit + 1], before[previous + edit + 1] - previous);
            while (x > px && y > py)
            {
                (x, y) = (x - 1, y - 1);
                steps.Add((DiffKind.Same, aStart + x, bStart + y));
            }

            if (down)
            {
                y--;
                steps.Add((DiffKind.Added, -1, bStart + y));
            }
            else
            {
                x--;
                steps.Add((DiffKind.Removed, aStart + x, -1));
            }
        }

        while (x > 0 && y > 0)
        {
            (x, y) = (x - 1, y - 1);
            steps.Add((DiffKind.Same, aStart + x, bStart + y));
        }

        steps.Reverse();
        return steps;
    }
}
