namespace IzzyBlazor;

/// <summary>What a run of a diff's lines is drawn as.</summary>
internal enum DiffBlockKind
{
    /// <summary>Unchanged lines that are shown.</summary>
    Same,

    /// <summary>Removed lines, then added lines.</summary>
    Change,

    /// <summary>Unchanged lines folded away until opened.</summary>
    Fold,
}

/// <summary>A run of a diff's lines, by index into the diff.</summary>
/// <param name="Kind">What the run is drawn as.</param>
/// <param name="Old">The lines on the old side: the removed lines of a change, or every line of the others.</param>
/// <param name="New">The lines on the new side: the added lines of a change, or every line of the others.</param>
internal sealed record DiffBlock(DiffBlockKind Kind, IReadOnlyList<int> Old, IReadOnlyList<int> New)
{
    /// <summary>The rows it takes side by side.</summary>
    public int Rows => Math.Max(Old.Count, New.Count);
}

/// <summary>Cuts a diff into the runs <see cref="Diff"/> draws, and finds the changed words of paired lines.</summary>
internal static class DiffBlocks
{
    /// <summary>
    /// The diff's runs, in order. With <paramref name="fold"/>, unchanged lines more than <paramref name="context"/>
    /// lines from a change are folded, unless only one would be.
    /// </summary>
    public static List<DiffBlock> Build(IReadOnlyList<DiffLine> lines, int context, bool fold)
    {
        // How far each unchanged line is from the nearest change.
        var far = new bool[lines.Count];
        if (fold)
        {
            var distance = new int[lines.Count];
            var last = int.MinValue / 2;
            for (var i = 0; i < lines.Count; i++)
            {
                last = lines[i].Kind == DiffKind.Same ? last : i;
                distance[i] = i - last;
            }

            last = int.MaxValue / 2;
            for (var i = lines.Count - 1; i >= 0; i--)
            {
                last = lines[i].Kind == DiffKind.Same ? last : i;
                distance[i] = Math.Min(distance[i], last - i);
                far[i] = distance[i] > context;
            }

            // A fold of one line would take as much room as the line.
            for (var i = 0; i < lines.Count; i++)
            {
                if (far[i] && (i == 0 || !far[i - 1]) && (i + 1 == lines.Count || !far[i + 1]))
                {
                    far[i] = false;
                }
            }
        }

        var blocks = new List<DiffBlock>();
        var index = 0;
        while (index < lines.Count)
        {
            if (lines[index].Kind == DiffKind.Same)
            {
                var folded = far[index];
                var run = new List<int>();
                while (index < lines.Count && lines[index].Kind == DiffKind.Same && far[index] == folded)
                {
                    run.Add(index++);
                }

                blocks.Add(new DiffBlock(folded ? DiffBlockKind.Fold : DiffBlockKind.Same, run, run));
                continue;
            }

            var removed = new List<int>();
            var added = new List<int>();
            while (index < lines.Count && lines[index].Kind != DiffKind.Same)
            {
                (lines[index].Kind == DiffKind.Removed ? removed : added).Add(index++);
            }

            blocks.Add(new DiffBlock(DiffBlockKind.Change, removed, added));
        }

        return blocks;
    }

    // Past this many words removed and added, a pair of lines isn't marked word by word.
    private const int MaxWordChanges = 200;

    /// <summary>
    /// The changed parts of a removed line and the added line beside it, as character ranges in each. Null when less
    /// than half of the longer line (not counting whitespace) is unchanged, since a rewritten line would be all marks,
    /// or when they differ by too many words.
    /// </summary>
    public static (List<(int Start, int End)> Old, List<(int Start, int End)> New)? Words(string oldLine, string newLine)
    {
        var (a, b) = (Split(oldLine), Split(newLine));
        if (TextDiff.Edits(a, b, MaxWordChanges) is not { } edits)
        {
            return null;
        }

        var same = 0;
        foreach (var (kind, x, _) in edits)
        {
            if (kind == DiffKind.Same && !string.IsNullOrWhiteSpace(a[x]))
            {
                same += a[x].Length;
            }
        }

        var longer = Math.Max(Visible(oldLine), Visible(newLine));
        if (same * 2 < longer)
        {
            return null;
        }

        return (Ranges(edits, a, DiffKind.Removed, e => e.A), Ranges(edits, b, DiffKind.Added, e => e.B));
    }

    private static int Visible(string line) => line.Count(c => !char.IsWhiteSpace(c));

    // The character ranges of one side's changed words: touching ones joined, and ones only whitespace apart too.
    private static List<(int Start, int End)> Ranges(List<(DiffKind Kind, int A, int B)> edits, List<string> words, DiffKind changed, Func<(DiffKind Kind, int A, int B), int> at)
    {
        var marked = new bool[words.Count];
        foreach (var edit in edits)
        {
            if (edit.Kind == changed)
            {
                marked[at(edit)] = true;
            }
        }

        for (var i = 1; i + 1 < words.Count; i++)
        {
            marked[i] |= marked[i - 1] && marked[i + 1] && string.IsNullOrWhiteSpace(words[i]);
        }

        var ranges = new List<(int Start, int End)>();
        var offset = 0;
        for (var i = 0; i < words.Count; i++)
        {
            var end = offset + words[i].Length;
            if (marked[i])
            {
                if (i > 0 && marked[i - 1])
                {
                    ranges[^1] = (ranges[^1].Start, end);
                }
                else
                {
                    ranges.Add((offset, end));
                }
            }

            offset = end;
        }

        return ranges;
    }

    // Words: runs of letters, digits and underscores; runs of whitespace; each other character alone. A character is a
    // text element (an emoji's surrogate pair, a letter with its combining accents), so a mark never splits one.
    private static List<string> Split(string line)
    {
        var words = new List<string>();
        var i = 0;
        while (i < line.Length)
        {
            var kind = KindAt(line, i);
            var j = i + System.Globalization.StringInfo.GetNextTextElementLength(line, i);
            while (kind != 0 && j < line.Length && KindAt(line, j) == kind)
            {
                j += System.Globalization.StringInfo.GetNextTextElementLength(line, j);
            }

            words.Add(line[i..j]);
            i = j;
        }

        return words;
    }

    // 1 for a word character, 2 for whitespace, 0 for anything else (which stands alone).
    private static int KindAt(string line, int i) =>
        char.IsLetterOrDigit(line, i) || line[i] == '_' ? 1 : char.IsWhiteSpace(line[i]) ? 2 : 0;
}
