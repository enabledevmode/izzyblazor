using System.Text.RegularExpressions;

namespace IzzyBlazor.Cli;

/// <summary>
/// A setting's label for a form: the manifest's own <c>title</c>, else its name in words ("PrimaryHref" is
/// "Primary link", "ImageAlt" is "Image alt text"), so a front end never shows the name as code writes it.
/// </summary>
static partial class Titles
{
    // Words a reader wouldn't say as the name has them.
    static readonly Dictionary<string, string> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        ["href"] = "link", ["src"] = "source", ["alt"] = "alt text", ["css"] = "CSS", ["https"] = "HTTPS", ["http"] = "HTTP",
        ["mcp"] = "MCP", ["db"] = "database", ["tld"] = "TLD", ["guid"] = "GUID", ["nav"] = "navigation", ["auth"] = "sign-in",
    };

    public static string For(string name)
    {
        var words = Boundary().Split(name).Where(w => w.Length > 0)
            .Select(w => Words.TryGetValue(w, out var said) ? said : w.ToLowerInvariant()).ToList();
        if (words.Count == 0) return name;
        var text = string.Join(' ', words);
        return char.ToUpperInvariant(text[0]) + text[1..];
    }

    // Between a lowercase letter or digit and a capital, and before the last capital of a run ("LocalDB" is Local, DB).
    [GeneratedRegex(@"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")]
    private static partial Regex Boundary();
}
