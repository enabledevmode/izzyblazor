using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using IzzyBlazor.Generation;

namespace IzzyBlazor.Cli;

/// <summary>
/// A component's tag on a page, for an editor's inspector: which component is at a position and what each of
/// its attributes says (izzy inspect), and the edit that sets or removes one (izzy set). A value written as an
/// expression (<c>@_variant</c>) is reported as one and never rewritten unless a new value is given for it.
/// </summary>
static partial class TagEdit
{
    /// <summary>
    /// The component's tag at the offset: the opening tag it's in, else the nearest component whose content
    /// it's in. Null when it's in neither.
    /// </summary>
    public static RazorTag? Find(string page, int offset, Components library)
    {
        var tags = RazorPage.Tags(page).Where(t => library.Options(t.Name) is not null).ToList();
        if (tags.FirstOrDefault(t => offset >= t.Start && offset < t.End) is { } inside) return inside;
        // Open and close tags up to the offset, in order: what's still open is what the offset is inside.
        var open = new List<RazorTag>();
        // A closing tag in a comment closes nothing.
        var comments = RazorPage.Comments(page);
        var closes = Close().Matches(page).Where(m => m.Index < offset && !comments.Any(c => m.Index >= c.Start && m.Index < c.End)).Select(m => (m.Index, Name: m.Groups[1].Value)).ToList();
        var next = 0;
        foreach (var tag in tags.Where(t => t.End <= offset))
        {
            for (; next < closes.Count && closes[next].Index < tag.Start; next++) Pop(open, closes[next].Name);
            if (!tag.SelfClosing) open.Add(tag);
        }
        for (; next < closes.Count; next++) Pop(open, closes[next].Name);
        return open.Count > 0 ? open[^1] : null;

        static void Pop(List<RazorTag> open, string name)
        {
            var index = open.FindLastIndex(t => t.Name == name);
            if (index >= 0) open.RemoveRange(index, open.Count - index);
        }
    }

    /// <summary>The tag as data: its range, and each attribute with its value as written and as a form holds it.</summary>
    public static JsonObject Inspect(string page, string path, RazorTag tag, Components library)
    {
        var component = library.Find(tag.Name);
        var parameters = library.Parameters(component).GroupBy(p => (string)p["name"]!).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var attributes = new JsonArray();
        foreach (var a in tag.Attributes)
        {
            var bound = a.Name.StartsWith("@bind-", StringComparison.Ordinal);
            var name = bound ? a.Name["@bind-".Length..].Split(':')[0] : a.Name;
            parameters.TryGetValue(name, out var parameter);
            var written = a.HasValue ? page.Substring(a.ValueStart, a.ValueLength) : null;
            var value = parameter is null || bound ? null : Plain((string)parameter["kind"]!, parameter, written);
            attributes.Add(new JsonObject
            {
                ["name"] = a.Name,
                // The option it sets; null for an attribute that isn't one of the component's (class, an event, @ref).
                ["parameter"] = parameter is null ? null : name,
                ["bound"] = bound,
                ["written"] = written,
                // The value a form holds (true, "Outline", 12, "Save"); null when it's an expression.
                ["value"] = value,
                // The C# it's written as, where it isn't a plain value (new Responsive<int>() { Base = 2 }, _size):
                // what a form shows in place of a control. Null beside a value, and for an attribute that isn't an option.
                ["expression"] = parameter is not null && value is null ? Code((string)parameter["kind"]!, written) : null,
                ["start"] = a.Start, ["end"] = a.End,
                ["valueStart"] = a.HasValue ? a.ValueStart : null, ["valueLength"] = a.HasValue ? a.ValueLength : null,
            });
        }
        return new JsonObject
        {
            ["component"] = tag.Name,
            ["docs"] = (string?)component["docs"],
            ["path"] = path,
            ["start"] = tag.Start, ["end"] = tag.End, ["selfClosing"] = tag.SelfClosing,
            ["originalHash"] = Inserts.Hash(page),
            ["attributes"] = attributes,
        };
    }

    // An expression as C#: without the @ Razor asks for, and without the parentheses around the whole of it.
    // Text with an expression in it (Title="Hello @name") stays as written.
    static string Code(string kind, string? written)
    {
        var text = (written ?? "").Trim();
        if (text.StartsWith("@(", StringComparison.Ordinal) && Closes(text, 1) == text.Length - 1) return text[2..^1].Trim();
        // Text is one expression when it is a name with what follows a name: @name, @user.Name, @L["Save"], @Title().
        return text.StartsWith('@') && (kind != "string" || OneName().IsMatch(text)) ? text[1..].Trim() : text;

        // Where the parenthesis at an index closes; -1 when it doesn't. Text in quotes is passed over.
        static int Closes(string text, int open)
        {
            var depth = 0;
            for (var i = open; i < text.Length; i++)
            {
                if (text[i] is '"' or '\'')
                {
                    var quote = text[i];
                    for (i++; i < text.Length && text[i] != quote; i++) if (text[i] == '\\') i++;
                }
                else if (text[i] == '(') depth++;
                else if (text[i] == ')' && --depth == 0) return i;
            }
            return -1;
        }
    }

