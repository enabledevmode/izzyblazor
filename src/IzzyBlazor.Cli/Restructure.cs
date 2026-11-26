using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using IzzyBlazor.Generation;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace IzzyBlazor.Cli;

/// <summary>An element on a page: its opening tag, and where it ends (just past its closing tag).</summary>
sealed record RazorElement(RazorTag Tag, int Start, int End);

/// <summary>
/// Edits that move markup without changing what it says, for an editor's refactorings: a wrapper around an
/// element or a run of siblings (izzy wrap), a wrapper taken away (izzy unwrap) and markup moved to a
/// component of its own (izzy extract). Each is one replacement of the page's text, indented as the page is.
/// </summary>
static partial class Restructure
{
    /// <summary>Every element outside <c>@code</c> and comments whose end can be read, outermost first.</summary>
    public static List<RazorElement> Elements(string page)
    {
        var elements = new List<RazorElement>();
        foreach (var tag in RazorPage.Tags(page))
        {
            var end = tag.SelfClosing ? tag.End - 1 : RazorPage.ElementEnd(page, tag.Start);
            if (end >= 0) elements.Add(new RazorElement(tag, tag.Start, end + 1));
        }
        return elements;
    }

    /// <summary>The innermost element the offset is in; null when it's in none.</summary>
    public static RazorElement? At(List<RazorElement> elements, int offset) =>
        elements.Where(e => offset >= e.Start && offset < e.End).OrderBy(e => e.End - e.Start).FirstOrDefault();

    /// <summary>The element an element sits in; null at the top of the page.</summary>
    public static RazorElement? Parent(List<RazorElement> elements, RazorElement element) =>
        elements.Where(e => e != element && e.Start <= element.Start && e.End >= element.End).OrderBy(e => e.End - e.Start).FirstOrDefault();

    /// <summary>
    /// The run of siblings from the element at one offset to the element at another; one element when the
    /// second is null. The two must sit in the same parent.
    /// </summary>
    public static (int Start, int End, List<RazorElement> Run, RazorElement? Parent) Range(string page, string path, int from, int? to)
    {
        var elements = Elements(page);
        var first = At(elements, from) ?? throw new UsageException($"No element at that place in {path}: put the position on a tag.");
        var parent = Parent(elements, first);
        if (to is not { } other) return (first.Start, first.End, [first], parent);
        var last = At(elements, other) ?? throw new UsageException($"No element at the range's end in {path}: put the position on a tag.");
        if (last == first) return (first.Start, first.End, [first], parent);
        if (Parent(elements, last) != parent)
            throw new UsageException($"The range starts in {Named(parent)} and ends in {Named(Parent(elements, last))}: give a range of elements that sit side by side in one parent.");
        var (start, end) = (Math.Min(first.Start, last.Start), Math.Max(first.End, last.End));
        var run = elements.Where(e => e.Start >= start && e.End <= end && Parent(elements, e) == parent).ToList();
        return (start, end, run, parent);

        static string Named(RazorElement? e) => e is null ? "the page itself" : $"<{e.Tag.Name}>";
    }

    /// <summary>What one indent step is on this page: a tab, or its smallest run of leading spaces.</summary>
    public static string IndentUnit(string page)
    {
        // Lines that start a tag or a directive: a comment's or an attribute's continuation lines are aligned, not indented.
        var step = 0;
        foreach (var line in page.Split('\n'))
        {
            var text = line.TrimStart(' ', '\t');
            if (text.Length == 0 || text[0] is not ('<' or '@')) continue;
            if (line[0] == '\t') return "\t";
            for (var (a, b) = (step, line.Length - text.Length); ; (a, b) = (b, a % b))
                if (b == 0) { step = a; break; }
        }
        return new string(' ', step is >= 2 and <= 8 ? step : 4);
    }

    // Whether the text from start to end is all of its lines: nothing but spaces before it and after it.
    static bool WholeLines(string page, int start, int end, out string indent)
    {
        var lineStart = page.LastIndexOf('\n', Math.Max(start - 1, 0)) is var n && n >= 0 && start > 0 ? n + 1 : 0;
        indent = page[lineStart..start];
        var lineEnd = page.IndexOf('\n', end) is var m and >= 0 ? m : page.Length;
        return indent.Trim().Length == 0 && page[end..lineEnd].Trim().Length == 0;
    }

