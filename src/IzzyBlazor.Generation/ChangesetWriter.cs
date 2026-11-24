using System.Text;

namespace IzzyBlazor.Generation;

/// <summary>Writes a <see cref="Changeset"/> to a project folder, or shows it as a unified diff.</summary>
public static class ChangesetWriter
{
    static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    static readonly UTF8Encoding Strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

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
            full = ProjectInfo.Existing(full) ?? full;
            var bytes = File.ReadAllBytes(full);
            var bom = bytes is [0xEF, 0xBB, 0xBF, ..];
            string text;
            try
            {
                text = Strict.GetString(bom ? bytes[3..] : bytes);
            }
            catch (DecoderFallbackException)
            {
                // Read as UTF-8, its other characters would come back as question marks.
                throw new GenerationException($"{group.Key} isn't saved as UTF-8; nothing was written. Save it as UTF-8 and generate again.");
            }
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

    /// <summary>The changeset as a unified diff against <paramref name="project"/>'s files: new files whole, changes with three lines around them.</summary>
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
            AppendHunks(diff, Lines(original), Lines(Merge(original, group)));
        }
        return diff.ToString();
    }

    // The lines that differ, from the longest run the two have in common, with three lines around each change.
    static void AppendHunks(StringBuilder diff, string[] before, string[] after)
    {
        // Only the lines between the same first and last ones are compared, so a big file costs no more than its changes.
        var head = 0;
        while (head < before.Length && head < after.Length && before[head] == after[head]) head++;
        var tail = 0;
        while (tail < before.Length - head && tail < after.Length - head && before[^(tail + 1)] == after[^(tail + 1)]) tail++;
        var middleBefore = before[head..^tail];
        var middleAfter = after[head..^tail];
        var common = new int[middleBefore.Length + 1, middleAfter.Length + 1];
        for (var i = middleBefore.Length - 1; i >= 0; i--)
            for (var j = middleAfter.Length - 1; j >= 0; j--)
                common[i, j] = middleBefore[i] == middleAfter[j] ? common[i + 1, j + 1] + 1 : Math.Max(common[i + 1, j], common[i, j + 1]);
        // Each line with its line numbers after it, old and new: the same lines first, the middle, the same lines last.
        var script = new List<(char Op, string Line, int Old, int New)>();
        for (var i = 0; i < head; i++) script.Add((' ', before[i], i + 1, i + 1));
        int x = 0, y = 0;
        while (x < middleBefore.Length || y < middleAfter.Length)
        {
            if (x < middleBefore.Length && y < middleAfter.Length && middleBefore[x] == middleAfter[y]) script.Add((' ', middleBefore[x++], head + x, head + ++y));
            // A replaced line reads old, then new.
            else if (x < middleBefore.Length && (y == middleAfter.Length || common[x + 1, y] >= common[x, y + 1])) script.Add(('-', middleBefore[x++], head + x, head + y));
            else script.Add(('+', middleAfter[y++], head + x, head + y));
        }
        for (var i = 0; i < tail; i++) script.Add((' ', before[head + x + i], head + x + i + 1, head + y + i + 1));
        var changed = script.Select((s, index) => (s, index)).Where(s => s.s.Op != ' ').Select(s => s.index).ToList();
        var k = 0;
        while (k < changed.Count)
        {
            var start = Math.Max(0, changed[k] - 3);
            var end = Math.Min(script.Count, changed[k] + 4);
            while (k + 1 < changed.Count && changed[k + 1] - 3 <= end) end = Math.Min(script.Count, changed[++k] + 4);
            k++;
            var hunk = script.GetRange(start, end - start);
            var oldStart = (start == 0 ? 0 : script[start - 1].Old) + 1;
            var newStart = (start == 0 ? 0 : script[start - 1].New) + 1;
            var oldCount = hunk.Count(h => h.Op != '+');
            var newCount = hunk.Count(h => h.Op != '-');
            diff.Append($"@@ -{(oldCount == 0 ? oldStart - 1 : oldStart)},{oldCount} +{(newCount == 0 ? newStart - 1 : newStart)},{newCount} @@\n");
            foreach (var h in hunk) diff.Append(h.Op).Append(h.Line).Append('\n');
        }
    }

    /// <summary>
    /// The original text with every change made at its offset, in the order given when offsets tie: an insert
    /// listed after a replacement at the same offset goes after the replacing text. Changes that overlap are refused.
    /// </summary>
    static string Merge(string original, IEnumerable<TextInsert> inserts)
    {
        var result = new StringBuilder(original.Length);
        var copied = 0;
        foreach (var change in inserts.Select((i, order) => (i, order)).OrderBy(x => x.i.Offset).ThenBy(x => x.order).Select(x => x.i))
        {
            if (change.Offset < copied) throw new GenerationException($"Two changes to {change.Path} overlap; nothing was written.");
            result.Append(original, copied, change.Offset - copied).Append(change.Text);
            copied = change.Offset + change.Length;
        }
        return result.Append(original, copied, original.Length - copied).ToString();
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
        if (!ProjectInfo.Inside(fullRoot, full)) throw new GenerationException($"{path} is outside the project.");
        // A link inside the project (a symlink, a junction) that leads out of it is not a way out for a write.
        var real = Real(full);
        if (real != full && !ProjectInfo.Inside(Real(fullRoot), real))
            throw new GenerationException($"{path} goes through a link to {real}, outside the project. izzy writes inside the project only.");
        return full;
    }

    // The path with the links along it followed; a part that doesn't exist yet stays as written.
    static string Real(string full, int depth = 0)
    {
        var current = Path.GetPathRoot(full) ?? "";
        foreach (var part in full[current.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (depth < 16 && info.LinkTarget is not null && info.ResolveLinkTarget(returnFinalTarget: true) is { } target)
                current = Real(target.FullName, depth + 1);
        }
        return current;
    }
}
