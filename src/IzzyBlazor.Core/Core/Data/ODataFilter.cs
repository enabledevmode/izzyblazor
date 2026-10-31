using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace IzzyBlazor;

/// <summary>
/// Reads and writes filters in a subset of OData's <c>$filter</c>. Parsing builds the filter tree
/// and nothing else: names are field keys, literals are values, and nothing is evaluated.
/// </summary>
/// <remarks>
/// <code>
/// or      := and ('or' and)*
/// and     := unary ('and' unary)*
/// unary   := 'not' unary | primary
/// primary := '(' or ')' | 'true' | 'false'
///          | ('contains' | 'startswith' | 'endswith') '(' operand ',' literal ')' [('eq' | 'ne') ('true' | 'false')]
///          | operand ('contains' | 'startswith' | 'endswith') literal
///          | operand ('eq' | 'ne' | 'gt' | 'ge' | 'lt' | 'le') literal
///          | operand 'in' '(' literal (',' literal)* ')'
///          | 'date' '(' field ')' 'eq' literal
/// operand := field | ('tolower' | 'toupper') '(' field ')'
/// literal := 'text' (a quote doubled inside) | number | true | false | null | 2026-01-31
///          | 2026-01-31T09:30:00Z | a Guid | Type'Name' (an enum)
/// </code>
/// A field's path may use <c>/</c> (OData) or <c>.</c> (a column key); both read as <c>.</c>.
/// </remarks>
internal static partial class ODataFilter
{
    private const int MaxDepth = 32;

    public static Filter Parse(string text)
    {
        var parser = new Parser(Tokenize(text));
        var filter = parser.Or(0);
        if (!parser.AtEnd)
        {
            throw new FormatException($"Unexpected \"{parser.Peek.Text}\" in the filter.");
        }

        return filter;
    }

    private enum TokenKind
    {
        Word,
        Text,
        Open,
        Close,
        Comma,
    }

    private readonly record struct Token(TokenKind Kind, string Text);

