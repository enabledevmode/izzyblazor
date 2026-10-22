using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace IzzyBlazor.DocsGen;

/// <summary>
/// Reads the compiler's XML doc files and turns their standard tags into HTML: <c>para</c>,
/// <c>c</c>, <c>code</c>, <c>list</c>, <c>see</c> and <c>paramref</c>. A <c>see cref</c> becomes a link
/// to <c>cref:{doc id}</c>, which the reader resolves to a page.
/// </summary>
public sealed partial class XmlDocs
{
    private readonly Dictionary<string, XElement> _members = new(StringComparer.Ordinal);

    public void Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No XML doc file at {path}. Is GenerateDocumentationFile on?", path);
        }

        foreach (var member in XDocument.Load(path).Descendants("member"))
        {
            if (member.Attribute("name")?.Value is { } name)
            {
                _members[name] = member;
            }
        }
    }

    public int Count => _members.Count;

    public XElement? Get(string id) => _members.GetValueOrDefault(id);

    public XElement? ForType(Type type) => Get("T:" + DocName(type));

    public XElement? ForProperty(PropertyInfo property) =>
        Resolve(property, p => Get($"P:{DocName(p.DeclaringType!)}.{p.Name}"), p => BaseProperty(p));

    public XElement? ForField(FieldInfo field) => Get($"F:{DocName(field.DeclaringType!)}.{field.Name}");

    public XElement? ForMethod(MethodInfo method)
    {
        var prefix = $"M:{DocName(method.DeclaringType!)}.{method.Name}";
        var parameters = method.GetParameters();
        var exact = parameters.Length == 0 ? prefix : $"{prefix}({string.Join(",", parameters.Select(p => ParamDocName(p.ParameterType)))})";
        return Get(exact) ?? _members.FirstOrDefault(m => m.Key.StartsWith(prefix + "(", StringComparison.Ordinal) || m.Key == prefix).Value;
    }

    // <inheritdoc /> on an override: use the member it overrides.
    private static XElement? Resolve<T>(T member, Func<T, XElement?> get, Func<T, T?> next) where T : class
    {
        for (var current = member; current is not null; current = next(current))
        {
            var doc = get(current);
            if (doc is not null && doc.Element("inheritdoc") is null)
            {
                return doc;
            }
        }

        return null;
    }

    private static PropertyInfo? BaseProperty(PropertyInfo property)
    {
        var getter = property.GetMethod;
        var baseGetter = getter?.GetBaseDefinition();
        if (getter is null || baseGetter is null || baseGetter.DeclaringType == property.DeclaringType)
        {
            return null;
        }

        return baseGetter.DeclaringType!.GetProperty(property.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
    }

    /// <summary>The name doc ids use for a type: <c>IzzyBlazor.Select`1</c>, nested types with a dot.</summary>
    public static string DocName(Type type)
    {
        if (type.IsGenericType && !type.IsGenericTypeDefinition)
        {
            type = type.GetGenericTypeDefinition();
        }

        return (type.FullName ?? type.Name).Replace('+', '.');
    }

    private static string ParamDocName(Type type)
    {
        if (type.IsGenericParameter)
        {
            return type.DeclaringMethod is not null ? $"``{type.GenericParameterPosition}" : $"`{type.GenericParameterPosition}";
        }

        if (type.IsByRef)
        {
            return ParamDocName(type.GetElementType()!) + "@";
        }

        if (type.IsArray)
        {
            return ParamDocName(type.GetElementType()!) + "[]";
        }

        if (type.IsGenericType)
        {
            var name = DocName(type.GetGenericTypeDefinition());
            name = name[..name.IndexOf('`')];
            return $"{name}{{{string.Join(",", type.GetGenericArguments().Select(ParamDocName))}}}";
        }

        return DocName(type);
    }

    /// <summary>The HTML for one tag of a member's docs (<c>summary</c>, <c>remarks</c>), or null.</summary>
    public static string? Html(XElement? member, string tag)
    {
        var element = member?.Element(tag);
        if (element is null)
        {
            return null;
        }

        var html = Paragraphs(element);
        return string.IsNullOrWhiteSpace(html) ? null : html;
    }

    /// <summary>The HTML for one <c>param</c> tag of a member's docs (a positional record's values), or null.</summary>
    public static string? Param(XElement? member, string name)
    {
        var element = member?.Elements("param").FirstOrDefault(p => p.Attribute("name")?.Value == name);
        if (element is null)
        {
            return null;
        }

        var html = Paragraphs(element);
        return string.IsNullOrWhiteSpace(html) ? null : html;
    }

    /// <summary>The code of the member's <c>example</c> tags, dedented, as plain text.</summary>
    public static List<string>? Examples(XElement? member)
    {
        var codes = member?.Elements("example").SelectMany(e => e.Elements("code")).Select(c => Dedent(c.Value)).Where(c => c.Length > 0).ToList();
        return codes is { Count: > 0 } ? codes : null;
    }

    // Splits the content at <para> and block elements into paragraphs.
    private static string Paragraphs(XElement element)
    {
        var blocks = new List<string>();
        var inline = new StringBuilder();

        void Flush()
        {
            var text = inline.ToString().Trim();
            if (text.Length > 0)
            {
                blocks.Add($"<p>{text}</p>");
            }

            inline.Clear();
        }

        foreach (var node in element.Nodes())
        {
            if (node is XElement { Name.LocalName: "para" } para)
            {
                Flush();
                blocks.Add(Paragraphs(para));
            }
            else if (node is XElement { Name.LocalName: "list" } list)
            {
                Flush();
                blocks.Add(List(list));
            }
            else if (node is XElement { Name.LocalName: "code" } code)
            {
                Flush();
                blocks.Add($"<pre><code>{WebUtility.HtmlEncode(Dedent(code.Value))}</code></pre>");
            }
            else
            {
                inline.Append(Inline(node));
            }
        }

        Flush();
        return string.Join("", blocks);
    }

    private static string List(XElement list)
    {
        var tag = list.Attribute("type")?.Value == "number" ? "ol" : "ul";
        var items = list.Elements("item").Select(item =>
        {
            var term = item.Element("term");
            var description = item.Element("description");
            var body = description is not null ? InlineAll(description) : InlineAll(item);
            return term is not null ? $"<li><strong>{InlineAll(term)}</strong>: {body}</li>" : $"<li>{body}</li>";
        });
        return $"<{tag}>{string.Concat(items)}</{tag}>";
    }

    private static string InlineAll(XElement element) => string.Concat(element.Nodes().Select(Inline)).Trim();

    private static string Inline(XNode node) => node switch
    {
        XText text => WebUtility.HtmlEncode(Whitespace().Replace(text.Value, " ")),
        XElement { Name.LocalName: "c" } c => $"<code>{WebUtility.HtmlEncode(c.Value)}</code>",
        XElement { Name.LocalName: "see" or "seealso" } see => See(see),
        XElement { Name.LocalName: "paramref" or "typeparamref" } p => $"<code>{WebUtility.HtmlEncode(p.Attribute("name")?.Value ?? "")}</code>",
        XElement { Name.LocalName: "br" } => "<br>",
        XElement { Name.LocalName: "b" or "strong" } b => $"<strong>{InlineAll(b)}</strong>",
        XElement { Name.LocalName: "i" or "em" } i => $"<em>{InlineAll(i)}</em>",
        XElement e => InlineAll(e),
        _ => "",
    };

    private static string See(XElement see)
    {
        if (see.Attribute("langword")?.Value is { } word)
        {
            return $"<code>{WebUtility.HtmlEncode(word)}</code>";
        }

        if (see.Attribute("href")?.Value is { } href)
        {
            var label = see.Value.Trim();
            return $"<a href=\"{WebUtility.HtmlEncode(href)}\">{WebUtility.HtmlEncode(label.Length > 0 ? label : href)}</a>";
        }

        var cref = see.Attribute("cref")?.Value;
        if (cref is null)
        {
            return InlineAll(see);
        }

        var text = see.Value.Trim();
        var inner = text.Length > 0 ? WebUtility.HtmlEncode(text) : $"<code>{WebUtility.HtmlEncode(CrefText(cref))}</code>";
        return $"<a href=\"cref:{WebUtility.HtmlEncode(cref)}\">{inner}</a>";
    }

    /// <summary>How a cref reads in text: a type's name, a member's name, an enum value as <c>Type.Value</c>.</summary>
    public static string CrefText(string cref)
    {
        var kind = cref.Length > 2 && cref[1] == ':' ? cref[0] : 'T';
        var name = cref.Length > 2 && cref[1] == ':' ? cref[2..] : cref;
        var paren = name.IndexOf('(');
        if (paren >= 0)
        {
            name = name[..paren];
        }

        name = GenericArity().Replace(name, "");
        var parts = name.Split('.');
        return kind switch
        {
            'F' when parts.Length >= 2 => $"{parts[^2]}.{parts[^1]}",
            'T' => GenericTypeText(cref, parts[^1]),
            _ => parts[^1],
        };
    }

    private static string GenericTypeText(string cref, string name)
    {
        var arity = Regex.Match(cref, @"`(\d+)$");
        if (!arity.Success)
        {
            return name;
        }

        var count = int.Parse(arity.Groups[1].Value);
        return count == 1 ? $"{name}<T>" : $"{name}<{string.Join(", ", Enumerable.Range(1, count).Select(i => $"T{i}"))}>";
    }

    /// <summary>Removes the common indentation and blank lines around a code block.</summary>
    public static string Dedent(string code)
    {
        var lines = code.Replace("\r\n", "\n").Split('\n').ToList();
        while (lines.Count > 0 && lines[0].Trim().Length == 0)
        {
            lines.RemoveAt(0);
        }

        while (lines.Count > 0 && lines[^1].Trim().Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        var indent = lines.Where(l => l.Trim().Length > 0).Select(l => l.Length - l.TrimStart().Length).DefaultIfEmpty(0).Min();
        return string.Join("\n", lines.Select(l => l.Length >= indent ? l[indent..] : l.TrimStart()));
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"`+\d+")]
    private static partial Regex GenericArity();
}
