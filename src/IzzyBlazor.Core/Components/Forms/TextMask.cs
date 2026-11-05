using System.Text;
using System.Text.RegularExpressions;

namespace IzzyBlazor;

/// <summary>
/// A <see cref="TextField.Mask"/>, parsed: the slots a character goes in and the literals between them.
/// The same algorithm runs in the browser (Scripts/elements/mask.ts); keep the two in step.
/// </summary>
/// <remarks>
/// Fitting walks the mask and the text together. A literal the text has at that point is taken;
/// otherwise it waits, and is written only when a later slot gets a character, so the value never ends
/// in a literal until the mask is done. A required slot drops text characters that don't fit it; an
/// optional slot that the next character doesn't fit stays empty. Characters past the last slot are
/// dropped. So any text (typed, pasted, posted with dots or spaces) comes out in the mask's form.
/// </remarks>
internal sealed class TextMask
{
    private enum Case { None, Upper, Lower }

    private readonly record struct Element(char Literal, Func<char, bool>? Test, bool Optional, Case Case)
    {
        public bool IsSlot => Test is not null;
    }

    private readonly Element[] _elements;

    /// <summary>What the field ends up with: the formatted text, its slot characters, and whether every required slot is filled.</summary>
    internal readonly record struct Fit(string Text, string Raw, bool Complete);

    public TextMask(string mask, IReadOnlyDictionary<char, string>? rules = null)
    {
        var custom = new Dictionary<char, Func<char, bool>>();
        if (rules is not null)
        {
            foreach (var (key, pattern) in rules)
            {
                custom[key] = RuleTest(key, pattern);
            }
        }

        var elements = new List<Element>();
        var casing = Case.None;
        for (var i = 0; i < mask.Length; i++)
        {
            var c = mask[i];
            if (custom.TryGetValue(c, out var test))
            {
                elements.Add(new(c, test, false, casing));
                continue;
            }

            switch (c)
            {
                case '>': casing = Case.Upper; continue;
                case '<': casing = Case.Lower; continue;
                case '|': casing = Case.None; continue;
                case '\\':
                    if (i + 1 < mask.Length)
                    {
                        elements.Add(new(mask[++i], null, false, Case.None));
                    }

                    continue;
            }

            Func<char, bool>? builtIn = c switch
            {
                '0' or '9' => IsDigit,
                'L' or '?' => char.IsLetter,
                'A' or 'a' => IsAlphanumeric,
                '&' or 'C' => IsAny,
                _ => null,
            };
            elements.Add(new(c, builtIn, c is '9' or '?' or 'a' or 'C', casing));
        }

        _elements = [.. elements];
    }

    /// <summary>Whether every slot takes only digits, for <c>inputmode="numeric"</c>.</summary>
    public bool DigitsOnly => _elements.Any(e => e.IsSlot) && _elements.All(e => !e.IsSlot || (e.Test == IsDigit));

    /// <summary>Fits text to the mask.</summary>
    public Fit Apply(string? text)
    {
        text ??= "";
        var output = new StringBuilder();
        var raw = new StringBuilder();
        var pending = new StringBuilder();
        var trailing = new StringBuilder();
        var complete = true;
        var i = 0;
        foreach (var element in _elements)
        {
            if (!element.IsSlot)
            {
                if (i < text.Length && text[i] == element.Literal)
                {
                    i++;
                }

                pending.Append(element.Literal);
                trailing.Append(element.Literal);
                continue;
            }

            trailing.Clear();

            char? taken = null;
            while (i < text.Length)
            {
                var c = Convert(text[i], element.Case);
                if (element.Test!(c))
                {
                    taken = c;
                    i++;
                    break;
                }

                if (element.Optional)
                {
                    break;
                }

                i++;
            }

            if (taken is { } ch)
            {
                output.Append(pending).Append(ch);
                pending.Clear();
                raw.Append(ch);
            }
            else if (!element.Optional)
            {
                complete = false;
            }
        }

        // The mask is done: the literals after its last slot follow.
        if (complete && raw.Length > 0)
        {
            output.Append(trailing);
        }

        return new(output.ToString(), raw.ToString(), complete);
    }

    private static char Convert(char c, Case casing) => casing switch
    {
        Case.Upper => char.ToUpperInvariant(c),
        Case.Lower => char.ToLowerInvariant(c),
        _ => c,
    };

    private static bool IsDigit(char c) => c is >= '0' and <= '9';

    private static bool IsAlphanumeric(char c) => IsDigit(c) || char.IsLetter(c);

    private static bool IsAny(char c) => !char.IsWhiteSpace(c);

    private static Func<char, bool> RuleTest(char key, string pattern)
    {
        // A class only, so it means the same in .NET and in the browser's RegExp.
        if (pattern.Length < 3 || pattern[0] != '[' || pattern[^1] != ']')
        {
            throw new ArgumentException($"MaskRules['{key}'] must be one character class, such as \"[A-F0-9]\"; it was \"{pattern}\".");
        }

        var regex = new Regex("^" + pattern + "$", RegexOptions.CultureInvariant);
        return c => regex.IsMatch(c.ToString());
    }
}
