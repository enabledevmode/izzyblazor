using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using IzzyBlazor.Generation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace IzzyBlazor.Cli;

/// <summary>
/// Puts one of the docs' examples on a page that has content of its own. An example is a whole .razor file, so
/// it goes in as three parts: its markup at the line asked for, its directives (<c>@using</c>, <c>@inject</c>)
/// with the page's, and its <c>@code</c> members inside the page's <c>@code</c> block. A name the page already
/// has (Model, Save, a form's name) gets a number in what's inserted.
/// </summary>
static partial class ExampleInsert
{
    /// <param name="page">The page's text.</param>
    /// <param name="path">Its path in the project, for the inserts.</param>
    /// <param name="offset">Where the markup goes: the start of a line.</param>
    /// <param name="source">The example's source.</param>
    /// <param name="imports">The text of the <c>_Imports.razor</c> files that apply to the page.</param>
    /// <param name="library">The components, to tell an attribute that holds C# from one that holds text.</param>
    public static (List<TextInsert> Inserts, List<string> Warnings) Place(string page, string path, int offset, string source, string imports, Components library)
    {
        var eol = page.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var (directives, markup, code) = Split(source.Replace("\r\n", "\n", StringComparison.Ordinal));
        var warnings = new List<string>();

        // The names the page has, and the ones the example brings.
        var pageBlocks = RazorPage.CodeBlocks(page);
        var taken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (_, open, close) in pageBlocks)
            foreach (var member in Members(page[(open + 1)..close])) taken.Add(member.Name);
        foreach (Match m in Inject().Matches(page)) taken.Add(m.Groups[1].Value);

        var here = new HashSet<string>(page.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Concat(imports.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')).Select(l => l.Trim()), StringComparer.Ordinal);
        var needed = directives.Where(d => !here.Contains(d)).ToList();

        var brought = Members(code);
        var names = brought.Select(m => m.Name).Concat(needed.Select(d => Inject().Match(d)).Where(m => m.Success).Select(m => m.Groups[1].Value)).Distinct().ToList();
        var renames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in names.Where(taken.Contains))
        {
            var number = 2;
            while (taken.Contains(name + number) || names.Contains(name + number) || renames.ContainsValue(name + number)) number++;
            renames[name] = name + number;
        }
        // An override can't take another name: it becomes a method of its own for the page's to call.
        var overrides = brought.Where(m => m.Override && renames.ContainsKey(m.Name)).Select(m => m.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var name in overrides)
            warnings.Add($"{path} already overrides {name}, so the example's is {renames[name]}(): call it from yours.");

        // A form's name is unique on a page.
        var forms = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in FormName().Matches(source))
        {
            var name = m.Groups[1].Value;
            if (forms.ContainsKey(name) || !page.Contains($"\"{name}\"", StringComparison.Ordinal)) continue;
            var number = 2;
            while (page.Contains($"\"{name}-{number}\"", StringComparison.Ordinal)) number++;
            forms[name] = $"{name}-{number}";
        }

        // An example's constructor is named for the example's file: here it's the page's.
        code = Constructors(code, Path.GetFileNameWithoutExtension(path));
        if (renames.Count > 0)
        {
            code = RenameCode(code, renames, overrides);
            markup = RenameMarkup(markup, renames, library);
            needed = needed.Select(d => Inject().Match(d) is { Success: true } m && renames.TryGetValue(m.Groups[1].Value, out var to) ? d[..m.Groups[1].Index] + to : d).ToList();
        }
        foreach (var (from, to) in forms)
        {
            code = code.Replace($"\"{from}\"", $"\"{to}\"", StringComparison.Ordinal);
            markup = markup.Replace($"\"{from}\"", $"\"{to}\"", StringComparison.Ordinal);
        }

        var hash = Inserts.Hash(page);
        var inserts = new List<TextInsert>();
        string Lines(string text) => text.Replace("\n", eol, StringComparison.Ordinal);