    // An attribute's value as a form holds it; null when it isn't written plainly.
    static JsonNode? Plain(string kind, JsonObject parameter, string? written)
    {
        // A bare attribute is true.
        if (written is null) return kind == "bool" ? true : null;
        // @(2) is 2 as @2 is.
        if (written.StartsWith('@') && kind != "string") written = Code(kind, written);
        switch (kind)
        {
            case "bool":
                return written is "true" or "false" ? written == "true" : null;
            case "enum":
                var type = ((string)parameter["write"]!).Split('.')[0];
                var member = written.StartsWith(type + ".", StringComparison.Ordinal) ? written[(type.Length + 1)..] : written;
                return parameter["values"]!.AsArray().Any(v => (string)v! == member) ? member : null;
            case "number":
                return double.TryParse(written, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;
            case "string":
                // Razor reads @@ as an @, and an @ between letters or digits (an email address) as text.
                var text = written.Replace("@@", "\u0001", StringComparison.Ordinal);
                for (var i = text.IndexOf('@'); i >= 0; i = text.IndexOf('@', i + 1))
                    if (i == 0 || i == text.Length - 1 || !char.IsLetterOrDigit(text[i - 1]) || !char.IsLetterOrDigit(text[i + 1])) return null;
                return System.Net.WebUtility.HtmlDecode(text.Replace('\u0001', '@'));
            default:
                return null;
        }
    }

    /// <summary>
    /// The edits that set (<paramref name="value"/> given) or remove (null) options on the tag. An option is
    /// named as the component writes it, in any case, with or without dashes.
    /// </summary>
    public static List<TextInsert> Set(string page, string path, RazorTag tag, Components library, IReadOnlyList<(string Option, string? Value)> changes)
    {
        // The last value given for an option is the one that counts.
        changes = changes.GroupBy(c => c.Option.Replace("-", "", StringComparison.Ordinal), StringComparer.OrdinalIgnoreCase).Select(g => g.Last()).ToList();
        var component = library.Find(tag.Name);
        var parameters = library.Parameters(component).GroupBy(p => (string)p["name"]!).Select(g => g.First()).ToList();
        var hash = Inserts.Hash(page);
        var edits = new List<TextInsert>();
        var added = new List<string>();
        foreach (var (option, value) in changes)
        {
            var parameter = parameters.FirstOrDefault(p => Same((string)p["name"]!, option))
                ?? throw new UsageException($"{tag.Name} has no option named \"{option}\". izzy describe component {tag.Name} lists them.");
            var name = (string)parameter["name"]!;
            if (tag.Attributes.Any(a => a.Name == $"@bind-{name}" || a.Name.StartsWith($"@bind-{name}:", StringComparison.Ordinal)))
                throw new UsageException($"{name} is bound with @bind-{name}: change what it's bound to on the page.");
            // HTML reads an attribute's name in any case, and so does Razor for a component's.
            var existing = tag.Attributes.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (value is null)
            {
                if (existing is null) continue;
                // With the space before it, so the tag closes up.
                var from = existing.Start;
                while (from > tag.NameEnd && char.IsWhiteSpace(page[from - 1])) from--;
                edits.Add(new TextInsert(path, from, "", hash) { Length = existing.End - from });
                continue;
            }
            if (ReferenceEquals(value, Switch) && (string)parameter["kind"]! != "bool")
                throw new UsageException($"{name} needs a value: --{option} <value>.");
            var text = Attribute(name, parameter, ReferenceEquals(value, Switch) ? "true" : value);
            if (existing is not null) edits.Add(new TextInsert(path, existing.Start, text, hash) { Length = existing.End - existing.Start });
            else added.Add(text);
        }
        // New ones go after the last attribute, on the tag's first line of them.
        if (added.Count > 0)
        {
            var at = tag.Attributes.Count > 0 ? tag.Attributes[^1].End : tag.NameEnd;
            edits.Add(new TextInsert(path, at, string.Concat(added.Select(a => " " + a)), hash));
        }
        return edits;
    }

    /// <summary>The value of an option given with none: on, for one that's true or false.</summary>
    public static readonly string Switch = new("true");

    // The attribute as Razor writes it for the option's kind.
    static string Attribute(string name, JsonObject parameter, string value)
    {
        switch ((string)parameter["kind"]!)
        {
            case "bool":
                if (!bool.TryParse(value, out var on)) throw new UsageException($"{name} is true or false, not \"{value}\".");
                return on ? name : $"{name}=\"false\"";
            case "enum":
                var member = parameter["values"]!.AsArray().Select(v => (string)v!).FirstOrDefault(v => v.Equals(value, StringComparison.OrdinalIgnoreCase))
                    ?? throw new UsageException($"{name} is one of {string.Join(", ", parameter["values"]!.AsArray().Select(v => (string)v!))}, not \"{value}\".");
                return $"{name}=\"{((string)parameter["write"]!).Replace("{value}", member, StringComparison.Ordinal)}\"";
            case "number":
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) throw new UsageException($"{name} is a number, not \"{value}\".");
                return $"{name}=\"{value}\"";
            case "string":
                // Text, not C#: an @ would start an expression.
                return $"{name}=\"{System.Net.WebUtility.HtmlEncode(value).Replace("@", "@@", StringComparison.Ordinal)}\"";
            default:
                // C# as given: an expression of the option's type.
                return value.Contains('"') ? $"{name}='{value}'" : $"{name}=\"{value}\"";
        }
    }

    static bool Same(string name, string option) =>
        name.Equals(option.Replace("-", "", StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"^@[A-Za-z_]\w*(?:\.[A-Za-z_]\w*|\([^()]*\)|\[[^\[\]]*\])*$")]
    private static partial Regex OneName();

    [GeneratedRegex(@"</([A-Za-z][\w.]*)\s*>")]
    private static partial Regex Close();
}
