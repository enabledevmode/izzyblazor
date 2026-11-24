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
    /// <param name="codeBehind">The page's <c>.razor.cs</c> file, when it has one: its members' names are taken too.</param>
    /// <param name="qualified">Types of the example's own files that a member of the page hides, each with its full name: the example gets a <c>@using</c> name for it.</param>
    public static (List<TextInsert> Inserts, List<string> Warnings) Place(string page, string path, int offset, string source, string imports, Components library, string? codeBehind = null, IReadOnlyDictionary<string, string>? qualified = null)
    {
        var eol = page.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var (directives, markup, code) = Split(source.Replace("\r\n", "\n", StringComparison.Ordinal));
        var warnings = new List<string>();

        // The names the page has, and the ones the example brings.
        var pageBlocks = RazorPage.CodeBlocks(page);
        var taken = Names(page, imports, codeBehind);

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

        // A type the page hides gets another name by a @using, as a clashing member gets one. Its full name in
        // place wouldn't do: a part of it can be hidden too (an app named "app" has Program's app).
        var aliases = new List<string>();
        foreach (var (type, full) in (qualified ?? new Dictionary<string, string>()).Where(q => !renames.ContainsKey(q.Key)))
        {
            var number = 2;
            while (taken.Contains(type + number) || names.Contains(type + number) || renames.ContainsValue(type + number)) number++;
            renames[type] = type + number;
            if (!here.Contains($"@using {type + number} = {full}")) aliases.Add($"@using {type + number} = {full}");
        }

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
            needed.AddRange(aliases);
        }
        // A local of a @{ } block belongs to the page's one render method, so the example's can't have the name of
        // one the page has (the same example added twice).
        var pageLocals = Locals(page);
        var locals = Locals(markup);
        var relocated = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in locals.Where(pageLocals.Contains))
        {
            var number = 2;
            while (pageLocals.Contains(name + number) || locals.Contains(name + number) || taken.Contains(name + number)) number++;
            relocated[name] = name + number;
        }
        if (relocated.Count > 0) markup = RenameMarkup(markup, relocated, library);
        foreach (var (from, to) in forms)
        {
            code = code.Replace($"\"{from}\"", $"\"{to}\"", StringComparison.Ordinal);
            markup = markup.Replace($"\"{from}\"", $"\"{to}\"", StringComparison.Ordinal);
        }

        var hash = Inserts.Hash(page);
        var inserts = new List<TextInsert>();
        string Lines(string text) => text.Replace("\n", eol, StringComparison.Ordinal);

        // A page whose last line has no line break gets one before what's added after it.
        string Lead(int at) => at == page.Length && page.Length > 0 && !page.EndsWith('\n') ? "\n" : "";

        // Directives: after the page's own, at its top.
        if (needed.Count > 0)
        {
            var top = DirectivesEnd(page);
            inserts.Add(new TextInsert(path, top, Lines(Lead(top) + string.Join("\n", needed) + "\n"), hash));
        }

        // Markup: at the line, indented as that line is (or the one before it, when it's blank).
        if (markup.Length > 0)
        {
            var indent = Indent(page, offset);
            var body = string.Join("\n", markup.Split('\n').Select(l => l.Length > 0 ? indent + l : l)) + "\n";
            // After directives added at the same place, the break is already there.
            var lead = needed.Count > 0 && DirectivesEnd(page) == offset ? "" : Lead(offset);
            inserts.Add(new TextInsert(path, offset, Lines(lead + body), hash));
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
                // What was added at the page's end before this already ends in a line break.
                var ended = page.Length == 0 || page.EndsWith('\n') || inserts.Any(i => i.Offset == page.Length);
                inserts.Add(new TextInsert(path, page.Length, Lines($"{(ended ? "\n" : "\n\n")}@code {{\n{body}\n}}\n"), hash));
            }
        }
        return (inserts, warnings);
    }

    /// <summary>
    /// The names a page's own code has: the members of its <c>@code</c> blocks and of its code-behind file, and
    /// what it and its <c>_Imports.razor</c> files inject.
    /// </summary>
    public static HashSet<string> Names(string page, string imports, string? codeBehind)
    {
        var taken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (_, open, close) in RazorPage.CodeBlocks(page))
            foreach (var member in Members(page[(open + 1)..close])) taken.Add(member.Name);
        foreach (Match m in Inject().Matches(page)) taken.Add(m.Groups[1].Value);
        foreach (Match m in Inject().Matches(imports)) taken.Add(m.Groups[1].Value);
        if (codeBehind is not null)
            foreach (var type in CSharpSyntaxTree.ParseText(codeBehind).GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>().Where(t => t.Parent is not TypeDeclarationSyntax))
                foreach (var member in Members(type)) taken.Add(member.Name);
        return taken;
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

    // What the @{ } blocks in markup declare: variables (out and pattern ones too) and local functions.
    static HashSet<string> Locals(string markup)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var comments = RazorPage.Comments(markup);
        for (var i = 0; i < markup.Length - 1; i++)
        {
            if (markup[i] != '@' || markup[i + 1] != '{' || comments.Any(c => i >= c.Start && i < c.End)) continue;
            var close = RazorPage.MatchBrace(markup, i + 1);
            if (close < 0) break;
            foreach (var node in CSharpSyntaxTree.ParseText("class C { void M() {" + markup[(i + 2)..close] + "\n}}").GetRoot().DescendantNodes())
            {
                switch (node)
                {
                    case VariableDeclaratorSyntax { Parent.Parent: LocalDeclarationStatementSyntax } variable: names.Add(variable.Identifier.Text); break;
                    case SingleVariableDesignationSyntax designation: names.Add(designation.Identifier.Text); break;
                    case LocalFunctionStatementSyntax function: names.Add(function.Identifier.Text); break;
                }
            }
            i = close;
        }
        return names;
    }

    sealed record Declared(string Name, bool Override);

    // What a @code block declares: its fields, properties, methods and nested types.
    static List<Declared> Members(string code)
    {
        return Wrapped(code) is { } type ? Members(type) : [];
    }

    static List<Declared> Members(TypeDeclarationSyntax type)
    {
        var declared = new List<Declared>();
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
            // record Feedback(string Answer) declares Feedback's own Answer, and Answer: names an argument.
            if (token.Parent is ParameterSyntax { Parent.Parent: TypeDeclarationSyntax }) continue;
            if (token.Parent is IdentifierNameSyntax { Parent: NameColonSyntax }) continue;
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

    // In markup a name is C# only in some places: after an @, in a code block's statements, and in an attribute
    // that takes a value other than text. "Save" on a button's face stays, and so does a word inside a string.
    static string RenameMarkup(string markup, Dictionary<string, string> renames, Components library)
    {
        var word = new Regex(@"(?<![\w.])(" + string.Join("|", renames.Keys.OrderByDescending(k => k.Length).Select(Regex.Escape)) + @")(?!\w)");

        var spans = new List<(int Start, int End)>();
        foreach (var tag in RazorPage.Tags(markup))
        {
            var options = library.Options(tag.Name);
            foreach (var a in tag.Attributes.Where(a => a.HasValue))
            {
                var value = markup.Substring(a.ValueStart, a.ValueLength);
                // On a component, an option that takes text holds text, as an HTML attribute (class, id) does; the rest hold C#.
                var isCode = a.Name.StartsWith('@') || value.StartsWith('@')
                    || (options is not null && char.IsUpper(a.Name[0]) && !(options.TryGetValue(a.Name, out var text) && text));
                if (isCode) spans.Add((a.ValueStart, a.ValueStart + a.ValueLength));
            }
        }

        // @name, @name.Member, @(expression), @{ statements }, and the conditions and statements of @if, @foreach and the like.
        var comments = RazorPage.Comments(markup);
        for (var i = 0; i < markup.Length - 1; i++)
        {
            if (markup[i] != '@') continue;
            if (comments.FirstOrDefault(c => i >= c.Start && i < c.End) is { End: > 0 } comment) { i = comment.End - 1; continue; }
            if (markup[i + 1] == '@') { i++; continue; }
            if (markup[i + 1] == '{')
            {
                if (RazorPage.MatchBrace(markup, i + 1) is var close and >= 0) { spans.Add((i, close)); i = close; }
                continue;
            }
            if (Keyword().Match(markup, i) is { Success: true } keyword && keyword.Index == i)
            {
                var open = i + keyword.Length - 1;
                var close = RazorPage.MatchBracket(markup, open);
                if (close < 0) continue;
                spans.Add((open, close));
                Body(close + 1);
                i = close;
                continue;
            }
            if (RazorPage.ExpressionEnd(markup, i) is var end && end > i) { spans.Add((i, end)); i = end - 1; }
        }

        // The braces after a condition: the statements between the markup, then an else's own condition and braces.
        void Body(int at)
        {
            while (at < markup.Length && char.IsWhiteSpace(markup[at])) at++;
            if (at >= markup.Length || markup[at] != '{') return;
            var close = RazorPage.MatchBrace(markup, at);
            if (close < 0) return;
            var line = at + 1;
            while (line < close)
            {
                var next = markup.IndexOf('\n', line) is var newline and >= 0 && newline < close ? newline : close;
                var text = markup[line..next];
                var trimmed = text.TrimStart(' ', '\t', '}');
                var from = line + (text.Length - trimmed.Length);
                if (Statement().Match(trimmed) is { Success: true } statement)
                {
                    var open = from + statement.Length - 1;
                    if (RazorPage.MatchBracket(markup, open) is var end and >= 0) spans.Add((open, end));
                }
                else if (trimmed.TrimEnd().EndsWith(';') && !trimmed.StartsWith('<') && !trimmed.StartsWith('@') && !Entity().IsMatch(trimmed))
                {
                    spans.Add((from, next));
                }
                line = next + 1;
            }
            var after = close + 1;
            while (after < markup.Length && char.IsWhiteSpace(markup[after])) after++;
            if (Else().Match(markup, after) is not { Success: true } other || other.Index != after) return;
            after += other.Length;
            if (other.Groups[1].Success)
            {
                var open = after - 1;
                var end = RazorPage.MatchBracket(markup, open);
                if (end < 0) return;
                spans.Add((open, end));
                after = end + 1;
            }
            Body(after);
        }

        // Spans can sit inside one another (an attribute in a @{ } block): each stretch is renamed once.
        var merged = new List<(int Start, int End)>();
        foreach (var span in spans.OrderBy(sp => sp.Start))
        {
            if (merged.Count > 0 && span.Start <= merged[^1].End) merged[^1] = (merged[^1].Start, Math.Max(merged[^1].End, span.End));
            else merged.Add(span);
        }
        var result = new StringBuilder(markup);
        foreach (var (start, end) in merged.OrderByDescending(sp => sp.Start))
        {
            result.Remove(start, end - start).Insert(start, RenameCSharp(markup[start..end], word, renames));
        }
        return result.ToString();
    }

    // The names in a stretch of C#, outside its strings and comments: "Delete {file}" keeps its word, and {file} is code again.
    static string RenameCSharp(string text, Regex word, Dictionary<string, string> renames)
    {
        var result = new StringBuilder();
        var code = 0;
        void Flush(int to)
        {
            if (to > code) result.Append(word.Replace(text[code..to], m => renames[m.Value]));
        }
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '/' && i + 1 < text.Length && text[i + 1] is '/' or '*')
            {
                Flush(i);
                var end = text[i + 1] == '/' ? text.IndexOf('\n', i) : text.IndexOf("*/", i + 2, StringComparison.Ordinal) is var close and >= 0 ? close + 2 : -1;
                if (end < 0) end = text.Length;
                result.Append(text, i, end - i);
                code = end;
                i = end - 1;
            }
            else if (text[i] == '\'' && RazorPage.CharEnd(text, i) is var charEnd and > 0)
            {
                Flush(i);
                result.Append(text, i, charEnd - i + 1);
                code = charEnd + 1;
                i = charEnd;
            }
            else if (text[i] == '"')
            {
                Flush(i);
                var end = Math.Min(RazorPage.StringEnd(text, i), text.Length - 1);
                var literal = text[i..(end + 1)];
                var interpolated = i > 0 && (text[i - 1] == '$' || (text[i - 1] == '@' && i > 1 && text[i - 2] == '$'));
                if (!interpolated) result.Append(literal);
                else
                {
                    // The holes are code; {{ is a brace.
                    for (var j = 0; j < literal.Length; j++)
                    {
                        if (literal[j] == '{' && j + 1 < literal.Length && literal[j + 1] == '{') { result.Append("{{"); j++; }
                        else if (literal[j] == '{' && RazorPage.MatchBracket(literal, j) is var close and > 0)
                        {
                            result.Append('{').Append(RenameCSharp(literal[(j + 1)..close], word, renames)).Append('}');
                            j = close;
                        }
                        else result.Append(literal[j]);
                    }
                }
                code = end + 1;
                i = end;
            }
        }
        Flush(text.Length);
        return result.ToString();
    }

    [GeneratedRegex(@"\G@(?:if|foreach|for|while|switch|using|lock)\s*\(")]
    private static partial Regex Keyword();

    [GeneratedRegex(@"^(?:else\s+if|if|foreach|for|while|switch|using|lock)\s*\(")]
    private static partial Regex Statement();

    [GeneratedRegex(@"\Gelse(?:\s+(if)\s*\()?")]
    private static partial Regex Else();

    [GeneratedRegex(@"&#?\w+;\s*$")]
    private static partial Regex Entity();

    [GeneratedRegex(@"^\s*@(using|inject|attribute|implements|inherits|rendermode|typeparam)\b")]
    private static partial Regex Directive();

    [GeneratedRegex(@"^@(page|using|inject|attribute|implements|inherits|rendermode|typeparam|layout|namespace|preservewhitespace)\b|^@\*.*\*@$")]
    private static partial Regex PageDirective();

    [GeneratedRegex(@"^\s*@inject\s+.+?\s+(\w+)\s*$", RegexOptions.Multiline)]
    private static partial Regex Inject();

    [GeneratedRegex("FormName\\s*=\\s*\"([^\"]+)\"")]
    private static partial Regex FormName();

}