    private static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
            }
            else if (c == '(')
            {
                tokens.Add(new(TokenKind.Open, "("));
                i++;
            }
            else if (c == ')')
            {
                tokens.Add(new(TokenKind.Close, ")"));
                i++;
            }
            else if (c == ',')
            {
                tokens.Add(new(TokenKind.Comma, ","));
                i++;
            }
            else if (c == '\'')
            {
                tokens.Add(new(TokenKind.Text, ReadText(text, ref i)));
            }
            else if (IsWordChar(c))
            {
                var start = i;
                while (i < text.Length && IsWordChar(text[i]))
                {
                    i++;
                }

                // An enum literal, Type'Name': the name is the value.
                if (i < text.Length && text[i] == '\'')
                {
                    tokens.Add(new(TokenKind.Text, ReadText(text, ref i)));
                }
                else
                {
                    tokens.Add(new(TokenKind.Word, text[start..i]));
                }
            }
            else
            {
                throw new FormatException($"Unexpected \"{c}\" in the filter.");
            }
        }

        return tokens;
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '.' or '-' or '/' or ':' or '+';

    // 'text', with '' for a quote inside.
    private static string ReadText(string text, ref int i)
    {
        var value = new StringBuilder();
        i++;
        while (true)
        {
            if (i >= text.Length)
            {
                throw new FormatException("A quoted value in the filter has no closing quote.");
            }

            if (text[i] == '\'')
            {
                if (i + 1 < text.Length && text[i + 1] == '\'')
                {
                    value.Append('\'');
                    i += 2;
                    continue;
                }

                i++;
                return value.ToString();
            }

            value.Append(text[i++]);
        }
    }

    private sealed class Parser(List<Token> tokens)
    {
        private int _at;

        public bool AtEnd => _at >= tokens.Count;

        public Token Peek => AtEnd ? throw new FormatException("The filter ends too soon.") : tokens[_at];

        private Token Next()
        {
            var token = Peek;
            _at++;
            return token;
        }

        private bool IsWord(string word, int ahead = 0) =>
            _at + ahead < tokens.Count && tokens[_at + ahead].Kind == TokenKind.Word && string.Equals(tokens[_at + ahead].Text, word, StringComparison.OrdinalIgnoreCase);

        private bool IsKind(TokenKind kind, int ahead = 0) => _at + ahead < tokens.Count && tokens[_at + ahead].Kind == kind;

        private void Expect(TokenKind kind)
        {
            if (Next().Kind != kind)
            {
                throw new FormatException($"Expected \"{(kind == TokenKind.Open ? "(" : kind == TokenKind.Close ? ")" : ",")}\" in the filter.");
            }
        }

        private string ExpectWord()
        {
            var token = Next();
            return token.Kind == TokenKind.Word ? token.Text : throw new FormatException($"Unexpected \"{token.Text}\" in the filter.");
        }

        public Filter Or(int depth)
        {
            var parts = new List<Filter> { And(depth) };
            while (IsWord("or"))
            {
                _at++;
                parts.Add(And(depth));
            }

            return parts.Count == 1 ? parts[0] : new FilterGroup(FilterLogic.Or, parts);
        }

        private Filter And(int depth)
        {
            var parts = new List<Filter> { Unary(depth) };
            while (IsWord("and"))
            {
                _at++;
                parts.Add(Unary(depth));
            }

            return parts.Count == 1 ? parts[0] : new FilterGroup(FilterLogic.And, parts);
        }

        private Filter Unary(int depth)
        {
            if (depth > MaxDepth)
            {
                throw new FormatException("The filter nests too deeply.");
            }

            if (!IsWord("not"))
            {
                return Primary(depth);
            }

            _at++;
            var inner = Unary(depth + 1);

            // not contains(...) and not (x in (...)) are the operators that say so.
            return inner switch
            {
                FieldFilter { Operator: FilterOperator.Contains } f => f with { Operator = FilterOperator.DoesNotContain },
                FieldFilter { Operator: FilterOperator.In } f => f with { Operator = FilterOperator.NotIn },
                _ => new NotFilter(inner),
            };
        }

        private Filter Primary(int depth)
        {
            if (IsKind(TokenKind.Open))
            {
                _at++;
                var inner = Or(depth + 1);
                Expect(TokenKind.Close);
                return inner;
            }

            if (IsWord("true") || IsWord("false"))
            {
                return string.Equals(Next().Text, "true", StringComparison.OrdinalIgnoreCase) ? Filter.All() : Filter.Any();
            }

            // contains(field, 'x'), startswith(...), endswith(...); OData writes "eq true" after them.
            if (TextOperator(Peek.Text) is { } function && IsKind(TokenKind.Open, 1))
            {
                _at += 2;
                var field = Operand();
                Expect(TokenKind.Comma);
                var value = Literal();
                Expect(TokenKind.Close);
                var condition = new FieldFilter(field, function, value);
                if (IsWord("eq") || IsWord("ne"))
                {
                    var negate = IsWord("ne");
                    _at++;
                    var truth = Literal() as bool? ?? throw new FormatException("Expected true or false in the filter.");
                    return truth != negate ? condition : Negate(condition);
                }

                return condition;
            }

            // date(field) eq 2026-01-31: the day.
            if (IsWord("date") && IsKind(TokenKind.Open, 1))
            {
                _at += 2;
                var field = Field(ExpectWord());
                Expect(TokenKind.Close);
                if (!IsWord("eq"))
                {
                    throw new FormatException("date() takes eq in the filter.");
                }

                _at++;
                return new FieldFilter(field, FilterOperator.OnDay, Literal());
            }

            var name = Operand();
            var op = ExpectWord().ToLowerInvariant();
            switch (op)
            {
                case "in":
                {
                    Expect(TokenKind.Open);
                    var values = new List<object?> { Literal() };
                    while (IsKind(TokenKind.Comma))
                    {
                        _at++;
                        values.Add(Literal());
                    }

                    Expect(TokenKind.Close);
                    return new FieldFilter(name, FilterOperator.In, values);
                }

                case "contains" or "startswith" or "endswith":
                    return new FieldFilter(name, TextOperator(op)!.Value, Literal());
            }

            var value2 = Literal();
            return (op, value2) switch
            {
                ("eq", null) => new FieldFilter(name, FilterOperator.IsNull),
                ("ne", null) => new FieldFilter(name, FilterOperator.IsNotNull),
                ("eq", _) => new FieldFilter(name, FilterOperator.Equals, value2),
                ("ne", _) => new FieldFilter(name, FilterOperator.NotEquals, value2),
                ("gt", _) => new FieldFilter(name, FilterOperator.GreaterThan, value2),
                ("ge", _) => new FieldFilter(name, FilterOperator.GreaterThanOrEqual, value2),
                ("lt", _) => new FieldFilter(name, FilterOperator.LessThan, value2),
                ("le", _) => new FieldFilter(name, FilterOperator.LessThanOrEqual, value2),
                _ => throw new FormatException($"Unknown operator \"{op}\" in the filter."),
            };
        }

        private static Filter Negate(FieldFilter condition) =>
            condition.Operator == FilterOperator.Contains ? condition with { Operator = FilterOperator.DoesNotContain } : new NotFilter(condition);

        private static FilterOperator? TextOperator(string word) => word.ToLowerInvariant() switch
        {
            "contains" => FilterOperator.Contains,
            "startswith" => FilterOperator.StartsWith,
            "endswith" => FilterOperator.EndsWith,
            _ => null,
        };

        // A field, or tolower(field) / toupper(field), which text ignores anyway.
        private string Operand()
        {
            if ((IsWord("tolower") || IsWord("toupper")) && IsKind(TokenKind.Open, 1))
            {
                _at += 2;
                var field = Field(ExpectWord());
                Expect(TokenKind.Close);
                return field;
            }

            return Field(ExpectWord());
        }

        private static string Field(string word) =>
            FieldName().IsMatch(word) ? word.Replace('/', '.') : throw new FormatException($"\"{word}\" isn't a field name.");

        private object? Literal()
        {
            var token = Next();
            if (token.Kind == TokenKind.Text)
            {
                return token.Text;
            }

            if (token.Kind != TokenKind.Word)
            {
                throw new FormatException($"Expected a value in the filter, not \"{token.Text}\".");
            }

            var word = token.Text;
            switch (word.ToLowerInvariant())
            {
                case "null":
                    return null;
                case "true":
                    return true;
                case "false":
                    return false;
            }

            if (DateLiteral().IsMatch(word) && DateOnly.TryParseExact(word, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            {
                return day;
            }

            if (DateTimeLiteral().IsMatch(word))
            {
                if (word.EndsWith('Z') || OffsetSuffix().IsMatch(word))
                {
                    return DateTimeOffset.TryParse(word, CultureInfo.InvariantCulture, DateTimeStyles.None, out var stamp)
                        ? stamp
                        : throw new FormatException($"\"{word}\" isn't a date and time.");
                }

                return DateTime.TryParse(word, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)
                    ? local
                    : throw new FormatException($"\"{word}\" isn't a date and time.");
            }

            if (GuidLiteral().IsMatch(word) && Guid.TryParse(word, out var guid))
            {
                return guid;
            }

            if (NumberLiteral().IsMatch(word))
            {
                if (long.TryParse(word, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var whole))
                {
                    return whole;
                }

                if (!word.Contains('e', StringComparison.OrdinalIgnoreCase)
                    && decimal.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out var fraction))
                {
                    return fraction;
                }

                if (double.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                {
                    return number;
                }
            }

            throw new FormatException($"\"{word}\" isn't a value: put text in single quotes.");
        }
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_./-]*$")]
    private static partial Regex FieldName();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    private static partial Regex DateLiteral();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:\d{2})?$", RegexOptions.IgnoreCase)]
    private static partial Regex DateTimeLiteral();

    [GeneratedRegex(@"[+-]\d{2}:\d{2}$")]
    private static partial Regex OffsetSuffix();

    [GeneratedRegex(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")]
    private static partial Regex GuidLiteral();

    [GeneratedRegex(@"^-?(\d+(\.\d*)?|\.\d+)([eE][+-]?\d+)?$")]
    private static partial Regex NumberLiteral();

    /// <summary>
    /// The filter as text. For an OData endpoint (<paramref name="forOData"/>), text that ignores
    /// case compares lower-cased (<c>contains(tolower(customer),'ada')</c>), since OData's
    /// <c>contains</c> and <c>eq</c> match case; field paths use <c>/</c>. Otherwise in the form
    /// <see cref="Parse"/> reads, where text ignores case (and <see cref="FieldFilter.MatchCase"/>
    /// isn't written).
    /// </summary>
    public static string Write(Filter filter, bool forOData)
    {
        var text = new StringBuilder();
        Write(text, filter, forOData, top: true);
        return text.ToString();
    }

    private static void Write(StringBuilder text, Filter filter, bool forOData, bool top)
    {
        switch (filter)
        {
            case FilterGroup { Filters.Count: 0 } g:
                text.Append(g.Logic == FilterLogic.And ? "true" : "false");
                break;
            case FilterGroup { Filters.Count: 1 } g:
                Write(text, g.Filters[0], forOData, top);
                break;
            case FilterGroup g:
                if (!top)
                {
                    text.Append('(');
                }

                // A group inside one of the same logic needs no parentheses: a and (b and c) is a and b and c.
                var parts = Flatten(g).ToList();
                for (var i = 0; i < parts.Count; i++)
                {
                    if (i > 0)
                    {
                        text.Append(g.Logic == FilterLogic.And ? " and " : " or ");
                    }

                    Write(text, parts[i], forOData, top: false);
                }

                if (!top)
                {
                    text.Append(')');
                }

                break;
            case NotFilter n:
                text.Append("not (");
                Write(text, n.Filter, forOData, top: true);
                text.Append(')');
                break;
            case FieldFilter f:
                WriteCondition(text, f, forOData, top);
                break;
        }
    }

    private static IEnumerable<Filter> Flatten(FilterGroup group) =>
        group.Filters.SelectMany(f => f is FilterGroup { Filters.Count: > 1 } inner && inner.Logic == group.Logic ? Flatten(inner) : [f]);

    private static void WriteCondition(StringBuilder text, FieldFilter f, bool forOData, bool top)
    {
        var field = forOData ? f.Field.Replace('.', '/') : f.Field;

        // OData compares text with case; lower both sides to ignore it. Only for text values: a
        // number or date isn't text, and tolower() of a number field is an error there.
        var lower = forOData && !f.MatchCase && FilterConvert.Unwrap(f.Value) is string;
        var operand = lower ? $"tolower({field})" : field;
        string Value(object? v) => Literal(lower && FilterConvert.Unwrap(v) is string s ? s.ToLowerInvariant() : v);

        void Wrap(string inner)
        {
            text.Append(top ? inner : $"({inner})");
        }

        switch (f.Operator)
        {
            case FilterOperator.Equals:
                text.Append(FilterConvert.Unwrap(f.Value) is null ? $"{field} eq null" : $"{operand} eq {Value(f.Value)}");
                break;
            case FilterOperator.NotEquals:
                text.Append(FilterConvert.Unwrap(f.Value) is null ? $"{field} ne null" : $"{operand} ne {Value(f.Value)}");
                break;
            case FilterOperator.LessThan:
                text.Append($"{field} lt {Literal(f.Value)}");
                break;
            case FilterOperator.LessThanOrEqual:
                text.Append($"{field} le {Literal(f.Value)}");
                break;
            case FilterOperator.GreaterThan:
                text.Append($"{field} gt {Literal(f.Value)}");
                break;
            case FilterOperator.GreaterThanOrEqual:
                text.Append($"{field} ge {Literal(f.Value)}");
                break;
            case FilterOperator.Between:
            {
                var (low, high) = (Present(f.Value), Present(f.Value2));
                if (low && high)
                {
                    Wrap($"{field} ge {Literal(f.Value)} and {field} le {Literal(f.Value2)}");
                }
                else if (low)
                {
                    text.Append($"{field} ge {Literal(f.Value)}");
                }
                else if (high)
                {
                    text.Append($"{field} le {Literal(f.Value2)}");
                }
                else
                {
                    text.Append("true");
                }

                break;
            }

            case FilterOperator.Contains:
                text.Append($"contains({operand},{Value(f.Value)})");
                break;
            case FilterOperator.DoesNotContain:
                text.Append($"not contains({operand},{Value(f.Value)})");
                break;
            case FilterOperator.StartsWith:
                text.Append($"startswith({operand},{Value(f.Value)})");
                break;
            case FilterOperator.EndsWith:
                text.Append($"endswith({operand},{Value(f.Value)})");
                break;
            case FilterOperator.In or FilterOperator.NotIn:
            {
                var values = f.Values;
                var listLower = forOData && !f.MatchCase && values.Count > 0 && values.All(v => FilterConvert.Unwrap(v) is string);
                var inOperand = listLower ? $"tolower({field})" : field;
                var list = values.Count == 0
                    ? null
                    : string.Join(",", values.Select(v => Literal(listLower ? ((string)FilterConvert.Unwrap(v)!).ToLowerInvariant() : v)));
                if (f.Operator == FilterOperator.In)
                {
                    text.Append(list is null ? "false" : $"{inOperand} in ({list})");
                }
                else
                {
                    text.Append(list is null ? "true" : $"not ({inOperand} in ({list}))");
                }

                break;
            }

            case FilterOperator.IsNull:
                text.Append($"{field} eq null");
                break;
            case FilterOperator.IsNotNull:
                text.Append($"{field} ne null");
                break;
            case FilterOperator.IsEmpty:
                Wrap($"{field} eq null or {field} eq ''");
                break;
            case FilterOperator.IsNotEmpty:
                Wrap($"{field} ne null and {field} ne ''");
                break;
            case FilterOperator.OnDay:
                text.Append(FilterConvert.TryDay(f.Value, out var day)
                    ? $"date({field}) eq {day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}"
                    : $"date({field}) eq {Literal(f.Value)}");
                break;
        }
    }

    private static bool Present(object? value) => FilterConvert.Unwrap(value) is not (null or string { Length: 0 });

    /// <summary>A value as an OData literal: text quoted (a quote doubled), numbers in the invariant form, dates in ISO form, enums by name.</summary>
    public static string Literal(object? value) => FilterConvert.Unwrap(value) switch
    {
        null => "null",
        string s => $"'{s.Replace("'", "''", StringComparison.Ordinal)}'",
        bool b => b ? "true" : "false",
        Enum e => $"'{e}'",
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime { Kind: DateTimeKind.Utc } dt => dt.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture),
        DateTime dt => dt.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture).TrimEnd('.'),
        DateTimeOffset dto => dto.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz", CultureInfo.InvariantCulture),
        Guid g => g.ToString("D"),
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        float f => f.ToString("R", CultureInfo.InvariantCulture),
        IFormattable n when FilterConvert.IsNumeric(n.GetType()) => n.ToString(null, CultureInfo.InvariantCulture),
        IFormattable other => $"'{other.ToString(null, CultureInfo.InvariantCulture).Replace("'", "''", StringComparison.Ordinal)}'",
        var other => $"'{(other.ToString() ?? "").Replace("'", "''", StringComparison.Ordinal)}'",
    };
}
