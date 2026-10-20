using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;

namespace IzzyBlazor;

/// <summary>
/// The headings of the page, shared by an <see cref="AppShell"/>: each <see cref="Heading"/> in
/// <see cref="AppMain"/> adds itself, and <see cref="OnThisPage"/> lists them. Everything happens
/// while the page renders, so the list is in the first response, with JavaScript off.
/// </summary>
/// <remarks>
/// Headings get ids (and anchor links) only once an <see cref="OnThisPage"/> reads the outline,
/// so an app page without one renders its headings as before.
/// </remarks>
internal sealed class PageOutline
{
    /// <summary>The cascade name <see cref="AppMain"/> gives the outline, so only headings in the main area find it.</summary>
    internal const string MainCascade = "ib-page-outline-main";

    private readonly List<OutlineEntry> _entries = [];
    private readonly Dictionary<object, (int Min, int Max)> _readers = [];

    /// <summary>The headings, in the order they rendered.</summary>
    public IReadOnlyList<OutlineEntry> Entries => _entries;

    /// <summary>Whether an <see cref="OnThisPage"/> reads the outline, so headings need ids.</summary>
    public bool Active => _readers.Count > 0;

    /// <summary>Raised when a heading is added, changed or removed.</summary>
    public event Action? EntriesChanged;

    /// <summary>Raised when an <see cref="OnThisPage"/> starts or stops reading, or changes its levels.</summary>
    public event Action? ReadersChanged;

    /// <summary>Whether a heading of this level is listed by some <see cref="OnThisPage"/>.</summary>
    public bool Lists(int level)
    {
        foreach (var (min, max) in _readers.Values)
        {
            if (level >= min && level <= max)
            {
                return true;
            }
        }

        return false;
    }

    internal void SetReader(object reader, int min, int max)
    {
        if (_readers.TryGetValue(reader, out var range) && range == (min, max))
        {
            return;
        }

        _readers[reader] = (min, max);
        ReadersChanged?.Invoke();
    }

    internal void RemoveReader(object reader)
    {
        if (_readers.Remove(reader))
        {
            ReadersChanged?.Invoke();
        }
    }

    internal OutlineEntry Add(int level, string text, string? explicitId)
    {
        var entry = new OutlineEntry { Level = level, Text = text, ExplicitId = explicitId };
        entry.Id = explicitId ?? UniqueSlug(text, entry);
        _entries.Add(entry);
        EntriesChanged?.Invoke();
        return entry;
    }

    internal void Update(OutlineEntry entry, int level, string text, string? explicitId)
    {
        if (entry.Level == level && entry.Text == text && entry.ExplicitId == explicitId)
        {
            return;
        }

        var slugChanged = entry.Text != text || entry.ExplicitId != explicitId;
        entry.Level = level;
        entry.Text = text;
        entry.ExplicitId = explicitId;
        if (slugChanged)
        {
            entry.Id = explicitId ?? UniqueSlug(text, entry);
        }

        EntriesChanged?.Invoke();
    }

    internal void Remove(OutlineEntry entry)
    {
        if (_entries.Remove(entry))
        {
            EntriesChanged?.Invoke();
        }
    }

    // "Getting started", then "getting-started-2" for a second heading with the same text.
    private string UniqueSlug(string text, OutlineEntry self)
    {
        var slug = Slug(text);
        var candidate = slug;
        for (var n = 2; _entries.Any(e => e != self && e.Id == candidate); n++)
        {
            candidate = string.Create(CultureInfo.InvariantCulture, $"{slug}-{n}");
        }

        return candidate;
    }

    /// <summary>
    /// An id from a heading's text: lowercase, letters and digits kept (in any script), spaces and
    /// dashes as one dash, everything else dropped. "Rows &amp; columns" becomes "rows-columns".
    /// </summary>
    internal static string Slug(string text)
    {
        var sb = new StringBuilder(text.Length);
        var dash = false;
        foreach (var c in text.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                if (dash && sb.Length > 0)
                {
                    sb.Append('-');
                }

                dash = false;
                sb.Append(c);
            }
            else if (char.IsWhiteSpace(c) || c is '-' or '_' or '.' or '/')
            {
                dash = true;
            }
        }

        return sb.Length > 0 ? sb.ToString() : "section";
    }

    /// <summary>
    /// The plain text of a heading's content, for its id and its entry: literal and encoded text,
    /// and text inside elements; components inside are skipped. Also says whether the content holds
    /// a link or button of its own (the heading then gets no anchor link around it).
    /// </summary>
    internal static (string Text, bool HasControl) ReadText(RenderFragment? fragment)
    {
        if (fragment is null)
        {
            return ("", false);
        }

#pragma warning disable BL0006 // Reading render frames: the content is rendered here only to read its text.
        using var builder = new Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder();
        fragment(builder);
        var frames = builder.GetFrames();
        var text = new StringBuilder();
        var hasControl = false;
        for (var i = 0; i < frames.Count; i++)
        {
            ref var frame = ref frames.Array[i];
            switch (frame.FrameType)
            {
                case RenderTreeFrameType.Text:
                    text.Append(frame.TextContent);
                    break;
                case RenderTreeFrameType.Markup:
                    var markup = frame.MarkupContent;
                    hasControl |= markup.Contains("<a", StringComparison.OrdinalIgnoreCase) || markup.Contains("<button", StringComparison.OrdinalIgnoreCase);
                    text.Append(WebUtility.HtmlDecode(StripTags(markup)));
                    break;
                case RenderTreeFrameType.Element:
                    hasControl |= frame.ElementName is "a" or "button";
                    break;
                case RenderTreeFrameType.Component:
                    // Its content is a parameter, not frames here: skip the component and its attributes.
                    i += frame.ComponentSubtreeLength - 1;
                    break;
            }
        }
#pragma warning restore BL0006

        return (CollapseSpaces(text.ToString()), hasControl);
    }

    private static string StripTags(string markup)
    {
        if (!markup.Contains('<'))
        {
            return markup;
        }

        var sb = new StringBuilder(markup.Length);
        var inTag = false;
        foreach (var c in markup)
        {
            if (c == '<') inTag = true;
            else if (c == '>') inTag = false;
            else if (!inTag) sb.Append(c);
        }

        return sb.ToString();
    }

    private static string CollapseSpaces(string text)
    {
        var sb = new StringBuilder(text.Length);
        var space = false;
        foreach (var c in text.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                space = true;
                continue;
            }

            if (space)
            {
                sb.Append(' ');
                space = false;
            }

            sb.Append(c);
        }

        return sb.ToString();
    }
}

/// <summary>One heading in a <see cref="PageOutline"/>.</summary>
internal sealed class OutlineEntry
{
    public int Level { get; set; }

    public string Text { get; set; } = "";

    public string Id { get; set; } = "";

    public string? ExplicitId { get; set; }
}

/// <summary>Links to a place on the current page.</summary>
internal static class PageLinks
{
    /// <summary>
    /// The current page's path and query with <c>#id</c>, relative to the base URL. A bare
    /// <c>#id</c> would resolve against <c>&lt;base href&gt;</c> and lead to another page.
    /// </summary>
    internal static string ToFragment(NavigationManager navigation, string id)
    {
        var uri = navigation.Uri;
        var hash = uri.IndexOf('#', StringComparison.Ordinal);
        if (hash >= 0)
        {
            uri = uri[..hash];
        }

        return $"{navigation.ToBaseRelativePath(uri)}#{Uri.EscapeDataString(id)}";
    }
}
