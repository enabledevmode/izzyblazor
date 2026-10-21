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

    internal OutlineEntry Add(int level, string text, string? explicitId, Action? idChanged = null)
    {
        var entry = new OutlineEntry { Level = level, Text = text, ExplicitId = explicitId, IdChanged = idChanged };
        _entries.Add(entry);
        AssignIds(entry);
        EntriesChanged?.Invoke();
        return entry;
    }

    internal void Update(OutlineEntry entry, int level, string text, string? explicitId)
    {
        if (entry.Level == level && entry.Text == text && entry.ExplicitId == explicitId)
        {
            return;
        }

        var idsChange = entry.Text != text || entry.ExplicitId != explicitId;
        entry.Level = level;
        entry.Text = text;
        entry.ExplicitId = explicitId;
        if (idsChange)
        {
            AssignIds(entry);
        }

        EntriesChanged?.Invoke();
    }

    internal void Remove(OutlineEntry entry)
    {
        if (_entries.Remove(entry))
        {
            AssignIds(null);
            EntriesChanged?.Invoke();
        }
    }

    // Slugs in page order, so the ids are the same however the headings got here: "Getting
    // started", then "getting-started-2" for a second heading with the same text, never one an
    // explicit id has. Other headings whose id changes re-render; the one rendering now doesn't need to.
    private void AssignIds(OutlineEntry? rendering)
    {
        var taken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in _entries)
        {
            if (e.ExplicitId is { } id)
            {
                taken.Add(id);
            }
        }

        foreach (var e in _entries)
        {
            string id;
            if (e.ExplicitId is { } explicitId)
            {
                id = explicitId;
            }
            else
            {
                var slug = Slug(e.Text);
                id = slug;
                for (var n = 2; taken.Contains(id); n++)
                {
                    id = string.Create(CultureInfo.InvariantCulture, $"{slug}-{n}");
                }

                taken.Add(id);
            }

            if (e.Id != id)
            {
                var first = e.Id.Length == 0;
                e.Id = id;
                if (!first && e != rendering)
                {
                    e.IdChanged?.Invoke();
                }
            }
        }
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
    /// The plain text of some content, for a heading's id and entry or a banner's key: literal and
    /// encoded text, text inside elements, and the content of IzzyBlazor components inside. Also
    /// says whether the content holds a link or button of its own (<c>a</c>, <c>button</c>,
    /// <see cref="Anchor"/>, a button component, <c>NavLink</c>, or a component given an
    /// <c>Href</c> or <c>OnClick</c>), whether it holds a component that might render one (any but
    /// <see cref="Icon"/>, <see cref="Badge"/>, <see cref="Kbd"/> and <see cref="TextBlock"/>), and
    /// whether it holds a component from outside IzzyBlazor, whose text can't be read here.
    /// </summary>
    internal static ContentText ReadText(RenderFragment? fragment)
    {
        var text = new StringBuilder();
        var result = new ContentText();
        Read(fragment, text, ref result, 0);
        result.Text = CollapseSpaces(text.ToString());
        return result;
    }

    // Components that only show what they're given (or an icon).
    private static readonly HashSet<Type> PlainComponents = [typeof(Icon), typeof(Badge), typeof(Kbd), typeof(TextBlock)];

    private static void Read(RenderFragment? fragment, StringBuilder text, ref ContentText result, int depth)
    {
        if (fragment is null || depth > 8)
        {
            return;
        }

#pragma warning disable BL0006 // Reading render frames: the content is rendered here only to read its text.
        using var builder = new Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder();
        fragment(builder);
        var frames = builder.GetFrames();
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
                    result.HasControl |= markup.Contains("<a", StringComparison.OrdinalIgnoreCase) || markup.Contains("<button", StringComparison.OrdinalIgnoreCase);
                    text.Append(WebUtility.HtmlDecode(StripTags(markup)));
                    break;
                case RenderTreeFrameType.Element:
                    result.HasControl |= frame.ElementName is "a" or "button";
                    break;
                case RenderTreeFrameType.Attribute:
                    // An element's link target tells two messages with the same words apart.
                    if (frame.AttributeName == "href" && frame.AttributeValue is string elementHref)
                    {
                        result.Links += elementHref + "\n";
                    }

                    break;
                case RenderTreeFrameType.Component:
                    var type = frame.ComponentType;
                    var end = i + frame.ComponentSubtreeLength;
                    var ours = type.Assembly == typeof(PageOutline).Assembly;
                    var isControl = type == typeof(Anchor) || typeof(ButtonBase).IsAssignableFrom(type) || type == typeof(Microsoft.AspNetCore.Components.Routing.NavLink);
                    for (var j = i + 1; j < end; j++)
                    {
                        ref var attribute = ref frames.Array[j];
                        if (attribute.FrameType != RenderTreeFrameType.Attribute)
                        {
                            break;
                        }

                        var name = attribute.AttributeName;
                        if (name is "Href" or "href" or "OnClick" or "onclick")
                        {
                            isControl = true;
                            if (attribute.AttributeValue is string href)
                            {
                                result.Links += href + "\n";
                            }
                        }
                        else if (ours && attribute.AttributeValue is RenderFragment child)
                        {
                            Read(child, text, ref result, depth + 1);
                        }
                    }

                    result.HasControl |= isControl;
                    result.MayHaveControl |= !isControl && !PlainComponents.Contains(type);
                    result.Opaque |= !ours && type != typeof(Microsoft.AspNetCore.Components.Routing.NavLink);
                    i = end - 1;
                    break;
            }
        }
#pragma warning restore BL0006
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

/// <summary>What <see cref="PageOutline.ReadText"/> found in some content.</summary>
internal struct ContentText
{
    /// <summary>The text, with runs of white space as one space.</summary>
    public string Text { get; set; }

    /// <summary>Whether the content holds a link or button.</summary>
    public bool HasControl { get; set; }

    /// <summary>Whether the content holds a component that might render a link or button.</summary>
    public bool MayHaveControl { get; set; }

    /// <summary>Whether the content holds a component from outside IzzyBlazor, whose text can't be read here.</summary>
    public bool Opaque { get; set; }

    /// <summary>The link targets in the content, one per line.</summary>
    public string? Links { get; set; }
}

/// <summary>One heading in a <see cref="PageOutline"/>.</summary>
internal sealed class OutlineEntry
{
    public int Level { get; set; }

    public string Text { get; set; } = "";

    public string Id { get; set; } = "";

    public string? ExplicitId { get; set; }

    /// <summary>Called when the id changes because of another heading, so this one re-renders.</summary>
    public Action? IdChanged { get; set; }
}

/// <summary>Links to a place on the current page.</summary>
internal static class PageLinks
{
    /// <summary>
    /// The current page's path and query with <c>#id</c>, relative to the base URL. A bare
    /// <c>#id</c> would resolve against <c>&lt;base href&gt;</c> and lead to another page. It starts
    /// with <c>./</c>, so a path like <c>javascript:x</c> or <c>//other.example</c> (a route that
    /// echoes the request) stays a path on this site.
    /// </summary>
    internal static string ToFragment(NavigationManager navigation, string id)
    {
        var uri = navigation.Uri;
        var hash = uri.IndexOf('#', StringComparison.Ordinal);
        if (hash >= 0)
        {
            uri = uri[..hash];
        }

        return $"./{navigation.ToBaseRelativePath(uri)}#{Uri.EscapeDataString(id)}";
    }
}
