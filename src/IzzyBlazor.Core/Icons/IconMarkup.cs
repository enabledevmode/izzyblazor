using System.Collections.Concurrent;
using System.Xml;

namespace IzzyBlazor;

/// <summary>
/// Checks an icon's markup before <see cref="Icon"/> writes it into the page unescaped. Only SVG
/// drawing elements are allowed; anything that can run script or load something (script,
/// foreignObject, use, image, a, animate, event handlers, href, javascript: values, url() other
/// than a same-document #id) is rejected. So are comments and CDATA, and text anywhere but inside
/// title and desc: the browser parses the string as HTML, not XML, and reads those differently
/// (an XML comment like &lt;!--&gt;...--&gt; ends early in HTML, letting markup after it through).
/// Each distinct body is checked once, so the generated icon sets cost a lookup.
/// </summary>
internal static class IconMarkup
{
    private static readonly HashSet<string> Elements = new(StringComparer.OrdinalIgnoreCase)
    {
        "path", "circle", "ellipse", "line", "polyline", "polygon", "rect", "g",
        "defs", "clipPath", "mask", "linearGradient", "radialGradient", "stop", "title", "desc",
    };

    private const int CacheLimit = 4096;
    private static readonly ConcurrentDictionary<string, string?> Checked = new(StringComparer.Ordinal);

    /// <summary>Throws if the markup isn't plain SVG drawing; otherwise returns it.</summary>
    public static string Verify(string body, string name)
    {
        if (string.IsNullOrEmpty(body))
        {
            return body;
        }

        if (!Checked.TryGetValue(body, out var problem))
        {
            problem = Find(body);
            if (Checked.Count < CacheLimit)
            {
                Checked.TryAdd(body, problem);
            }
        }

        return problem is null
            ? body
            : throw new InvalidOperationException(
                $"Icon \"{(string.IsNullOrEmpty(name) ? "(unnamed)" : name)}\" can't be rendered: {problem}. Icon markup may only draw shapes (path, circle, rect, g and the like).");
    }

    /// <summary>What's wrong with the markup, or null if it's safe.</summary>
    internal static string? Find(string body)
    {
        var settings = new XmlReaderSettings
        {
            ConformanceLevel = ConformanceLevel.Fragment,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = false,
            IgnoreWhitespace = true,
        };

        // Open elements, to know where text is (only directly inside title and desc).
        var open = new Stack<string>();
        try
        {
            using var reader = XmlReader.Create(new StringReader(body), settings);
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        if (!Elements.Contains(reader.LocalName))
                        {
                            return $"<{reader.LocalName}> isn't allowed";
                        }

                        if (open.TryPeek(out var parent) && IsTextElement(parent))
                        {
                            return $"<{reader.LocalName}> inside <{parent}> isn't allowed";
                        }

                        var empty = reader.IsEmptyElement;

                        if (reader.MoveToFirstAttribute())
                        {
                            do
                            {
                                var attribute = reader.LocalName;
                                if (attribute.StartsWith("on", StringComparison.OrdinalIgnoreCase))
                                {
                                    return $"the {attribute} attribute isn't allowed";
                                }

                                if (attribute.Equals("href", StringComparison.OrdinalIgnoreCase))
                                {
                                    return "href isn't allowed";
                                }

                                if (reader.Value.Contains("javascript:", StringComparison.OrdinalIgnoreCase))
                                {
                                    return "javascript: values aren't allowed";
                                }

                                if (HasExternalUrl(reader.Value))
                                {
                                    return "url() other than a #id isn't allowed";
                                }
                            }
                            while (reader.MoveToNextAttribute());

                            reader.MoveToElement();
                        }

                        if (!empty)
                        {
                            open.Push(reader.LocalName);
                        }

                        break;
                    case XmlNodeType.Text:
                        if (!open.TryPeek(out var holder) || !IsTextElement(holder))
                        {
                            return "text is only allowed inside <title> and <desc>";
                        }

                        break;
                    case XmlNodeType.EndElement:
                        open.TryPop(out _);
                        break;
                    default:
                        return $"{reader.NodeType} nodes aren't allowed";
                }
            }
        }
        catch (XmlException e)
        {
            return $"it isn't well-formed markup ({e.Message})";
        }

        return null;
    }

    private static bool IsTextElement(string name) =>
        name.Equals("title", StringComparison.OrdinalIgnoreCase) || name.Equals("desc", StringComparison.OrdinalIgnoreCase);

    /// <summary>True if the value has a url(...) that isn't a same-document #id reference.</summary>
    private static bool HasExternalUrl(string value)
    {
        var at = 0;
        while ((at = value.IndexOf("url(", at, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            at += 4;
            var rest = value.AsSpan(at).TrimStart().TrimStart("'\"");
            if (rest.IsEmpty || rest[0] != '#')
            {
                return true;
            }
        }

        return false;
    }
}
