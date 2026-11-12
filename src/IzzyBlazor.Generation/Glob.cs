using System.Text;
using System.Text.RegularExpressions;

namespace IzzyBlazor.Generation;

/// <summary>A path pattern: <c>*</c> and <c>?</c> stay within a folder, <c>**</c> crosses folders, and the whole path must match.</summary>
internal sealed class Glob(string pattern)
{
    readonly Regex _regex = new(ToRegex(pattern), RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public bool Matches(string path) => _regex.IsMatch(path);

    static string ToRegex(string pattern)
    {
        var regex = new StringBuilder("^");
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c == '*' && i + 1 < pattern.Length && pattern[i + 1] == '*')
            {
                var slash = i + 2 < pattern.Length && pattern[i + 2] == '/';
                regex.Append(slash ? "(?:.*/)?" : ".*");
                i += slash ? 2 : 1;
            }
            else if (c == '*') regex.Append("[^/]*");
            else if (c == '?') regex.Append("[^/]");
            else regex.Append(Regex.Escape(c.ToString()));
        }
        return regex.Append('$').ToString();
    }
}
