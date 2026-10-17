using System.Globalization;

namespace IzzyBlazor;

/// <summary>FileUpload's default hint: the file types and size allowed, as "PNG or JPG · up to 2 MB".</summary>
internal static class FileHints
{
    /// <summary>
    /// The hint for an accept attribute and a size limit, or null when there's nothing to say. Types
    /// are named only when every one has a plain name (".pdf", "image/png"); a wildcard ("image/*")
    /// or a vendor type leaves them out, since a partial list would mislead.
    /// </summary>
    public static string? Describe(string? accept, long? maxFileSize, IzzyText text)
    {
        var parts = new List<string>(2);
        if (TypeNames(accept) is { Count: > 0 } names)
        {
            parts.Add(names.Count == 1
                ? names[0]
                : string.Join(", ", names.Take(names.Count - 1)) + " " + text.ListOr + " " + names[^1]);
        }

        if (maxFileSize is { } max && max > 0)
        {
            parts.Add(string.Format(CultureInfo.CurrentCulture, text.FileSizeLimit, FormatSize(max)));
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    /// <summary>2000000 becomes "2 MB"; 1536 becomes "1.5 KB". Matches the script's formatSize.</summary>
    public static string FormatSize(long bytes)
    {
        string[] units = ["bytes", "KB", "MB", "GB"];
        double size = bytes;
        var unit = 0;
        while (size >= 1000 && unit < units.Length - 1)
        {
            size /= 1000;
            unit++;
        }

        return Math.Round(size, 1).ToString(CultureInfo.InvariantCulture) + " " + units[unit];
    }

    private static List<string>? TypeNames(string? accept)
    {
        if (string.IsNullOrWhiteSpace(accept)) return null;
        var names = new List<string>();
        foreach (var raw in accept.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string name;
            if (raw.StartsWith('.'))
            {
                name = raw[1..];
            }
            else
            {
                var slash = raw.IndexOf('/');
                if (slash < 0) return null;
                name = raw[(slash + 1)..];
                var plus = name.IndexOf('+');
                if (plus > 0) name = name[..plus];
                if (name is "*" or "" || name.StartsWith("vnd.", StringComparison.OrdinalIgnoreCase) || name.StartsWith("x-", StringComparison.OrdinalIgnoreCase) || name.Contains('.') || name.Contains('-'))
                {
                    return null;
                }
            }

            if (name.Length == 0) return null;
            name = name.ToLowerInvariant() switch
            {
                "plain" => "TXT",
                "msword" => "DOC",
                "quicktime" => "MOV",
                var other => other.ToUpperInvariant(),
            };
            if (!names.Contains(name)) names.Add(name);
        }

        return names;
    }
}
