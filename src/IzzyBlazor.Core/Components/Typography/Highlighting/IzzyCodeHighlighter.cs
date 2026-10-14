using System.Text;

namespace IzzyBlazor;

/// <summary>
/// The built-in highlighter: small hand-written lexers for C#, Razor, HTML and XML, CSS,
/// JavaScript and TypeScript, JSON, and shell (bash, PowerShell). They color what readers look
/// for (keywords, strings, comments, numbers, types, calls, tags, attributes) without parsing:
/// rare constructs may color plainly, but every character of the code is kept as written.
/// </summary>
public sealed class IzzyCodeHighlighter : ICodeHighlighter
{
    /// <summary>The shared instance.</summary>
    public static IzzyCodeHighlighter Instance { get; } = new();

    private enum Lang { None, CSharp, Razor, Html, Css, Js, Json, Shell }

    /// <summary>The language names this highlighter understands (case doesn't matter).</summary>
    public static IReadOnlyCollection<string> Languages => Names.Keys;

    private static readonly Dictionary<string, Lang> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["c#"] = Lang.CSharp, ["cs"] = Lang.CSharp, ["csharp"] = Lang.CSharp,
        ["razor"] = Lang.Razor, ["cshtml"] = Lang.Razor, ["blazor"] = Lang.Razor,
        ["html"] = Lang.Html, ["xml"] = Lang.Html, ["svg"] = Lang.Html, ["xaml"] = Lang.Html, ["csproj"] = Lang.Html,
        ["css"] = Lang.Css, ["scss"] = Lang.Css,
        ["js"] = Lang.Js, ["javascript"] = Lang.Js, ["mjs"] = Lang.Js, ["ts"] = Lang.Js, ["typescript"] = Lang.Js,
        ["json"] = Lang.Json, ["jsonc"] = Lang.Json,
        ["bash"] = Lang.Shell, ["sh"] = Lang.Shell, ["shell"] = Lang.Shell, ["zsh"] = Lang.Shell, ["console"] = Lang.Shell,
        ["terminal"] = Lang.Shell, ["powershell"] = Lang.Shell, ["pwsh"] = Lang.Shell, ["ps1"] = Lang.Shell, ["cmd"] = Lang.Shell,
    };

    private static readonly HashSet<string> CSharpKeywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "async", "await", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "dynamic", "else", "enum",
        "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "get", "goto", "if",
        "implicit", "in", "init", "int", "interface", "internal", "is", "lock", "long", "namespace", "new", "not",
        "null", "and", "or", "nint", "nuint", "object", "operator", "out", "override", "params", "partial", "private",
        "protected", "public", "readonly", "record", "ref", "required", "return", "sbyte", "sealed", "set", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof",
        "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "var", "virtual", "void", "volatile", "when",
        "where", "while", "with", "yield", "nameof", "global",
    };

    private static readonly HashSet<string> JsKeywords = new(StringComparer.Ordinal)
    {
        "abstract", "any", "as", "async", "await", "boolean", "break", "case", "catch", "class", "const", "continue",
        "debugger", "declare", "default", "delete", "do", "else", "enum", "export", "extends", "false", "finally",
        "for", "from", "function", "get", "if", "implements", "import", "in", "instanceof", "interface", "keyof",
        "let", "namespace", "never", "new", "null", "number", "of", "private", "protected", "public", "readonly",
        "return", "set", "static", "string", "super", "switch", "this", "throw", "true", "try", "type", "typeof",
        "undefined", "unknown", "var", "void", "while", "with", "yield",
    };

    // Words before a type name: "new Foo", "class Foo : Bar".
    private static readonly HashSet<string> BeforeType = new(StringComparer.Ordinal)
    {
        "new", "class", "struct", "interface", "record", "enum", "extends", "implements", "typeof", "is", "as",
        "nameof", "throw", "catch",
    };

    private static readonly HashSet<string> RazorDirectives = new(StringComparer.Ordinal)
    {
        "page", "using", "inject", "inherits", "namespace", "layout", "implements", "attribute", "typeparam",
        "rendermode", "model", "preservewhitespace", "section",
    };

    private static readonly HashSet<string> RazorBlocks = new(StringComparer.Ordinal)
    {
        "if", "else", "foreach", "for", "while", "switch", "do", "try", "catch", "finally", "lock", "await",
    };

    private static readonly HashSet<string> ShellKeywords = new(StringComparer.Ordinal)
    {
        "if", "then", "else", "elif", "fi", "for", "while", "until", "do", "done", "case", "esac", "in", "function",
        "return", "export", "local",
    };

    /// <inheritdoc />
    public bool TryHighlight(string code, string language, out IReadOnlyList<CodeToken> tokens)
    {
        ArgumentNullException.ThrowIfNull(code);
        if (language is null || !Names.TryGetValue(language.Trim(), out var lang))
        {
            tokens = [];
            return false;
        }

        var output = new Output(code);
        switch (lang)
        {
            case Lang.CSharp: CLike(output, 0, code.Length, csharp: true); break;
            case Lang.Razor: Markup(output, 0, code.Length, razor: true); break;
            case Lang.Html: Markup(output, 0, code.Length, razor: false); break;
            case Lang.Css: Css(output, 0, code.Length); break;
            case Lang.Js: CLike(output, 0, code.Length, csharp: false); break;
            case Lang.Json: Json(output, 0, code.Length); break;
            case Lang.Shell: Shell(output, 0, code.Length); break;
        }

        tokens = output.Done();
        return true;
    }

    /// <summary>Collects tokens, merging neighbors of the same kind.</summary>
    private sealed class Output(string code)
    {
        public string Code { get; } = code;
        private readonly List<CodeToken> _tokens = [];
        private readonly StringBuilder _run = new();
        private CodeTokenKind _kind;

        public void Add(CodeTokenKind kind, int start, int end)
        {
            if (end <= start)
            {
                return;
            }

            if (_run.Length > 0 && kind != _kind)
            {
                Flush();
            }

            _kind = kind;
            _run.Append(Code, start, end - start);
        }

        private void Flush()
        {
            _tokens.Add(new CodeToken(_kind, _run.ToString()));
            _run.Clear();
        }

        public List<CodeToken> Done()
        {
            if (_run.Length > 0)
            {
                Flush();
            }

            return _tokens;
        }
    }

    // ---- Shared scanning helpers ----

    private static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_';

    private static bool IsIdent(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static int LineEnd(string s, int i, int end)
    {
        var j = s.IndexOf('\n', i, end - i);
        return j < 0 ? end : j;
    }

    private static int After(string s, string close, int i, int end)
    {
        var j = s.IndexOf(close, i, end - i, StringComparison.Ordinal);
        return j < 0 ? end : j + close.Length;
    }

    private static int SkipSpaces(string s, int i, int end)
    {
        while (i < end && (s[i] == ' ' || s[i] == '\t'))
        {
            i++;
        }

        return i;
    }

    private static bool AtLineStart(string s, int i, int start)
    {
        for (var k = i - 1; k >= start; k--)
        {
            if (s[k] == '\n')
            {
                return true;
            }

            if (s[k] != ' ' && s[k] != '\t')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The end of a quoted string starting at <paramref name="i"/> (the quote).</summary>
    private static int QuotedEnd(string s, int i, int end, bool escapes = true, bool multiline = false)
    {
        var quote = s[i];
        var j = i + 1;
        while (j < end)
        {
            var c = s[j];
            if (escapes && c == '\\' && j + 1 < end)
            {
                j += 2;
                continue;
            }

            if (c == quote)
            {
                return j + 1;
            }

            if (c == '\n' && !multiline)
            {
                return j;
            }

            j++;
        }

        return end;
    }

    /// <summary>The index after the bracket matching the one at <paramref name="i"/>, skipping strings.</summary>
    private static int Matching(string s, int i, int end)
    {
        var open = s[i];
        var close = open switch { '(' => ')', '[' => ']', _ => '}' };
        var depth = 0;
        var j = i;
        while (j < end)
        {
            var c = s[j];
            if (c == '"' || c == '\'')
            {
                j = QuotedEnd(s, j, end);
                continue;
            }

            if (c == open)
            {
                depth++;
            }
            else if (c == close && --depth == 0)
            {
                return j + 1;
            }

            j++;
        }

        return end;
    }

    // ---- C# and JavaScript/TypeScript ----

    private static void CLike(Output o, int start, int end, bool csharp)
    {
        var s = o.Code;
        var keywords = csharp ? CSharpKeywords : JsKeywords;
        var i = start;
        string? lastWord = null;
        var lastChar = '\0';
        while (i < end)
        {
            var c = s[i];
            var next = i + 1 < end ? s[i + 1] : '\0';

            if (c == '/' && next == '/')
            {
                var j = LineEnd(s, i, end);
                o.Add(CodeTokenKind.Comment, i, j);
                i = j;
                continue;
            }

            if (c == '/' && next == '*')
            {
                var j = After(s, "*/", i + 2, end);
                o.Add(CodeTokenKind.Comment, i, j);
                i = j;
                continue;
            }

            if (csharp && c == '#' && AtLineStart(s, i, start))
            {
                var j = LineEnd(s, i, end);
                o.Add(CodeTokenKind.Meta, i, j);
                i = j;
                continue;
            }

            if (!csharp && c == '@' && IsIdentStart(next))
            {
                var j = i + 1;
                while (j < end && IsIdent(s[j]))
                {
                    j++;
                }

                o.Add(CodeTokenKind.Meta, i, j);
                i = j;
                continue;
            }

            var stringEnd = csharp ? CSharpStringEnd(s, i, end) : JsStringEnd(s, i, end);
            if (stringEnd > i)
            {
                o.Add(CodeTokenKind.String, i, stringEnd);
                i = stringEnd;
                lastChar = '"';
                continue;
            }

            if (char.IsAsciiDigit(c) || (c == '.' && char.IsAsciiDigit(next) && !IsIdent(lastChar)))
            {
                var j = i + 1;
                while (j < end && (char.IsAsciiLetterOrDigit(s[j]) || s[j] == '_' || (s[j] == '.' && j + 1 < end && char.IsAsciiDigit(s[j + 1]))))
                {
                    j++;
                }

                o.Add(CodeTokenKind.Number, i, j);
                i = j;
                lastChar = '0';
                continue;
            }

            if (IsIdentStart(c) || (c == '@' && csharp && IsIdentStart(next)) || (c == '$' && !csharp))
            {
                var j = i + 1;
                while (j < end && (IsIdent(s[j]) || (!csharp && s[j] == '$')))
                {
                    j++;
                }

                var word = s[i..j];
                var afterDot = lastChar == '.' || (lastChar == '?' && i >= 2 && s[i - 1] == '.');
                var k = SkipSpaces(s, j, end);
                var follow = k < end ? s[k] : '\0';
                var kind = CodeTokenKind.Text;
                if (!afterDot && keywords.Contains(word))
                {
                    kind = CodeTokenKind.Keyword;
                }
                else if (!afterDot && char.IsUpper(word[0]) &&
                         ((lastWord is not null && BeforeType.Contains(lastWord)) || (csharp && lastChar == '[')))
                {
                    // "new Foo(", "record Foo(", and C# attributes: "[Parameter]", "[Range(1, 9)]".
                    kind = CodeTokenKind.Type;
                }
                else if (follow == '(')
                {
                    kind = CodeTokenKind.Function;
                }
                else if (!afterDot && char.IsUpper(word[0]) && IsTypePosition(s, j, k, end, follow))
                {
                    kind = CodeTokenKind.Type;
                }

                o.Add(kind, i, j);
                lastWord = word;
                lastChar = s[j - 1];
                i = j;
                continue;
            }

            o.Add(CodeTokenKind.Text, i, i + 1);
            if (!char.IsWhiteSpace(c))
            {
                lastChar = c;
                if (c != '.' && c != ':' && c != '<' && c != ',' && c != '?')
                {
                    lastWord = null;
                }
            }

            i++;
        }
    }

    /// <summary>Whether a capitalized word reads as a type: after "new", before a generic, a name or "[]".</summary>
    private static bool IsTypePosition(string s, int wordEnd, int k, int end, char follow)
    {
        // A generic argument: "List<Rule>", "Dictionary<string, Rule>".
        if (wordEnd < end && s[wordEnd] == '>')
        {
            return true;
        }

        if (wordEnd < end && (s[wordEnd] == '<' || s[wordEnd] == '.' || s[wordEnd] == '?' || s[wordEnd] == '['))
        {
            return wordEnd + 1 < end && (s[wordEnd] != '?' || s[wordEnd + 1] == ' ');
        }

        // A declaration: "Foo bar".
        return k > wordEnd && IsIdentStart(follow);
    }

    private static int CSharpStringEnd(string s, int i, int end)
    {
        var j = i;
        var verbatim = false;
        while (j < end && (s[j] == '$' || s[j] == '@') && j - i < 3)
        {
            verbatim |= s[j] == '@';
            j++;
        }

        if (j < end && s[j] == '\'' && j == i)
        {
            return QuotedEnd(s, j, end);
        }

        if (j >= end || s[j] != '"')
        {
            return i;
        }

        if (j + 2 < end && s[j + 1] == '"' && s[j + 2] == '"')
        {
            var q = j;
            while (q < end && s[q] == '"')
            {
                q++;
            }

            var quotes = new string('"', q - j);
            return After(s, quotes, q, end);
        }

        if (!verbatim)
        {
            return QuotedEnd(s, j, end);
        }

        var k = j + 1;
        while (k < end)
        {
            if (s[k] == '"')
            {
                if (k + 1 < end && s[k + 1] == '"')
                {
                    k += 2;
                    continue;
                }

                return k + 1;
            }

            k++;
        }

        return end;
    }

    private static int JsStringEnd(string s, int i, int end) => s[i] switch
    {
        '"' or '\'' => QuotedEnd(s, i, end),
        '`' => QuotedEnd(s, i, end, multiline: true),
        _ => i,
    };

    // ---- HTML, XML and Razor ----

    private static void Markup(Output o, int start, int end, bool razor)
    {
        var s = o.Code;
        var i = start;
        while (i < end)
        {
            var c = s[i];
            // An "@" after a letter or digit is text, as in an email address.
            if (razor && c == '@' && !(i > start && char.IsLetterOrDigit(s[i - 1])))
            {
                i = RazorTransition(o, i, end);
                continue;
            }

            // "} else {" and "} else if (...)" after a Razor block.
            if (razor && c == '}')
            {
                o.Add(CodeTokenKind.Text, i, i + 1);
                i = RazorElse(o, i + 1, end);
                continue;
            }

            if (c == '<' && i + 1 < end)
            {
                if (string.CompareOrdinal(s, i, "<!--", 0, 4) == 0)
                {
                    var j = After(s, "-->", i + 4, end);
                    o.Add(CodeTokenKind.Comment, i, j);
                    i = j;
                    continue;
                }

                var n = s[i + 1];
                if (char.IsLetter(n) || n == '/' || n == '!' || n == '?')
                {
                    i = Tag(o, i, end, razor);
                    continue;
                }
            }

            var stop = i + 1;
            while (stop < end && s[stop] != '<' && !(razor && ((s[stop] == '@' && !char.IsLetterOrDigit(s[stop - 1])) || s[stop] == '}')))
            {
                stop++;
            }

            o.Add(CodeTokenKind.Text, i, stop);
            i = stop;
        }
    }

    private static int RazorElse(Output o, int i, int end)
    {
        var s = o.Code;
        var k = i;
        while (k < end && char.IsWhiteSpace(s[k]))
        {
            k++;
        }

        if (k + 4 > end || string.CompareOrdinal(s, k, "else", 0, 4) != 0 || (k + 4 < end && IsIdent(s[k + 4])))
        {
            return i;
        }

        o.Add(CodeTokenKind.Text, i, k);
        o.Add(CodeTokenKind.Keyword, k, k + 4);
        var j = SkipSpaces(s, k + 4, end);
        if (j + 2 <= end && string.CompareOrdinal(s, j, "if", 0, 2) == 0 && (j + 2 == end || !IsIdent(s[j + 2])))
        {
            o.Add(CodeTokenKind.Text, k + 4, j);
            o.Add(CodeTokenKind.Keyword, j, j + 2);
            var p = SkipSpaces(s, j + 2, end);
            if (p < end && s[p] == '(')
            {
                o.Add(CodeTokenKind.Text, j + 2, p);
                var close = Matching(s, p, end);
                CLike(o, p, close, csharp: true);
                return close;
            }

            return j + 2;
        }

        return k + 4;
    }

    private static int RazorTransition(Output o, int i, int end)
    {
        var s = o.Code;
        var next = i + 1 < end ? s[i + 1] : '\0';
        switch (next)
        {
            case '@':
                o.Add(CodeTokenKind.Text, i, i + 2);
                return i + 2;
            case '*':
            {
                var j = After(s, "*@", i + 2, end);
                o.Add(CodeTokenKind.Comment, i, j);
                return j;
            }
            case '(' or '{':
            {
                o.Add(CodeTokenKind.Meta, i, i + 1);
                var j = Matching(s, i + 1, end);
                CLike(o, i + 1, j, csharp: true);
                return j;
            }
        }

        if (!IsIdentStart(next))
        {
            o.Add(CodeTokenKind.Text, i, i + 1);
            return i + 1;
        }

        var w = i + 1;
        while (w < end && IsIdent(s[w]))
        {
            w++;
        }

        var word = s[(i + 1)..w];
        if (word is "code" or "functions")
        {
            o.Add(CodeTokenKind.Meta, i, w);
            var b = w;
            while (b < end && char.IsWhiteSpace(s[b]))
            {
                b++;
            }

            if (b < end && s[b] == '{')
            {
                o.Add(CodeTokenKind.Text, w, b);
                var j = Matching(s, b, end);
                CLike(o, b, j, csharp: true);
                return j;
            }

            return w;
        }

        if (RazorDirectives.Contains(word))
        {
            o.Add(CodeTokenKind.Meta, i, w);
            var j = LineEnd(s, w, end);
            CLike(o, w, j, csharp: true);
            return j;
        }

        if (RazorBlocks.Contains(word))
        {
            o.Add(CodeTokenKind.Meta, i, i + 1);
            o.Add(CodeTokenKind.Keyword, i + 1, w);
            var k = SkipSpaces(s, w, end);
            if (k < end && s[k] == '(')
            {
                o.Add(CodeTokenKind.Text, w, k);
                var j = Matching(s, k, end);
                CLike(o, k, j, csharp: true);
                return j;
            }

            return w;
        }

        // An implicit expression: @name, @Model.Title, @Url("x"), @items[0].Name.
        var e = w;
        while (e < end)
        {
            if (s[e] == '(' || s[e] == '[')
            {
                e = Matching(s, e, end);
            }
            else if (s[e] == '.' && e + 1 < end && IsIdentStart(s[e + 1]))
            {
                e++;
                while (e < end && IsIdent(s[e]))
                {
                    e++;
                }
            }
            else
            {
                break;
            }
        }

        o.Add(CodeTokenKind.Meta, i, i + 1);
        CLike(o, i + 1, e, csharp: true);
        return e;
    }

    private static int Tag(Output o, int i, int end, bool razor)
    {
        var s = o.Code;
        var j = i + 1;
        if (s[j] == '/' || s[j] == '!' || s[j] == '?')
        {
            j++;
        }

        o.Add(CodeTokenKind.Text, i, j);
        var nameStart = j;
        while (j < end && (IsIdent(s[j]) || s[j] is '-' or ':' or '.'))
        {
            j++;
        }

        var name = s[nameStart..j];
        o.Add(CodeTokenKind.Tag, nameStart, j);
        while (j < end)
        {
            var c = s[j];
            if (c == '>')
            {
                o.Add(CodeTokenKind.Text, j, j + 1);
                j++;
                break;
            }

            if ((c == '/' || c == '?') && j + 1 < end && s[j + 1] == '>')
            {
                o.Add(CodeTokenKind.Text, j, j + 2);
                return j + 2;
            }

            if (char.IsWhiteSpace(c) || c == '=')
            {
                o.Add(CodeTokenKind.Text, j, j + 1);
                j++;
                if (c == '=' && j < end)
                {
                    j = AttributeValue(o, j, end, razor);
                }

                continue;
            }

            if (c == '"' || c == '\'')
            {
                var q = QuotedEnd(s, j, end, escapes: false, multiline: true);
                o.Add(CodeTokenKind.String, j, q);
                j = q;
                continue;
            }

            var a = j;
            while (a < end && !char.IsWhiteSpace(s[a]) && s[a] is not ('=' or '>' or '"' or '\''))
            {
                if (s[a] == '/' && a + 1 < end && s[a + 1] == '>')
                {
                    break;
                }

                a++;
            }

            if (a == j)
            {
                a++;
            }

            o.Add(CodeTokenKind.Attribute, j, a);
            j = a;
        }

        // Script and style contents are JavaScript and CSS.
        if (s[i + 1] != '/' && (name.Equals("script", StringComparison.OrdinalIgnoreCase) || name.Equals("style", StringComparison.OrdinalIgnoreCase)))
        {
            var close = s.IndexOf("</" + name, j, end - j, StringComparison.OrdinalIgnoreCase);
            var bodyEnd = close < 0 ? end : close;
            if (name.Equals("style", StringComparison.OrdinalIgnoreCase))
            {
                Css(o, j, bodyEnd);
            }
            else
            {
                CLike(o, j, bodyEnd, csharp: false);
            }

            return bodyEnd;
        }

        return j;
    }

    private static int AttributeValue(Output o, int j, int end, bool razor)
    {
        var s = o.Code;
        if (s[j] == '"' || s[j] == '\'')
        {
            // A Razor value starting with an expression ("@Url("x")", "@(a + b)") colors it as code;
            // the expression may itself contain quotes, so it's read before looking for the close.
            if (razor && j + 1 < end && s[j + 1] == '@' && (j + 2 >= end || s[j + 2] != '@'))
            {
                o.Add(CodeTokenKind.String, j, j + 1);
                var e = RazorTransition(o, j + 1, end);
                var close = s.IndexOf(s[j], e, end - e);
                var q2 = close < 0 ? end : close + 1;
                o.Add(CodeTokenKind.String, e, q2);
                return q2;
            }

            var q = QuotedEnd(s, j, end, escapes: false, multiline: true);
            o.Add(CodeTokenKind.String, j, q);
            return q;
        }

        var u = j;
        while (u < end && !char.IsWhiteSpace(s[u]) && s[u] != '>')
        {
            u++;
        }

        o.Add(CodeTokenKind.String, j, u);
        return u;
    }

    // ---- CSS ----

    private static void Css(Output o, int start, int end)
    {
        var s = o.Code;
        var i = start;
        while (i < end)
        {
            var c = s[i];
            if (char.IsWhiteSpace(c) || c == '{' || c == '}' || c == ';')
            {
                o.Add(CodeTokenKind.Text, i, i + 1);
                i++;
                continue;
            }

            if (c == '/' && i + 1 < end && s[i + 1] == '*')
            {
                var j = After(s, "*/", i + 2, end);
                o.Add(CodeTokenKind.Comment, i, j);
                i = j;
                continue;
            }

            // A statement runs to the next '{', ';' or '}' outside strings and parentheses.
            var k = i;
            var depth = 0;
            while (k < end)
            {
                var ch = s[k];
                if (ch == '"' || ch == '\'')
                {
                    k = QuotedEnd(s, k, end);
                    continue;
                }

                if (ch == '/' && k + 1 < end && s[k + 1] == '*')
                {
                    break;
                }

                if (ch == '(')
                {
                    depth++;
                }
                else if (ch == ')')
                {
                    depth--;
                }
                else if (depth <= 0 && (ch == '{' || ch == ';' || ch == '}'))
                {
                    break;
                }

                k++;
            }

            if (c == '@')
            {
                var w = i + 1;
                while (w < k && (IsIdent(s[w]) || s[w] == '-'))
                {
                    w++;
                }

                o.Add(CodeTokenKind.Meta, i, w);
                CssValue(o, w, k);
            }
            else if (k < end && s[k] == '{')
            {
                CssSelector(o, i, k);
            }
            else
            {
                var colon = s.IndexOf(':', i, k - i);
                if (colon > i && IsCssName(s, i, colon))
                {
                    o.Add(CodeTokenKind.Attribute, i, colon);
                    o.Add(CodeTokenKind.Text, colon, colon + 1);
                    CssValue(o, colon + 1, k);
                }
                else
                {
                    CssValue(o, i, k);
                }
            }

            i = k;
        }
    }

    private static bool IsCssName(string s, int i, int end)
    {
        var trimmed = end;
        while (trimmed > i && char.IsWhiteSpace(s[trimmed - 1]))
        {
            trimmed--;
        }

        for (var k = i; k < trimmed; k++)
        {
            if (!(IsIdent(s[k]) || s[k] == '-'))
            {
                return false;
            }
        }

        return trimmed > i;
    }

    private static void CssSelector(Output o, int i, int end)
    {
        var s = o.Code;
        while (i < end)
        {
            var c = s[i];
            if (char.IsWhiteSpace(c) || c is ',' or '>' or '+' or '~' or '(' or ')' or '&' or '*')
            {
                o.Add(CodeTokenKind.Text, i, i + 1);
                i++;
                continue;
            }

            if (c == '"' || c == '\'')
            {
                var q = QuotedEnd(s, i, end);
                o.Add(CodeTokenKind.String, i, q);
                i = q;
                continue;
            }

            var j = i + 1;
            while (j < end && !char.IsWhiteSpace(s[j]) && s[j] is not (',' or '>' or '+' or '~' or '(' or ')' or ':' or '"' or '\''))
            {
                j++;
            }

            o.Add(c == ':' ? CodeTokenKind.Keyword : CodeTokenKind.Tag, i, j);
            i = j;
        }
    }

    private static void CssValue(Output o, int i, int end)
    {
        var s = o.Code;
        while (i < end)
        {
            var c = s[i];
            if (c == '"' || c == '\'')
            {
                var q = QuotedEnd(s, i, end);
                o.Add(CodeTokenKind.String, i, q);
                i = q;
                continue;
            }

            if (c == '#' && i + 1 < end && char.IsAsciiHexDigit(s[i + 1]))
            {
                var j = i + 1;
                while (j < end && char.IsAsciiLetterOrDigit(s[j]))
                {
                    j++;
                }

                o.Add(CodeTokenKind.Number, i, j);
                i = j;
                continue;
            }

            if (char.IsAsciiDigit(c) || (c == '.' && i + 1 < end && char.IsAsciiDigit(s[i + 1])) ||
                (c == '-' && i + 1 < end && (char.IsAsciiDigit(s[i + 1]) || s[i + 1] == '.') && (i == 0 || !IsIdent(s[i - 1]))))
            {
                var j = i + 1;
                while (j < end && (char.IsAsciiLetterOrDigit(s[j]) || s[j] == '.' || s[j] == '%'))
                {
                    j++;
                }

                o.Add(CodeTokenKind.Number, i, j);
                i = j;
                continue;
            }

            if (c == '!' )
            {
                var j = i + 1;
                while (j < end && char.IsAsciiLetter(s[j]))
                {
                    j++;
                }

                o.Add(CodeTokenKind.Keyword, i, j);
                i = j;
                continue;
            }

            if (IsIdentStart(c) || c == '-')
            {
                var j = i + 1;
                while (j < end && (IsIdent(s[j]) || s[j] == '-'))
                {
                    j++;
                }

                var kind = j < end && s[j] == '(' ? CodeTokenKind.Function
                    : s[i] == '-' && j > i + 1 && s[i + 1] == '-' ? CodeTokenKind.Attribute
                    : CodeTokenKind.Text;
                o.Add(kind, i, j);
                i = j;
                continue;
            }

            o.Add(CodeTokenKind.Text, i, i + 1);
            i++;
        }
    }

    // ---- JSON ----

    private static void Json(Output o, int start, int end)
    {
        var s = o.Code;
        var i = start;
        while (i < end)
        {
            var c = s[i];
            if (c == '"')
            {
                var q = QuotedEnd(s, i, end);
                var k = q;
                while (k < end && char.IsWhiteSpace(s[k]))
                {
                    k++;
                }

                o.Add(k < end && s[k] == ':' ? CodeTokenKind.Attribute : CodeTokenKind.String, i, q);
                i = q;
                continue;
            }

            if (c == '/' && i + 1 < end && (s[i + 1] == '/' || s[i + 1] == '*'))
            {
                var j = s[i + 1] == '/' ? LineEnd(s, i, end) : After(s, "*/", i + 2, end);
                o.Add(CodeTokenKind.Comment, i, j);
                i = j;
                continue;
            }

            if (char.IsAsciiDigit(c) || c == '-')
            {
                var j = i + 1;
                while (j < end && (char.IsAsciiDigit(s[j]) || s[j] is '.' or 'e' or 'E' or '+' or '-'))
                {
                    j++;
                }

                o.Add(CodeTokenKind.Number, i, j);
                i = j;
                continue;
            }

            if (char.IsAsciiLetter(c))
            {
                var j = i + 1;
                while (j < end && char.IsAsciiLetter(s[j]))
                {
                    j++;
                }

                o.Add(s[i..j] is "true" or "false" or "null" ? CodeTokenKind.Keyword : CodeTokenKind.Text, i, j);
                i = j;
                continue;
            }

            o.Add(CodeTokenKind.Text, i, i + 1);
            i++;
        }
    }

    // ---- Shell ----

    private static void Shell(Output o, int start, int end)
    {
        var s = o.Code;
        var i = start;
        var command = true;
        while (i < end)
        {
            var c = s[i];
            if (c == '\n')
            {
                o.Add(CodeTokenKind.Text, i, i + 1);
                i++;
                // A line ending in a backslash or backtick continues the command.
                var prev = i - 2 >= start ? s[i - 2] : '\0';
                command = prev != '\\' && prev != '`';
                continue;
            }

            if (c == ' ' || c == '\t' || c == '\r')
            {
                o.Add(CodeTokenKind.Text, i, i + 1);
                i++;
                continue;
            }

            if (command && AtLineStart(s, i, start) && (c == '$' || c == '>') && i + 1 < end && s[i + 1] == ' ')
            {
                o.Add(CodeTokenKind.Meta, i, i + 1);
                i++;
                continue;
            }

            if (c == '#' && (i == start || char.IsWhiteSpace(s[i - 1])))
            {
                var j = LineEnd(s, i, end);
                o.Add(CodeTokenKind.Comment, i, j);
                i = j;
                continue;
            }

            if (c == '"' || c == '\'')
            {
                var q = QuotedEnd(s, i, end, escapes: c == '"', multiline: true);
                o.Add(CodeTokenKind.String, i, q);
                i = q;
                command = false;
                continue;
            }

            if (c == '$' && i + 1 < end && (IsIdentStart(s[i + 1]) || s[i + 1] is '{' or '(' or '?' or '_'))
            {
                var j = i + 1;
                if (s[j] is '{' or '(')
                {
                    j = Matching(s, j, end);
                }
                else
                {
                    while (j < end && IsIdent(s[j]))
                    {
                        j++;
                    }

                    // PowerShell's $env:NAME.
                    if (j + 1 < end && s[j] == ':' && IsIdentStart(s[j + 1]) && s.AsSpan(i + 1, j - i - 1).Equals("env", StringComparison.OrdinalIgnoreCase))
                    {
                        j++;
                        while (j < end && IsIdent(s[j]))
                        {
                            j++;
                        }
                    }
                }

                o.Add(CodeTokenKind.Meta, i, j);
                i = j;
                command = false;
                continue;
            }

            if (c is '|' or ';' or '&')
            {
                var j = i + 1;
                while (j < end && s[j] is '|' or '&')
                {
                    j++;
                }

                o.Add(CodeTokenKind.Text, i, j);
                i = j;
                command = true;
                continue;
            }

            // A word: the command, a keyword, a flag or an argument.
            var e = i;
            while (e < end && !char.IsWhiteSpace(s[e]) && s[e] is not ('|' or ';' or '"' or '\''))
            {
                if (s[e] == '&' || (s[e] == '$' && e > i))
                {
                    break;
                }

                e++;
            }

            if (e == i)
            {
                e++;
            }

            var word = s[i..e];
            CodeTokenKind kind;
            if (ShellKeywords.Contains(word) && (command || word == "in"))
            {
                kind = CodeTokenKind.Keyword;
                command = word is "if" or "then" or "else" or "elif" or "do" or "while" or "until";
            }
            else if (command && !word.Contains('='))
            {
                kind = CodeTokenKind.Function;
                command = false;
            }
            else if (word.Length > 1 && word[0] == '-' && !char.IsAsciiDigit(word[1]))
            {
                kind = CodeTokenKind.Attribute;
            }
            else
            {
                kind = CodeTokenKind.Text;
            }

            o.Add(kind, i, e);
            i = e;
        }
    }
}