    static string Eol(string page) => page.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    // Text with every line after its first one step deeper (or shallower); blank lines stay blank.
    static string Shift(string text, string unit, bool deeper)
    {
        var lines = text.Split('\n');
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim().Length == 0) continue;
            lines[i] = deeper ? unit + lines[i] : lines[i].StartsWith(unit, StringComparison.Ordinal) ? lines[i][unit.Length..] : lines[i].TrimStart(' ', '\t');
        }
        return string.Join('\n', lines);
    }

    static readonly HashSet<string> Html = new(StringComparer.Ordinal)
    {
        "div", "span", "section", "article", "aside", "header", "footer", "main", "nav", "p", "ul", "ol", "li", "form", "fieldset", "label",
        "a", "button", "figure", "details", "blockquote", "strong", "em", "small", "h1", "h2", "h3", "h4", "h5", "h6",
    };

    /// <summary>
    /// The wrapper as its tag is written: a component's name in the library's own case (row is Row), or an HTML
    /// element's as given.
    /// </summary>
    public static string WrapperName(string given, Components library) =>
        library.Options(given) is not null || Html.Contains(given) ? given
        : library.All.Select(c => (string)c["name"]!).FirstOrDefault(n => n.Equals(given, StringComparison.OrdinalIgnoreCase) && library.Options(n) is not null) ?? given;

    /// <summary>The wrapper's opening tag: a component's with its options checked, or a plain element's.</summary>
    public static string OpenTag(string name, Components library, IReadOnlyList<(string Option, string? Value)> options)
    {
        if (library.Options(name) is not null)
        {
            var open = $"<{name}>";
            var edits = TagEdit.Set(open, "", RazorPage.TagAt(open, 0)!, library, options);
            foreach (var edit in edits.OrderByDescending(e => e.Offset)) open = open[..edit.Offset] + edit.Text + open[(edit.Offset + edit.Length)..];
            return open;
        }
        if (!Html.Contains(name))
            throw new UsageException($"{name} isn't an IzzyBlazor component or an HTML element that holds content. izzy list component lists the components.");
        var tag = new StringBuilder("<").Append(name);
        foreach (var (option, value) in options)
        {
            if (!Regex.IsMatch(option, @"^[a-z][\w-]*$")) throw new UsageException($"--{option} isn't an attribute's name.");
            tag.Append(' ').Append(option);
            if (value is not null && !ReferenceEquals(value, TagEdit.Switch)) tag.Append("=\"").Append(System.Net.WebUtility.HtmlEncode(value).Replace("@", "@@", StringComparison.Ordinal)).Append('"');
        }
        return tag.Append('>').ToString();
    }

    /// <summary>The edits that put a wrapper around the text from start to end.</summary>
    public static List<TextInsert> Wrap(string page, string path, int start, int end, string name, string open, string imports, Components library)
    {
        var hash = Inserts.Hash(page);
        var eol = Eol(page);
        var content = page[start..end];
        var text = WholeLines(page, start, end, out var indent)
            ? open + eol + indent + IndentUnit(page) + Shift(content, IndentUnit(page), deeper: true) + eol + indent + $"</{name}>"
            : open + content + $"</{name}>";
        var edits = new List<TextInsert> { new(path, start, text, hash) { Length = end - start } };
        if (library.Options(name) is not null && Using(page, path, imports, "IzzyBlazor", hash) is { } added) edits.Insert(0, added);
        return edits;
    }

    // The @using line a page needs for a namespace that neither it nor its _Imports files name.
    public static TextInsert? Using(string page, string path, string imports, string ns, string hash)
    {
        var line = new Regex($@"^\s*@using\s+{Regex.Escape(ns)}\s*;?\s*$", RegexOptions.Multiline);
        if (line.IsMatch(page) || line.IsMatch(imports)) return null;
        return new TextInsert(path, ExampleInsert.DirectivesEnd(page), $"@using {ns}{Eol(page)}", hash);
    }

    /// <summary>The edit that takes an element away and keeps what's inside it, one level out.</summary>
    public static List<TextInsert> Unwrap(string page, string path, RazorElement element, Components library)
    {
        var name = element.Tag.Name;
        if (element.Tag.SelfClosing || element.End == element.Tag.End)
            throw new UsageException($"<{name}> has nothing inside it to keep. Delete it instead.");
        if (library.Options(name) is not null)
        {
            var slots = library.Find(name)["slots"]?.AsArray().Select(s => (string)s!["name"]!).Where(s => s != "ChildContent").ToHashSet(StringComparer.Ordinal) ?? [];
            var elements = Elements(page);
            var held = elements.Where(e => slots.Contains(e.Tag.Name) && Parent(elements, e)?.Start == element.Start).Select(e => $"<{e.Tag.Name}>").Distinct().ToList();
            if (held.Count > 0)
                throw new UsageException($"<{name}> holds {string.Join(" and ", held)}, which {(held.Count == 1 ? "is" : "are")} {name}'s own and can't stand alone. Move or remove {(held.Count == 1 ? "it" : "them")} first.");
        }
        var close = page.LastIndexOf("</", element.End - 1, StringComparison.Ordinal);
        var inner = page[element.Tag.End..close];
        var hash = Inserts.Hash(page);
        // The opening and closing tags each on lines of their own: their lines go, and what's between moves out a step.
        var innerStart = page.IndexOf('\n', element.Tag.End) + 1;
        var closeLine = page.LastIndexOf('\n', close) + 1;
        if (WholeLines(page, element.Start, element.End, out _) && innerStart > 0 && page[element.Tag.End..innerStart].Trim().Length == 0
            && closeLine >= innerStart && page[closeLine..close].Trim().Length == 0)
        {
            var lineStart = element.Start == 0 ? 0 : page.LastIndexOf('\n', element.Start - 1) + 1;
            var body = page[innerStart..closeLine];
            var unit = IndentUnit(page);
            var moved = string.Join('\n', body.Split('\n').Select(l => l.Trim().Length == 0 ? l : l.StartsWith(unit, StringComparison.Ordinal) ? l[unit.Length..] : l.TrimStart(' ', '\t')));
            // Without the last line's ending, which the text after the element still has.
            moved = moved.EndsWith("\r\n", StringComparison.Ordinal) ? moved[..^2] : moved.EndsWith('\n') ? moved[..^1] : moved;
            return [new TextInsert(path, lineStart, moved, hash) { Length = element.End - lineStart }];
        }
        return [new TextInsert(path, element.Start, inner, hash) { Length = element.End - element.Start }];
    }

    /// <summary>The wrappers that fit around a run of elements, the likeliest first.</summary>
    public static JsonArray Suggest(List<RazorElement> run, RazorElement? parent, Components library)
    {
        var found = new List<(string Component, string Why)>();
        var names = run.Select(e => e.Tag.Name).ToList();
        bool Is(string name, params string[] categories) => library.Options(name) is not null && categories.Contains((string?)library.Find(name)["category"]);
        var inputs = names.Count(n => n is "input" or "select" or "textarea" || Is(n, "Inputs", "Dropdowns", "Date and time"));

        if (parent?.Tag.Name == "Grid" && names.Any(n => n != "GridItem"))
            found.Add(("GridItem", "It sits in a Grid: a GridItem says how many columns it spans."));
        if (parent?.Tag.Name == "TileLayout" && names.Any(n => n != "Tile"))
            found.Add(("Tile", "It sits in a TileLayout, which places Tiles."));
        // A control of the page's own, not one of the library's, which bring their label, hint and errors.
        if (run.Count == 1 && parent?.Tag.Name != "FieldFrame" && names[0] is "input" or "select" or "textarea" or "InputText" or "InputNumber" or "InputDate" or "InputSelect" or "InputTextArea" or "InputCheckbox" or "InputFile")
            found.Add(("FieldFrame", "A control of your own: a FieldFrame gives it a label, a hint and its errors. Set Label, and Id to the control's id. A TextField or a Select does this itself, and may be the better choice."));
        if (inputs >= 2)
            found.Add(("Fieldset", "Several fields that belong together: a Fieldset groups them under one legend."));
        if (run.Count >= 2)
        {
            found.Add(("Stack", "Puts them one under another with an even gap."));
            found.Add(("Row", "Puts them side by side, and wraps them when there's no room."));
            found.Add(("Grid", "Places them in columns that change with the width."));
        }
        if (run.Count == 1 && names[0] is "Button" or "IconButton" or "Badge" or "Avatar" or "Icon")
            found.Add(("Tooltip", "Shows a short note beside it on hover and on focus."));
        if (parent is null) found.Add(("Container", "Centers the content and keeps its lines a readable width."));
        found.Add(("Card", "Sets the content apart on a surface of its own."));
        found.Add(("Box", "A plain box for padding, a background or a border."));
        if (run.Count == 1) found.Add(("Center", "Centers it in the space it has."));

        var list = new JsonArray();
        foreach (var (component, why) in found.Where(f => library.Options(f.Component) is not null && !names.Contains(f.Component) || f.Component is "Card" or "Box").DistinctBy(f => f.Component))
        {
            if (library.Options(component) is null) continue;
            var c = library.Find(component);
            list.Add(new JsonObject { ["component"] = component, ["title"] = (string?)c["displayName"] ?? component, ["why"] = why, ["docs"] = (string?)c["docs"] });
        }
        return list;
    }

    /// <summary>
    /// The edits that move the text from start to end into a component of its own: the new file, and its tag in
    /// the text's place. A field or property of the page's <c>@code</c> that the markup reads becomes a
    /// parameter, and a service the page injects is injected there too. What can't be passed that way (a method,
    /// a value the markup changes, a loop's variable) is named in the warnings, for a person to wire.
    /// </summary>
    public static (List<TextInsert> Inserts, FileCreate Create, List<string> Warnings) Extract(string page, string path, int start, int end, string name, string file, Components library)
    {
        var eol = Eol(page);
        var warnings = new List<string>();
        var block = WholeLines(page, start, end, out var indent);
        var markup = page[start..end].Replace("\r\n", "\n", StringComparison.Ordinal);
        if (block) markup = Shift(markup, indent, deeper: false);

        // What the page's @code declares, and the services it injects.
        var code = string.Concat(RazorPage.CodeBlocks(page).Select(b => page[(b.Open + 1)..b.Close] + "\n"));
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var methods = new HashSet<string>(StringComparer.Ordinal);
        var types = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in CSharpSyntaxTree.ParseText("class C {" + code + "\n}").GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First().Members)
        {
            switch (member)
            {
                case FieldDeclarationSyntax field:
                    foreach (var variable in field.Declaration.Variables) values[variable.Identifier.Text] = field.Declaration.Type.ToString();
                    break;
                case PropertyDeclarationSyntax property: values[property.Identifier.Text] = property.Type.ToString(); break;
                case MethodDeclarationSyntax method: methods.Add(method.Identifier.Text); break;
                case BaseTypeDeclarationSyntax type: types.Add(type.Identifier.Text); break;
            }
        }
        var injected = Inject().Matches(page).ToDictionary(m => m.Groups[2].Value, m => m.Groups[1].Value.Trim(), StringComparer.Ordinal);

        // Names the markup around it declares (a loop's variable, a template's context) and the text itself doesn't.
        var around = Declares(page[..start]);
        around.ExceptWith(Declares(markup));
        around.ExceptWith(values.Keys);

        var known = values.Keys.Concat(methods).Concat(types).Concat(injected.Keys).Concat(around).Distinct().ToList();
        var used = new HashSet<string>(StringComparer.Ordinal);
        if (known.Count > 0)
        {
            var marked = ExampleInsert.RenameMarkup(markup, known.ToDictionary(k => k, k => $"\u0001{k}\u0002"), library);
            used = known.Where(k => marked.Contains($"\u0001{k}\u0002", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
        }
        if (UsesOuterContext(markup, library)) used.Add("context");

        // A field or property is a parameter under a public name: _orders is Orders.
        var parameters = new List<(string Member, string Parameter, string Type)>();
        foreach (var member in values.Keys.Where(used.Contains))
        {
            var bare = member.TrimStart('_');
            var parameter = bare.Length == 0 ? "Value" : char.ToUpperInvariant(bare[0]) + bare[1..];
            while (parameters.Any(p => p.Parameter == parameter) || (parameter != member && used.Contains(parameter))) parameter += "Value";
            parameters.Add((member, parameter, values[member]));
            if (Regex.IsMatch(markup, $@"@bind(?:-[\w:]+)?\s*=\s*""@?{Regex.Escape(member)}\b|(?<![\w.]){Regex.Escape(member)}\s*(?:\+\+|--|[-+*/]?=(?![=>]))"))
                warnings.Add($"{member} is changed in the markup that moved. {name} gets its value as the parameter {parameter}, and a change there doesn't reach {path}: pass the change back with an EventCallback.");
        }
        foreach (var type in types.Where(t => !used.Contains(t) && parameters.Any(p => Regex.IsMatch(p.Type, $@"(?<![\w.]){Regex.Escape(t)}(?!\w)"))).ToList()) used.Add(type);
        var renames = parameters.Where(p => p.Member != p.Parameter).ToDictionary(p => p.Member, p => p.Parameter, StringComparer.Ordinal);
        if (renames.Count > 0) markup = ExampleInsert.RenameMarkup(markup, renames, library);
        foreach (var method in methods.Where(used.Contains))
            warnings.Add($"{method}() is a method of {path}, and {name} can't call it. Give {name} a parameter for it (an EventCallback, or a Func) and pass it from the page.");
        foreach (var type in types.Where(used.Contains))
            warnings.Add($"{type} is declared in the @code of {path}. Move it to a file of its own so {name} can use it too.");
        foreach (var local in around.Where(used.Contains).Append("context").Where(used.Contains).Distinct())
            warnings.Add($"{local} comes from the markup around what moved (a loop, a block or a template). Give {name} a parameter for it and pass it from the page.");

        var text = new StringBuilder();
        foreach (Match line in UsingLine().Matches(page)) text.Append(line.Value.Trim()).Append('\n');
        foreach (var service in injected.Where(s => used.Contains(s.Key))) text.Append($"@inject {service.Value} {service.Key}\n");
        if (text.Length > 0) text.Append('\n');
        text.Append(markup).Append('\n');
        if (parameters.Count > 0)
        {
            text.Append("\n@code {\n");
            foreach (var (_, parameter, type) in parameters) text.Append($"    [Parameter] public {type} {parameter} {{ get; set; }}{(type.EndsWith('?') ? "" : " = default!;")}\n");
            text.Append("}\n");
        }

        var tag = new StringBuilder("<").Append(name);
        foreach (var (member, parameter, _) in parameters) tag.Append($" {parameter}=\"@{member}\"");
        tag.Append(" />");
        var hash = Inserts.Hash(page);
        return ([new TextInsert(path, start, tag.ToString(), hash) { Length = end - start }], new FileCreate(file, text.ToString().Replace("\n", eol, StringComparison.Ordinal), null), warnings);
    }

    // The names that markup declares for what's inside it: a loop's variable, a template's Context, a @{ } block's locals.
    static HashSet<string> Declares(string markup)
    {
        var names = ExampleInsert.Locals(markup);
        foreach (Match m in LoopVariable().Matches(markup)) names.Add(m.Groups[1].Value);
        foreach (Match m in ContextName().Matches(markup)) names.Add(m.Groups[1].Value);
        return names;
    }

    // Whether the markup reads a template's context that no template inside it gives.
    static bool UsesOuterContext(string markup, Components library)
    {
        var elements = Elements(markup);
        foreach (Match m in ContextWord().Matches(markup))
        {
            var given = elements.Where(e => m.Index >= e.Tag.End && m.Index < e.End).Any(e =>
                // A component of the app's, or one of the library's whose content is a template; or a template's own tag.
                library.Options(e.Tag.Name) is null
                    ? char.IsUpper(e.Tag.Name[0])
                    : library.Find(e.Tag.Name)["slots"]?.AsArray().Any(s => ((string?)s!["type"] ?? "").StartsWith("RenderFragment<", StringComparison.Ordinal)) == true);
            if (!given) return true;
        }
        return false;
    }

    [GeneratedRegex(@"^[ \t]*@inject\s+(.+?)\s+([A-Za-z_]\w*)\s*;?[ \t]*\r?$", RegexOptions.Multiline)]
    private static partial Regex Inject();

    [GeneratedRegex(@"^[ \t]*@using\s+[^\r\n]+", RegexOptions.Multiline)]
    private static partial Regex UsingLine();

    [GeneratedRegex(@"@(?:foreach|for|using)\s*\(\s*(?:var|[\w.<>\[\]?,]+)\s+([A-Za-z_]\w*)\s*(?:in\b|=)")]
    private static partial Regex LoopVariable();

    [GeneratedRegex(@"\bContext\s*=\s*""([A-Za-z_]\w*)""")]
    private static partial Regex ContextName();

    [GeneratedRegex(@"(?<![\w.""])context(?=[.\[)\s,!?])")]
    private static partial Regex ContextWord();
}
