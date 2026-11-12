using System.Globalization;

namespace IzzyBlazor.Generation;

/// <summary>
/// Evaluates a condition such as <c>(UseServer &amp;&amp; !Empty)</c> or <c>Interactivity == "Auto"</c>: the
/// subset of <c>dotnet new</c>'s expressions the catalogue uses, so an item's conditions mean the same in both.
/// Names are settings or computed values; a name that isn't one is an error, not false.
/// </summary>
internal static class Condition
{
    public static bool Evaluate(string expression, IReadOnlyDictionary<string, object> values)
    {
        var parser = new Parser(expression, values);
        var result = parser.ParseOr();
        parser.ExpectEnd();
        return result is bool b ? b : throw new GenerationException($"Condition \"{expression}\" is not true or false.");
    }

    sealed class Parser(string text, IReadOnlyDictionary<string, object> values)
    {
        int _pos;

        public object ParseOr()
        {
            var left = ParseAnd();
            while (Take("||")) left = AsBool(left) | AsBool(ParseAnd());
            return left;
        }

        object ParseAnd()
        {
            var left = ParseUnary();
            while (Take("&&")) left = AsBool(left) & AsBool(ParseUnary());
            return left;
        }

        object ParseUnary() => Take("!") ? !AsBool(ParseUnary()) : ParseComparison();

        object ParseComparison()
        {
            var left = ParsePrimary();
            if (Take("==")) return Equal(left, ParsePrimary());
            if (Take("!=")) return !Equal(left, ParsePrimary());
            return left;
        }

        object ParsePrimary()
        {
            SkipSpace();
            if (Take("("))
            {
                var inner = ParseOr();
                if (!Take(")")) throw Error("a closing )");
                return inner;
            }
            if (_pos < text.Length && text[_pos] is '"' or '\'')
            {
                var quote = text[_pos++];
                var end = text.IndexOf(quote, _pos);
                if (end < 0) throw Error("a closing quote");
                var s = text[_pos..end];
                _pos = end + 1;
                return s;
            }
            var start = _pos;
            while (_pos < text.Length && (char.IsLetterOrDigit(text[_pos]) || text[_pos] is '_' or '.' or '-')) _pos++;
            var word = text[start.._pos];
            if (word.Length == 0) throw Error("a name, a value or (");
            if (word.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (word.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            if (long.TryParse(word, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n)) return n;
            return values.TryGetValue(word, out var value) ? value : throw new GenerationException($"Condition \"{text}\" names \"{word}\", which is not a setting.");
        }

        static bool Equal(object a, object b) => (a, b) switch
        {
            (string x, string y) => string.Equals(x, y, StringComparison.Ordinal),
            (long x, long y) => x == y,
            (bool x, bool y) => x == y,
            _ => string.Equals(Convert.ToString(a, CultureInfo.InvariantCulture), Convert.ToString(b, CultureInfo.InvariantCulture), StringComparison.Ordinal),
        };

        bool AsBool(object value) => value is bool b ? b : throw new GenerationException($"Condition \"{text}\" uses \"{value}\" as true or false.");

        bool Take(string token)
        {
            SkipSpace();
            if (string.CompareOrdinal(text, _pos, token, 0, token.Length) != 0) return false;
            _pos += token.Length;
            return true;
        }

        void SkipSpace()
        {
            while (_pos < text.Length && char.IsWhiteSpace(text[_pos])) _pos++;
        }

        public void ExpectEnd()
        {
            SkipSpace();
            if (_pos != text.Length) throw Error("the end");
        }

        GenerationException Error(string expected) => new($"Condition \"{text}\": expected {expected} at position {_pos + 1}.");
    }
}
