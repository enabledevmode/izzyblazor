using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace IzzyBlazor.Generation;

/// <summary>Finds where an insert goes and makes its text fit the file.</summary>
internal static class Inserts
{
    /// <summary>
    /// The offset and text for <paramref name="text"/> beside the first line holding the anchor, in the file's
    /// line endings and ending in one; null when no line holds it, or when the file already holds the text.
    /// </summary>
    public static (int Offset, string Text, int Length)? Place(string file, InsertSpec spec, string text)
    {
        if (spec.Position == InsertPosition.Replace) return Replace(file, spec, text);
        if (AlreadyThere(file, text)) return null;
        var eol = file.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var body = Normalize(text).Replace("\n", eol, StringComparison.Ordinal);
        if (!body.EndsWith(eol, StringComparison.Ordinal)) body += eol;
        if (spec.Position == InsertPosition.End)
        {
            // After the last line; a file with no final line break gets one first.
            return file.Length == 0 || file.EndsWith('\n') ? (file.Length, body, 0) : (file.Length, eol + body, 0);
        }

        var start = 0;
        while (start < file.Length || start == 0)
        {
            var newline = file.IndexOf('\n', start);
            var end = newline < 0 ? file.Length : newline + 1;
            var line = file[start..end].TrimEnd('\r', '\n');
            var hit = spec.Anchor is not null
                ? line.Contains(spec.Anchor, StringComparison.Ordinal)
                : Regex.IsMatch(line, spec.Pattern!, RegexOptions.None, TimeSpan.FromSeconds(1));
            if (hit)
            {
                body = Indented(body, line, eol);
                if (spec.Position == InsertPosition.Before) return (start, body, 0);
                // After the last line of a file with no final line break: start a line first.
                return newline < 0 ? (end, eol + body[..^eol.Length], 0) : (end, body, 0);
            }
            if (newline < 0) break;
            start = end;
        }
        return null;
    }

    // The first match on a line, replaced; a line with nothing else on it is removed whole. Null when no line
    // holds a match: the replacement is already made, or there was nothing to replace.
    static (int Offset, string Text, int Length)? Replace(string file, InsertSpec spec, string text)
    {
        var start = 0;
        while (start < file.Length)
        {
            var newline = file.IndexOf('\n', start);
            var end = newline < 0 ? file.Length : newline + 1;
            var line = file[start..end];
            var (at, length) = spec.Anchor is not null
                ? (line.IndexOf(spec.Anchor, StringComparison.Ordinal), spec.Anchor.Length)
                : Regex.Match(line.TrimEnd('\r', '\n'), spec.Pattern!, RegexOptions.None, TimeSpan.FromSeconds(1)) is { Success: true } m ? (m.Index, m.Length) : (-1, 0);
            if (at >= 0)
            {
                var left = (line[..at] + text + line[(at + length)..]).Trim();
                return left.Length == 0 ? (start, "", end - start) : (start + at, text, length);
            }
            start = end;
        }
        return null;
    }

    /// <summary>The offset and text for <paramref name="text"/> at the start of a line (from 1), in the file's line endings.</summary>
    public static (int Offset, string Text) AtLine(string file, string path, int line, string text)
    {
        var eol = file.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var body = Normalize(text).TrimStart('\n').Replace("\n", eol, StringComparison.Ordinal);
        if (!body.EndsWith(eol, StringComparison.Ordinal)) body += eol;
        var offset = 0;
        for (var at = 1; at < line; at++)
        {
            var newline = file.IndexOf('\n', offset);
            if (newline >= 0)
            {
                offset = newline + 1;
                continue;
            }
            // The line just past the last of a file with no final line break: start a line first.
            if (at == line - 1 && offset < file.Length) return (file.Length, eol + body[..^eol.Length]);
            throw new GenerationException($"{path} has no line {line}.");
        }
        return (offset, body);
    }

    // Text with no indent of its own (app.UseMaintenance();) takes the indent of the line it goes beside,
    // so it lines up in a Program.cs inside a Main method too.
    static string Indented(string body, string line, string eol)
    {
        var indent = line[..(line.Length - line.TrimStart(' ', '\t').Length)];
        var first = body.Split(eol).FirstOrDefault(l => l.Length > 0);
        if (indent.Length == 0 || first is null || first[0] is ' ' or '\t') return body;
        return string.Join(eol, body.Split(eol).Select(l => l.Length > 0 ? indent + l : l));
    }

    /// <summary>Whether the file already holds the text, line endings aside, so inserting again would repeat it.</summary>
    public static bool AlreadyThere(string file, string text) =>
        Normalize(file).Contains(Normalize(text).TrimEnd('\n'), StringComparison.Ordinal);

    public static string NoAnchor(string id, string path, InsertSpec spec) =>
        $"{id}: no line in {path} holds {(spec.Anchor is not null ? $"\"{spec.Anchor}\"" : $"a match for /{spec.Pattern}/")}, so this wasn't added; add it by hand:\n{spec.Text}";

    public static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);
}
