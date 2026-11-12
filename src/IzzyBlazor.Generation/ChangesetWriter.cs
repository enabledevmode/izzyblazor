using System.Text;

namespace IzzyBlazor.Generation;

/// <summary>Writes a <see cref="Changeset"/> to a project folder, or shows it as a unified diff.</summary>
public static class ChangesetWriter
{
    static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Creates the files and makes the inserts. Refuses, writing nothing, when a file to create already exists
    /// with other content (unless <paramref name="overwrite"/>) or a file to insert into has changed since the
    /// changeset was made. Packages are left to the caller (<c>dotnet add package</c>), which knows the project's
    /// central package management and sources.
    /// </summary>
    public static void Apply(Changeset changeset, string projectDirectory, bool overwrite = false)
    {
        if (changeset.Conflicts.Count > 0 && !overwrite)
            throw new GenerationException($"These files exist with other content; nothing was written. Overwrite to replace them: {string.Join(", ", changeset.Conflicts)}.");

        // Check every insert target first, so a refusal writes nothing.
        var edits = new List<(string FullPath, byte[] Bytes)>();
        foreach (var group in changeset.Inserts.GroupBy(i => i.Path))
        {
            var full = FullPath(projectDirectory, group.Key);
            var bytes = File.ReadAllBytes(full);
            var bom = bytes is [0xEF, 0xBB, 0xBF, ..];
            var text = Encoding.UTF8.GetString(bom ? bytes[3..] : bytes);
            if (Inserts.Hash(text) != group.First().OriginalHash)
                throw new GenerationException($"{group.Key} changed after these changes were made; nothing was written. Generate again.");
            var updated = Encoding.UTF8.GetBytes(Merge(text, group));
            edits.Add((full, bom ? [0xEF, 0xBB, 0xBF, .. updated] : updated));
        }

        foreach (var create in changeset.Creates)
        {
            var full = FullPath(projectDirectory, create.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            if (create.Text is not null) File.WriteAllText(full, create.Text, Utf8NoBom);
            else File.WriteAllBytes(full, create.Bytes!);
        }
        foreach (var (full, bytes) in edits) File.WriteAllBytes(full, bytes);
    }

    /// <summary>The changeset as a unified diff against <paramref name="project"/>'s files: new files whole, inserts with three lines around them.</summary>
    public static string Diff(Changeset changeset, ProjectInfo project)
    {
        var diff = new StringBuilder();
        foreach (var create in changeset.Creates)
        {
            var existing = project.ReadFile(create.Path);
            diff.Append(existing is null ? "--- /dev/null\n" : $"--- a/{create.Path}\n").Append($"+++ b/{create.Path}\n");
            if (create.Text is null)
            {
                diff.Append("Binary file\n");
                continue;
            }
            var oldLines = existing is null ? [] : Lines(existing);
            var newLines = Lines(create.Text);
            diff.Append($"@@ -{(oldLines.Length == 0 ? 0 : 1)},{oldLines.Length} +1,{newLines.Length} @@\n");
            foreach (var line in oldLines) diff.Append('-').Append(line).Append('\n');
            foreach (var line in newLines) diff.Append('+').Append(line).Append('\n');
        }
        foreach (var group in changeset.Inserts.GroupBy(i => i.Path))
        {
            var original = project.ReadFile(group.Key) ?? "";
            diff.Append($"--- a/{group.Key}\n+++ b/{group.Key}\n");
            var oldLines = Lines(original);
            // Each insert starts at a line boundary: the number of lines before its offset.
            var added = group
                .Select((insert, order) => (Line: Lines(original[..insert.Offset]).Length, Order: order, Lines: Lines(insert.Text)))
                .OrderBy(a => a.Line).ThenBy(a => a.Order).ToList();
            var shift = 0;
            foreach (var hunk in Hunks(added.Select(a => a.Line).ToList(), oldLines.Length))
            {
                var inHunk = added.Where(a => a.Line >= hunk.Start && a.Line <= hunk.End).ToList();
                var count = inHunk.Sum(a => a.Lines.Length);
                var oldCount = Math.Min(hunk.End, oldLines.Length) - hunk.Start;
                diff.Append($"@@ -{hunk.Start + 1},{oldCount} +{hunk.Start + 1 + shift},{oldCount + count} @@\n");
                for (var line = hunk.Start; line <= Math.Min(hunk.End, oldLines.Length); line++)
                {
                    foreach (var a in inHunk.Where(a => a.Line == line))
                        foreach (var l in a.Lines) diff.Append('+').Append(l).Append('\n');
                    if (line < Math.Min(hunk.End, oldLines.Length)) diff.Append(' ').Append(oldLines[line]).Append('\n');
                }
                shift += count;
            }
        }
        return diff.ToString();
    }

    /// <summary>The original text with every insert placed at its offset, in the order given when offsets tie.</summary>
    static string Merge(string original, IEnumerable<TextInsert> inserts)
    {
        var result = new StringBuilder(original);
        foreach (var insert in inserts.Select((i, order) => (i, order)).OrderByDescending(x => x.i.Offset).ThenByDescending(x => x.order))
            result.Insert(insert.i.Offset, insert.i.Text);
        return result.ToString();
    }

    /// <summary>Line ranges [Start, End) of context around the insert points, three lines each side, merged when they touch.</summary>
    static List<(int Start, int End)> Hunks(List<int> points, int lineCount)
    {
        var hunks = new List<(int Start, int End)>();
        foreach (var p in points.Distinct().Order())
        {
            var start = Math.Max(0, p - 3);
            var end = Math.Min(lineCount, p + 3);
            if (hunks.Count > 0 && start <= hunks[^1].End) hunks[^1] = (hunks[^1].Start, Math.Max(hunks[^1].End, end));
            else hunks.Add((start, end));
        }
        return hunks;
    }

    static string[] Lines(string text)
    {
        if (text.Length == 0) return [];
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        return text.EndsWith('\n') ? lines[..^1] : lines;
    }

    static string FullPath(string root, string path)
    {
        var fullRoot = Path.GetFullPath(root);
        var full = Path.GetFullPath(Path.Combine(fullRoot, path));
        if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new GenerationException($"{path} is outside the project.");
        return full;
    }
}