        // Directives: after the page's own, at its top.
        if (needed.Count > 0)
        {
            var top = DirectivesEnd(page);
            inserts.Add(new TextInsert(path, top, Lines(string.Join("\n", needed) + "\n"), hash));
        }

        // Markup: at the line, indented as that line is (or the one before it, when it's blank).
        if (markup.Length > 0)
        {
            var indent = Indent(page, offset);
            var body = string.Join("\n", markup.Split('\n').Select(l => l.Length > 0 ? indent + l : l)) + "\n";
            inserts.Add(new TextInsert(path, offset, Lines(body), hash));
        }

        // Code: inside the page's last @code block, or in one of its own at the end.
        if (code.Trim().Length > 0)
        {
            if (pageBlocks.Count > 0)
            {
                var (_, _, close) = pageBlocks[^1];
                var lineStart = page.LastIndexOf('\n', close) + 1;
                var alone = page[lineStart..close].Trim().Length == 0;
                var body = string.Join("\n", code.Trim('\n').Split('\n').Select(l => l.Length > 0 ? "    " + l : l));
                inserts.Add(alone
                    ? new TextInsert(path, lineStart, Lines("\n" + body + "\n"), hash)
                    : new TextInsert(path, close, Lines("\n\n" + body + "\n"), hash));
            }
            else
            {
                var body = string.Join("\n", code.Trim('\n').Split('\n').Select(l => l.Length > 0 ? "    " + l : l));
                var lead = page.Length == 0 || page.EndsWith('\n') ? "\n" : "\n\n";
                inserts.Add(new TextInsert(path, page.Length, Lines($"{lead}@code {{\n{body}\n}}\n"), hash));
            }
        }
        return (inserts, warnings);
    }

    /// <summary>An example's directives, its markup and the inside of its <c>@code</c> blocks.</summary>
    public static (List<string> Directives, string Markup, string Code) Split(string source)
    {
        var code = new StringBuilder();
        var blocks = RazorPage.CodeBlocks(source);
        for (var i = blocks.Count - 1; i >= 0; i--)
        {
            var (start, open, close) = blocks[i];
            code.Insert(0, Dedent(source[(open + 1)..close].Trim('\n')) + (code.Length > 0 ? "\n\n" : ""));
            var end = close + 1 < source.Length && source[close + 1] == '\n' ? close + 2 : close + 1;
            source = source[..start] + source[end..];
        }
        // A directive is a line of its own wherever it stands (an example may open with a comment).
        var directives = new List<string>();
        var lines = new List<string>();
        foreach (var line in source.Split('\n'))
        {
            if (Directive().IsMatch(line)) directives.Add(line.Trim());
            else lines.Add(line);
        }
        return (directives, string.Join("\n", lines).Trim('\n'), code.ToString());
    }

    static string Dedent(string text)
    {
        var lines = text.Split('\n');
        var indent = lines.Where(l => l.Trim().Length > 0).Select(l => l.Length - l.TrimStart(' ').Length).DefaultIfEmpty(0).Min();
        return string.Join("\n", lines.Select(l => l.Length >= indent ? l[indent..] : l.TrimStart(' ')));
    }

    // Just past the page's opening run of directives; 0 when it has none.
    static int DirectivesEnd(string page)
    {
        var end = 0;
        var offset = 0;
        while (offset < page.Length)
        {
            var newline = page.IndexOf('\n', offset);
            var next = newline < 0 ? page.Length : newline + 1;
            var line = page[offset..next].Trim();
            if (line.Length > 0 && !PageDirective().IsMatch(line)) break;
            if (line.Length > 0) end = next;
            offset = next;
        }
        return end;
    }

    // The indent of the line at the offset, or of the nearest line above it that isn't blank.
    static string Indent(string page, int offset)
    {
        var start = offset;
        while (true)
        {
            var end = page.IndexOf('\n', start) is var newline and >= 0 ? newline : page.Length;
            var line = page[start..end].TrimEnd('\r');
            if (line.Trim().Length > 0) return line[..(line.Length - line.TrimStart(' ', '\t').Length)];
            if (start == 0) return "";
            start = start >= 2 ? page.LastIndexOf('\n', start - 2) + 1 : 0;
        }
    }

    sealed record Declared(string Name, bool Override);

    // What a @code block declares: its fields, properties, methods and nested types.
    static List<Declared> Members(string code)
    {
        var declared = new List<Declared>();
        if (Wrapped(code) is not { } type) return declared;
        foreach (var member in type.Members)
        {
            switch (member)
            {
                case FieldDeclarationSyntax field:
                    declared.AddRange(field.Declaration.Variables.Select(v => new Declared(v.Identifier.Text, false)));
                    break;
                case EventFieldDeclarationSyntax field:
                    declared.AddRange(field.Declaration.Variables.Select(v => new Declared(v.Identifier.Text, false)));
                    break;
                case PropertyDeclarationSyntax property:
                    declared.Add(new Declared(property.Identifier.Text, property.Modifiers.Any(SyntaxKind.OverrideKeyword)));
                    break;
                case MethodDeclarationSyntax method:
                    declared.Add(new Declared(method.Identifier.Text, method.Modifiers.Any(SyntaxKind.OverrideKeyword)));
                    break;
                case BaseTypeDeclarationSyntax nested:
                    declared.Add(new Declared(nested.Identifier.Text, false));
                    break;
                case DelegateDeclarationSyntax nested:
                    declared.Add(new Declared(nested.Identifier.Text, false));
                    break;
            }
        }
        return declared.DistinctBy(d => d.Name).ToList();
    }

    const string Wrapper = "class __Page {\n";

    static ClassDeclarationSyntax? Wrapped(string code) =>
        CSharpSyntaxTree.ParseText(Wrapper + code + "\n}").GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();

    // Every use of a renamed name in the code, by token, so a string or a comment holding the word is left.
    static string RenameCode(string code, Dictionary<string, string> renames, HashSet<string> overrides)
    {
        if (Wrapped(code) is not { } type) return code;
        var edits = new List<(int Start, int Length, string Text)>();
        foreach (var token in type.DescendantTokens().Where(t => t.IsKind(SyntaxKind.IdentifierToken) && renames.ContainsKey(t.Text)))
        {
            // x.Model is another object's member; this.Model and Model are the page's.
            if (token.Parent is IdentifierNameSyntax { Parent: MemberAccessExpressionSyntax access } name && access.Name == name && access.Expression is not ThisExpressionSyntax) continue;
            // new Contact { Email = "" } and with { Email = "" } name the object's own members.
            if (token.Parent is IdentifierNameSyntax { Parent: AssignmentExpressionSyntax { Parent: InitializerExpressionSyntax } assignment } left && assignment.Left == left) continue;
            // A member of a nested type is not the page's.
            if (token.Parent is MemberDeclarationSyntax or VariableDeclaratorSyntax && !Direct(token.Parent, type)) continue;
            edits.Add((token.SpanStart, token.Span.Length, renames[token.Text]));
        }
        foreach (var method in type.Members.OfType<MethodDeclarationSyntax>().Where(m => overrides.Contains(m.Identifier.Text)))
            foreach (var modifier in method.Modifiers)
            {
                if (modifier.IsKind(SyntaxKind.OverrideKeyword)) edits.Add((modifier.SpanStart, modifier.FullSpan.End - modifier.SpanStart, ""));
                else if (modifier.IsKind(SyntaxKind.ProtectedKeyword) || modifier.IsKind(SyntaxKind.PublicKeyword)) edits.Add((modifier.SpanStart, modifier.Span.Length, "private"));
            }
        var text = new StringBuilder(Wrapper + code + "\n}");
        foreach (var (start, length, to) in edits.OrderByDescending(e => e.Start)) text.Remove(start, length).Insert(start, to);
        return text.ToString()[Wrapper.Length..^2];
    }

    static string Constructors(string code, string page)
    {
        if (Wrapped(code) is not { } type) return code;
        var text = new StringBuilder(Wrapper + code + "\n}");
        foreach (var constructor in type.Members.OfType<ConstructorDeclarationSyntax>().OrderByDescending(c => c.SpanStart))
            text.Remove(constructor.Identifier.SpanStart, constructor.Identifier.Span.Length).Insert(constructor.Identifier.SpanStart, page);
        return text.ToString()[Wrapper.Length..^2];
    }

    static bool Direct(SyntaxNode? node, ClassDeclarationSyntax type)
    {
        for (; node is not null; node = node.Parent)
            if (node is MemberDeclarationSyntax member && member.Parent is BaseTypeDeclarationSyntax owner) return owner == type;
        return false;
    }

    // In markup a name is C# only in some places: after an @, and in an attribute that takes a value other
    // than text. "Save" on a button's face stays.
    static string RenameMarkup(string markup, Dictionary<string, string> renames, Components library)
    {
        var word = new Regex(@"(?<![\w.])(" + string.Join("|", renames.Keys.OrderByDescending(k => k.Length).Select(Regex.Escape)) + @")(?!\w)");
        string Rename(string text) => word.Replace(text, m => renames[m.Value]);

        var spans = new List<(int Start, int Length)>();
        foreach (var tag in RazorPage.Tags(markup))
        {
            var options = library.Options(tag.Name);
            foreach (var a in tag.Attributes.Where(a => a.HasValue))
            {
                var value = markup.Substring(a.ValueStart, a.ValueLength);
                // On a component, an option that takes text holds text, as an HTML attribute (class, id) does; the rest hold C#.
                var isCode = a.Name.StartsWith('@') || value.StartsWith('@')
                    || (options is not null && char.IsUpper(a.Name[0]) && !(options.TryGetValue(a.Name, out var text) && text));
                if (isCode) spans.Add((a.ValueStart, a.ValueLength));
            }
        }
        // @name, @name.Member, @(expression), and the condition of @if, @foreach and the like.
        foreach (Match m in Expression().Matches(markup))
        {
            if (spans.Any(s => m.Index >= s.Start && m.Index < s.Start + s.Length)) continue;
            var end = m.Index + m.Length;
            if (m.Value.EndsWith('('))
            {
                var depth = 1;
                while (end < markup.Length && depth > 0)
                {
                    if (markup[end] == '(') depth++;
                    else if (markup[end] == ')') depth--;
                    end++;
                }
            }
            spans.Add((m.Index, end - m.Index));
        }
        var result = new StringBuilder(markup);
        foreach (var (start, length) in spans.OrderByDescending(s => s.Start))
        {
            var renamed = Rename(markup.Substring(start, length));
            result.Remove(start, length).Insert(start, renamed);
        }
        return result.ToString();
    }

    [GeneratedRegex(@"^\s*@(using|inject|attribute|implements|inherits|rendermode|typeparam)\b")]
    private static partial Regex Directive();

    [GeneratedRegex(@"^@(page|using|inject|attribute|implements|inherits|rendermode|typeparam|layout|namespace|preservewhitespace)\b|^@\*.*\*@$")]
    private static partial Regex PageDirective();

    [GeneratedRegex(@"^\s*@inject\s+.+?\s+(\w+)\s*$", RegexOptions.Multiline)]
    private static partial Regex Inject();

    [GeneratedRegex("FormName\\s*=\\s*\"([^\"]+)\"")]
    private static partial Regex FormName();

    [GeneratedRegex(@"@(?:(?:if|foreach|for|while|switch|using|lock)\s*\(|\(|[A-Za-z_][\w.!?\[\]]*\(?)")]
    private static partial Regex Expression();
}
