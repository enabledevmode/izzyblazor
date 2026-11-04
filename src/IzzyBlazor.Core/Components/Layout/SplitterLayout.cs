using System.Globalization;
using System.Text.RegularExpressions;

namespace IzzyBlazor;

/// <summary>
/// What a <see cref="Splitter"/> shares with its panes: its direction, the panes in order, and the
/// sizes kept for this person (from the cookie, or a drag on an interactive page).
/// </summary>
internal sealed partial class SplitterContext(Splitter owner)
{
    private readonly List<SplitterPane> _panes = [];

    public Splitter Owner { get; } = owner;

    public bool Vertical => Owner.Orientation == Orientation.Vertical;

    /// <summary>Each pane's kept size and whether it's collapsed, by position; null when none.</summary>
    public SplitterLayout? Kept { get; set; }

    public int Register(SplitterPane pane)
    {
        _panes.Add(pane);
        return _panes.Count - 1;
    }

    public void Unregister(SplitterPane pane) => _panes.Remove(pane);

    public int IndexOf(SplitterPane pane) => _panes.IndexOf(pane);

    /// <summary>The pane before this one, which the bar before it resizes; null for the first.</summary>
    public SplitterPane? Before(SplitterPane pane) => IndexOf(pane) is > 0 and var i ? _panes[i - 1] : null;

    /// <summary>A layout the script reported on an interactive page: each pane draws it.</summary>
    public void Apply(SplitterLayout layout)
    {
        Kept = layout;
        foreach (var pane in _panes.ToArray())
        {
            pane.Refresh();
        }
    }

    public string? KeptSize(SplitterPane pane) => Kept?.Size(IndexOf(pane));

    public bool? KeptCollapsed(SplitterPane pane) => Kept?.Collapsed(IndexOf(pane));
}

/// <summary>
/// A splitter's kept layout as the cookie stores it: one entry per pane, in order, separated by
/// <c>|</c>. An entry is a size in <c>px</c> or <c>%</c>, or empty for a pane that fills; a
/// leading <c>!</c> marks it collapsed: <c>!256px|35.5%|</c>.
/// </summary>
internal sealed partial class SplitterLayout
{
    private readonly (string? Size, bool Collapsed)[] _entries;

    private SplitterLayout((string? Size, bool Collapsed)[] entries) => _entries = entries;

    public string? Size(int index) => index >= 0 && index < _entries.Length ? _entries[index].Size : null;

    public bool? Collapsed(int index) => index >= 0 && index < _entries.Length ? _entries[index].Collapsed : null;

    /// <summary>The layout in a cookie, or null when it's missing or damaged.</summary>
    public static SplitterLayout? Parse(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 400)
        {
            return null;
        }

        var parts = value.Split('|');
        var entries = new (string?, bool)[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            var match = Entry().Match(parts[i]);
            if (!match.Success)
            {
                return null;
            }

            string? size = null;
            if (match.Groups["n"].Success)
            {
                // Rewritten from the number, so nothing from the cookie reaches the style as typed.
                var n = double.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture);
                size = n.ToString("0.##", CultureInfo.InvariantCulture) + match.Groups["u"].Value;
            }

            entries[i] = (size, match.Groups["c"].Success);
        }

        return new SplitterLayout(entries);
    }

    [GeneratedRegex(@"^(?<c>!)?(?:(?<n>\d{1,5}(?:\.\d{1,2})?)(?<u>px|%))?$", RegexOptions.CultureInvariant)]
    private static partial Regex Entry();
}
